using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal interface IProfileFactory
    {
        Task<Profile> CreateAsync(ProfileKey key, CancellationToken cancellationToken = default);

        Profile Restore(ProfilePersistenceData data);

        Task ReloadAsync(Profile profile, CancellationToken cancellationToken = default);
    }
}
