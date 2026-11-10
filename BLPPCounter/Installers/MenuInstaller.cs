using System.Collections.Generic;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using Zenject;

namespace BLPPCounter.Installers
{
    internal sealed class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            BindSettingsServices();
            BindListFactories();
            BindViewControllers();
            BindLifecycle();
        }

        private void BindSettingsServices()
        {
            Container.BindInterfacesAndSelfTo<SettingsHandler>().AsSingle();

            Container.BindInterfacesAndSelfTo<PpInfoTabHandler>().AsSingle();

            Container.Bind<ProfilePanelPresenter>().AsSingle();

            Container.Bind<SimpleSettingsHandler>().AsSingle();

            Container.Bind<LeaderboardSettingsHandler>().AsSingle();
        }

        private void BindListFactories()
        {
            Container.Bind<TargetInfoFactory>().AsSingle();

            Container.BindFactory<Leaderboards, LeaderboardListInfo, LeaderboardListInfo.Factory>();

            Container.BindFactory<Play, string, SessionListInfo, SessionListInfo.Factory>();

            Container.BindFactory<
                System.Reflection.PropertyInfo,
                System.Action,
                ColorListInfo,
                ColorListInfo.Factory
            >();

            Container.BindFactory<List<object>, FormatListInfo, FormatListInfo.Factory>();
        }

        private void BindViewControllers()
        {
            Container.BindInterfacesAndSelfTo<MenuSettingsHandler>().FromNewComponentAsViewController().AsSingle();

            Container
                .BindInterfacesAndSelfTo<SimpleMenuSettingsHandler>()
                .FromNewComponentAsViewController()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<FormatEditorHandler>().FromNewComponentAsViewController().AsSingle();

            Container.BindInterfacesAndSelfTo<ColorSettingsHandler>().FromNewComponentAsViewController().AsSingle();

            Container.BindInterfacesAndSelfTo<CustomAliasHandler>().FromNewComponentAsViewController().AsSingle();
        }

        private void BindLifecycle()
        {
            Container.BindInterfacesAndSelfTo<MenuLifecycle>().AsSingle().NonLazy();
        }
    }
}
