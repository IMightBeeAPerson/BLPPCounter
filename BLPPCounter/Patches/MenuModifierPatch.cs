using BLPPCounter.Settings.SettingHandlers;
using HarmonyLib;
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
        internal static void Prefix(GameplayModifiersPanelController __instance)
        {
            PpInfoTabHandler.Instance.Gmpc = __instance;
            //Plugin.Log.Info("GMPC has been set.");
            if (PpInfoTabHandler.Instance.Sldvc is null) return;
            //PpInfoTabHandler.Instance.GmpcInit();
        }
    }
}
