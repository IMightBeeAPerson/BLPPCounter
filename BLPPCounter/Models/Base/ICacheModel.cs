using BLPPCounter.Utils.Misc_Classes;

namespace BLPPCounter.Models.Base
{
    public interface ICacheModel : IModel, IDeepCloneable
    {
        public string LeaderboardId { get; }
        public string Mode { get; }
        public BeatmapDifficulty Difficulty { get; }
        public string Hash { get; }
    }
}
