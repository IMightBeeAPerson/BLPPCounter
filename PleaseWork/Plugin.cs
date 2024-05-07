using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PleaseWork.Counters;
using PleaseWork.Helpfuls;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
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
            new PlaylistLoader();
            /*ClanCounter.FormatTheFormat();
            var test = ClanCounter.displayClan;
            Log.Info(test.Invoke(true, "<color=\"yellow\">", "0", 1900.00f, "<color=\"green\">", "+314.15", 768.69f, "PP"));//*/
        }

        [OnDisable]
        public void OnDisable() { }

        private void LoadData()
        {
            string dir = HelpfulPaths.THE_FOLDER;
            if (!Directory.Exists(dir)) {
                Log.Info("Folder for this mod doesn't exist! Creating new one...");
                Directory.CreateDirectory(dir);
            }
        }
    }
}
