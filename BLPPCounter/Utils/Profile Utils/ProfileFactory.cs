using System;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class ProfileFactory : IProfileFactory
    {
        private readonly PluginConfig _config;
        private readonly ISessionFactory _sessionFactory;
        private readonly ILeaderboardApiProvider _apiProvider;

        public ProfileFactory(PluginConfig config, ISessionFactory sessionFactory, ILeaderboardApiProvider apiProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        }

        public async Task<Profile> CreateAsync(ProfileKey key, CancellationToken cancellationToken = default)
        {
            (Play[] scores, float totalPp) = await FetchProfileDataAsync(key, cancellationToken).ConfigureAwait(false);

            return new Profile(key, scores, totalPp, _config, _sessionFactory);
        }

        public Profile Restore(ProfilePersistenceData data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.Validate();

            ProfileKey key = new(data.Leaderboard, data.UserID, data.AccSaberType);

            Play[] scores = Profile.CreatePlaysFromPersistenceData(data);

            return new Profile(key, scores, data.TotalPP, _config, _sessionFactory);
        }

        public async Task ReloadAsync(Profile profile, CancellationToken cancellationToken = default)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            (Play[] scores, float totalPp) = await FetchProfileDataAsync(profile.Key, cancellationToken)
                .ConfigureAwait(false);

            profile.ReplaceData(scores, totalPp);
        }

        private async Task<(Play[] Scores, float TotalPp)> FetchProfileDataAsync(
            ProfileKey key,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            APIHandler api = _apiProvider.Get(key.Leaderboard);

            int scoreCount = Profile.GetPlusOneCount(key.Leaderboard);

            Task<Play[]> scoresTask;
            Task<float> totalPpTask;

            if (
                key.Leaderboard == Leaderboards.Accsaber
                && key.AccSaberCategory != APCategory.All
                && key.AccSaberCategory != APCategory.None
            )
            {
                APAPI accSaberApi = _apiProvider.AccSaber;

                scoresTask = accSaberApi.GetScores(key.UserId, scoreCount, key.AccSaberCategory);

                totalPpTask = accSaberApi.GetProfilePP(key.UserId, key.AccSaberCategory);
            }
            else
            {
                scoresTask = api.GetScores(key.UserId, scoreCount);

                totalPpTask = api.GetProfilePP(key.UserId);
            }

            await Task.WhenAll(scoresTask, totalPpTask).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            Play[] scores = await scoresTask.ConfigureAwait(false) ?? [];

            float totalPp = await totalPpTask.ConfigureAwait(false);

            return (scores, totalPp);
        }
    }
}
