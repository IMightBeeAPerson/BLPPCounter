using BeatSaberMarkupLanguage.GameplaySetup;
using HarmonyLib;
using HMUI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Patches
{
    //[HarmonyPatch(typeof(GameplaySetup), "UpdateTabsVisibility")] // 1.37.0 and above
    [HarmonyPatch(typeof(GameplaySetup), "ClickedOffModal")] // 1.34.2 and below
    internal static class StupidGameplayPatch //I really hate that I need to do this.
    {
        [UsedImplicitly]
        internal static void Postfix()
        {
            TabSelectionPatch.UpdateLastSegmentControl?.Invoke();
        }
    }
}
