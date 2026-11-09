using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Map_Utils
{
    internal interface IMapRepository
    {
        Task InitializationTask { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);

        Task<Map?> GetAsync(
            string hash,
            string? mode,
            Leaderboards leaderboard,
            bool forceRemoteLoad = false,
            CancellationToken cancellationToken = default
        );

        bool TryGet(string hash, out Map? map);

        void Clear();
    }
}
