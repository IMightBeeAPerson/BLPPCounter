using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Misc_Classes;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class TargetInfo
    {
        private readonly ITargeter _targeter;
        private readonly SettingsHandler _settingsHandler;
        private readonly string _displayName;
        private readonly string _id;
        private readonly IReadOnlyDictionary<Leaderboards, int> _ranks;

        internal TargetInfo(
            ITargeter targeter,
            SettingsHandler settingsHandler,
            string displayName,
            string id,
            params TargetRank[] ranks
        )
        {
            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _settingsHandler = settingsHandler ?? throw new ArgumentNullException(nameof(settingsHandler));

            _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));

            _id = id ?? throw new ArgumentNullException(nameof(id));

            _ranks = ranks.ToDictionary(pair => pair.Leaderboard, pair => pair.Rank);
        }

        [UIValue(nameof(DisplayName))]
        public string DisplayName => _displayName.ClampString(25);

        [UIValue(nameof(ID))]
        public string ID => $"<color=#999>ID</color> " + $"<color=#777>{_id}</color>";

        public string RealID => _id;

        [UIValue(nameof(RankDisplay))]
        private string RankDisplay => $"#<color=orange>" + $"{(Rank > 0 ? Rank.ToString() : "?")}";

        public int Rank => _ranks.TryGetValue(Leaderboards.Beatleader, out int rank) ? rank : -1;

        public void SetAsTarget()
        {
            if (!long.TryParse(_id, out long id))
            {
                throw new InvalidOperationException($"\"{_id}\" is not a valid target ID.");
            }

            _targeter.SetTarget(_displayName, id);
            _settingsHandler.Target = _displayName;
        }
    }

    internal readonly record struct TargetRank(Leaderboards Leaderboard, int Rank);
}
