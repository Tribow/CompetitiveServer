extern alias Distance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Glicko2Rankings
{
    public class Glicko2Rankings : DistanceServerPlugin
    {
        public override string DisplayName => "Glicko-2 Rankings";
        public override string Author => "Tribow; Discord: Tribow#5673";
        public override int Priority => -6;
        public override SemanticVersion ServerVersion => new SemanticVersion("0.4.0");

        private bool matchEnded = false;
        private List<string> playersInMatch = new List<string>();
        private List<int> timeInMatch = new List<int>();
        private SimulateMatch calculateMatch = new SimulateMatch();

        public override void Start()
        {
            Log.Info("Welcome to the ranking system!");
            

            Server.OnPlayerValidatedEvent.Connect(player =>
            {
                //When a new player joins, post their rank (if they have one)
                int playerRank = calculateMatch.GetRating(player.Name);
                if (playerRank > 0)
                {
                    Server.SayChat(DistanceChat.Server("Glicko2Rankings:joinedPlayerRank", "[19e681]" + player.Name + " Rank: [-]" + playerRank));
                }
            });

            //A new match has started (Adding server version so I know I updated the server)
            Server.OnLevelStartInitiatedEvent.Connect(() =>
            {
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:matchEnded", "[00FFFF]A new match has started![-]"));
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:serverVersion", "Server Version: v0.0.3"));
                playersInMatch.Clear();
                timeInMatch.Clear();
                matchEnded = false;
            });

            //There might be a more efficient and cleaner way to do this, but for now I don't know it
            //Loop through all players and check if all finished, if they all did grab their finish times
            //If enough players were in a match, calculate their rank and display it
            Server.OnChatMessageEvent.Connect(message =>
            {
                bool allPlayersFinished = true;
                playersInMatch.Clear();
                timeInMatch.Clear();

                if (Server.DistancePlayers.Count > 0 && !matchEnded)
                {
                    
                    List<DistancePlayer> distancePlayers = new List<DistancePlayer>(Server.DistancePlayers.Values);
                    foreach (DistancePlayer player in distancePlayers)
                    {
                        if (player.Car != null && player.Car.Finished && player.Car.FinishType == Distance::FinishType.Normal)
                        {
                            
                            playersInMatch.Add(player.Name);
                            timeInMatch.Add(player.Car.FinishData);
                        }
                        else if (player.Car != null && player.Car.Finished && player.Car.FinishType == Distance::FinishType.Spectate || player.Car.FinishType == Distance::FinishType.JoinedLate)
                        {
                            playersInMatch.Add(player.Name);
                            timeInMatch.Add(0);
                        }
                        else
                        {
                            allPlayersFinished = false;
                        }
                    }
                }
                else
                {
                    allPlayersFinished = false;
                }

                if (allPlayersFinished && !matchEnded)
                {
                    matchEnded = true;

                    if (playersInMatch.Count > 1)
                    {
                        //Update Player ratings
                        foreach (string player in playersInMatch)
                        {
                            calculateMatch.InputPlayerInMatch(player);
                        }

                        //Calculate rankings
                        calculateMatch.CalculateResults(playersInMatch, timeInMatch);

                        //Post rankings in chat
                        List<int> playerRankings = calculateMatch.GetSpecificRatings(playersInMatch);

                        for(int i = 0; i < playersInMatch.Count; i++)
                        {
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:playerRanking", "[19e681]" + playersInMatch[i] + "'s new rank: [-]" + playerRankings[i]));
                        }
                    }

                    Server.SayChat(DistanceChat.Server("Glicko2Rankings:allFinished", "[00FFFF]Match Ended![-]"));
                }

            });

            
        }
    }
}
