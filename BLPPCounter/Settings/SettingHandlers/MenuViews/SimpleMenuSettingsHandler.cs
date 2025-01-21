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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class SimpleMenuSettingsHandler : BSMLResourceViewController
    {
#pragma warning disable IDE0044, IDE0051
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.SimpleSettings.bsml";
        private static PluginConfig PC => PluginConfig.Instance;
        public static SimpleMenuSettingsHandler Instance { get; private set; } = new SimpleMenuSettingsHandler();
        private int changes = 0;
        private readonly Action<int, bool> AddChange;
        public SimpleMenuSettingsHandler()
        {
            SettingsToSave = new bool[PC.SimpleMenuConfigLength];
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, PC.SimpleMenuConfig);
            AddChange = (id, newVal) => { if (SettingsToSave[id] == newVal) changes &= ~(1 << id); else changes |= 1 << id; saveButton.interactable = changes > 0; };
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
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, PC.SimpleMenuConfig);
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
            UICustomizer.TableView.ReloadData();
        }
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['\"])(.*?)\\2[^>]*?(?:(?<=hover-hint) *= *(['\"])(.*?)\\4[^>]*)?\\/>$";
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
            return outp;
        }

        
    }
}
