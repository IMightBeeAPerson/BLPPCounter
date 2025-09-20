using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Special_Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class SimpleMenuSettingsHandler : BSMLResourceViewController
    {
#pragma warning disable IDE0044, IDE0051, CS0649
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.SimpleSettings.bsml";
        private static PluginConfig PC => PluginConfig.Instance;
        public static SimpleMenuSettingsHandler Instance { get; private set; } = new SimpleMenuSettingsHandler();
        private readonly Action<int, bool> AddChange;
        private bool loaded = false;

        [UIComponent("UICustomizer")]
        private CustomCellListTableData UICustomizer;
        /*[UIComponent("SaveButton")]
        private UnityEngine.UI.Button saveButton;*/
        [UIValue("UISettings")]
        public List<object> UISettings { get; } = new List<object>();

        public SimpleMenuSettingsHandler()
        {
            AddChange = (id, newVal) => 
            {
                PC.SimpleMenuConfig[id] = newVal;
#if !NEW_VERSION
                // if (!(saveButton is null)) // 1.34.2 and below
#endif
                // saveButton.interactable = changes > 0;
            };
        }
        [UIAction("#back")]
        private void GoBack()
        {
            SimpleSettingsHandler.Instance.ChangeMenuTab();
            MenuSettingsHandler.Instance.GoBack();
        }
        public void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            UISettings.AddRange(ConvertMenu().Cast<object>());
            for (int i = 0; i < UISettings.Count; i++) 
                if (UISettings[i] is SettingToggleInfo sti) 
                    sti.Usable = PC.SimpleMenuConfig[i];
            //changes = 0;
#if NEW_VERSION
            UICustomizer?.TableView.ReloadData(); // 1.37.0 and above
#else
            UICustomizer?.tableView.ReloadData(); // 1.34.2 and below
#endif
        }
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = @"<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['""])(?!~)(.*?)\2[^>]*?(?:(?<=hover-hint) *= *(['""])(.*?)\4[^>]*)?\/>(?=[^<]*?$)";
            List<SettingToggleInfo> outp = new List<SettingToggleInfo>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (PC.SimpleMenuConfig.Length != mc.Count)
                PC.SimpleMenuConfig = new BoolStorage(mc.Count, true);
            /*{
                SettingsToSave = new bool[mc.Count];
                for (int i = 0; i < SettingsToSave.Length; i++) SettingsToSave[i] = true;
                PC.SimpleMenuConfigLength = mc.Count;
                PC.SimpleMenuConfig = HelpfulMisc.ConvertBoolsToInt64(SettingsToSave);
            }*/
            int count = 0;
            for (int i = 0; i < mc.Count; i++)
                outp.Add(new SettingToggleInfo(
                    mc[i].Groups[3].Value,
                    mc[i].Groups.Count >= 6 ? mc[i].Groups[5].Value : "",
                    mc[i].Groups[1].Value.Replace('-', ' '),
                    count++,
                    AddChange
                    ));
            //HelpfulMisc.PrintVars((nameof(outp), outp.Count));
            return outp;
        }

        
    }
}
