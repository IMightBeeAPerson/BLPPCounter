using BeatSaberMarkupLanguage.Attributes;
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
#pragma warning disable IDE0051
        internal Session.Play PlayData { get; private set; }
        [UIValue(nameof(BeatmapName))] public string BeatmapName => "<color=#AAA>" + PlayData.MapName + "</color>";
        [UIValue(nameof(BeatmapKey))] public string BeatmapKey => "<color=#4AF>" + PlayData.MapKey + "</color>";
        [UIValue(nameof(PP))] public string PP => "<color=purple>" + PlayData.Pp + $"</color> {PpInfoTabHandler.Instance.GetPPLabel()}";
        [UIValue(nameof(ProfilePP))] public string ProfilePP => "+<color=yellow>" + PlayData.ProfilePpGained + $"</color> Profile {PpInfoTabHandler.Instance.GetPPLabel().ToUpper()}";
        [UIComponent(nameof(SessionTableDiv))] public VerticalLayoutGroup SessionTableDiv;

        public SessionListInfo(Session.Play playData)
        {
            PlayData = playData;
        }
        [UIAction("#post-parse")] private void PostParse()
        {
            //CoroutineHost.Start(FixCellBackground());
        }
        private IEnumerator FixCellBackground()
        {
            // wait one frame so layout is applied
            yield return null;
            yield return new WaitForEndOfFrame();

            var bgImage = SessionTableDiv.GetComponentInChildren<Image>(true);
            if (bgImage != null)
            {
                // Use default UI material so green color shows solid
                bgImage.material = Graphic.defaultGraphicMaterial;
                bgImage.color = Color.green;

                // Stretch the RectTransform to fill the parent
                RectTransform rt = bgImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
        }
    }
}
