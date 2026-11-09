using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal interface ICompletedPlayService
    {
        Task<bool> AddCompletedPlayAsync(
            string userId,
            string hash,
            float accuracy,
            string mapName,
            BeatmapDifficulty difficulty,
            string characteristic = Profile.DEFAULT_MODE,
            GameplayModifiers? modifiers = null,
            CancellationToken cancellationToken = default
        );
    }
}
