extern alias Distance;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using WorkshopSearch;

namespace Glicko2Rankings
{
    public class Glicko2Rankings : DistanceServerPlugin
    {
        public override string DisplayName => "Glicko-2 Rankings";
        public override string Author => "Tribow; Discord: Tribow#5673";
        public override int Priority => -6;
        public override SemanticVersion ServerVersion => new SemanticVersion("0.4.0");

        private bool matchEnded = false;
        private SimulateMatch calculateMatch = new SimulateMatch();
        private List<DistancePlayer> uncheckedPlayers = new List<DistancePlayer>();

        public override void Start()
        {
            Log.Info("Welcome to the ranking system!");

            DistanceServerMainStarter.Instance.StartCoroutine(FindWorkshopLevels());

            Server.OnPlayerValidatedEvent.Connect(player =>
            {
                //When a new player joins, add them to the unchecked list
                uncheckedPlayers.Add(player);
            });

            //A new match has started (Adding server version so I know I updated the server)
            Server.OnLevelStartInitiatedEvent.Connect(() =>
            {
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:matchEnded", "[00FFFF]A new match has started![-]"));
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:serverVersion", "Server Version: v0.4.7"));
                matchEnded = false;
            });

            //Side wheelie easter egg
            DistanceServerMain.GetEvent<Events.Instanced.TrickComplete>().Connect(trickData =>
            {
                if (trickData.sideWheelieMeters_ > 20)
                {
                    Random rnd = new Random();
                    if(rnd.Next(0,11) < 1)
                    {
                        Server.SayChat(DistanceChat.Server("Glicko2Rankings:sidewheelie", "SIIICK " + trickData.sideWheelieMeters_ + " METER SIDE WHEELIE"));
                    }
                }
            });

            //When a player's data is submitted, go through the uncheckedPlayers list and post ranks of each player in the list.
            //There is a chance that a player's rank fails to get posted, but the situation is rare.
            DistanceServerMain.GetEvent<Events.ClientToServer.SubmitPlayerData>().Connect((d, info) =>
            {
                if (uncheckedPlayers.Count > 0)
                {
                    bool success = false;
                    foreach (DistancePlayer player in uncheckedPlayers)
                    {
                        if (player.Name == d.data_.playerName_)
                        {
                            string colorid = GetColorID(d.data_.carColors_);
                            int playerRank = calculateMatch.GetRating(player.Name + "|||||" + colorid);
                            if (playerRank > 0)
                            {
                                Server.SayChat(DistanceChat.Server("Glicko2Rankings:joinedPlayerRank", "[19e681]" + player.Name + " Rank: [-]" + playerRank));
                            }
                            success = true;
                        }
                    }

                    if(success)
                    {
                        uncheckedPlayers.Clear();
                    }
                }
            });

            //Loop through all players and check if all finished, if they all did grab their finish times
            //If enough players were in a match, calculate their rank and display it
            //(THis part can have a NullReference but I'm not sure how yet)
            DistanceServerMain.GetEvent<Events.Instanced.Finished>().Connect((instance, data) =>
            {
                bool allPlayersFinished = true;

                List<DistancePlayer> distancePlayers = new List<DistancePlayer>(Server.DistancePlayers.Values);

                if (Server.DistancePlayers.Count > 0 && !matchEnded)
                {
                    foreach (DistancePlayer player in distancePlayers)
                    {
                        if (!player.Car.Finished)
                        {
                            allPlayersFinished = false;
                        }
                    }
                }

                if (allPlayersFinished && !matchEnded)
                {
                    matchEnded = true;

                    List<string> playersInMatch = new List<string>();
                    List<int> timeInMatch = new List<int>();

                    if (distancePlayers.Count > 1)
                    {
                        foreach (DistancePlayer player in distancePlayers)
                        {
                            string colorid = GetColorID(player.Car.CarColors);
                            playersInMatch.Add(player.Name + "|||||" + colorid);

                            if (player.Car.FinishType == Distance::FinishType.Normal)
                                timeInMatch.Add(player.Car.FinishData);
                            else
                                timeInMatch.Add(0);
                        }

                        //Calculate rankings
                        calculateMatch.CalculateResults(playersInMatch, timeInMatch);

                        //Post rankings in chat
                        List<int> playerRankings = calculateMatch.GetSpecificRatings(playersInMatch);

                        for (int i = 0; i < playersInMatch.Count; i++)
                        {
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:playerRanking", "[19e681]" + distancePlayers[i].Name + "'s new rank: [-]" + playerRankings[i]));
                        }

                        playersInMatch.Clear();
                        timeInMatch.Clear();
                        playerRankings.Clear();
                    }

                    Server.SayChat(DistanceChat.Server("Glicko2Rankings:allFinished", "[00FFFF]Match Ended![-]"));
                }

                distancePlayers.Clear();
            });
        }

        /// <summary>
        /// Finds the workshop levels it needs for the competitive server. Logs what it finds as well.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator FindWorkshopLevels()
        {
            DistanceSearchRetriever retriever = null;

            try
            {
                retriever = new DistanceSearchRetriever(new DistanceSearchParameters()
                {
                    Search = WorkshopSearchParameters.CollectionFiles("2799461592"),
                }, false);
            }
            catch (Exception e)
            {
                Log.Error($"Error retrieving workshop level settings:\n{e}");
            }

            if (retriever == null)
            {
                Log.Error("No workshop levels defined.");
            }
            else
            {
                retriever.StartCoroutine();
                yield return retriever.TaskCoroutine;
                if (retriever.HasError)
                {
                    Log.Error($"Error retrieving levels: {retriever.Error}");
                }

                List<DistanceLevel> results = retriever.Results.ConvertAll(result => result.DistanceLevelResult);
                string listString = $"Levels ({results.Count}):";
                foreach (DistanceLevel level in results)
                {
                    listString += $"\n{level.Name}";
                }
                Log.Info(listString);

                if (results.Count == 0)
                {
                    Log.Error("Workshop search returned nothing");
                }
                else
                {
                    BasicAutoServer.BasicAutoServer AutoServer = DistanceServerMain.Instance.GetPlugin<BasicAutoServer.BasicAutoServer>();
                    AutoServer.Playlist.AddRange(results);
                    AutoServer.Playlist.Shuffle();
                }
            }
            yield break;
        }

        /// <summary>
        /// Gets the colorID for that specific player. This ID is based on the car's colors.
        /// </summary>
        /// <param name="carColor">The player's CarColors needed to generate the ID</param>
        /// <returns></returns>
        private string GetColorID(Distance.CarColors carColor)
        {
            Regex regex = new Regex("[^1-9]");
            return regex.Replace(carColor.primary_.ToString()
                                + carColor.secondary_.ToString()
                                + carColor.glow_.ToString()
                                + carColor.sparkle_.ToString(), "");
        }

       
    }

    static class ListShuffler
    {
        /// <summary>
        /// Creates a new System.Random that will truly be a lot more random than just calling it normally
        /// </summary>
        /// <returns></returns>
        public static Random ThreadSafeRandom()
        {
            Random Local = new Random();
            return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));
        }

        /// <summary>
        /// Shuffles the order of a list
        /// </summary>
        /// <typeparam name="T">the type</typeparam>
        /// <param name="list">the list to be shuffled</param>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom().Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}