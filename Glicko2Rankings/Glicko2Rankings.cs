extern alias Distance;

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MongoDB.Driver;

namespace Glicko2Rankings
{
    public class Glicko2Rankings : DistanceServerPlugin
    {
        public override string DisplayName => "Glicko-2 Rankings";
        public override string Author => "Tribow; Discord: Tribow#5673";
        public override int Priority => -6;
        public override SemanticVersion ServerVersion => new SemanticVersion("0.4.1");

        private bool matchEnded = false;
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
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:serverVersion", "Server Version: v0.2.7"));
                matchEnded = false;
            });

            //Loop through all players and check if all finished, if they all did grab their finish times
            //If enough players were in a match, calculate their rank and display it
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
                            playersInMatch.Add(player.Name);

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
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:playerRanking", "[19e681]" + playersInMatch[i] + "'s new rank: [-]" + playerRankings[i]));
                        }

                        playersInMatch.Clear();
                        timeInMatch.Clear();
                    }

                    Server.SayChat(DistanceChat.Server("Glicko2Rankings:allFinished", "[00FFFF]Match Ended![-]"));
                }
            });
        }
    }
}