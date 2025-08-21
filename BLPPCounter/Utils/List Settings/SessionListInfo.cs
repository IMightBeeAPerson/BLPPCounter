using BeatSaberMarkupLanguage.Attributes;
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
        [UIValue(nameof(BeatmapName))] public string BeatmapName => PlayData.MapName;
        [UIValue(nameof(BeatmapKey))] public string BeatmapKey => PlayData.MapKey;
        [UIValue(nameof(PP))] public float PP => PlayData.Pp;
    }
}
