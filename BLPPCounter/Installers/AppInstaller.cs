using System;
using System.Collections.Generic;
using System.Net.Http;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.FormatTypes;
using BLPPCounter.Utils.TokenParser.Printers;
using TMPro;
using Zenject;

namespace BLPPCounter.Installers
{
    internal sealed class AppInstaller : Installer
    {
        private readonly PluginConfig _pluginConfig;
        private readonly PluginActivationState _activationState;
        private readonly HttpClient _httpClient;

        public AppInstaller(PluginConfig pluginConfig, PluginActivationState activationState)
        {
            _pluginConfig = pluginConfig ?? throw new ArgumentNullException(nameof(pluginConfig));

            _activationState = activationState ?? throw new ArgumentNullException(nameof(activationState));

            _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(pluginConfig.APITimeout) };
        }

        public override void InstallBindings()
        {
            BindConfiguration();
            BindThreading();
            BindApis();
            BindCalculators();
            BindMaps();
            BindProfiles();
            BindTargets();
            BindCounters();
            BindFormatting();
            BindFormatter();
            BindMiscFactories();
            BindLifecycle();
        }

        private void BindConfiguration()
        {
            Container.BindInstance(_pluginConfig).AsSingle();

            Container.BindInstance(_activationState).AsSingle();

            Container.BindInstance(_httpClient).AsSingle();
        }

        private void BindThreading()
        {
            Container
                .BindInterfacesAndSelfTo<MainThreadDispatcher>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<BackgroundTaskRunner>().AsSingle();
        }

        private void BindApis()
        {
            Container.Bind<BLAPI>().AsSingle();
            Container.Bind<SSAPI>().AsSingle();
            Container.Bind<APAPI>().AsSingle();

            Container.BindInterfacesAndSelfTo<LeaderboardApiProvider>().AsSingle();

            Container.BindInterfacesAndSelfTo<BeatSaverMetadataService>().AsSingle();
        }

        private void BindCalculators()
        {
            Container.Bind<BLCalc>().AsSingle();
            Container.Bind<SSCalc>().AsSingle();
            Container.Bind<APCalc>().AsSingle();

            Container.BindInterfacesAndSelfTo<CalculatorProvider>().AsSingle();

            Container.Bind<LeaderboardSelector>().AsSingle();

            Container.Bind<PathUtils>().AsSingle();
        }

        private void BindMaps()
        {
            Container.BindInterfacesAndSelfTo<MapRepository>().AsSingle().NonLazy();
        }

        private void BindProfiles()
        {
            Container.BindInterfacesAndSelfTo<SessionFactory>().AsSingle();

            Container.BindInterfacesAndSelfTo<ProfileFactory>().AsSingle();

            Container.BindInterfacesAndSelfTo<ProfileRepository>().AsSingle();

            Container.BindInterfacesAndSelfTo<CompletedPlayService>().AsSingle();
        }

        private void BindTargets()
        {
            Container.BindInterfacesAndSelfTo<Targeter>().AsSingle().NonLazy();

            Container.BindInterfacesAndSelfTo<LocalReplayService>().AsSingle();
        }

        private void BindCounters()
        {
            Container.BindInterfacesAndSelfTo<ClanService>().AsSingle();

            Container.BindInterfacesAndSelfTo<CounterLabelProvider>().AsSingle();

            Container.BindInterfacesAndSelfTo<CounterCatalog>().AsSingle().NonLazy();

            Container.BindInterfacesAndSelfTo<CounterFactory>().AsSingle();
        }

        private void BindFormatting()
        {
            Container.BindInterfacesAndSelfTo<MainCounterFormattingService>().AsSingle();

            Container.BindInterfacesAndSelfTo<RankCounterFormattingService>().AsSingle();

            Container.BindInterfacesAndSelfTo<RelativeCounterFormattingService>().AsSingle();

            Container.BindInterfacesAndSelfTo<ClanCounterFormattingService>().AsSingle();

            Container.BindInterfacesAndSelfTo<FormatRegistry>().AsSingle().NonLazy();
        }

        private void BindFormatter()
        {
            Container.BindInterfacesAndSelfTo<Tokens>().AsSingle();
            Container.BindExecutionOrder<Tokens>(-100);

            Container.BindInterfacesAndSelfTo<TokenParser>().AsSingle();

            Container.BindInterfacesAndSelfTo<HelpfulFormatter>().AsSingle();
        }

        private void BindMiscFactories()
        {
            Container.BindFactory<TextMeshProUGUI, string[][], IEnumerable<string>, Table, Table.Factory>();

            Container.BindFactory<Chunk[], FormatWrapper, Formatter, Formatter.Factory>();

            Container.BindFactory<Leaderboards, float[], RatingContainer, RatingContainer.Factory>();

            Container.BindFactory<
                Map,
                BeatmapDifficulty,
                string,
                GameplayModifiers.SongSpeed,
                RatingContainer,
                MapSelection,
                MapSelection.Factory
            >();
        }

        private void BindLifecycle()
        {
            Container.BindInterfacesAndSelfTo<PluginLifecycle>().AsSingle().NonLazy();
        }
    }
}
