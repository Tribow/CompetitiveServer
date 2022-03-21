using System;

namespace Glicko2Rankings
{
    /// <summary>
    /// https://github.com/MaartenStaa/glicko2-csharp (adjusted)
    /// Respresents the result of a match between players
    /// </summary>
    public class Result
    {
        private const double PointsForWin = 1.0;
        private const double PointsForLoss = 0.0;
        private const double PointsForDraw = 0.5;

        private readonly bool _isDraw;
        private readonly Rating _winner;
        private readonly Rating _loser;

        /// <summary>
        /// Record a new result from a match between two players
        /// </summary>
        /// <param name="winner"></param>
        /// <param name="loser"></param>
        /// <param name="isDraw">false by default</param>
        public Result(Rating winner, Rating loser, bool isDraw = false)
        {
            if (!ValidPlayers(winner, loser))
            {
                Log.Info("Players winner and loser are the same player");
            }

            _winner = winner;
            _loser = loser;
            _isDraw = isDraw;
        }

        /// <summary>
        /// Check if the players are not the same for some reason.
        /// </summary>
        /// <param name="player1"></param>
        /// <param name="player2"></param>
        /// <returns></returns>
        private static bool ValidPlayers(Rating player1, Rating player2)
        {
            return player1 != player2;
        }

        /// <summary>
        /// Check if a player participated in the match in this result
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool Participated(Rating player)
        {
            return player == _winner || player == _loser;
        }

        /// <summary>
        /// Returns the "score" for a match.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public double GetScore(Rating player)
        {
            double score;

            if (_winner == player)
                score = PointsForWin;
            else if (_loser == player)
                score = PointsForLoss;
            else
                throw new ArgumentException("Player did not participate in match", "player");

            if (_isDraw)
                score = PointsForDraw;

            return score;
        }

        /// <summary>
        /// Returns the opponent of the given player.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public Rating GetOpponent(Rating player)
        {
            Rating opponent;

            if (_winner == player)
                opponent = _loser;
            else if (_loser == player)
                opponent = _winner;
            else
                throw new ArgumentException("Player did not participate in match", "player");

            return opponent;
        }

        public Rating GetWinner()
        {
            return _winner;
        }

        public Rating GetLoser()
        {
            return _loser;
        }
    }
}
