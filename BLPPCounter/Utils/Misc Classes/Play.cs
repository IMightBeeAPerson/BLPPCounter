using BLPPCounter.Utils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal struct Play
    {
        public string MapName, MapKey, Mode;
        public BeatmapDifficulty Difficulty;
        public float Pp, ProfilePpGained, OldPp;
        public APCategory AccSaberCategory;
        public bool IsImproved => OldPp > 0;

        public Play(string mapName, string mapKey, BeatmapDifficulty difficulty, string mode, float pp, float profilePpGained = -1, float oldPp = -1)
        {
            MapName = mapName;
            MapKey = mapKey;
            Difficulty = difficulty;
            Mode = mode;
            Pp = pp;
            ProfilePpGained = profilePpGained;
            OldPp = oldPp;
            AccSaberCategory = APCategory.None;
        }
    }
}
