using BLPPCounter.Settings.SettingHandlers;
using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(StandardLevelDetailViewController), "DidActivate")]
    internal static class MenuLevelPatch
    {
        internal static void Prefix(StandardLevelDetailViewController __instance, bool firstActivation)
        {
            if (firstActivation)
            {
                PpInfoTabHandler.Instance.Sldvc = __instance;
                if (PpInfoTabHandler.Instance.Gmpc is null) return;
                PpInfoTabHandler.Instance.SldvcInit();
            }
        }
    }
}
