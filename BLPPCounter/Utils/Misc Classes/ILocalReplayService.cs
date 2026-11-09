using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal interface ILocalReplayService
    {
        Task LoadAsync(CancellationToken cancellationToken = default);

        Task<string?> GetReplayNameAsync(
            string userId,
            string hash,
            string mode,
            string difficulty,
            CancellationToken cancellationToken = default
        );

        string? GetReplayPath(string replayName);

        void Clear();
    }
}
