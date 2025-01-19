using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(SegmentedControl), "CreateCells")]
    internal static class TabSelectionPatch
    {
#pragma warning disable IDE0051, IDE0044
        private static HashSet<int> LoadedObjects = new HashSet<int>();
        public static readonly string REFERENCE_TAB = "PP Calculator";
        public static bool IsReferenceTabSelected = false;
        public static Action ReferenceTabSelected;
        private static void Postfix(SegmentedControl __instance)
        {
            if (LoadedObjects.Contains(__instance.GetHashCode())) return;
            LoadedObjects.Add(__instance.GetHashCode());
            if (!__instance.name.Equals("BSMLTabSelector")) return;
            if (!__instance.cells.Any(scc => scc is TextSegmentedControlCell tscc && tscc.text.Equals(REFERENCE_TAB))) return;
            __instance.didSelectCellEvent +=
                (sc, index) =>
                {
                    IsReferenceTabSelected = sc.cells[index] is TextSegmentedControlCell tscc && tscc.text.Equals(REFERENCE_TAB);
                    if (IsReferenceTabSelected) ReferenceTabSelected?.Invoke();
                };
        }
    }
}
