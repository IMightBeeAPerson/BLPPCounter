using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Utils.LangAddons;
using HarmonyLib;
using HMUI;
using JetBrains.Annotations;

namespace BLPPCounter.Patches;

[HarmonyPatch(typeof(SegmentedControl), nameof(SegmentedControl.CreateCells))]
public static class TabSelectionPatch
{
    private static readonly Dictionary<string, Action?> TabSelectedActions = [with(StringComparer.Ordinal)];

    private static readonly HashSet<string> PendingTabNames = [with(StringComparer.Ordinal)];

    // We retain selector references, not their hash codes.
    private static readonly HashSet<SegmentedControl> KnownSelectors = [];

    private static readonly Dictionary<SegmentedControl, Action<SegmentedControl, int>> ModsSelectorHandlers = [];

    private static readonly Dictionary<SegmentedControl, Action<SegmentedControl, int>> ModTabSelectorHandlers = [];

    private static SegmentedControl? _ignoredTabSelector;
    private static bool _startPatchAdded;

    public static event Action<string>? ModTabSelected;

    /// <summary>
    /// The selected mod sub-tab, whether or not the user is currently viewing Mods.
    /// </summary>
    public static string LastSelectedModTab { get; private set; } = "";

    /// <summary>
    /// True while the top-level Mods tab is selected.
    /// </summary>
    public static bool IsOnModsTab { get; private set; }

    public static bool AllTabsFound => PendingTabNames.Count == 0;

    [UsedImplicitly]
    private static void Postfix(SegmentedControl __instance)
    {
        ObserveSelector(__instance);
    }

    public static void SetIgnoredSelector(SegmentedControl? segmentedControl)
    {
        if (ReferenceEquals(_ignoredTabSelector, segmentedControl))
        {
            return;
        }

        if (_ignoredTabSelector is not null)
        {
            UnregisterSelector(_ignoredTabSelector);
            KnownSelectors.Remove(_ignoredTabSelector);
        }

        _ignoredTabSelector = segmentedControl;
    }

    public static void AddTabName(string name, Action? tabSelected = null)
    {
        if (BetterString.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A tab name is required.", nameof(name));
        }

        PendingTabNames.Add(name);

        if (TabSelectedActions.TryGetValue(name, out Action? existingAction))
        {
            TabSelectedActions[name] = existingAction + tabSelected;
        }
        else
        {
            TabSelectedActions.Add(name, tabSelected);
        }

        RegisterExistingSelectors();
    }

    public static void AddToTabSelectedAction(string tab, Action toAdd)
    {
        if (toAdd is null)
        {
            throw new ArgumentNullException(nameof(toAdd));
        }

        if (TabSelectedActions.TryGetValue(tab, out Action? existingAction))
        {
            TabSelectedActions[tab] = existingAction + toAdd;
        }
        else
        {
            AddTabName(tab, toAdd);
        }
    }

    public static void RemoveFromTabSelectedAction(string tab, Action toRemove)
    {
        if (TabSelectedActions.TryGetValue(tab, out Action? existingAction))
        {
            TabSelectedActions[tab] = existingAction - toRemove;
        }
    }

    /// <summary>
    /// Returns true only when the supplied mod sub-tab is selected and Mods is open.
    /// </summary>
    public static bool GetIfTabIsSelected(string tab)
    {
        return IsOnModsTab && string.Equals(tab, LastSelectedModTab, StringComparison.Ordinal);
    }

    internal static void AddStartPatch()
    {
        if (_startPatchAdded)
        {
            return;
        }

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

        RegisterExistingSelectors();
    }

    internal static void Reset()
    {
        if (_startPatchAdded)
        {
            SoloFlowPatch.EnteredMainMenu -= HandleEnteredMainMenu;
            _startPatchAdded = false;
        }

        foreach (KeyValuePair<SegmentedControl, Action<SegmentedControl, int>> pair in ModsSelectorHandlers)
        {
            pair.Key.didSelectCellEvent -= pair.Value;
        }

        foreach (KeyValuePair<SegmentedControl, Action<SegmentedControl, int>> pair in ModTabSelectorHandlers)
        {
            pair.Key.didSelectCellEvent -= pair.Value;
        }

        KnownSelectors.Clear();
        ModsSelectorHandlers.Clear();
        ModTabSelectorHandlers.Clear();

        PendingTabNames.Clear();
        TabSelectedActions.Clear();

        _ignoredTabSelector = null;
        LastSelectedModTab = "";
        IsOnModsTab = false;

        ModTabSelected = null;
    }

    /// <summary>
    /// Refreshes all tracked selectors from SegmentedControl.selectedCellNumber.
    /// Pass true when the active tab's registered callback must be run again.
    /// </summary>
    internal static void RefreshAllSelectors(bool invokeSelectedTabAction)
    {
        // Do this first so sub-tab processing knows whether Mods is active.
        foreach (SegmentedControl selector in ModsSelectorHandlers.Keys.ToArray())
        {
            RefreshSelector(selector, invokeSelectedTabAction: false);
        }

        foreach (SegmentedControl selector in ModTabSelectorHandlers.Keys.ToArray())
        {
            RefreshSelector(selector, invokeSelectedTabAction);
        }
    }

    /// <summary>
    /// Called by the SelectCellWithNumber Harmony postfix.
    /// </summary>
    internal static void RefreshSelector(SegmentedControl segmentedControl)
    {
        RefreshSelector(segmentedControl, invokeSelectedTabAction: true);
    }

    private static void ObserveSelector(SegmentedControl segmentedControl)
    {
        if (
            !string.Equals(segmentedControl.name, "BSMLTabSelector", StringComparison.Ordinal)
            || ReferenceEquals(segmentedControl, _ignoredTabSelector)
        )
        {
            return;
        }

        KnownSelectors.Add(segmentedControl);

        if (IsModsSelector(segmentedControl))
        {
            RegisterModsSelector(segmentedControl);
            RefreshSelector(segmentedControl, invokeSelectedTabAction: false);
            return;
        }

        RegisterModTabSelectorIfRelevant(segmentedControl);
    }

    private static void RegisterExistingSelectors()
    {
        foreach (SegmentedControl selector in KnownSelectors.ToArray())
        {
            if (ReferenceEquals(selector, _ignoredTabSelector))
            {
                continue;
            }

            if (IsModsSelector(selector))
            {
                RegisterModsSelector(selector);
            }
            else
            {
                RegisterModTabSelectorIfRelevant(selector);
            }
        }
    }

    private static bool IsModsSelector(SegmentedControl segmentedControl)
    {
        return segmentedControl.cells.Count == 2
            && segmentedControl.cells.Any(cell =>
                cell is TextSegmentedControlCell textCell
                && string.Equals(textCell.text, "Mods", StringComparison.Ordinal)
            );
    }

    private static void RegisterModsSelector(SegmentedControl segmentedControl)
    {
        if (ModsSelectorHandlers.ContainsKey(segmentedControl))
        {
            return;
        }

        Action<SegmentedControl, int> handler = HandleModsSelection;

        segmentedControl.didSelectCellEvent += handler;
        ModsSelectorHandlers.Add(segmentedControl, handler);
    }

    private static void RegisterModTabSelectorIfRelevant(SegmentedControl segmentedControl)
    {
        foreach (TextSegmentedControlCell cell in segmentedControl.cells.OfType<TextSegmentedControlCell>())
        {
            if (TabSelectedActions.ContainsKey(cell.text))
            {
                PendingTabNames.Remove(cell.text);
            }
        }

        bool containsRegisteredTab = segmentedControl
            .cells.OfType<TextSegmentedControlCell>()
            .Any(cell => TabSelectedActions.ContainsKey(cell.text));

        if (!containsRegisteredTab)
        {
            return;
        }

        if (!ModTabSelectorHandlers.ContainsKey(segmentedControl))
        {
            Action<SegmentedControl, int> handler = HandleModTabSelection;

            segmentedControl.didSelectCellEvent += handler;
            ModTabSelectorHandlers.Add(segmentedControl, handler);
        }

        RefreshSelector(segmentedControl, invokeSelectedTabAction: true);
    }

    private static void HandleModsSelection(SegmentedControl segmentedControl, int selectedIndex)
    {
        ApplyModsSelection(segmentedControl, selectedIndex);
    }

    private static void HandleModTabSelection(SegmentedControl segmentedControl, int selectedIndex)
    {
        ApplyModTabSelection(segmentedControl, selectedIndex, invokeSelectedTabAction: true);
    }

    private static void RefreshSelector(SegmentedControl segmentedControl, bool invokeSelectedTabAction)
    {
        int selectedIndex = segmentedControl.selectedCellNumber;

        if (ModsSelectorHandlers.ContainsKey(segmentedControl))
        {
            ApplyModsSelection(segmentedControl, selectedIndex);
            return;
        }

        if (ModTabSelectorHandlers.ContainsKey(segmentedControl))
        {
            ApplyModTabSelection(segmentedControl, selectedIndex, invokeSelectedTabAction);
        }
    }

    private static void ApplyModsSelection(SegmentedControl segmentedControl, int selectedIndex)
    {
        if (!TryGetCellText(segmentedControl, selectedIndex, out string selectedTab))
        {
            return;
        }

        bool wasOnModsTab = IsOnModsTab;
        IsOnModsTab = string.Equals(selectedTab, "Mods", StringComparison.Ordinal);

        if (wasOnModsTab != IsOnModsTab)
        {
            ModTabSelected?.Invoke(selectedTab);
        }

        if (IsOnModsTab && !BetterString.IsNullOrWhiteSpace(LastSelectedModTab))
        {
            InvokeTabSelected(LastSelectedModTab);
        }
    }

    private static void ApplyModTabSelection(
        SegmentedControl segmentedControl,
        int selectedIndex,
        bool invokeSelectedTabAction
    )
    {
        if (!TryGetCellText(segmentedControl, selectedIndex, out string selectedTab))
        {
            return;
        }

        LastSelectedModTab = selectedTab;

        if (IsOnModsTab && invokeSelectedTabAction)
        {
            InvokeTabSelected(selectedTab);
        }
    }

    private static bool TryGetCellText(SegmentedControl segmentedControl, int selectedIndex, out string text)
    {
        text = "";

        if (
            selectedIndex < 0
            || selectedIndex >= segmentedControl.cells.Count
            || segmentedControl.cells[selectedIndex] is not TextSegmentedControlCell textCell
        )
        {
            return false;
        }

        text = textCell.text;
        return true;
    }

    private static void HandleEnteredMainMenu()
    {
        if (!IsOnModsTab)
        {
            return;
        }

        IsOnModsTab = false;
        ModTabSelected?.Invoke("");
    }

    private static void UnregisterSelector(SegmentedControl segmentedControl)
    {
        if (ModsSelectorHandlers.TryGetValue(segmentedControl, out Action<SegmentedControl, int>? modsHandler))
        {
            segmentedControl.didSelectCellEvent -= modsHandler;
            ModsSelectorHandlers.Remove(segmentedControl);
        }

        if (ModTabSelectorHandlers.TryGetValue(segmentedControl, out Action<SegmentedControl, int>? tabHandler))
        {
            segmentedControl.didSelectCellEvent -= tabHandler;
            ModTabSelectorHandlers.Remove(segmentedControl);
        }
    }

    private static void InvokeTabSelected(string tabName)
    {
        if (TabSelectedActions.TryGetValue(tabName, out Action? action))
        {
            action?.Invoke();
        }
    }
}
