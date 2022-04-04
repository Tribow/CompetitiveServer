namespace Glicko2Rankings
{
    /// <summary>
    /// https://github.com/MaartenStaa/glicko2-csharp (adjusted)
    /// Holds an individual's Glicko-2 rating.
    /// 
    /// Glicko-2 ratings are an average skill value, a standard deviation and a volatility
    /// (how consistent the player is). Prof Glickman's paper on the algorithm allows scaling
    /// of these values to be more directly comparable with existing rating systems such as
    /// Elo or USCF's derivation thereof. This implementation outputs ratings at this larger
    /// scale.
    /// </summary>
    public class Rating
    {
        private readonly RatingCalculator _ratingSystem;

        private double _rating;
        private double _ratingDeviation;
        private double _volatility;
        private int _numberOfResults;

        //The following variables hold values temporarily during calculations
        private double _workingRating;
        private double _workingRatingDeviation;
        private double _workingVolatility;

        /// <summary>
        /// Constructor. Takes the rating, deviation, and volatility default values
        /// </summary>
        /// <param name="ratingSystem"></param>
        public Rating(RatingCalculator ratingSystem)
        {
            _ratingSystem = ratingSystem;
            _rating = _ratingSystem.GetDefaultRating();
            _ratingDeviation = _ratingSystem.GetDefaultRatingDeviation();
            _volatility = ratingSystem.GetDefaultVolatility();
        }

        /// <summary>
        /// Constuctor. Allows you to pass in the rating, deviation, and volatility.
        /// </summary>
        /// <param name="ratingSystem">The RatingCalculator</param>
        /// <param name="initRating">Initialized rating</param>
        /// <param name="initRatingDeviation">Initialized deviation</param>
        /// <param name="initVolatility">Initialized volatility</param>
        public Rating(RatingCalculator ratingSystem, double initRating, double initRatingDeviation, double initVolatility)
        {
            _ratingSystem = ratingSystem;
            _rating = initRating;
            _ratingDeviation = initRatingDeviation;
            _volatility = initVolatility;
        }

        /// <summary>
        /// Return the average skill value of the player.
        /// </summary>
        /// <returns></returns>
        public double GetRating()
        {
            return _rating;
        }

        /// <summary>
        /// Set the average skill value of the player
        /// </summary>
        /// <param name="rating"></param>
        public void SetRating(double rating)
        {
            _rating = rating;
        }

        /// <summary>
        /// Returns the average skill value of the player scaled down to the scale used by the algorithm.
        /// </summary>
        /// <returns></returns>
        public double GetGlicko2Rating()
        {
            return _ratingSystem.ConvertRatingToGlicko2Scale(_rating);
        }

        /// <summary>
        /// Set the average skill value, taking in a value in Glicko2 scale.
        /// </summary>
        /// <param name="rating"></param>
        public void SetGlicko2Rating(double rating)
        {
            _rating = _ratingSystem.ConvertRatingToOriginalGlickoScale(rating);
        }

        public double GetVolatility()
        {
            return _volatility;
        }

        public void SetVolatility(double volatility)
        {
            _volatility = volatility;
        }

        public double GetRatingDeviation()
        {
            return _ratingDeviation;
        }

        public void SetRatingDeviation(double ratingDeviation)
        {
            _ratingDeviation = ratingDeviation;
        }

        /// <summary>
        /// Return the rating deviation of the player scaled down to the scale used by the algorithm
        /// </summary>
        /// <returns></returns>
        public double GetGlicko2RatingDeviation()
        {
            return _ratingSystem.ConvertRatingDeviationToGlicko2Scale(_ratingDeviation);
        }

        /// <summary>
        /// Set the rating deviation, taking in a value in Glicko2 scale.
        /// </summary>
        /// <param name="ratingDeviation"></param>
        public void SetGlicko2RatingDeviation(double ratingDeviation)
        {
            _ratingDeviation = _ratingSystem.ConvertRatingDeviationToOriginalGlickoScale(ratingDeviation);
        }

        /// <summary>
        /// Used by the RatingCalculator to move interim calculations into their "proper" places
        /// </summary>
        public void FinaliseRating()
        {
            SetGlicko2Rating(_workingRating);
            SetGlicko2RatingDeviation(_workingRatingDeviation);
            SetVolatility(_workingVolatility);

            SetWorkingRatingDeviation(0);
            SetWorkingRating(0);
            SetWorkingVolatility(0);
        }

        public int GetNumberOfResults()
        {
            return _numberOfResults;
        }

        public void IncrementNumberOfResults(int increment)
        {
            _numberOfResults = _numberOfResults + increment;
        }

        public void SetWorkingVolatility(double workingVolatility)
        {
            _workingVolatility = workingVolatility;
        }

        public void SetWorkingRating(double workingRating)
        {
            _workingRating = workingRating;
        }

        public void SetWorkingRatingDeviation(double workingRatingDeviation)
        {
            _workingRatingDeviation = workingRatingDeviation;
        }
    }
}