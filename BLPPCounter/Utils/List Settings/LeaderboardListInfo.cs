using System;
using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils.Enums;
using Zenject;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class LeaderboardListInfo
    {
        private readonly LeaderboardSettingsHandler _handler;

        public LeaderboardListInfo(Leaderboards leaderboard, LeaderboardSettingsHandler handler)
        {
            Leaderboard = leaderboard;

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        [UIValue(nameof(Leaderboard))]
        public string LeaderboardStr => Leaderboard.ToString();

        public Leaderboards Leaderboard { get; }

        [UIAction(nameof(RemoveLeaderboard))]
        private void RemoveLeaderboard()
        {
            _handler.RemoveCell(Leaderboard);
        }

        [UIAction(nameof(SwapUp))]
        private void SwapUp()
        {
            _handler.SwapUp(Leaderboard);
        }

        [UIAction(nameof(SwapDown))]
        private void SwapDown()
        {
            _handler.SwapDown(Leaderboard);
        }

        internal sealed class Factory : PlaceholderFactory<Leaderboards, LeaderboardListInfo> { }
    }
}
