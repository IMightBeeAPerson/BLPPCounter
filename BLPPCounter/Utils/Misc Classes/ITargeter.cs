using System;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Utils.Serializable_Classes;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal interface ITargeter
    {
        event Action? Initialized;

        event Action? TargetListReloaded;

        bool IsInitialized { get; }

        TargeterInfo Target { get; }

        Task InitializationTask { get; }

        Task WaitUntilInitializedAsync(CancellationToken cancellationToken = default);

        Task ReloadCustomPlayersAsync(CancellationToken cancellationToken = default);

        Task ReloadFollowersAsync(CancellationToken cancellationToken = default);

        Task SaveAllAsync(CancellationToken cancellationToken = default);

        Task AddTargetAsync(CustomTarget target, CancellationToken cancellationToken = default);

        Task AddTargetAsync(string name, string id, int rank, CancellationToken cancellationToken = default);

        Task<bool> DeleteTargetAsync(string id, CancellationToken cancellationToken = default);

        void SetTarget(string name, long id);

        void SetTarget(CustomTarget target);

        void ResetTarget();
    }
}
