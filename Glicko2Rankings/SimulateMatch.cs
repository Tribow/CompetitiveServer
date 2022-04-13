using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using System.IO;
//using System.Reflection;

namespace Glicko2Rankings
{
    //Right now this is entirely local data, which is cringe. I will need to put this in a database so it can
    //Upload the rankings to a database and update that information with new data whenever necessary
    public class SimulateMatch
    {
        private Dictionary<string, Rating> players = new Dictionary<string, Rating>();
        private readonly RatingCalculator calculator = new RatingCalculator();
        private readonly RatingPeriodResults results = new RatingPeriodResults();
        private XmlDocument rankXml;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SimulateMatch()
        {
            //Log.Debug(Directory.GetCurrentDirectory() + @"\RankData.xml");
            if (File.Exists(Directory.GetCurrentDirectory() + @"\RankData.xml"))
            {
                Log.Info("RankData file exists");
                rankXml.Load(Directory.GetCurrentDirectory() + @"\RankData.xml");

                //Fill the dictionary with data from the xml here.
            }
            else
            {
                //This will be moved to a "CreateDocument" function later.
                rankXml = new XmlDocument();
                XmlNode root = rankXml.CreateElement("RankData");
                XmlNode player = rankXml.CreateElement("Player");
                /*XmlAttribute id = rankXml.CreateAttribute("id");
                id.Value = rankXml.SelectNodes("Data/Player").Count.ToString();
                XmlElement name = rankXml.CreateElement("Name");
                XmlElement rank = rankXml.CreateElement("Rank");
                rankXml.AppendChild(root);
                root.AppendChild(player);
                player.Attributes.Append(id);
                player.AppendChild(name);
                name.InnerText = "Test";
                player.AppendChild(rank);
                rank.InnerText = "1500";*/
                rankXml.AppendChild(root);
                root.AppendChild(player);


                rankXml.Save(Directory.GetCurrentDirectory() + @"\RankData.xml");
            }
        }

        /// <summary>
        /// Adds new players from the match into the database.
        /// </summary>
        /// <param name="player"></param>
        private void InputPlayerInMatch(string player)
        {
            Rating playerRating = new Rating(calculator);
      
            //For updating the XML
            XmlNodeList nodes = rankXml.SelectNodes("//Player");
            foreach (XmlNode node in nodes)
            {
                Log.Info(node.Value);
                if (player == node.Attributes[0].Value)
                {
                    Log.Info("Player already exists in database!");
                    return;
                }
            }

            XmlNode root = rankXml.SelectSingleNode("RankData");
            XmlNode playerNode = rankXml.CreateElement("Player");
            XmlAttribute name = rankXml.CreateAttribute("name");
            name.Value = player;
            XmlElement rank = rankXml.CreateElement("Rank");
            root.AppendChild(playerNode);
            playerNode.Attributes.Append(name);
            playerNode.AppendChild(rank);
            rank.InnerText = ((int)playerRating.GetRating()).ToString();
            Log.Info("Added " + player + " to the Xml");

            rankXml.Save(Directory.GetCurrentDirectory() + @"\RankData.xml");


            //For updating the Dictionary
            try
            {
                players.Add(player, playerRating);
            }
            catch (ArgumentException)
            {
                Log.Info("Player already exists locally! (This should not ever be seen)");
            }
        }


        private void UpdateXml()
        {
            List<string> playerNames = new List<string>(players.Keys);
            for(int i = 0; i < playerNames.Count; i++)
            {
                Log.Debug(rankXml.SelectSingleNode("RankData/Player[@name='" + playerNames[i] + "']/Rank").InnerText + " Rank of player " + playerNames[i]);
                XmlNode nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + playerNames[i] + "']/Rank");
                nodeToChange.InnerText = ((int)players[playerNames[i]].GetRating()).ToString();
            }
        }

        public void CalculateResults(List<string> playerNames, List<int> playerTimes)
        {
            //Be sure any new players get added into database
            foreach(string player in playerNames)
            {
                InputPlayerInMatch(player);
            }

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

        //THESE FUNCTIONS NEED TO BE CHANGED SO THAT THE DATA IS GRABBED FROM THE XML AND NOT THE DICTIONARY

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