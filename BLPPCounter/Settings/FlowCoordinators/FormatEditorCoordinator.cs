using BeatSaberMarkupLanguage;
using BLPPCounter.Settings.SettingHandlers;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Settings.FlowCoordinators
{
    internal class FormatEditorCoordinator: FlowCoordinator
    {
        public static FormatEditorCoordinator Instance;
        public MenuSettingsHandler MenuSettingsHandler;
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            Instance = this;
            if (firstActivation)
            {
                SetTitle("PP Format Editor");
                showBackButton = true;
                MenuSettingsHandler = BeatSaberUI.CreateViewController<MenuSettingsHandler>();
                ProvideInitialViewControllers(MenuSettingsHandler);
            }
        }
    }
}
