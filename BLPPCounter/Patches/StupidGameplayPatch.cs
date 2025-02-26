using BeatSaberMarkupLanguage.GameplaySetup;
using HarmonyLib;
using JetBrains.Annotations;

namespace BLPPCounter.Patches
{
#if NEW_VERSION
    [HarmonyPatch(typeof(GameplaySetup), "UpdateTabsVisibility")] // 1.37.0 and above
#else
    [HarmonyPatch(typeof(GameplaySetup), "ClickedOffModal")] // 1.34.2 and below
#endif
    internal static class StupidGameplayPatch //I really hate that I need to do this.
    {
        [UsedImplicitly]
        internal static void Postfix()
        {
            TabSelectionPatch.UpdateLastSegmentControl?.Invoke();
        }
    }
}
