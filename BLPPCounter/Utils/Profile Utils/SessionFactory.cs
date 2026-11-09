using System;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class SessionFactory : ISessionFactory
    {
        private readonly PluginConfig _config;
        private readonly ILeaderboardApiProvider _apiProvider;

        public SessionFactory(PluginConfig config, ILeaderboardApiProvider apiProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        }

        public Session Create(Leaderboards leaderboard, string userId, float initialProfilePp)
        {
            return new Session(
                leaderboard,
                userId,
                initialProfilePp,
                _config.DecimalPrecision,
                _apiProvider.Get(leaderboard)
            );
        }
    }
}
