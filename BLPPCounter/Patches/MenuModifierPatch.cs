using BLPPCounter.Settings.SettingHandlers;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(GameplayModifiersPanelController), "Awake")]
    internal static class MenuModifierPatch
    {
        [UsedImplicitly]
        internal static void Prefix(GameplayModifiersPanelController __instance)
        {
            PpInfoTabHandler.Instance.Gmpc = __instance;
            if (PpInfoTabHandler.Instance.Sldvc is null) return;
            //PpInfoTabHandler.Instance.GmpcInit();
        }
    }
}
