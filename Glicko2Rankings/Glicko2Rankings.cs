extern alias Distance;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security;
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
        private bool updatingPlaylist = false;
        private bool diversionlmao = true;
        private SimulateMatch calculateMatch = new SimulateMatch();
        private List<DistancePlayer> uncheckedPlayers = new List<DistancePlayer>();
        private List<DistancePlayer> playersAtLevelStart = new List<DistancePlayer>();
        private Process DatabaseHandler;

        public override void Start()
        {
            Log.Info("Welcome to the ranking system!");

            try
            {
                DatabaseHandler = Process.Start(@"CompetitiveServerDatabaseHandler");
            }
            catch(Exception e)
            {
                Log.Error("Could not find the DatabaseHandler! Without it I cannot communicate to the database!");
            }

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
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:serverVersion", "Server Version: v1.5.1"));
                matchEnded = false;

                BasicAutoServer.BasicAutoServer AutoServer = DistanceServerMain.Instance.GetPlugin<BasicAutoServer.BasicAutoServer>();

                //If the current level is the same as the last level of the playlist
                if (Server.CurrentLevel == AutoServer.Playlist[AutoServer.Playlist.Count - 1] && !updatingPlaylist)
                {
                    //Reshuffle the list of levels and this should also update with the Competitive Levels collection
                    DistanceServerMainStarter.Instance.StartCoroutine(FindWorkshopLevels());
                }
            });

            //Track the players who were at the start of the match. 
            //This check happens really early, would rather have it be right when the countdown ends
            Server.OnLevelStartedEvent.Connect(() =>
            {
                //Server.SayChat(DistanceChat.Server("Glicko2Rankings:levelStarted", "LEVEL START!"));
                playersAtLevelStart = new List<DistancePlayer>(Server.DistancePlayers.Values);
                Server.SayChat(DistanceChat.Server("Glicko2Rankings:leveldifficulty", $"Level Difficulty: {Server.CurrentLevel.Difficulty}"));
            });

            //Side wheelie easter egg
            DistanceServerMain.GetEvent<Events.Instanced.TrickComplete>().Connect(trickData =>
            {
                if (trickData.sideWheelieMeters_ > 20)
                {
                    Random rnd = new Random();
                    if(rnd.Next(0,11) < 1)
                    {
                        Server.SayChat(DistanceChat.Server("Glicko2Rankings:sidewheelie", $"SIIICK {trickData.sideWheelieMeters_} METER SIDE WHEELIE"));
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
                            int playerRating = calculateMatch.GetRating($"{SecurityElement.Escape(player.Name)}|||||{colorid}");
                            string playerRank = calculateMatch.GetRank($"{SecurityElement.Escape(player.Name)}|||||{colorid}");
                            if (playerRating > 0)
                            {
                                Server.SayChat(DistanceChat.Server("Glicko2Rankings:joinedPlayerRank", $"[19e681]{player.Name} Rating: [-]{playerRating} | Rank: {playerRank}"));
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

            //Commands
            Server.OnChatMessageEvent.Connect((chatMessage) =>
            {
                //The "/rank" command will post the player's rank information once they send it
                if (Regex.Match(chatMessage.Message, @"(?<=/rank ).*").Success)
                {
                    string[] splitMessage = chatMessage.Message.Split(new string[] { @"/rank "}, StringSplitOptions.None);

                    string rankInput = splitMessage[1];
                    //If the player added a number after the search 
                    if (Regex.Match(rankInput, @"^\d+$").Success)
                    {
                        int theRank = int.Parse(rankInput);
                        List<string> playersAtRank = calculateMatch.GetPlayersAtRank(theRank);

                        if(playersAtRank.Count > 0)
                        {
                            foreach (string player in playersAtRank)
                            {
                                string[] splitInfo = player.Split(new string[] { "|||||" }, StringSplitOptions.None);
                                int playerRating = calculateMatch.GetRating(player);
                                Server.SayChat(DistanceChat.Server("Gilcko2Rankings:commandSearchedNumberRank", $"[19e681]{splitInfo[0]} Rating: [-]{playerRating}"));
                            }
                        }
                        else
                        {
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:commandSearchedNumberFailed", $"There are no players at rank: {theRank}"));
                        }
                    }
                        
                    //The command inputted something that wasn't just numbers so they must be searching for a player!
                    List<string> playersAtName = calculateMatch.GetPlayers(SecurityElement.Escape(rankInput));
                    if(playersAtName.Count > 0)
                    {
                        foreach(string player in playersAtName)
                        {
                            int playerRating = calculateMatch.GetRating(player);
                            string playerRank = calculateMatch.GetRank(player);
                            if (playerRating > 0)
                            {
                                Server.SayChat(DistanceChat.Server("Glicko2Rankings:commandSearchedPlayersRank", $"[19e681]{rankInput} Rating: [-]{playerRating} | Rank: {playerRank}"));
                            } 
                        }
                    }
                }
                else if (Regex.Match(chatMessage.Message, @"/rank").Success)
                {
                    //If there's nothing after "/rank" then the player must have  typed it by itself. They want to know their own data not someone else's!
                    DistancePlayer player = Server.GetDistancePlayer(chatMessage.SenderGuid);
                    string colorid = GetColorID(player.Car.CarColors);
                    int playerRating = calculateMatch.GetRating($"{SecurityElement.Escape(player.Name)}|||||{colorid}");
                    string playerRank = calculateMatch.GetRank($"{SecurityElement.Escape(player.Name)}|||||{colorid}");
                    if (playerRating > 0)
                    {
                        Server.SayChat(DistanceChat.Server("Glicko2Rankings:commandPlayerRank", $"[19e681]{player.Name} Rating: [-]{playerRating} | Rank: {playerRank}"));
                    }
                }
            });

            //Loop through all players and check if all finished, if they all did grab their finish times
            //If enough players were in a match, calculate their rank and display it
            //(THis part can have a NullReference but I'm not sure how yet)
            DistanceServerMain.GetEvent<Events.Instanced.Finished>().Connect((instance, data) =>
            {
                TryCalculateMatch();
            });

            //Check when a player leaves too
            Server.OnPlayerDisconnectedEvent.Connect((player) =>
            {
                TryCalculateMatch();
            });

            //Be sure to close the DatabaseHandler when the server stops
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                try 
                { 
                    DatabaseHandler.Kill(); 
                } 
                catch(Exception e) 
                { 
                    Log.Error("There is no Database Handler!"); 
                }
            };
        }

        private void TryCalculateMatch()
        {
            bool allPlayersFinished = true;

            List<DistancePlayer> distancePlayers = new List<DistancePlayer>(Server.DistancePlayers.Values);

            //Check if all players have finished
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

            //When all are finished, it's time to do calculations
            if (allPlayersFinished && !matchEnded)
            {
                matchEnded = true;

                List<PlayerMatchData> playerMatchDatas = new List<PlayerMatchData>();
                List<string> playersInMatch = new List<string>(); //List that holds playerinfo of each player in match
                List<int> timeInMatch = new List<int>(); //List that holds the finish time of each player in the match
                List<int> oldplayerRatings = new List<int>(); //List the holds the previous ratings before the calculation of each player in the match

                if (distancePlayers.Count > 1)
                {
                    foreach (DistancePlayer player in distancePlayers)
                    {
                        bool joinedLate = true; //If this remains true, the player will be marked a participant
                        string colorid = GetColorID(player.Car.CarColors);
                        string playerID = $"{SecurityElement.Escape(player.Name)}|||||{colorid}";
                        PlayerMatchData matchData = new PlayerMatchData();
                        matchData.PlayerID = playerID; //playersInMatch.Add(playerID);
                        matchData.OldRating = calculateMatch.GetRating(playerID); //oldplayerRatings.Add(calculateMatch.GetRating(playerID));
                        matchData.PlayerName = player.Name;


                        //Check to be sure the player in the match was a player at the start of the match
                        foreach (DistancePlayer startPlayer in playersAtLevelStart)
                            if (startPlayer == player)
                                joinedLate = false;

                        if (player.Car.FinishType == Distance::FinishType.Normal && !joinedLate)
                            matchData.PlayerTime = player.Car.FinishData; //timeInMatch.Add(player.Car.FinishData);
                        else if (player.Car.FinishType == Distance::FinishType.DNF && !joinedLate)
                            matchData.PlayerTime = 2000000000; //timeInMatch.Add(2000000000);
                        else if (player.Car.FinishType == Distance::FinishType.Spectate && !joinedLate)
                            matchData.PlayerTime = 2100000000; //timeInMatch.Add(2100000000);
                        else
                            matchData.PlayerTime = 0; //timeInMatch.Add(0);

                        playerMatchDatas.Add(matchData);
                    }

                    playerMatchDatas.Sort();

                    foreach (PlayerMatchData matchData in playerMatchDatas)
                    {
                        playersInMatch.Add(matchData.PlayerID);
                        timeInMatch.Add(matchData.PlayerTime);
                        oldplayerRatings.Add(matchData.OldRating);
                    }


                    //Calculate rankings
                    calculateMatch.CalculateResults(playersInMatch, timeInMatch);

                    //Post ratings/rankings in chat
                    List<int> playerRatings = calculateMatch.GetSpecificRatings(playersInMatch);
                    List<string> playerRankings = calculateMatch.GetSpecificRanks(playersInMatch);

                    Server.SayChat(DistanceChat.Server("Glicko2Rankings:thelegend", "[19e681]Player[-] | Rating | [00FF00]Earn[-]/[FF0000]Loss[-] | Rank"));

                    for (int i = 0; i < playersInMatch.Count; i++)
                    {
                        int ratingDifference = playerRatings[i] - oldplayerRatings[i];

                        if (ratingDifference >= 0)
                        {
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:playerRanking", $"[19e681]{playerMatchDatas[i].PlayerName}[-] | {playerRatings[i]} | [00FF00]{ratingDifference}[-] | {playerRankings[i]}"));
                        }
                        else
                        {
                            Server.SayChat(DistanceChat.Server("Glicko2Rankings:playerRanking", $"[19e681]{playerMatchDatas[i].PlayerName}[-] | {playerRatings[i]} | [FF0000]{ratingDifference}[-] | {playerRankings[i]}"));
                        }
                    }

                    playerRatings.Clear();
                    playerRankings.Clear();
                    playersInMatch.Clear();
                    timeInMatch.Clear();
                    oldplayerRatings.Clear();
                    playerMatchDatas.Clear();
                }

                Server.SayChat(DistanceChat.Server("Glicko2Rankings:allFinished", "[00FFFF]Match Ended![-]"));
            }

            distancePlayers.Clear();
        }

        /// <summary>
        /// Finds the workshop levels it needs for the competitive server. Logs what it finds as well.
        /// Adds what it finds into a shuffled list for BasicAutoServer to use.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator FindWorkshopLevels()
        {
            updatingPlaylist = true;
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

                if (results.Count == 0)
                {
                    Log.Error("Workshop search returned nothing");
                }
                else
                {
                    BasicAutoServer.BasicAutoServer AutoServer = DistanceServerMain.Instance.GetPlugin<BasicAutoServer.BasicAutoServer>();
                    AutoServer.Playlist.Clear();
                    AutoServer.Playlist.AddRange(OfficialPlaylist);
                    AutoServer.Playlist.AddRange(results);
                    AutoServer.Playlist.Shuffle();

                    string listString = $"Levels ({results.Count}):";
                    foreach (DistanceLevel level in AutoServer.Playlist)
                    {
                        listString += $"\n{level.Name}";
                    }
                    Log.Info(listString);

                    //The first level that gets chosen is always diversion so this will skip diversion I think maybe
                    if (diversionlmao)
                    {
                        diversionlmao = false;
                        AutoServer.NextLevel();

                    }
                }
            }
            updatingPlaylist = false;
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

        List<DistanceLevel> OfficialPlaylist = new List<DistanceLevel>()
        {
            new DistanceLevel()
            {
                Name = "Forgotten Utopia",
                RelativeLevelPath = "OfficialLevels/Forgotten Utopia.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "A Deeper Void",
                RelativeLevelPath = "OfficialLevels/A Deeper Void.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Eye of the Storm",
                RelativeLevelPath = "OfficialLevels/Eye of the Storm.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "The Sentinel Still Watches",
                RelativeLevelPath = "OfficialLevels/The Sentinel Still Watches.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Shadow of the Beast",
                RelativeLevelPath = "OfficialLevels/Shadow of the Beast.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Pulse of a Violent Heart",
                RelativeLevelPath = "OfficialLevels/Pulse of a Violent Heart.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "It Was Supposed To Be Perfect",
                RelativeLevelPath = "OfficialLevels/It Was Supposed To Be Perfect.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Abyss",
                RelativeLevelPath = "OfficialLevels/Abyss.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Aftermath",
                RelativeLevelPath = "OfficialLevels/Aftermath.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Approach",
                RelativeLevelPath = "OfficialLevels/Approach.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Brink",
                RelativeLevelPath = "OfficialLevels/Brink.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Broken Symmetry",
                RelativeLevelPath = "OfficialLevels/Broken Symmetry.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Cataclysm",
                RelativeLevelPath = "OfficialLevels/Cataclysm.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Chroma",
                RelativeLevelPath = "OfficialLevels/Chroma.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Compression",
                RelativeLevelPath = "OfficialLevels/Compression.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Contagion",
                RelativeLevelPath = "OfficialLevels/Contagion.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Continuum",
                RelativeLevelPath = "OfficialLevels/Continuum.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Corruption",
                RelativeLevelPath = "OfficialLevels/Corruption.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Departure",
                RelativeLevelPath = "OfficialLevels/Departure.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Destination Unknown",
                RelativeLevelPath = "OfficialLevels/Destination Unknown.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Deterrence",
                RelativeLevelPath = "OfficialLevels/Deterrence.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Digital",
                RelativeLevelPath = "OfficialLevels/Digital.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Dissolution",
                RelativeLevelPath = "OfficialLevels/Dissolution.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Diversion",
                RelativeLevelPath = "OfficialLevels/Diversion.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Eclipse",
                RelativeLevelPath = "OfficialLevels/Eclipse.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Embers",
                RelativeLevelPath = "OfficialLevels/Embers.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Factory",
                RelativeLevelPath = "OfficialLevels/Factory.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Falling Through",
                RelativeLevelPath = "OfficialLevels/Falling Through.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Friction",
                RelativeLevelPath = "OfficialLevels/Friction.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Fulcrum",
                RelativeLevelPath = "OfficialLevels/Fulcrum.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Ground Zero",
                RelativeLevelPath = "OfficialLevels/Ground Zero.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Hard Light Transfer",
                RelativeLevelPath = "OfficialLevels/Hard Light Transfer.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Incline",
                RelativeLevelPath = "OfficialLevels/Incline.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Isolation",
                RelativeLevelPath = "OfficialLevels/Isolation.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Le Teleputo",
                RelativeLevelPath = "OfficialLevels/Le Teleputo.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Liminal",
                RelativeLevelPath = "OfficialLevels/Liminal.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Lost Society",
                RelativeLevelPath = "OfficialLevels/Lost Society.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Luminescence",
                RelativeLevelPath = "OfficialLevels/Luminescence.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Micro",
                RelativeLevelPath = "OfficialLevels/Micro.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Monolith",
                RelativeLevelPath = "OfficialLevels/Monolith.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Negative Space",
                RelativeLevelPath = "OfficialLevels/Negative Space.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Neo Seoul",
                RelativeLevelPath = "OfficialLevels/Neo Seoul.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Neo Seoul II",
                RelativeLevelPath = "OfficialLevels/Neo Seoul II.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Observatory",
                RelativeLevelPath = "OfficialLevels/Observatory.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Overload",
                RelativeLevelPath = "OfficialLevels/Overload.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Precept",
                RelativeLevelPath = "OfficialLevels/Precept.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Projection",
                RelativeLevelPath = "OfficialLevels/Projection.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Repulsion",
                RelativeLevelPath = "OfficialLevels/Repulsion.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Research",
                RelativeLevelPath = "OfficialLevels/Research.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Resonance",
                RelativeLevelPath = "OfficialLevels/Resonance.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Rooftops",
                RelativeLevelPath = "OfficialLevels/Rooftops.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Sector 0",
                RelativeLevelPath = "OfficialLevels/Sector 0.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Serenity",
                RelativeLevelPath = "OfficialLevels/Serenity.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "SR Motorplex",
                RelativeLevelPath = "OfficialLevels/SR Motorplex.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Station",
                RelativeLevelPath = "OfficialLevels/Station.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Stronghold",
                RelativeLevelPath = "OfficialLevels/Stronghold.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Tharsis Tholus",
                RelativeLevelPath = "OfficialLevels/Tharsis Tholus.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "The Observer Effect",
                RelativeLevelPath = "OfficialLevels/The Observer Effect.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Virtual Rift",
                RelativeLevelPath = "OfficialLevels/Virtual Rift.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "White Lightning Returns",
                RelativeLevelPath = "OfficialLevels/White Lightning Returns.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Zenith",
                RelativeLevelPath = "OfficialLevels/Zenith.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Affect",
                RelativeLevelPath = "CommunityLevels/Affect.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Binary Construct",
                RelativeLevelPath = "CommunityLevels/Binary Construct.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Candles of Hekate",
                RelativeLevelPath = "CommunityLevels/Candles of Hekate.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Event Horizon",
                RelativeLevelPath = "CommunityLevels/Event Horizon.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Forsaken Shrine",
                RelativeLevelPath = "CommunityLevels/Forsaken Shrine.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Gravity",
                RelativeLevelPath = "CommunityLevels/Gravity.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Hardline",
                RelativeLevelPath = "CommunityLevels/Hardline.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Impulse",
                RelativeLevelPath = "CommunityLevels/Impulse.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Industrial Fury",
                RelativeLevelPath = "CommunityLevels/Industrial Fury.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Inferno",
                RelativeLevelPath = "CommunityLevels/Inferno.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Instability",
                RelativeLevelPath = "CommunityLevels/Instability.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Knowledge",
                RelativeLevelPath = "CommunityLevels/Knowledge.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Mentality",
                RelativeLevelPath = "CommunityLevels/Mentality.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Method",
                RelativeLevelPath = "CommunityLevels/Method.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Moonlight",
                RelativeLevelPath = "CommunityLevels/Moonlight.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Noir",
                RelativeLevelPath = "CommunityLevels/Noir.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Outrun",
                RelativeLevelPath = "CommunityLevels/Outrun.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Paradise Lost",
                RelativeLevelPath = "CommunityLevels/Paradise Lost.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Particular Journey",
                RelativeLevelPath = "CommunityLevels/Particular Journey.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Red",
                RelativeLevelPath = "CommunityLevels/Red.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Ruin",
                RelativeLevelPath = "CommunityLevels/Ruin.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Sea",
                RelativeLevelPath = "CommunityLevels/Sea.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Sender",
                RelativeLevelPath = "CommunityLevels/Sender.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Shafty",
                RelativeLevelPath = "CommunityLevels/Shafty.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Shallow",
                RelativeLevelPath = "CommunityLevels/Shallow.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Shrine",
                RelativeLevelPath = "CommunityLevels/Shrine.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Static Fire Signal",
                RelativeLevelPath = "CommunityLevels/Static Fire Signal.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Sugar Rush",
                RelativeLevelPath = "CommunityLevels/Sugar Rush.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Sword",
                RelativeLevelPath = "CommunityLevels/Sword.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Tetreal",
                RelativeLevelPath = "CommunityLevels/Tetreal.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Turbines",
                RelativeLevelPath = "CommunityLevels/Turbines.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Volcanic Rush",
                RelativeLevelPath = "CommunityLevels/Volcanic Rush.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Whisper",
                RelativeLevelPath = "CommunityLevels/Whisper.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "White",
                RelativeLevelPath = "CommunityLevels/White.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Wired",
                RelativeLevelPath = "CommunityLevels/Wired.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
            new DistanceLevel()
            {
                Name = "Yellow",
                RelativeLevelPath = "CommunityLevels/Yellow.bytes",
                WorkshopFileId = "",
                GameMode = "Sprint",
            },
        };
    }

    /// <summary>
    /// Literally just exists to shuffle lists better
    /// </summary>
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
            Random rnd = ThreadSafeRandom();
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}