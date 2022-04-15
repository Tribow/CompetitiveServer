using System;
using System.Collections.Generic;

namespace Glicko2Rankings
{
    // https://github.com/MaartenStaa/glicko2-csharp (adjusted)

    public class RatingCalculator
    {
        private const double DefaultRating = 1500.0;
        private const double DefaultDeviation = 350;
        private const double DefaultVolatility = 0.06;
        private const double DefaultTau = 0.75;
        private const double Multiplier = 173.7178;
        private const double ConvergeanceTolerance = 0.000001;

        private readonly double _tau; //constrains volatility over time
        private readonly double _defaultVolatility;

        /// <summary>
        /// Standard constructor, uses default values for tau and volatility.
        /// </summary>
        public RatingCalculator()
        {
            _tau = DefaultTau;
            _defaultVolatility = DefaultVolatility;
        }

        /// <summary>
        /// Constructor, allows you to specify values for tau and volatiliy.
        /// </summary>
        /// <param name="initVolatility">Initial volatility</param>
        /// <param name="tau">the tau</param>
        public RatingCalculator(double initVolatility, double tau)
        {
            _tau = tau;
            _defaultVolatility = initVolatility;
        }

        /// <summary>
        /// Run through all players within a resultset and calculate their new ratings
        /// 
        /// Players within the resultset who did not compete during the rating period
        /// will have their diation increase.
        /// 
        /// Note that this method will clear results held in the association result set.
        /// </summary>
        /// <param name="results"></param>
        public void UpdateRatings(RatingPeriodResults results)
        {
            foreach (var player in results.GetParticipants())
            {
                if (results.GetResults(player).Count > 0)
                {
                    CalculateNewRating(player, results.GetResults(player));
                }
                else
                {
                    player.SetWorkingRating(player.GetGlicko2Rating());
                    player.SetWorkingRatingDeviation(CalculateNewRatingDeviation(player.GetGlicko2RatingDeviation(), player.GetVolatility()));
                    player.SetWorkingVolatility(player.GetVolatility());
                }
            }

            //now iterate through participants and confirm new ratings
            foreach (var player in results.GetParticipants())
            {
                player.FinaliseRating();
            }

            //Lastly, clear the result set for next rating period.
            results.Clear();
        }


        /// <summary>
        /// This is the function processing in step 5 of Glicko2
        /// </summary>
        /// <param name="player"></param>
        /// <param name="results"></param>
        private void CalculateNewRating(Rating player, IList<Result> results)
        {
            var phi = player.GetGlicko2RatingDeviation();
            var sigma = player.GetVolatility();
            var a = Math.Log(Math.Pow(sigma, 2));
            var delta = Delta(player, results);
            var v = V(player, results);

            //Step 5.2 - Set the initial values of the iterative algorithm to come in step 5.4
            var A = a;
            double B;
            if (Math.Pow(delta, 2) > Math.Pow(phi, 2) + v)
            {
                B = Math.Log(Math.Pow(delta, 2) - Math.Pow(phi, 2) - v);
            }
            else
            {
                double k = 1;
                B = a - (k * Math.Abs(_tau));

                while (F(B, delta, phi, v, a, _tau) < 0)
                {
                    k++;
                    B = a - (k * Math.Abs(_tau));
                }
            }

            //Step 5.3
            var fA = F(A, delta, phi, v, a, _tau);
            var fB = F(B, delta, phi, v, a, _tau);

            //step 5.4
            while (Math.Abs(B - A) > ConvergeanceTolerance)
            {
                var C = A + (((A - B) * fA) / (fB - fA));
                var fC = F(C, delta, phi, v, a, _tau);

                if (fC * fB < 0)
                {
                    A = B;
                    fA = fB;
                }
                else
                {
                    fA = fA / 2.0;
                }

                B = C;
                fB = fC;
            }

            var newSigma = Math.Exp(A / 2.0);

            player.SetWorkingVolatility(newSigma);

            //Step 6
            var phiStar = CalculateNewRatingDeviation(phi, newSigma);

            //Step 7
            //φ′ = 1 / (sqrt((1/φ*^2) + (1/v))
            var newPhi = 1.0 / Math.Sqrt((1.0 / Math.Pow(phiStar, 2)) + (1.0 / v));

            //              m
            //μ′ = μ + φ′^2 ∑ g(φj){sj - E(μ,μj,φj)} 
            //             j=1
            //note that the newly calculated rating values are stored in a "working" area in the Rating object
            //this avoids us attempting to calculate subsequent participants ratings against a moving target
            player.SetWorkingRating(player.GetGlicko2Rating() + (Math.Pow(newPhi, 2) * OutcomeBasedRating(player, results)));
            player.SetWorkingRatingDeviation(newPhi);
            player.IncrementNumberOfResults(results.Count);
        }

        //         e^x(∆^2 − φ^2 − v − ex)     (x - a)
        //f (x) = ------------------------- -  -------
        //           2(φ^2 + v + e^x)^2          τ^2  
        private static double F(double x, double delta, double phi, double v, double a, double tau)
        {
            return (Math.Exp(x) * (Math.Pow(delta, 2) - Math.Pow(phi, 2) - v - Math.Exp(x)) /
                (2.0 * Math.Pow(Math.Pow(phi, 2) + v + Math.Exp(x), 2))) -
                ((x - a) / Math.Pow(tau, 2));
        }

        //                  1
        //g (φ) = ----------------------- 
        //         sqrt(1 + 3*φ^2 / π^2)
        private static double G(double deviation)
        {
            return 1.0 / (Math.Sqrt(1.0 + (3.0 * Math.Pow(deviation, 2) / Math.Pow(Math.PI, 2))));
        }

        //                             1
        //E (μ, μj, φj) =  ------------------------
        //                  1 + exp(-g(φj)(μ - μj))
        private static double E(double playerRating, double opponentRating, double opponentDeviation)
        {
            return 1.0 / (1.0 + Math.Exp(-1.0 * G(opponentDeviation) * (playerRating - opponentRating)));
        }

        //      ⎾  m                                       ⏋^-1
        //∆ = v |   ∑  g(φj)^2 * E(μ,μj,φj){1 - E(μ,μj,φj)} |
        //      ⎿ j=1                                      ⏌
        private static double V(Rating player, IEnumerable<Result> results)
        {
            double v = 0.0;

            foreach (Result result in results)
            {
                v = v + ((Math.Pow(G(result.GetOpponent(player).GetGlicko2RatingDeviation()), 2))
                    * E(player.GetGlicko2Rating(),
                        result.GetOpponent(player).GetGlicko2Rating(),
                        result.GetOpponent(player).GetGlicko2RatingDeviation())
                    * (1.0 - E(player.GetGlicko2Rating(),
                        result.GetOpponent(player).GetGlicko2Rating(),
                        result.GetOpponent(player).GetGlicko2RatingDeviation())
                    ));
            }

            return Math.Pow(v, -1);
        }

        /// <summary>
        /// This is the formula of delta in step 4 of Glicko2 
        /// </summary>
        /// <param name="player"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private double Delta(Rating player, IList<Result> results)
        {
            return V(player, results) * OutcomeBasedRating(player, results);
        }

        /// <summary>
        /// This is part of the delta formula in step 4 of Glicko2
        /// </summary>
        /// <param name="player"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private static double OutcomeBasedRating(Rating player, IEnumerable<Result> results)
        {
            double outcomeBasedRating = 0;

            foreach (Result result in results)
            {
                outcomeBasedRating = outcomeBasedRating
                                        + (G(result.GetOpponent(player).GetGlicko2RatingDeviation())
                                            * (result.GetScore(player) - E(
                                                player.GetGlicko2Rating(),
                                                result.GetOpponent(player).GetGlicko2Rating(),
                                                result.GetOpponent(player).GetGlicko2RatingDeviation()))
                                                );
            }

            return outcomeBasedRating;
        }

        /// <summary>
        /// φ* = sqrt(φ^2 + σ′^2)
        /// Used for players who have not competed during rating period.
        /// </summary>
        /// <param name="phi"></param>
        /// <param name="sigma"></param>
        /// <returns></returns>
        private static double CalculateNewRatingDeviation(double phi, double sigma)
        {
            return Math.Sqrt(Math.Pow(phi, 2) + Math.Pow(sigma, 2));
        }

        /// <summary>
        /// Converts from the value used in the algorithm to a rating
        /// in the same range as traditional Elo et al
        /// </summary>
        /// <param name="rating"></param>
        /// <returns></returns>
        public double ConvertRatingToOriginalGlickoScale(double rating)
        {
            return ((rating * Multiplier) + DefaultRating);
        }

        /// <summary>
        /// Converts from a rating in the same range as traditional Elo
        /// et al to the value used within the algorithm
        /// </summary>
        /// <param name="rating"></param>
        /// <returns></returns>
        public double ConvertRatingToGlicko2Scale(double rating)
        {
            return ((rating - DefaultRating) / Multiplier);
        }

        /// <summary>
        /// Converts from the value used in the algorithm to a
        /// rating deviation in the same range as traditional Elo et al.
        /// </summary>
        /// <param name="ratingDeviation"></param>
        /// <returns></returns>
        public double ConvertRatingDeviationToOriginalGlickoScale(double ratingDeviation)
        {
            return (ratingDeviation * Multiplier);
        }

        /// <summary>
        /// Converts from a rating deviation in the same range as traditional Elo et al
        /// to the value used within the algorithm.
        /// </summary>
        /// <param name="ratingDeviation"></param>
        /// <returns></returns>
        public double ConvertRatingDeviationToGlicko2Scale(double ratingDeviation)
        {
            return (ratingDeviation / Multiplier);
        }

        public double GetDefaultRating()
        {
            return DefaultRating;
        }

        public double GetDefaultVolatility()
        {
            return _defaultVolatility;
        }

        public double GetDefaultRatingDeviation()
        {
            return DefaultDeviation;
        }
    }
}