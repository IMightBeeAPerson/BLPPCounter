using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.Misc_Classes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BLPPCounter.Utils.List_Settings
{
    internal class SessionListInfo
    {
        internal Play PlayData { get; private set; }
        private readonly DateTime PlayDate;
        [UIValue(nameof(BeatmapName))] public string BeatmapName => PlayData.MapName.ClampString(40);
        [UIValue(nameof(BeatmapKey))] public string BeatmapKey => "<color=#4AF>" + PlayData.MapKey + "</color>";
        [UIValue(nameof(PP))] public string PP => "<color=purple>" + PlayData.Pp + $"</color> <color=#CCC>{PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}</color>";
        [UIValue(nameof(ProfilePP))] public string ProfilePP => "+<color=yellow>" + PlayData.ProfilePpGained + $"</color> <color=#CCC>Profile {PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}</color>";
        [UIValue(nameof(Date))] public string Date => $"<color=#555>{(DateTime.Now - PlayDate).FormatTime()}</color>";

        public SessionListInfo(Play playData)
        {
            PlayData = playData;
            PlayDate = DateTime.Now;
        }
    }
}
