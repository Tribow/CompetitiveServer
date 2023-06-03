using System;
using System.Collections.Generic;

namespace Glicko2Rankings
{
    public class PlayerMatchData : IEquatable<PlayerMatchData> , IComparable<PlayerMatchData>
    {
        public string PlayerID { get; set; }
        public string PlayerName { get; set; }
        public string Color { get; set; }
        public int PlayerTime { get; set; }
        public int OldRating { get; set; }

        public override string ToString()
        {
            return $"ID: {PlayerID}   Name: {PlayerName}   Color: {Color}   Finish Time: {PlayerTime}   Previous Rating: {OldRating}"; 
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            PlayerMatchData objAsData = obj as PlayerMatchData;
            if (objAsData == null)
                return false;
            else
                return Equals(objAsData);
        }

        public override int GetHashCode()
        {
            return PlayerTime;
        }

        public int CompareTo(PlayerMatchData compareData)
        {
            if (compareData == null)
                return 1;
            else
                return PlayerTime.CompareTo(compareData.PlayerTime);
        }

        public bool Equals(PlayerMatchData other)
        {
            if (other == null)
                return false;
            return PlayerTime.Equals(other.PlayerTime);
        }
    }
}
