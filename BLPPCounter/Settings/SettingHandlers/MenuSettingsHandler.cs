using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace BLPPCounter.Settings.SettingHandlers
{
    public class MenuSettingsHandler
    {
        #region Variables
        #region Static
        private static PluginConfig pc => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; private set; } = new MenuSettingsHandler();
        #endregion
        #region Settings
        private int changes = 0;
        private readonly Action<int, bool> AddChange;
        #endregion
        #endregion
        #region Init
        public MenuSettingsHandler()
        {
            SettingsToSave = new bool[pc.SimpleMenuConfigLength];
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, pc.SimpleMenuConfig);
            AddChange = (id, newVal) => { if (SettingsToSave[id] == newVal) changes &= ~(1 << id); else changes |= 1 << id; saveButton.interactable = changes > 0; };
        }
        #endregion
        #region Main Settings
        [UIValue("SimpleUI")]
        public bool SimpleUI
        {
            get => pc.SimpleUI;
            set  {
                pc.SimpleUI = value;
                //Plugin.Instance.ChangeMenuTab();
            }
        }
        #region Simple Settings Editor
        public bool[] SettingsToSave { get; private set; }
        [UIComponent("UICustomizer")]
        private CustomCellListTableData ccltd;
        [UIComponent("SaveButton")]
        private Button saveButton;
        [UIValue("UISettings")]
        public List<object> UISettings { get; } = new List<object>();
        private bool loaded = false;
#pragma warning disable IDE0051
        [UIAction("SaveChanges")]
        private void SaveChanges()
        {
            pc.SimpleMenuConfig ^= changes;
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, pc.SimpleMenuConfig);
            changes = 0;
            saveButton.interactable = false;
        }
        [UIAction("LoadMenu")]
        public void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            UISettings.AddRange(ConvertMenu().Cast<object>());
            int count = 0;
            foreach (object item in UISettings) if (item is SettingToggleInfo sti) sti.Usable = SettingsToSave[count++];
            changes = 0;
            //Plugin.Log.Info("UISettings: \n" + string.Join("\n", UISettings));
            ccltd.TableView.ReloadData();
            //Plugin.Log.Info("Simple Menu Settings has been loaded!");
        }
#pragma warning restore IDE0051
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['\"])(.*?)\\2[^>]*?(?:(?<=hover-hint) *= *(['\"])(.*?)\\4[^>]*)?\\/>$";
            List<SettingToggleInfo> outp = new List<SettingToggleInfo>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (pc.SimpleMenuConfigLength != mc.Count)
            {
                SettingsToSave = new bool[mc.Count];
                for (int i = 0; i < SettingsToSave.Length; i++) SettingsToSave[i] = true;
                pc.SimpleMenuConfigLength = mc.Count;
                pc.SimpleMenuConfig = HelpfulMisc.ConvertBoolsToInt32(SettingsToSave);
            }
            int count = 0;
            foreach (Match m in mc) 
                outp.Add(new SettingToggleInfo(m.Groups[3].Value, m.Groups.Count >= 6 ? m.Groups[5].Value : "", m.Groups[1].Value.Replace('-', ' '), count++, AddChange));
            return outp;
        }
        #endregion
        #endregion
    }
}
