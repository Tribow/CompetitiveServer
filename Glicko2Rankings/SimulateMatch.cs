using System;
using System.Collections.Generic;
using System.Xml;
using System.Security;
using System.Linq;
using System.IO;
//using System.Reflection;

namespace Glicko2Rankings
{
    //Right now this is entirely local data, which is cringe. I will need to put this in a database so it can
    //Upload the rankings to a database and update that information with new data whenever necessary
    //Unfortunately, this doesn't seem viable so for now the data is saved to an xml document
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
            if (File.Exists(Directory.GetCurrentDirectory() + @"/RankData.xml"))
            {
                Log.Info("RankData file exists!");
                rankXml.Load(Directory.GetCurrentDirectory() + @"/RankData.xml");

                //Fill the dictionary with data from the xml here.
                XmlNodeList nodes = rankXml.SelectNodes("//Player");
                foreach (XmlNode node in nodes)
                {
                    
                    if (node.Attributes.Count > 1)
                    {
                        //Add the Player node's information to our dictionary to make sure it updates rankings correctly
                        Rating rating = new Rating(calculator);
                        double importantNumber;
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
                        players.Add(node.Attributes[0].Value + "|||||" + node.Attributes[1].Value, rating);
                        Log.Info("Loaded player " + node.Attributes[0].Value + " with ID: " + node.Attributes[1].Value);
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


                rankXml.Save(Directory.GetCurrentDirectory() + @"/RankData.xml");
            }
        }

        /// <summary>
        /// Adds new players from the match into the database.
        /// </summary>
        /// <param name="playerInfo">This should contain the player's name & colorid. Formatted like this name|||||colorid</param>
        private void InputPlayerInMatch(string playerInfo)
        {
            Rating playerRating = new Rating(calculator);
            string[] splitInfo = playerInfo.Split(new string[] { "|||||" }, StringSplitOptions.None);
            string player = splitInfo[0];
            string colorid = splitInfo[1];
      
            //For updating the XML
            XmlNodeList nodes = rankXml.SelectNodes("//Player");
            foreach (XmlNode node in nodes)
            {
                if (node.Attributes.Count > 1)
                {
                    if (player == node.Attributes[0].Value && colorid == node.Attributes[1].Value)
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
            name.Value = player;
            XmlAttribute id = rankXml.CreateAttribute("id");
            id.Value = colorid;
            XmlElement rank = rankXml.CreateElement("Rank");
            XmlElement rankDeviation = rankXml.CreateElement("RankDeviation");
            XmlElement volatility = rankXml.CreateElement("Volatility");
            root.AppendChild(playerNode);
            playerNode.Attributes.Append(name);
            playerNode.Attributes.Append(id);
            playerNode.AppendChild(rank);
            rank.InnerText = playerRating.GetRating().ToString();
            playerNode.AppendChild(rankDeviation);
            rankDeviation.InnerText = playerRating.GetRatingDeviation().ToString();
            playerNode.AppendChild(volatility);
            volatility.InnerText = playerRating.GetVolatility().ToString();
            Log.Info("Added " + player + " with ID: " + colorid + " to the Xml");

            rankXml.Save(Directory.GetCurrentDirectory() + @"/RankData.xml");


            //For updating the Dictionary
            try
            {
                players.Add(playerInfo, playerRating);
            }
            catch (ArgumentException)
            {
                Log.Info("Player already exists locally! (This should not ever be seen)");
            }
        }


        private void UpdateXml()
        {
            List<string> playerInfos = new List<string>(players.Keys);
            for(int i = 0; i < playerInfos.Count; i++)
            {
                string[] splitInfo = playerInfos[i].Split(new string[] { "|||||" }, StringSplitOptions.None);
                string safePlayer = splitInfo[0];
                string colorid = splitInfo[1];
                XmlNode nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safePlayer + "'][@id='" + colorid + "']/Rank");
                nodeToChange.InnerText = players[playerInfos[i]].GetRating().ToString();
                nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safePlayer + "'][@id='" + colorid + "']/RankDeviation");
                nodeToChange.InnerText = players[playerInfos[i]].GetRatingDeviation().ToString();
                nodeToChange = rankXml.SelectSingleNode("RankData/Player[@name='" + safePlayer + "'][@id='" + colorid + "']/Volatility");
                nodeToChange.InnerText = players[playerInfos[i]].GetVolatility().ToString();
            }
            
            rankXml.Save(Directory.GetCurrentDirectory() + @"/RankData.xml");
        }

        public void CalculateResults(List<string> playerInfos, List<int> playerTimes)
        {
            //Be sure any new players get added into database
            foreach(string info in playerInfos)
            {
                InputPlayerInMatch(info);
            }

            //Just in case something was wrong
            if (playerInfos.Count != playerTimes.Count)
            {
                Log.Info("The amount of players do not match the amount of times!");
                return;
            }

            for (int i = 0; i < playerTimes.Count; i++)
            {
                //Times that are 0 are spectators or joined laters
                if (playerTimes[i] == 0)
                    results.AddParticipant(players[playerInfos[i]]);

                for (int j = 0; j < playerTimes.Count; j++)
                {
                    if (j > i)
                    {
                        //Player names should not be the same nor should any of the times be 0
                        if (playerInfos[i] != playerInfos[j] && playerTimes[i] != 0 && playerTimes[j] != 0)
                        {
                            //Checking for Draw
                            if (playerTimes[i] == playerTimes[j])
                            {
                                results.AddDraw(players[playerInfos[i]], players[playerInfos[j]]);
                            }
                            else
                            {
                                //Win or Lose Results
                                if (playerTimes[i] < playerTimes[j])
                                {
                                    results.AddResult(players[playerInfos[i]], players[playerInfos[j]]);
                                }
                                else
                                {
                                    results.AddResult(players[playerInfos[j]], players[playerInfos[i]]);
                                }
                            }
                        }
                    }
                }
            }


            calculator.UpdateRatings(results);
            UpdateXml();

        }

        /// <summary>
        /// Returns a list of ratings in the order of the list it was given.
        /// The list will be empty if none of the players given have a rating
        /// </summary>
        /// <param name="playerInfos">This should contain a list of player's name & colorid. Formatted like this name|||||colorid</param>
        /// <returns></returns>
        public List<int> GetSpecificRatings(List<string> playerInfos)
        {
            List<int> ratings = new List<int>();

            foreach (string player in playerInfos)
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
        /// <param name="playerInfo">This should contain the player's name & colorid. Formatted like this name|||||colorid</param>
        /// <returns></returns>
        public int GetRating(string playerInfo)
        {
            int rating = 0;

            try
            {
                rating = (int)players[playerInfo].GetRating();
            }
            catch (KeyNotFoundException)
            {
                Log.Info("Player does not have a rating!");
            }

            return rating;
        }

        /// <summary>
        /// Gets all players with the given name and returns a list of the players
        /// </summary>
        /// <param name="playerName">The name to search for</param>
        /// <returns>Note that the strings within the list are formatted like this: name|||||colorid</returns>
        public List<string> GetPlayers(string playerName)
        {
            List<string> returnStringList = new List<string>();

            foreach(string player in players.Keys)
            {
                string[] splitData = player.Split(new string[] { "|||||" }, StringSplitOptions.None);
                string regexName = splitData[0];

                if (regexName == playerName)
                {
                    returnStringList.Add(player);
                }
            }

            return returnStringList;
        }

        /// <summary>
        /// Gets All players at the given rank and returns a list of the players. 
        /// </summary>
        /// <param name="rank">The rank number to search for</param>
        /// <returns>Note that the strings within the list are formatted like this: name|||||colorid</returns>
        public List<string> GetPlayersAtRank(int rank)
        {
            List<string> returnStringList = new List<string>();
            List<Rating> sortedRatings = players.Values.ToList();

            sortedRatings = sortedRatings.OrderByDescending(number => number.GetRating()).ToList();
            for (int i = 0; i < sortedRatings.Count; i++)
            {
                if(i+1 == rank)
                {
                    return players.Where(pair => pair.Value == sortedRatings[i]).Select(pair => pair.Key).ToList();
                }
            }

            return returnStringList;
        }

        /// <summary>
        /// Gets the player's rank and returns it as a string to be displayed.
        /// If a player lacks a rank for some reason it will return "N/A".
        /// </summary>
        /// <param name="playerInfo">This should contain the player's name & colorid. Formatted like this name|||||colorid</param>
        /// <returns></returns>
        public string GetRank(string playerInfo)
        {
            List<double> sortedRatings = new List<double>();
            double ratingToCheck = 0;
            try
            {
                ratingToCheck = players[playerInfo].GetRating();
            }
            catch (KeyNotFoundException)
            {
                Log.Info("Player does not have a rating!");
                return "N/A";
            }

            foreach (Rating rating in players.Values)
            {
                sortedRatings.Add(rating.GetRating());
            }

            sortedRatings = sortedRatings.OrderByDescending(number => number).ToList();
            for(int i = 0; i < sortedRatings.Count; i++)
            {
                if (ratingToCheck == sortedRatings[i])
                {
                    return (i+1).ToString();
                }

            }


            return "N/A";
        }

        /// <summary>
        /// Returns a list of ranks in the order it was given.
        /// If a player lacks a rank for some reason it will say "N/A".
        /// </summary>
        /// <param name="playerInfo">This should contain the player's name & colorid. Formatted like this name|||||colorid</param>
        /// <returns></returns>
        public List<string> GetSpecificRanks(List<string> playerInfo)
        {
            List<string> ranks = new List<string>();

            foreach(string player in playerInfo)
            {
                List<double> sortedRatings = new List<double>();
                double ratingToCheck = 0;
                try
                {
                    ratingToCheck = players[player].GetRating();
                }
                catch (KeyNotFoundException)
                {
                    Log.Info("Player does not have a rating!");
                    ranks.Add("N/A");
                }

                foreach (Rating rating in players.Values)
                {
                    sortedRatings.Add(rating.GetRating());
                }

                sortedRatings = sortedRatings.OrderBy(number => number).ToList();
                sortedRatings.Reverse();
                bool success = false;
                for (int i = 0; i < sortedRatings.Count; i++)
                {
                    if (ratingToCheck == sortedRatings[i])
                    {
                        ranks.Add((i + 1).ToString());
                        success = true;
                    }
                }
                if(!success)
                    ranks.Add("N/A");
            }

            return ranks;
        }
    }
}