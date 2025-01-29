using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Settings;
using IPA;
using IPA.Config.Stores;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils;
using IPALogger = IPA.Logging.Logger;
using UnityEngine;
using Zenject;
using BeatSaberMarkupLanguage;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using HarmonyLib;
using BLPPCounter.Patches;
using Newtonsoft.Json.Linq;
using BS_Utils.Utilities;

namespace BLPPCounter
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
        internal static bool BLInstalled => true;
        internal static Harmony Harmony { get; private set; }
        internal static string Name => "PPCounter";
        [Init]
        /// <summary>
        /// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
        /// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
        /// Only use [Init] with one Constructor.
        /// </summary>
        public Plugin(IPALogger logger, IPA.Config.Config config)
        {
            PluginConfig.Instance = config.Generated<PluginConfig>();
            Instance = this;
            Log = logger;
        }// '<' = &#60; '>' = &#62;
        private void AddMenuStuff()
        {
            TabSelectionPatch.ClearData();
            /*BSMLSettings.Instance.AddSettingsMenu("BL PP Counter", HelpfulPaths.SETTINGS_BSML, MenuSettingsHandler.Instance);
            GameplaySetup.Instance.AddTab("PP Calculator", HelpfulPaths.PP_CALC_BSML, PpInfoTabHandler.Instance); // 1.37.0 and above */
            //BSMLSettings.instance.AddSettingsMenu("BL PP Counter", HelpfulPaths.SETTINGS_BSML, MenuSettingsHandler.Instance); Disabling this for now, needs a lot of fixes bc of BSML
            GameplaySetup.instance.AddTab("PP Calculator", HelpfulPaths.PP_CALC_BSML, PpInfoTabHandler.Instance); // 1.34.2 and below */
            SimpleSettingsHandler.Instance.ChangeMenuTab(false);
        }

        [OnEnable]
        public void OnEnable() {
            Targeter.GenerateClanNames(); //async
            //BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += AddMenuStuff; //async (kinda) || 1.37.0 and above
            BSEvents.menuSceneActive += AddMenuStuff; // 1.34.2 and below
            TabSelectionPatch.AddTabName("PP Calculator");
            TheCounter.InitCounterStatic();
            Harmony = new Harmony("Person.BLPPCounter");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());

            //new PlaylistLoader();
            /*ClanCounter.FormatTheFormat();
            var test = ClanCounter.displayClan;
            Log.Info(test.Invoke(true, () => "<color=yellow>", "0", 1900.00f, () => "<color=green>", "+314.15", 768.69f, "PP"));//*/
        }


        [OnDisable]
        public void OnDisable() 
        {
            /*GameplaySetup.Instance.RemoveTab("BL PP Counter");
            GameplaySetup.Instance.RemoveTab("PP Calculator");
            BSMLSettings.Instance.RemoveSettingsMenu(SettingsHandler.Instance);
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing -= AddMenuStuff; // 1.37.0 and above */
            GameplaySetup.instance.RemoveTab("BL PP Counter");
            GameplaySetup.instance.RemoveTab("PP Calculator");
            //BSMLSettings.instance.RemoveSettingsMenu(SettingsHandler.Instance);
            BSEvents.menuSceneActive -= AddMenuStuff; // 1.34.2 and below */
            Harmony.UnpatchSelf();
        }

    }
}
