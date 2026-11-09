using System;
using HarmonyLib;
using JetBrains.Annotations;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(MainMenuViewController), "DidActivate")]
    internal class SoloFlowPatch
    {
#pragma warning disable IDE0051
        public static Action? EnteredMainMenu;

        [UsedImplicitly]
        private static void Postfix()
        {
            EnteredMainMenu?.Invoke();
        }
    }
}
