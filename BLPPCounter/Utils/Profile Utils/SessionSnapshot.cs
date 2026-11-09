using System;
using System.Collections.Generic;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class SessionSnapshot
    {
        private readonly Play[] _scores;

        internal SessionSnapshot(
            Leaderboards leaderboard,
            string userId,
            float initialProfilePp,
            float gainedProfilePp,
            Play[] scores
        )
        {
            Leaderboard = leaderboard;

            UserId = userId ?? throw new ArgumentNullException(nameof(userId));

            InitialProfilePp = initialProfilePp;
            GainedProfilePp = gainedProfilePp;

            _scores = scores ?? throw new ArgumentNullException(nameof(scores));
        }

        public Leaderboards Leaderboard { get; }

        public string UserId { get; }

        public float InitialProfilePp { get; }

        public float GainedProfilePp { get; }

        public float CurrentProfilePp => InitialProfilePp + GainedProfilePp;

        public int PlaysSet => _scores.Length;

        public IReadOnlyList<Play> Scores => _scores;
    }
}
