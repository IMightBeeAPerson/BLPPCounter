using System;
using System.Collections.Generic;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class ProfileSnapshot
    {
        private readonly Play[] _scores;

        internal ProfileSnapshot(ProfileKey key, float totalPp, float plusOne, int pageNumber, Play[] scores)
        {
            Key = key;
            TotalPp = totalPp;
            PlusOne = plusOne;
            PageNumber = pageNumber;

            _scores = scores ?? throw new ArgumentNullException(nameof(scores));
        }

        public ProfileKey Key { get; }

        public float TotalPp { get; }

        public float PlusOne { get; }

        public int PageNumber { get; }

        public int ScoreCount => _scores.Length;

        public IReadOnlyList<Play> Scores => _scores;
    }
}
