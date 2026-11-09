using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal interface IProfileRepository
    {
        Task LoadAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);

        Task<Profile> GetAsync(
            Leaderboards leaderboard,
            string userId,
            APCategory accSaberCategory = APCategory.None,
            CancellationToken cancellationToken = default
        );

        bool TryGet(Leaderboards leaderboard, string userId, APCategory accSaberCategory, out Profile? profile);

        IReadOnlyList<Profile> GetLoadedProfilesSnapshot();

        Task ReloadAsync(Profile profile, CancellationToken cancellationToken = default);
    }
}
