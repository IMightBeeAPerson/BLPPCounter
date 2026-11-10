using System;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal readonly struct ProfileKey : IEquatable<ProfileKey>
    {
        public ProfileKey(Leaderboards leaderboard, string userId, APCategory accSaberCategory = APCategory.None)
        {
            if (BetterString.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("A profile key requires a user ID.", nameof(userId));
            }

            Leaderboard = leaderboard;
            UserId = userId;
            AccSaberCategory = leaderboard == Leaderboards.Accsaber ? accSaberCategory : APCategory.None;
        }

        public Leaderboards Leaderboard { get; }

        public string UserId { get; }

        public APCategory AccSaberCategory { get; }

        public bool Equals(ProfileKey other)
        {
            return Leaderboard == other.Leaderboard
                && AccSaberCategory == other.AccSaberCategory
                && string.Equals(UserId, other.UserId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ProfileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Leaderboard;
                hash = (hash * 397) ^ (int)AccSaberCategory;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(UserId);

                return hash;
            }
        }

        public override string ToString()
        {
            return AccSaberCategory == APCategory.None
                ? $"{UserId}_{(int)Leaderboard}"
                : $"{UserId}_{(int)Leaderboard}_" + $"{(int)AccSaberCategory}";
        }

        public static bool operator ==(ProfileKey left, ProfileKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProfileKey left, ProfileKey right)
        {
            return !left.Equals(right);
        }
    }
}
