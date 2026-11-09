using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.API_Handlers
{
    internal interface IBeatSaverMetadataService
    {
        Task<BeatSaverMapMetadata?> GetMapMetadataAsync(
            string hash,
            BeatmapDifficulty difficulty,
            string characteristic,
            CancellationToken cancellationToken = default
        );
    }
}
