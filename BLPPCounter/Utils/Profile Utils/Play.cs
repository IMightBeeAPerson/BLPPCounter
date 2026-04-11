using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal struct Play(string mapName, string mapKey, BeatmapDifficulty difficulty, string mode, float pp, uint rank, float profilePpGained = -1, float oldPp = -1)
    {
        public string MapName = mapName, MapKey = mapKey, Mode = mode;
        public BeatmapDifficulty Difficulty = difficulty;
        public float Pp = pp, ProfilePpGained = profilePpGained, OldPp = oldPp;
        public APCategory AccSaberCategory = APCategory.None;
        public uint Rank = rank;
        public readonly bool IsImproved => OldPp > 0;
    }
}
