using System;
using System.Collections.Generic;
using MongoDB.Driver;

namespace Glicko2Rankings
{
    //Right now this is entirely local data, which is cringe. I will need to put this in a database so it can
    //Upload the rankings to a database and update that information with new data whenever necessary
    public class SimulateMatch
    {
        private Dictionary<string, Rating> players = new Dictionary<string, Rating>();
        private readonly RatingCalculator calculator = new RatingCalculator();
        private readonly RatingPeriodResults results = new RatingPeriodResults();
        private MongoClientSettings settings;
        private MongoClient client;
        private IMongoDatabase database;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SimulateMatch()
        {
            
            settings = MongoClientSettings.FromConnectionString("rSc4HjHDHoBFNrLU");
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            client = new MongoClient(settings);
            database = client.GetDatabase("rankings");
        }

        /// <summary>
        /// Adds a player into the match. New players get added to the rating database
        /// </summary>
        /// <param name="player"></param>
        public void InputPlayerInMatch(string player)
        {
            Rating playerRating = new Rating(calculator);
            try
            {
                players.Add(player, playerRating);
            }
            catch (ArgumentException)
            {
                Log.Info("Player already exists!");
            }
        }


        public void CalculateResults(List<string> playerNames, List<int> playerTimes)
        {
            //Just in case something was wrong
            if (playerNames.Count != playerTimes.Count)
            {
                Log.Info("The amount of players do not match the amount of times!");
                return;
            }

            for (int i = 0; i < playerTimes.Count; i++)
            {
                //Times that are 0 are spectators or joined laters
                if (playerTimes[i] == 0)
                    results.AddParticipant(players[playerNames[i]]);

                for (int j = 0; j < playerTimes.Count; j++)
                {
                    if (j > i)
                    {
                        //Player names should not be the same nor should any of the times be 0
                        if (playerNames[i] != playerNames[j] && playerTimes[i] != 0 && playerTimes[j] != 0)
                        {
                            //Checking for Draw
                            if (playerTimes[i] == playerTimes[j])
                            {
                                results.AddDraw(players[playerNames[i]], players[playerNames[j]]);
                            }
                            else
                            {
                                //Win or Lose Results
                                if (playerTimes[i] < playerTimes[j])
                                {
                                    results.AddResult(players[playerNames[i]], players[playerNames[j]]);
                                }
                                else
                                {
                                    results.AddResult(players[playerNames[j]], players[playerNames[i]]);
                                }
                            }
                        }
                    }
                }
            }


            calculator.UpdateRatings(results);
        }

        /// <summary>
        /// Returns a list of ratings in the order of the list it was given.
        /// The list will be empty if none of the players given have a rating
        /// </summary>
        /// <param name="playerNames">Should be the list of players in a match</param>
        /// <returns></returns>
        public List<int> GetSpecificRatings(List<string> playerNames)
        {
            List<int> ratings = new List<int>();

            foreach (string player in playerNames)
            {
                try
                {
                    ratings.Add((int)players[player].GetRating());
                }
                catch (KeyNotFoundException)
                {
                    Log.Info("Player does not have a rating!");
                }
            }

            return ratings;
        }

        /// <summary>
        /// Returns a given player's ranking.
        /// Returns 0 if the player does not have a ranking
        /// </summary>
        /// <param name="playerName">The player you want the ranking for</param>
        /// <returns></returns>
        public int GetRating(string playerName)
        {
            int rating = 0;

            try
            {
                rating = (int)players[playerName].GetRating();
            }
            catch (KeyNotFoundException)
            {
                Log.Info("Player does not have a rating!");
            }

            return rating;
        }
    }
}