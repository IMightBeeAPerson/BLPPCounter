using BLPPCounter.Settings.SettingHandlers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(StandardLevelDetailViewController), "DidActivate")]
    internal static class MenuLevelPatch
    {
        internal static void Prefix(StandardLevelDetailViewController __instance)
        {
            PpInfoTabHandler.Instance.Sldvc = __instance;
            //Plugin.Log.Info("SLDVC has been set.");
            if (PpInfoTabHandler.Instance.Gmpc is null) return;
            PpInfoTabHandler.Instance.SldvcInit();
        }
    }
}
