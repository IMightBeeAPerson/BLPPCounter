using HarmonyLib;
using HMUI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(SegmentedControl), "CreateCells")]
    public static class TabSelectionPatch
    {
#pragma warning disable IDE0051, IDE0044
        private static readonly HashSet<int> LoadedObjects = new HashSet<int>();
        private static readonly Dictionary<string, Action> TabGotSelected = new Dictionary<string, Action>();
        private static readonly HashSet<string> TabNames = new HashSet<string>();
        public static event Action<string> ModTabSelected;
        internal static Action UpdateLastSegmentControl;
        public static string LastSelectedModTab { get; private set; } = "";
        private static string LastLoadedTabSelector = "";
        private static bool ModTabFound = false;
        public static bool AllTabsFound => TabNames.Count == 0;
        [UsedImplicitly]
        private static void Postfix(SegmentedControl __instance)
        {
            if (LoadedObjects.Contains(__instance.GetHashCode())) return;
            if (!__instance.name.Equals("BSMLTabSelector")) { LoadedObjects.Add(__instance.GetHashCode()); return; }
            string tabSelectorCells = __instance.cells.Aggregate("", (total, cell) => cell is TextSegmentedControlCell tscc ? total + ", " + tscc.text : total).Substring(2);
            if (LastLoadedTabSelector.Equals(tabSelectorCells))
            {
                if (__instance.cells[0] is TextSegmentedControlCell tscc && !tscc.text.Equals(LastSelectedModTab))
                {
                    TabGotSelected[tscc.text]?.Invoke();
                    LastSelectedModTab = tscc.text;
                }
                return;
            }
            LastLoadedTabSelector = tabSelectorCells;
            if (!ModTabFound && __instance.cells.Count() == 2 && __instance.cells.Any(scc => scc is TextSegmentedControlCell tscc && tscc.text.Equals("Mods")))
            {
                ModTabSelected += str =>
                {
                    if (str.Equals("Mods") && LastSelectedModTab.Length != 0) TabGotSelected[LastSelectedModTab]?.Invoke();
                };
                __instance.didSelectCellEvent += (sc, index) =>
                {
                    if (sc.cells[index] is TextSegmentedControlCell tscc) ModTabSelected?.Invoke(tscc.text);
                };
                LoadedObjects.Add(__instance.GetHashCode());
                ModTabFound = true;
                return;
            }
            //Plugin.Log.Info("Loaded, count = " + __instance.cells.Count() + ", names = [" + tabSelectorCells + "]");
            if (AllTabsFound && __instance.cells[0] is TextSegmentedControlCell textCell && TabGotSelected.ContainsKey(textCell.text))
            {
                string s = textCell.text;
                TabGotSelected[s]?.Invoke();
                LastSelectedModTab = s;
                return;
            }
            IEnumerable<SegmentedControlCell> cells = __instance.cells.Where(scc => scc is TextSegmentedControlCell tscc && TabNames.Contains(tscc.text));
            if (!cells.Any()) return;
            IEnumerable<string> names = cells.Select(scc => ((TextSegmentedControlCell)scc).text);
            foreach (string s in names)
            {
                TabNames.Remove(s);
                void CellEvent(SegmentedControl sc, int index)
                {
                    LastSelectedModTab = s;
                    if (sc.cells[index] is TextSegmentedControlCell tscc && tscc.text.Equals(s)) TabGotSelected[s]?.Invoke();
                }
                __instance.didSelectCellEvent += CellEvent;
                CellEvent(__instance, 0);
                UpdateLastSegmentControl = () => CellEvent(__instance, 0);
            }
        }
        public static void AddTabName(string name, Action TabSelected = null)
        {
            TabNames.Add(name);
            TabGotSelected.Add(name, TabSelected);
        }
        public static bool GetIfTabIsSelected(string tab) => LastSelectedModTab.Equals(tab);
        public static void AddToTabSelectedAction(string tab, Action toAdd)
        {
            if (!TabGotSelected.ContainsKey(tab)) return;
            TabGotSelected[tab] += toAdd;
        }
        internal static void ClearData()
        {
            TabNames.Clear();
            foreach (string s in TabGotSelected.Keys) TabNames.Add(s);
        }
    }
}
