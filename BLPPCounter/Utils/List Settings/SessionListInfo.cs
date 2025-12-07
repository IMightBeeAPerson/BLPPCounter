using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.Profile_Utils;
using System;

namespace BLPPCounter.Utils.List_Settings
{
    internal class SessionListInfo(Play playData)
    {
        internal Play PlayData { get; private set; } = playData;
        private readonly DateTime PlayDate = DateTime.Now;
        [UIValue(nameof(BeatmapName))] public string BeatmapName => PlayData.MapName.ClampString(40);
        [UIValue(nameof(BeatmapKey))] public string BeatmapKey => "<color=#4AF>" + PlayData.MapKey + "</color>";
        [UIValue(nameof(PP))] public string PP => "<color=purple>" + PlayData.Pp + $"</color> <color=#CCC>{PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}</color>";
        [UIValue(nameof(ProfilePP))] public string ProfilePP => "+<color=yellow>" + PlayData.ProfilePpGained + $"</color> <color=#CCC>Profile {PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}</color>";
        [UIValue(nameof(Date))] public string Date => $"<color=#555>{(DateTime.Now - PlayDate).FormatTime()}</color>";
    }
}
