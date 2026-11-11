using HarmonyLib;
using HMUI;
using JetBrains.Annotations;

namespace BLPPCounter.Patches;

[HarmonyPatch(typeof(SegmentedControl), nameof(SegmentedControl.SelectCellWithNumber))]
internal static class SegmentedControlSelectionPatch
{
    [UsedImplicitly]
    private static void Postfix(SegmentedControl __instance)
    {
        TabSelectionPatch.RefreshSelector(__instance);
    }
}
