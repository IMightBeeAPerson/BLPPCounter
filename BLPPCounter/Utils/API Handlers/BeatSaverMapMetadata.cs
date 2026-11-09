using System;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.API_Handlers
{
    internal sealed class BeatSaverMapMetadata
    {
        internal BeatSaverMapMetadata(string mapKey, Leaderboards rankedLeaderboards)
        {
            if (BetterString.IsNullOrWhiteSpace(mapKey))
            {
                throw new ArgumentException("BeatSaver metadata requires a map key.", nameof(mapKey));
            }

            MapKey = mapKey;
            RankedLeaderboards = rankedLeaderboards;
        }

        public string MapKey { get; }

        /// <summary>
        /// Leaderboards that BeatSaver metadata can identify directly.
        /// AccSaber availability is verified through its own map data.
        /// </summary>
        public Leaderboards RankedLeaderboards { get; }
    }
}
