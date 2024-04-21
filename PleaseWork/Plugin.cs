using IPA;
using IPA.Config.Stores;
using PleaseWork.Settings;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace PleaseWork
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
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
            Log.Info($"{PluginConfig.Instance != null}");
        }

        [OnEnable]
        public void OnEnable() { }

        [OnDisable]
        public void OnDisable() { }
    }
}
