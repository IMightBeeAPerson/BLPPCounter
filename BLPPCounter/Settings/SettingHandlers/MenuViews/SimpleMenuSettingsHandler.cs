using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class SimpleMenuSettingsHandler : BSMLResourceViewController
    {
#pragma warning disable IDE0044, IDE0051
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.SimpleSettings.bsml";
        private static PluginConfig PC => PluginConfig.Instance;
        public static SimpleMenuSettingsHandler Instance { get; private set; } = new SimpleMenuSettingsHandler();
        private long changes = 0;
        private readonly Action<int, bool> AddChange;
        public SimpleMenuSettingsHandler()
        {
            SettingsToSave = new bool[PC.SimpleMenuConfigLength];
            HelpfulMisc.ConvertInt64ToBools(SettingsToSave, PC.SimpleMenuConfig);
            AddChange = (id, newVal) => 
            { 
                if (SettingsToSave[id] == newVal) changes &= ~(1L << id);
                else changes |= 1L << id;
#if !NEW_VERSION
                if (!(saveButton is null)) // 1.34.2 and below
#endif
                saveButton.interactable = changes > 0;
            };
        }

        public bool[] SettingsToSave { get; private set; }
        [UIComponent("UICustomizer")]
        private CustomCellListTableData UICustomizer;
        [UIComponent("SaveButton")]
        private UnityEngine.UI.Button saveButton;
        [UIValue("UISettings")]
        public List<object> UISettings { get; } = new List<object>();
        private bool loaded = false;
        [UIAction("#back")] private void GoBack() => MenuSettingsHandler.Instance.GoBack();
        [UIAction("SaveChanges")]
        private void SaveChanges()
        {
            PC.SimpleMenuConfig ^= changes;
            HelpfulMisc.ConvertInt64ToBools(SettingsToSave, PC.SimpleMenuConfig);
            changes = 0;
            saveButton.interactable = false;
            SimpleSettingsHandler.Instance.ReloadTab();
        }
        public void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            UISettings.AddRange(ConvertMenu().Cast<object>());
            for (int i = 0; i < UISettings.Count; i++) if (UISettings[i] is SettingToggleInfo sti) sti.Usable = SettingsToSave[i];
            changes = 0;
#if NEW_VERSION
            UICustomizer?.TableView.ReloadData(); // 1.37.0 and above
#else
            UICustomizer?.tableView.ReloadData(); // 1.34.2 and below
#endif
        }
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = @"<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['""])(.*?)\2[^>]*?(?:(?<=hover-hint) *= *(['""])(.*?)\4[^>]*)?\/>(?=[^<]*?$)";
            List<SettingToggleInfo> outp = new List<SettingToggleInfo>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (PC.SimpleMenuConfigLength != mc.Count)
            {
                SettingsToSave = new bool[mc.Count];
                for (int i = 0; i < SettingsToSave.Length; i++) SettingsToSave[i] = true;
                PC.SimpleMenuConfigLength = mc.Count;
                PC.SimpleMenuConfig = HelpfulMisc.ConvertBoolsToInt32(SettingsToSave);
            }
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
