using System.Collections.Generic;

namespace Glicko2Rankings
{
    // https://github.com/MaartenStaa/glicko2-csharp (adjusted)

    public class RatingPeriodResults
    {
        private readonly List<Result> _results = new List<Result>();
        private readonly HashSet<Rating> _participants = new HashSet<Rating>();

        /// <summary>
        /// Constructor. Create an empty result set
        /// </summary>
        public RatingPeriodResults()
        {

        }

        /// <summary>
        /// Constructor. Allows you to initialize the list of participants
        /// </summary>
        /// <param name="participants"></param>
        public RatingPeriodResults(HashSet<Rating> participants)
        {
            _participants = participants;
        }

        /// <summary>
        /// Add a result to the set.
        /// </summary>
        /// <param name="winner"></param>
        /// <param name="loser"></param>
        public void AddResult(Rating winner, Rating loser)
        {
            Result result = new Result(winner, loser);

            _results.Add(result);
        }

        /// <summary>
        /// Add a draw to the set
        /// </summary>
        /// <param name="player1"></param>
        /// <param name="player2"></param>
        public void AddDraw(Rating player1, Rating player2)
        {
            Result result = new Result(player1, player2, true);

            _results.Add(result);
        }

        /// <summary>
        /// Get a list of the results for a given player.
        /// Removes the results the player did not participate in.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public IList<Result> GetResults(Rating player)
        {
            List<Result> filteredResults = new List<Result>();

            foreach (Result result in _results)
            {
                if (result.Participated(player))
                    filteredResults.Add(result);
            }

            return filteredResults;
        }

        /// <summary>
        /// Get all participants whose results are being tracked
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Rating> GetParticipants()
        {
            //Run through results and make sure all players are in the participants set.
            foreach (Result result in _results)
            {
                _participants.Add(result.GetWinner());
                _participants.Add(result.GetLoser());
            }

            return _participants;
        }

        /// <summary>
        /// Add a participant to the rating period so that their rating
        /// will be calculated even if they don't actually compete
        /// </summary>
        /// <param name="rating"></param>
        public void AddParticipant(Rating rating)
        {
            _participants.Add(rating);
        }

        /// <summary>
        /// Clear the result set. (Collect that garbage!)
        /// </summary>
        public void Clear()
        {
            _results.Clear();
        }
    }
}
