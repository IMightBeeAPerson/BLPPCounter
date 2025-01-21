using HarmonyLib;
using HMUI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(SegmentedControl), "CreateCells")]
    public static class TabSelectionPatch
    {
#pragma warning disable IDE0051, IDE0044
        private static HashSet<int> LoadedObjects = new HashSet<int>();
        private static readonly Dictionary<string, Action> TabGotSelected = new Dictionary<string, Action>();
        private static readonly Dictionary<string, bool> TabIsSelected = new Dictionary<string, bool>();
        private static HashSet<string> TabNames = new HashSet<string>();
        public static bool AllTabsFound => TabNames.Count == 0;
        [UsedImplicitly]
        private static void Postfix(SegmentedControl __instance)
        {
            if (AllTabsFound || LoadedObjects.Contains(__instance.GetHashCode())) return;
            if (!__instance.name.Equals("BSMLTabSelector")) { LoadedObjects.Add(__instance.GetHashCode()); return; }
            IEnumerable<SegmentedControlCell> cells = __instance.cells.Where(scc => scc is TextSegmentedControlCell tscc && TabNames.Contains(tscc.text));
            if (!cells.Any()) return;
            IEnumerable<string> names = cells.Select(scc => ((TextSegmentedControlCell)scc).text);
            foreach (string s in names)
            {
                TabNames.Remove(s);
                __instance.didSelectCellEvent += (sc, index) =>
                {
                    TabIsSelected[s] = sc.cells[index] is TextSegmentedControlCell tscc && tscc.text.Equals(s);
                    if (TabIsSelected[s]) TabGotSelected[s]?.Invoke();
                };
            }
        }
        public static void AddTabName(string name, Action TabSelected = null)
        {
            TabNames.Add(name);
            TabGotSelected.Add(name, TabSelected);
            TabIsSelected.Add(name, false);
        }
        public static bool GetIfTabIsSelected(string tab) => TabIsSelected.ContainsKey(tab) && TabIsSelected[tab];
        public static void AddToTabSelectedAction(string tab, Action toAdd)
        {
            if (!TabGotSelected.ContainsKey(tab)) return;
            TabGotSelected[tab] += toAdd;
        }
    }
}
