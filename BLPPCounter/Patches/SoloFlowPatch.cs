using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(MainMenuViewController), "DidActivate")]
    internal class SoloFlowPatch
    {
#pragma warning disable IDE0051
        public static Action EnteredMainMenu;
        [UsedImplicitly]
        private static void Postfix()
        {
            EnteredMainMenu?.Invoke();
        }
    }
}
