using BeatSaberMarkupLanguage.GameplaySetup;
using HarmonyLib;
using JetBrains.Annotations;

namespace BLPPCounter.Patches;

#if NEW_VERSION
[HarmonyPatch(typeof(GameplaySetup), "UpdateTabsVisibility")] // 1.37.0+
#else
[HarmonyPatch(typeof(GameplaySetup), "ClickedOffModal")] // 1.34.2 and below
#endif
internal static class GameplayTabRefreshPatch
{
    [UsedImplicitly]
    private static void Postfix()
    {
        TabSelectionPatch.RefreshAllSelectors(invokeSelectedTabAction: true);
    }
}
