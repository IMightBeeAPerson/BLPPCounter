using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Utils.LangAddons;
using HarmonyLib;
using HMUI;
using JetBrains.Annotations;

namespace BLPPCounter.Patches
{
    [HarmonyPatch(typeof(SegmentedControl), "CreateCells")]
    public static class TabSelectionPatch
    {
        private static readonly HashSet<int> LoadedObjects = [];

        private static readonly Dictionary<string, Action?> TabSelectedActions = [with(StringComparer.Ordinal)];

        private static readonly HashSet<string> PendingTabNames = [with(StringComparer.Ordinal)];

        private static SegmentedControl? _ignoredTabSelector;

        private static string _lastLoadedTabSelector = "";

        private static int _modTabHash = -1;
        private static bool _startPatchAdded;

        public static event Action<string>? ModTabSelected;

        internal static Action? UpdateLastSegmentControl;

        public static string LastSelectedModTab { get; private set; } = "";

        public static bool AllTabsFound => PendingTabNames.Count == 0;

        public static bool IsOnModsTab { get; private set; }

        [UsedImplicitly]
        private static void Postfix(SegmentedControl __instance)
        {
            int objectHash = __instance.GetHashCode();

            if (LoadedObjects.Contains(objectHash))
            {
                if (objectHash == _modTabHash)
                    ModTabSelected?.Invoke("");

                return;
            }

            if (
                !string.Equals(__instance.name, "BSMLTabSelector", StringComparison.Ordinal)
                || ReferenceEquals(__instance, _ignoredTabSelector)
            )
            {
                LoadedObjects.Add(objectHash);
                return;
            }

            string tabSelectorCells = string.Join(
                ", ",
                __instance.cells.OfType<TextSegmentedControlCell>().Select(cell => cell.text)
            );

            if (string.Equals(_lastLoadedTabSelector, tabSelectorCells, StringComparison.Ordinal))
            {
                HandleInitialSelection(__instance);

                return;
            }

            if (IsModsSelector(__instance))
            {
                ConfigureModsSelector(__instance, objectHash);

                return;
            }

            _lastLoadedTabSelector = tabSelectorCells;

            RegisterPendingTabs(__instance);
        }

        public static void SetIgnoredSelector(SegmentedControl? segmentedControl)
        {
            _ignoredTabSelector = segmentedControl;
        }

        public static void AddTabName(string name, Action? tabSelected = null)
        {
            if (BetterString.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A tab name is required.", nameof(name));
            }

            PendingTabNames.Add(name);

            if (!TabSelectedActions.ContainsKey(name))
            {
                TabSelectedActions.Add(name, tabSelected);
            }
            else if (tabSelected is not null)
            {
                TabSelectedActions[name] += tabSelected;
            }
        }

        public static void AddToTabSelectedAction(string tab, Action toAdd)
        {
            if (toAdd is null)
            {
                throw new ArgumentNullException(nameof(toAdd));
            }

            if (TabSelectedActions.ContainsKey(tab))
            {
                TabSelectedActions[tab] += toAdd;
            }
        }

        public static void RemoveFromTabSelectedAction(string tab, Action toRemove)
        {
            if (TabSelectedActions.ContainsKey(tab))
            {
                TabSelectedActions[tab] -= toRemove;
            }
        }

        public static bool GetIfTabIsSelected(string tab)
        {
            return string.Equals(tab, LastSelectedModTab, StringComparison.Ordinal);
        }

        internal static void AddStartPatch()
        {
            if (_startPatchAdded)
                return;

            SoloFlowPatch.EnteredMainMenu += HandleEnteredMainMenu;

            _startPatchAdded = true;
        }

        internal static void ClearData()
        {
            PendingTabNames.Clear();

            foreach (string tab in TabSelectedActions.Keys)
            {
                PendingTabNames.Add(tab);
            }
        }

        internal static void Reset()
        {
            if (_startPatchAdded)
            {
                SoloFlowPatch.EnteredMainMenu -= HandleEnteredMainMenu;

                _startPatchAdded = false;
            }

            LoadedObjects.Clear();
            PendingTabNames.Clear();

            TabSelectedActions.Clear();

            ModTabSelected = null;
            UpdateLastSegmentControl = null;

            _ignoredTabSelector = null;
            _lastLoadedTabSelector = "";
            _modTabHash = -1;

            LastSelectedModTab = "";
            IsOnModsTab = false;
        }

        private static void HandleEnteredMainMenu()
        {
            ModTabSelected?.Invoke("");
        }

        private static bool IsModsSelector(SegmentedControl segmentedControl)
        {
            return segmentedControl.cells.Count == 2
                && segmentedControl.cells.Any(cell =>
                    cell is TextSegmentedControlCell textCell
                    && string.Equals(textCell.text, "Mods", StringComparison.Ordinal)
                );
        }

        private static void ConfigureModsSelector(SegmentedControl segmentedControl, int objectHash)
        {
            ModTabSelected = text =>
            {
                IsOnModsTab = string.Equals(text, "Mods", StringComparison.Ordinal);

                if (IsOnModsTab && !BetterString.IsNullOrWhiteSpace(LastSelectedModTab))
                {
                    InvokeTabSelected(LastSelectedModTab);
                }
            };

            segmentedControl.didSelectCellEvent += HandleModsTabSelected;

            _modTabHash = objectHash;
            LoadedObjects.Add(objectHash);
        }

        private static void HandleModsTabSelected(SegmentedControl segmentedControl, int index)
        {
            if (segmentedControl.cells[index] is TextSegmentedControlCell textCell)
            {
                ModTabSelected?.Invoke(textCell.text);
            }
        }

        private static void RegisterPendingTabs(SegmentedControl segmentedControl)
        {
            foreach (
                TextSegmentedControlCell cell in segmentedControl
                    .cells.OfType<TextSegmentedControlCell>()
                    .Where(cell => PendingTabNames.Contains(cell.text))
                    .ToArray()
            )
            {
                string tabName = cell.text;

                PendingTabNames.Remove(tabName);

                void HandleSelection(SegmentedControl control, int index)
                {
                    LastSelectedModTab = control.cells[index] is TextSegmentedControlCell selected ? selected.text : "";

                    if (string.Equals(LastSelectedModTab, tabName, StringComparison.Ordinal))
                    {
                        InvokeTabSelected(tabName);
                    }
                }

                segmentedControl.didSelectCellEvent += HandleSelection;

                HandleSelection(segmentedControl, 0);

                UpdateLastSegmentControl = () => HandleSelection(segmentedControl, 0);
            }

            LoadedObjects.Add(segmentedControl.GetHashCode());
        }

        private static void HandleInitialSelection(SegmentedControl segmentedControl)
        {
            if (
                segmentedControl.cells.Count == 0
                || segmentedControl.cells[0] is not TextSegmentedControlCell firstCell
                || string.Equals(firstCell.text, LastSelectedModTab, StringComparison.Ordinal)
            )
            {
                return;
            }

            LastSelectedModTab = firstCell.text;

            InvokeTabSelected(firstCell.text);
        }

        private static void InvokeTabSelected(string tabName)
        {
            if (TabSelectedActions.TryGetValue(tabName, out Action? action))
            {
                action?.Invoke();
            }
        }
    }
}
