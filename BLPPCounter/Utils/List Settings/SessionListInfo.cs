using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.Misc_Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.List_Settings
{
    internal class SessionListInfo
    {
        internal Session.Play PlayData { get; private set; }
        [UIValue(nameof(BeatmapName))] public string BeatmapName => "<color=#AAA>" + PlayData.MapName + "</color>";
        [UIValue(nameof(BeatmapKey))] public string BeatmapKey => "<color=#4AF>" + PlayData.MapKey + "</color>";
        [UIValue(nameof(PP))] public string PP => "<color=purple>" + PlayData.Pp + $"</color> {PpInfoTabHandler.Instance.GetPPLabel()}";
        [UIValue(nameof(ProfilePP))] public string ProfilePP => "+<color=yellow>" + PlayData.ProfilePpGained + $"</color> Profile {PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}";

        public SessionListInfo(Session.Play playData)
        {
            PlayData = playData;
        }
    }
}
