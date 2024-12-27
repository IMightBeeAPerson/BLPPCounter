using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils;
using BLPPCounter.Utils.List_Settings;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BLPPCounter.Helpfuls.HelpfulFormatter;
namespace BLPPCounter.Settings.SettingHandlers
{
    public class MenuSettingsHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0044, IDE0051
        #region Variables
        private static PluginConfig pc => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; private set; } = new MenuSettingsHandler();
        public override string ResourceName => "BLPPCounter.Settings.BSML.MainMenuSettings.bsml";

        [UIObject("SimpleContainer")] private GameObject SimpleContainer;
        [UIObject("FormatContainer")] private GameObject FormatContainer;
        [UIObject("ColorContainer")] private GameObject ColorContainer;
        [UIParams] private BSMLParserParams ParserParams;
        #endregion
        #region Init
        [UIAction("#post-parse")]
        private void ActivateOthers()
        {
            HelpfulMisc.AddToComponent(SimpleMenuSettingsHandler.Instance, SimpleContainer);
            HelpfulMisc.AddToComponent(FormatEditorHandler.Instance, FormatContainer);
            HelpfulMisc.AddToComponent(ColorSettingsHandler.Instance, ColorContainer);
        }
        #endregion
        #region UI Variables
        [UIValue(nameof(SimpleUI))]
        public bool SimpleUI
        {
            get => pc.SimpleUI;
            set => pc.SimpleUI = value;
        }
        [UIValue(nameof(UpdatePreview))]
        public bool UpdatePreview
        {
            get => pc.UpdatePreview;
            set => pc.UpdatePreview = value;
        }
        #endregion
        #region UI Actions
        [UIAction(nameof(LoadSimpleMenu))]
        private void LoadSimpleMenu() => SimpleMenuSettingsHandler.Instance.LoadMenu();
        [UIAction(nameof(LoadFormatMenu))]
        private void LoadFormatMenu() => FormatEditorHandler.Instance.LoadMenu();
        [UIAction(nameof(LoadColorMenu))]
        private void LoadColorMenu() => ColorSettingsHandler.Instance.InitColorList();
        public void GoBack() => ParserParams.EmitEvent("back");
        #endregion

        
    }
}
