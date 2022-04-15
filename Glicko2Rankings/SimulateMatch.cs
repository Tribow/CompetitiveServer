using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using System.Security;
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
        private XmlDocument rankXml = new XmlDocument();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SimulateMatch()
        {
            //Log.Debug(Directory.GetCurrentDirectory() + @"\RankData.xml");
            if (File.Exists(Directory.GetCurrentDirectory() + @"\RankData.xml"))
            {
                Log.Info("RankData file exists!");
                rankXml.Load(Directory.GetCurrentDirectory() + @"\RankData.xml");

                //Fill the dictionary with data from the xml here.
                XmlNodeList nodes = rankXml.SelectNodes("//Player");
                foreach (XmlNode node in nodes)
                {
                    
                    if (node.Attributes.Count > 0)
                    {
                        //Add the Player node's information to our dictionary to make sure it updates rankings correctly
                        Rating rating = new Rating(calculator);
                        double importantNumber;
                        Log.Debug(node.FirstChild.InnerText);
                        Log.Debug(node.ChildNodes[1].InnerText);
                        Log.Debug(node.LastChild.InnerText);
                        bool success = double.TryParse(node.FirstChild.InnerText, out importantNumber);
                        if (!success)
                            rating.SetRatingDeviation(1500); //1500 is the default rank
                        else
                            rating.SetRating(importantNumber);
                        success = double.TryParse(node.ChildNodes[1].InnerText, out importantNumber);
                        if (!success)
                            rating.SetRatingDeviation(350); //350 is the default RatingDeviation
                        else
                            rating.SetRatingDeviation(importantNumber);
                        success = double.TryParse(node.LastChild.InnerText, out importantNumber);
                        if (!success)
                            rating.SetVolatility(0.06); //0.06 is the default volatility
                        else
                            rating.SetVolatility(importantNumber);
                        players.Add(node.Attributes[0].Value, rating);
                    }
                }
            }
            else
            {
                rankXml = new XmlDocument();
                XmlNode root = rankXml.CreateElement("RankData");
                XmlNode player = rankXml.CreateElement("Player");
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
                if (node.Attributes.Count > 0)
                {
                    if (player == SecurityElement.Escape(node.Attributes[0].Value))
                    {
                        Log.Info("Player already exists in database!");
                        return;
                    }
                }
            }

            //Sanitize player names like removing quotes or other symbols that may break XPATH
            XmlNode root = rankXml.SelectSingleNode("RankData");
            XmlNode playerNode = rankXml.CreateElement("Player");
            XmlAttribute name = rankXml.CreateAttribute("name");
            name.Value = SecurityElement.Escape(player);
            XmlElement rank = rankXml.CreateElement("Rank");
            XmlElement rankDeviation = rankXml.CreateElement("RankDeviation");
            XmlElement volatility = rankXml.CreateElement("Volatility");
            root.AppendChild(playerNode);
            playerNode.Attributes.Append(name);
            playerNode.AppendChild(rank);
            rank.InnerText = playerRating.GetRating().ToString();
            playerNode.AppendChild(rankDeviation);
            rankDeviation.InnerText = playerRating.GetRatingDeviation().ToString();
            playerNode.AppendChild(volatility);
            volatility.InnerText = playerRating.GetVolatility().ToString();
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
                string safeString = SecurityElement.Escape(playerNames[i]);
                XmlNode nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safeString + "']/Rank");
                nodeToChange.InnerText = players[playerNames[i]].GetRating().ToString();
                nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safeString + "']/RankDeviation");
                nodeToChange.InnerText = players[playerNames[i]].GetRatingDeviation().ToString();
                nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safeString + "']/Volatility");
                nodeToChange.InnerText = players[playerNames[i]].GetVolatility().ToString();
            }
            
            rankXml.Save(Directory.GetCurrentDirectory() + @"\RankData.xml");
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
            UpdateXml();

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