using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers
{
    internal sealed class LeaderboardSettingsHandler
    {
        private readonly PluginConfig _config;
        private readonly List<Leaderboards> _usableLeaderboards;

        public LeaderboardSettingsHandler(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _usableLeaderboards = [];

            for (int value = 1; value < (int)Leaderboards.All; value <<= 1)
            {
                _usableLeaderboards.Add((Leaderboards)value);
            }

            NextLeaderboardToAdd = AvailableLeaderboards.FirstOrDefault();
        }

        public event Action? LeaderboardUpdated;

        public IReadOnlyList<Leaderboards> UsableLeaderboards => _usableLeaderboards;

        public IReadOnlyList<Leaderboards> SelectedLeaderboards => _config.LeaderboardsInUse;

        public IReadOnlyList<Leaderboards> AvailableLeaderboards =>
            _usableLeaderboards.Where(leaderboard => !_config.LeaderboardsInUse.Contains(leaderboard)).ToArray();

        public Leaderboards NextLeaderboardToAdd { get; set; }

        internal void RemoveCell(Leaderboards leaderboard)
        {
            if (_config.LeaderboardsInUse.Count <= 1)
                return;

            if (!_config.LeaderboardsInUse.Remove(leaderboard))
                return;

            RefreshSelection();
        }

        internal void AddCell()
        {
            if (NextLeaderboardToAdd == Leaderboards.None || _config.LeaderboardsInUse.Contains(NextLeaderboardToAdd))
            {
                return;
            }

            _config.LeaderboardsInUse.Add(NextLeaderboardToAdd);

            RefreshSelection();
        }

        internal void SwapUp(Leaderboards leaderboard)
        {
            int index = _config.LeaderboardsInUse.IndexOf(leaderboard);

            if (index < 1)
                return;

            (_config.LeaderboardsInUse[index - 1], _config.LeaderboardsInUse[index]) = (
                _config.LeaderboardsInUse[index],
                _config.LeaderboardsInUse[index - 1]
            );

            NotifyUpdated();
        }

        internal void SwapDown(Leaderboards leaderboard)
        {
            int index = _config.LeaderboardsInUse.IndexOf(leaderboard);

            if (index < 0 || index >= _config.LeaderboardsInUse.Count - 1)
            {
                return;
            }

            (_config.LeaderboardsInUse[index], _config.LeaderboardsInUse[index + 1]) = (
                _config.LeaderboardsInUse[index + 1],
                _config.LeaderboardsInUse[index]
            );

            NotifyUpdated();
        }

        private void RefreshSelection()
        {
            NextLeaderboardToAdd = AvailableLeaderboards.FirstOrDefault();

            NotifyUpdated();
        }

        private void NotifyUpdated()
        {
            LeaderboardUpdated?.Invoke();
        }
    }
}
