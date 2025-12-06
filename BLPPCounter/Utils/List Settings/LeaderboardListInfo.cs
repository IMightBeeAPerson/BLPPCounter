using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using System;

namespace BLPPCounter.Utils.List_Settings
{
    internal class LeaderboardListInfo(Leaderboards leaderboard)
    {
        [UIValue(nameof(Leaderboard))]
        public string LeaderboardStr => Leaderboard.ToString();
        public Leaderboards Leaderboard = leaderboard;

        public LeaderboardListInfo(string leaderboard) : this((Leaderboards)Enum.Parse(typeof(Leaderboards), leaderboard))
        { }

        [UIAction(nameof(RemoveLeaderboard))]
        private void RemoveLeaderboard() => LeaderboardSettingsHandler.Instance.RemoveCell(Leaderboard);
        [UIAction(nameof(SwapUp))]
        private void SwapUp() => LeaderboardSettingsHandler.Instance.SwapUp(Leaderboard);
        [UIAction(nameof(SwapDown))]
        private void SwapDown() => LeaderboardSettingsHandler.Instance.SwapDown(Leaderboard);
    }
}
