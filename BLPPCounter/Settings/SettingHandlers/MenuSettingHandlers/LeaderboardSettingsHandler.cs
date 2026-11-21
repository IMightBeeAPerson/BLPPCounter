using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers
{
    internal sealed class LeaderboardSettingsHandler : INotifyPropertyChanged
    {
        private readonly PluginConfig _config;
        private readonly List<Leaderboards> _usableLeaderboards;

        public event PropertyChangedEventHandler? PropertyChanged;

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

            _config.RaisePropertyChanged(this, nameof(PluginConfig.LeaderboardsInUse));
            PropertyChanged?.Invoke(this, new(nameof(PluginConfig.LeaderboardsInUse)));

            RefreshSelection();
        }

        internal void AddCell()
        {
            if (NextLeaderboardToAdd == Leaderboards.None || _config.LeaderboardsInUse.Contains(NextLeaderboardToAdd))
            {
                return;
            }

            _config.LeaderboardsInUse.Add(NextLeaderboardToAdd);

            _config.RaisePropertyChanged(this, nameof(PluginConfig.LeaderboardsInUse));
            PropertyChanged?.Invoke(this, new(nameof(PluginConfig.LeaderboardsInUse)));

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

            _config.RaisePropertyChanged(this, nameof(PluginConfig.LeaderboardsInUse));
            PropertyChanged?.Invoke(this, new(nameof(PluginConfig.LeaderboardsInUse)));
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

            _config.RaisePropertyChanged(this, nameof(PluginConfig.LeaderboardsInUse));
            PropertyChanged?.Invoke(this, new(nameof(PluginConfig.LeaderboardsInUse)));
        }

        private void RefreshSelection()
        {
            NextLeaderboardToAdd = AvailableLeaderboards.FirstOrDefault();

            PropertyChanged?.Invoke(this, new(nameof(NextLeaderboardToAdd)));
        }
    }
}
