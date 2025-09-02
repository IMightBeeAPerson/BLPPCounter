using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.List_Settings
{
    internal class TargetInfo
    {
        [UIValue(nameof(DisplayName))] public string DisplayName => _displayName;
        private string _displayName;
        [UIValue(nameof(ID))] public string ID => $"<color=#999>ID</color> <color=#777>{_id}</color>";
        private readonly string _id;
        private readonly Dictionary<Leaderboards, int> Ranks;
        [UIValue(nameof(RankDisplay))] private string RankDisplay => $"#<color=orange>{Rank}";
        //public int Rank => Ranks.ContainsKey(PluginConfig.Instance.Leaderboard) ? Ranks[PluginConfig.Instance.Leaderboard] : -1;
        public int Rank => Ranks.ContainsKey(Leaderboards.Beatleader) ? Ranks[Leaderboards.Beatleader] : -1;

        public TargetInfo(string displayName, string id, params (Leaderboards, int)[] ranks)
        {
            _displayName = displayName; 
            _id = id;
            if (ranks is null || ranks.Length == 0)
                Ranks = new Dictionary<Leaderboards, int>();
            else
                Ranks = new Dictionary<Leaderboards, int>(ranks.Select(token => new KeyValuePair<Leaderboards, int>(token.Item1, token.Item2)));

        }
    }
}
