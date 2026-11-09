using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using UnityEngine;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public sealed class MenuSettingsHandler : BSMLResourceViewController
    {
        private PluginConfig _config = null!;
        private IFormatRegistry _formatRegistry = null!;
        private SimpleSettingsHandler _simpleSettingsHandler = null!;
        private SimpleMenuSettingsHandler _simpleMenuSettingsHandler = null!;
        private FormatEditorHandler _formatEditorHandler = null!;
        private ColorSettingsHandler _colorSettingsHandler = null!;
        private CustomAliasHandler _customAliasHandler = null!;

        [UIObject(nameof(SimpleContainer))]
        private readonly GameObject SimpleContainer = null!;

        [UIObject(nameof(FormatContainer))]
        private readonly GameObject FormatContainer = null!;

        [UIObject(nameof(ColorContainer))]
        private readonly GameObject ColorContainer = null!;

        [UIObject(nameof(AliasContainer))]
        private readonly GameObject AliasContainer = null!;

        [UIParams]
        private readonly BSMLParserParams ParserParams = null!;

        public override string ResourceName => "BLPPCounter.Settings.BSML.MainMenuSettings.bsml";

        internal IReadOnlyDictionary<(string FormatName, string CounterName), FormatRelation> AllFormatInfo =>
            _formatRegistry.Relations;

        [Inject]
        private void Construct(
            PluginConfig config,
            IFormatRegistry formatRegistry,
            SimpleSettingsHandler simpleSettingsHandler,
            SimpleMenuSettingsHandler simpleMenuSettingsHandler,
            FormatEditorHandler formatEditorHandler,
            ColorSettingsHandler colorSettingsHandler,
            CustomAliasHandler customAliasHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));

            _simpleSettingsHandler =
                simpleSettingsHandler ?? throw new ArgumentNullException(nameof(simpleSettingsHandler));

            _simpleMenuSettingsHandler =
                simpleMenuSettingsHandler ?? throw new ArgumentNullException(nameof(simpleMenuSettingsHandler));

            _formatEditorHandler = formatEditorHandler ?? throw new ArgumentNullException(nameof(formatEditorHandler));

            _colorSettingsHandler =
                colorSettingsHandler ?? throw new ArgumentNullException(nameof(colorSettingsHandler));

            _customAliasHandler = customAliasHandler ?? throw new ArgumentNullException(nameof(customAliasHandler));
        }

        [UIValue(nameof(SimpleUI))]
        public bool SimpleUI
        {
            get => _config.SimpleUI;
            set
            {
                _config.SimpleUI = value;
                _simpleSettingsHandler.ChangeMenuTab();
            }
        }

        [UIValue(nameof(UpdatePreview))]
        public bool UpdatePreview
        {
            get => _config.UpdatePreview;
            set => _config.UpdatePreview = value;
        }

        [UIValue(nameof(AutoUpdateRefs))]
        public bool AutoUpdateRefs
        {
            get => _config.AutoUpdateRefs;
            set => _config.AutoUpdateRefs = value;
        }

        [UIAction("#post-parse")]
        private void ActivateOthers()
        {
            HelpfulMisc.AddToComponent(_simpleMenuSettingsHandler, SimpleContainer);

            HelpfulMisc.AddToComponent(_formatEditorHandler, FormatContainer);

            HelpfulMisc.AddToComponent(_colorSettingsHandler, ColorContainer);

            HelpfulMisc.AddToComponent(_customAliasHandler, AliasContainer);
        }

        [UIAction(nameof(LoadSimpleMenu))]
        private void LoadSimpleMenu()
        {
            _simpleMenuSettingsHandler.LoadMenu();
        }

        [UIAction(nameof(LoadFormatMenu))]
        private void LoadFormatMenu()
        {
            _formatEditorHandler.LoadMenu();
        }

        [UIAction(nameof(LoadColorMenu))]
        private void LoadColorMenu()
        {
            _colorSettingsHandler.InitColorList();
        }

        public void GoBack()
        {
            ParserParams.EmitEvent("back");
        }
    }
}
