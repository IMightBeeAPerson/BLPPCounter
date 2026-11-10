using System.Reflection;
using BLPPCounter.Installers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Safety;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace BLPPCounter
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public sealed class Plugin
    {
        private PluginActivationState? _activationState;

        internal static IPALogger Log { get; private set; } = null!;

        internal static string Name => "PPCounter";

        [Init]
        public void Init(Zenjector zenjector, IPALogger logger, IPA.Config.Config config)
        {
            Log = logger;

            PluginConfig pluginConfig = config.Generated<PluginConfig>();

            _activationState = new PluginActivationState();

            zenjector.Install<AppInstaller>(Location.App, pluginConfig, _activationState);

            zenjector.Install<MenuInstaller>(Location.Menu);
        }

        [OnEnable]
        public void OnEnable()
        {
            _activationState?.SetEnabled(true);

            Harmony harmony = new("Person.BLPPCounter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [OnDisable]
        public void OnDisable()
        {
            _activationState?.SetEnabled(false);
        }
    }
}
