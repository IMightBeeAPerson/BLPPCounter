using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Misc_Classes;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.List_Settings
{
    internal class TargetInfo
    {
        [UIValue(nameof(DisplayName))] public string DisplayName => _displayName.ClampString(25);
        private readonly string _displayName;
        [UIValue(nameof(ID))] public string ID => $"<color=#999>ID</color> <color=#777>{_id}</color>";
        public string RealID => _id;
        private readonly string _id;
        private readonly Dictionary<Leaderboards, int> Ranks;
        [UIValue(nameof(RankDisplay))] private string RankDisplay => $"#<color=orange>{(Rank > 0 ? Rank.ToString() : "?")}";
        //public int Rank => Ranks.ContainsKey(PluginConfig.Instance.Leaderboard) ? Ranks[PluginConfig.Instance.Leaderboard] : -1;
        public int Rank => Ranks.ContainsKey(Leaderboards.Beatleader) ? Ranks[Leaderboards.Beatleader] : -1;

        public TargetInfo(string displayName, string id, params (Leaderboards, int)[] ranks)
        {
            _displayName = displayName; 
            _id = id;
            if (ranks is null || ranks.Length == 0)
                Ranks = [];
            else
                Ranks = new(ranks.Select(token => new KeyValuePair<Leaderboards, int>(token.Item1, token.Item2)));

        }

        public void SetAsTarget()
        {
            Targeter.SetTarget(_displayName, long.Parse(_id));
            SettingsHandler.Instance.Target = _displayName;
        }
    }
}
