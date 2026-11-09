using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Utils.Map_Utils;

namespace BLPPCounter.Counters
{
    internal interface IClanService
    {
        Task<ClanCaptureResult> GetCaptureAsync(MapSelection map, CancellationToken cancellationToken = default);

        Task<ClanCaptureResult> GetCaptureAsync(string mapId, CancellationToken cancellationToken = default);

        bool TryGetCached(MapSelection map, out ClanCaptureResult? result);

        void ClearCache();
    }
}
