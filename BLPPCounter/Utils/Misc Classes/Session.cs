using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal class Session
    {
        public readonly Leaderboards Leaderboard;
        public readonly string UserId;
        private readonly List<Play> Scores;
        public readonly float InitialProfilePp;

        public float GainedProfilePp => Scores.Aggregate(0.0f, (total, current) => total + current.ProfilePpGained);
        public int PlaysSet => Scores.Count;

        public Session(Leaderboards leaderboard, string userId, float initialProfilePp)
        {
            Leaderboard = leaderboard;
            UserId = userId;
            Scores = new List<Play>();
            InitialProfilePp = initialProfilePp;
        }

        public void AddPlay(string mapName, string mapKey, BeatmapDifficulty diff, string mode, float rawPp, float profilePpGained, float oldPp = -1)
        {
            Scores.Add(new Play(mapName, mapKey, diff, mode, rawPp, profilePpGained, oldPp));
        }

        internal struct Play
        {
            public string MapName, MapKey, Mode;
            public BeatmapDifficulty Difficulty;
            public float Pp, ProfilePpGained, OldPp;
            public bool IsImproved => OldPp > 0;

            public Play(string mapName, string mapKey, BeatmapDifficulty difficulty, string mode, float pp, float profilePpGained, float oldPp = -1)
            {
                MapName = mapName;
                MapKey = mapKey;
                Difficulty = difficulty;
                Mode = mode;
                Pp = pp;
                ProfilePpGained = profilePpGained;
                OldPp = oldPp;
            }
        }
    }
}
