using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System.Collections.Generic;
using UnityEngine;
namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class MenuSettingsHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0044, IDE0051, CS0649
        #region Variables
        private static PluginConfig PC => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; } = new MenuSettingsHandler();
        public override string ResourceName => "BLPPCounter.Settings.BSML.MainMenuSettings.bsml";

        [UIObject(nameof(SimpleContainer))] private GameObject SimpleContainer;
        [UIObject(nameof(FormatContainer))] private GameObject FormatContainer;
        [UIObject(nameof(ColorContainer))] private GameObject ColorContainer;
        [UIObject(nameof(AliasContainer))] private GameObject AliasContainer;
        [UIParams] private BSMLParserParams ParserParams;
        #endregion
        #region Init
        [UIAction("#post-parse")]
        private void ActivateOthers()
        {
            HelpfulMisc.AddToComponent(SimpleMenuSettingsHandler.Instance, SimpleContainer);
            HelpfulMisc.AddToComponent(FormatEditorHandler.Instance, FormatContainer);
            HelpfulMisc.AddToComponent(ColorSettingsHandler.Instance, ColorContainer);
            HelpfulMisc.AddToComponent(CustomAliasHandler.Instance, AliasContainer);
        }
        static MenuSettingsHandler()
        {
            AllFormatInfo = new Dictionary<(string, string), FormatRelation>()
            {
                { TheCounter.DefaultFormatRelation.GetKey, TheCounter.DefaultFormatRelation },
                { TheCounter.TargetFormatRelation.GetKey, TheCounter.TargetFormatRelation },
                { TheCounter.PercentNeededFormatRelation.GetKey, TheCounter.PercentNeededFormatRelation },
                { ClanCounter.ClanFormatRelation.GetKey, ClanCounter.ClanFormatRelation },
                { ClanCounter.WeightedFormatRelation.GetKey, ClanCounter.WeightedFormatRelation },
                { ClanCounter.MessageFormatRelation.GetKey, ClanCounter.MessageFormatRelation },
                { RelativeCounter.DefaultFormatRelation.GetKey, RelativeCounter.DefaultFormatRelation },
                { RankCounter.MainRelation.GetKey, RankCounter.MainRelation }
            };
            CustomAlias.ApplyAliases(PC.TokenSettings.TokenAliases, AllFormatInfo);
        }
        #endregion
        #region UI Variables
        [UIValue(nameof(SimpleUI))]
        public bool SimpleUI
        {
            get => PC.SimpleUI;
            set
            {
                PC.SimpleUI = value;
                SimpleSettingsHandler.Instance.ChangeMenuTab();
            }
        }
        [UIValue(nameof(UpdatePreview))]
        public bool UpdatePreview
        {
            get => PC.UpdatePreview;
            set => PC.UpdatePreview = value;
        }
        [UIValue(nameof(AutoUpdateRefs))]
        public bool AutoUpdateRefs
        {
            get => PC.AutoUpdateRefs;
            set => PC.AutoUpdateRefs = value;
        }
        #endregion
        #region Misc Variables
        internal static readonly Dictionary<(string, string), FormatRelation> AllFormatInfo;
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
        #region Misc Functions
        #endregion


    }
}
