using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Settings;
using IPA;
using IPA.Config.Stores;
using PleaseWork.Helpfuls;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System.IO;
using IPALogger = IPA.Logging.Logger;

namespace PleaseWork
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
        internal static bool BLInstalled => true;
        internal static string Name => "PPCounter";
        private bool tabInited = false;

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
            
        }

        [OnEnable]
        public void OnEnable() {
            Targeter.GenerateClanNames();
            //new PlaylistLoader();
            TheCounter.InitCounterStatic();
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += () =>
            {
                ChangeMenuTab();
                BSMLSettings.Instance.AddSettingsMenu("BL PP Counter", HelpfulPaths.SETTINGS_BSML, MenuSettingsHandler.Instance);
            };
            /*ClanCounter.FormatTheFormat();
            var test = ClanCounter.displayClan;
            Log.Info(test.Invoke(true, () => "<color=\"yellow\">", "0", 1900.00f, () => "<color=\"green\">", "+314.15", 768.69f, "PP"));//*/
        }
        public void ChangeMenuTab()
        {
            if (tabInited) GameplaySetup.Instance.RemoveTab("BL PP Counter"); else tabInited = true;
            if (PluginConfig.Instance.SimpleUI)
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, SettingsHandler.Instance, MenuType.All);
            else
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, SettingsHandler.Instance, MenuType.All);
        }

        [OnDisable]
        public void OnDisable() 
        { 
            GameplaySetup.Instance.RemoveTab("BL PP Counter");
            BSMLSettings.Instance.RemoveSettingsMenu(SettingsHandler.Instance); 
        }

        /*private void LoadData()
        {
            string dir = HelpfulPaths.THE_FOLDER;
            if (!Directory.Exists(dir)) {
                Log.Info("Folder for this mod doesn't exist! Creating new one...");
                Directory.CreateDirectory(dir);
            }
        }*/
    }
}
