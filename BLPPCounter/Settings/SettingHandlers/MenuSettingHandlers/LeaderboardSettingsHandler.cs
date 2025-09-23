using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.List_Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers
{
    internal class LeaderboardSettingsHandler
    {
        internal static LeaderboardSettingsHandler Instance = new LeaderboardSettingsHandler();
        private static PluginConfig PC => PluginConfig.Instance;

        public IReadOnlyList<Leaderboards> UsableLeaderboards => _usableLeaderboards;
        internal List<object> LeaderboardOptions => PC.LeaderboardsInUse.Select(token => (object)new LeaderboardListInfo(token)).ToList();
        internal List<object> LeaderboardList => PC.LeaderboardsInUse.Count == _usableLeaderboards.Count ?
            new List<object>(1) { Leaderboards.None.ToString() } :
            UsableLeaderboards.Where(token => !PC.LeaderboardsInUse.Contains(token)).Select(token => (object)token.ToString()).ToList();

        private readonly List<Leaderboards> _usableLeaderboards;
        public Leaderboards NextLeaderboardToAdd;
        internal ListSetting LeaderboardSelector = null;
        internal CustomCellListTableData LeaderboardTable = null;

        private LeaderboardSettingsHandler() 
        {
            _usableLeaderboards = new List<Leaderboards>((int)Math.Log((int)Leaderboards.All + 1, 2));
            for (int i = 1; i < (int)Leaderboards.All; i <<= 1)
                _usableLeaderboards.Add((Leaderboards)i);
        }

        internal void RemoveCell(Leaderboards leaderboard)
        {
            if (PC.LeaderboardsInUse.Count == 1) return;
            PC.LeaderboardsInUse.Remove(leaderboard);
            Refresh();
        }
        internal void AddCell()
        {
            if (NextLeaderboardToAdd == Leaderboards.None)
                return;
            PC.LeaderboardsInUse.Add(NextLeaderboardToAdd);
            Refresh();
        }
        internal void SwapUp(Leaderboards leaderboard)
        {
            int index = PC.LeaderboardsInUse.IndexOf(leaderboard);
            if (index < 1)
                return;
            PC.LeaderboardsInUse[index] = PC.LeaderboardsInUse[index - 1];
            PC.LeaderboardsInUse[index - 1] = leaderboard;
            Refresh();
        }
        internal void SwapDown(Leaderboards leaderboard)
        {
            int index = PC.LeaderboardsInUse.IndexOf(leaderboard);
            if (index < 0 || index == PC.LeaderboardsInUse.Count - 1)
                return;
            PC.LeaderboardsInUse[index] = PC.LeaderboardsInUse[index + 1];
            PC.LeaderboardsInUse[index + 1] = leaderboard;
            Refresh();
        }
        internal void Refresh()
        {
            LeaderboardTable.Data = LeaderboardOptions;
            LeaderboardSelector.Values = LeaderboardList;
            LeaderboardSelector.Value = LeaderboardList.First();
            NextLeaderboardToAdd = (Leaderboards)Enum.Parse(typeof(Leaderboards), LeaderboardList.First() as string);

            LeaderboardTable.TableView.ReloadData();
            LeaderboardSelector.ApplyValue();
        }
    }
}
