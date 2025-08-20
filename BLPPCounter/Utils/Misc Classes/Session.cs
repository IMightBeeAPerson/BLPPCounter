using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal class Session
    {
        internal readonly Leaderboards Leaderboard;
        internal readonly List<Play> Scores;
        internal readonly float InitialProfilePp;

        internal float GainedProfilePp => Scores.Aggregate(0.0f, (total, current) => total + current.ProfilePpGained);

        public Session(Leaderboards leaderboard, float initialProfilePp)
        {
            Leaderboard = leaderboard;
            Scores = new List<Play>();
            InitialProfilePp = initialProfilePp;
        }


        public struct Play
        {
            public string MapName, MapKey;
            public BeatmapDifficulty Difficulty;
            public float Pp, ProfilePpGained;

            public Play(string mapName, string mapKey, BeatmapDifficulty difficulty, float pp, float profilePpGained)
            {
                MapName = mapName;
                MapKey = mapKey;
                Difficulty = difficulty;
                Pp = pp;
                ProfilePpGained = profilePpGained;
            }
        }
    }
}
