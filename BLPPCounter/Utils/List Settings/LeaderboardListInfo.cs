using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using System;

namespace BLPPCounter.Utils.List_Settings
{
    internal class LeaderboardListInfo
    {
        [UIValue(nameof(Leaderboard))]
        public string LeaderboardStr => Leaderboard.ToString();
        public Leaderboards Leaderboard;

        public LeaderboardListInfo(string leaderboard) : this((Leaderboards)Enum.Parse(typeof(Leaderboards), leaderboard))
        { }
        public LeaderboardListInfo(Leaderboards leaderboard)
        {
            Leaderboard = leaderboard;
        }

        [UIAction(nameof(RemoveLeaderboard))]
        private void RemoveLeaderboard() => LeaderboardSettingsHandler.Instance.RemoveCell(Leaderboard);
        [UIAction(nameof(SwapUp))]
        private void SwapUp() => LeaderboardSettingsHandler.Instance.SwapUp(Leaderboard);
        [UIAction(nameof(SwapDown))]
        private void SwapDown() => LeaderboardSettingsHandler.Instance.SwapDown(Leaderboard);
    }
}
