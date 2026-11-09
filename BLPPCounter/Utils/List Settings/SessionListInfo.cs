using System;
using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using Zenject;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class SessionListInfo : SafeNotifyPropertyChanged
    {
        private readonly DateTime _playDate;
        private readonly string _ppLabel;

        private Play _playData;

        internal SessionListInfo(Play playData, string ppLabel)
        {
            if (BetterString.IsNullOrWhiteSpace(ppLabel))
            {
                throw new ArgumentException("A PP label is required.", nameof(ppLabel));
            }

            _playData = playData;
            _ppLabel = ppLabel.ToUpperInvariant();
            _playDate = DateTime.Now;
        }

        internal Play PlayData => _playData;

        [UIValue(nameof(BeatmapName))]
        public string BeatmapName => _playData.MapName.ClampString(40);

        [UIValue(nameof(BeatmapKey))]
        public string BeatmapKey => $"<color=#4AF>{_playData.MapKey}</color>";

        [UIValue(nameof(PP))]
        public string PP => $"<color=purple>{_playData.Pp}</color> " + $"<color=#CCC>{_ppLabel}</color>";

        [UIValue(nameof(ProfilePP))]
        public string ProfilePP =>
            $"+<color=yellow>" + $"{_playData.ProfilePpGained}</color> " + $"<color=#CCC>Profile {_ppLabel}</color>";

        [UIValue(nameof(Rank))]
        public string Rank =>
            _playData.Rank == 0 ? "<color=#777>Unknown</color>" : $"<color=#FF0>#{_playData.Rank}</color>";

        [UIValue(nameof(Date))]
        public string Date => $"<color=#555>" + $"{(DateTime.Now - _playDate).FormatTime()}" + "</color>";

        internal void Update(Play playData)
        {
            bool rankChanged = _playData.Rank != playData.Rank;

            bool ppChanged = !_playData.Pp.Equals(playData.Pp);

            bool profilePpChanged = !_playData.ProfilePpGained.Equals(playData.ProfilePpGained);

            _playData = playData;

            if (rankChanged)
                NotifyPropertyChanged(nameof(Rank));

            if (ppChanged)
                NotifyPropertyChanged(nameof(PP));

            if (profilePpChanged)
                NotifyPropertyChanged(nameof(ProfilePP));
        }

        internal sealed class Factory : PlaceholderFactory<Play, string, SessionListInfo> { }
    }
}
