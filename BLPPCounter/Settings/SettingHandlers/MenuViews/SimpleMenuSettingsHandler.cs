using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Special_Utils;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public sealed class SimpleMenuSettingsHandler : BSMLResourceViewController
    {
        private PluginConfig _config = null!;
        private SimpleSettingsHandler _simpleSettingsHandler = null!;
        private MenuSettingsHandler _menuSettingsHandler = null!;

        private readonly Action<int, bool> _changeSetting;

        private bool _loaded;

        public SimpleMenuSettingsHandler()
        {
            _changeSetting = ChangeSetting;
        }

        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.SimpleSettings.bsml";

        [UIComponent(nameof(UICustomizer))]
        private CustomCellListTableData UICustomizer = null!;

        [UIValue(nameof(MenuHeight))]
        private int MenuHeight => SettingsHandler.MENU_HEIGHT;

        [UIValue(nameof(MenuAnchor))]
        private int MenuAnchor => SettingsHandler.MENU_ANCHOR;

        [UIValue(nameof(UISettings))]
        public List<object> UISettings { get; } = [];

        [Inject]
        private void Construct(
            PluginConfig config,
            SimpleSettingsHandler simpleSettingsHandler,
            MenuSettingsHandler menuSettingsHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _simpleSettingsHandler =
                simpleSettingsHandler ?? throw new ArgumentNullException(nameof(simpleSettingsHandler));

            _menuSettingsHandler = menuSettingsHandler ?? throw new ArgumentNullException(nameof(menuSettingsHandler));
        }

        [UIAction("#back")]
        private void GoBack()
        {
            _simpleSettingsHandler.ChangeMenuTab();
            _menuSettingsHandler.GoBack();
        }

        public void LoadMenu()
        {
            if (_loaded)
                return;

            _loaded = true;

            UISettings.AddRange(ConvertMenu().Cast<object>());

            for (int index = 0; index < UISettings.Count; index++)
            {
                if (UISettings[index] is SettingToggleInfo setting)
                {
                    setting.Usable = _config.SimpleMenuConfig[index];
                }
            }

#if NEW_VERSION
            UICustomizer.TableView.ReloadData();
#else
            UICustomizer.tableView.ReloadData();
#endif
        }

        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";

            MatchCollection matches = HelpfulRegex.ConvertMenuRegex.Matches(
                Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resource)
            );

            if (_config.SimpleMenuConfig.Length != matches.Count)
            {
                _config.SimpleMenuConfig = new BoolStorage(matches.Count, true);
            }

            List<SettingToggleInfo> settings = [];

            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];

                settings.Add(
                    new SettingToggleInfo(
                        match.Groups[3].Value,
                        match.Groups.Count >= 6 ? match.Groups[5].Value : "",
                        match.Groups[1].Value.Replace('-', ' '),
                        index,
                        _changeSetting
                    )
                );
            }

            return settings;
        }

        private void ChangeSetting(int id, bool value)
        {
            _config.SimpleMenuConfig[id] = value;
        }
    }
}
