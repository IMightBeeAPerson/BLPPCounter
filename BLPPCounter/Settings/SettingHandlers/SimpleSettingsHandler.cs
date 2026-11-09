using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils;
using UnityEngine;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers
{
    internal sealed class SimpleSettingsHandler
    {
        private static readonly HashSet<string> NonSettingTags =
        [
            "settings-container",
            "vertical",
            "horizontal",
            "modal",
            "custom-list",
        ];

        private readonly PluginConfig _config;
        private readonly SettingsHandler _settingsHandler;
        private readonly LazyInject<SimpleMenuSettingsHandler> _simpleMenuSettingsHandler;

#if !NEW_VERSION
        private bool _loadData = true;
#endif

        [UIObject(nameof(Container))]
        private GameObject? Container;

        public SimpleSettingsHandler(
            PluginConfig config,
            SettingsHandler settingsHandler,
            LazyInject<SimpleMenuSettingsHandler> simpleMenuSettingsHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _settingsHandler = settingsHandler ?? throw new ArgumentNullException(nameof(settingsHandler));

            _simpleMenuSettingsHandler =
                simpleMenuSettingsHandler ?? throw new ArgumentNullException(nameof(simpleMenuSettingsHandler));
        }

        [UIAction("#post-parse")]
        private void LoadElements()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";

            MatchCollection matches = HelpfulRegex.LoadElementsRegex.Matches(
                Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resource)
            );

#if NEW_VERSION
            bool loadData =
                _config.SimpleMenuConfig.Length
                == matches
                    .OfType<Match>()
                    .Count(match => !NonSettingTags.Contains(match.Groups[1].Value) && !match.Value.Contains('~'));
#else
            int settingCount = matches
                .OfType<Match>()
                .Count(match => !NonSettingTags.Contains(match.Groups[1].Value) && !match.Value.Contains('~'));

            _loadData &= _config.SimpleMenuConfig.Length == settingCount;

            bool loadData = _loadData;
#endif

            if (loadData)
            {
                _simpleMenuSettingsHandler.Value.LoadMenu();
            }

            Dictionary<string, bool> usability = [];

            foreach (
                SettingToggleInfo setting in _simpleMenuSettingsHandler.Value.UISettings.OfType<SettingToggleInfo>()
            )
            {
                usability[setting.Text] = setting.Usable;
            }

            string markup = "";

            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;

                bool include = !loadData || NonSettingTags.Contains(tag) || match.Value.Contains('~');

                if (!include)
                {
                    string text = Regex.Match(match.Value, @"text=['""]([^'""]+)").Groups[1].Value;

                    include = !usability.TryGetValue(text, out bool usable) || usable;
                }

                if (include)
                    markup += match.Value + '\n';
            }

#if NEW_VERSION
            BSMLParser.Instance.Parse(markup, Container, _settingsHandler);
#else
            BSMLParser.instance.Parse(markup, Container, _settingsHandler);
#endif
        }

        public void ChangeMenuTab(bool removeTab = true)
        {
#if NEW_VERSION
            if (removeTab)
            {
                GameplaySetup.Instance.RemoveTab("BL PP Counter");
            }

            if (_config.SimpleUI)
            {
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, this);
            }
            else
            {
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, _settingsHandler);
            }
#else
            if (removeTab)
            {
                if (Container?.transform is null)
                {
                    return;
                }

                Container.transform.DetachChildren();
                _loadData = _config.SimpleUI;
                LoadElements();
                return;
            }

            if (_config.SimpleUI)
            {
                GameplaySetup.instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, this);
            }
            else
            {
                GameplaySetup.instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, _settingsHandler);
            }
#endif
        }
    }
}
