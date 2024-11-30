using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using HMUI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace BLPPCounter.Settings.SettingHandlers
{
    public class MenuSettingsHandler: BSMLResourceViewController
    {
        #region Variables
        private static PluginConfig pc => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; private set; } = new MenuSettingsHandler();
        public override string ResourceName => HelpfulPaths.SETTINGS_BSML;
        #endregion
        #region Init
        public MenuSettingsHandler()
        {
            SettingsToSave = new bool[pc.SimpleMenuConfigLength];
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, pc.SimpleMenuConfig);
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
        [UIValue("UISettings")]
        public List<object> UISettings { get; } = new List<object>();
        private bool loaded = false;
        public void ResetLoaded() => loaded = false;
        [UIAction("LoadMenu")]
        public void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            UISettings.AddRange(ConvertMenu().Cast<object>());
            int count = 0;
            foreach (object item in UISettings) if (item is SettingToggleInfo sti) sti.Usable = SettingsToSave[count++];
            //Plugin.Log.Info("UISettings: \n" + string.Join("\n", UISettings));
            ccltd.TableView.ReloadData();
            Plugin.Log.Info("Simple Menu Settings has been loaded!");
        }
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['\"])(.*?)\\2[^>]*?(?:(?<=hover-hint) *= *(['\"])(.*?)\\4[^>]*)?\\/>$";
            List<SettingToggleInfo> outp = new List<SettingToggleInfo>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (pc.SimpleMenuConfigLength == 0)
            {
                SettingsToSave = new bool[mc.Count];
                pc.SimpleMenuConfigLength = mc.Count;
            }
            int count = 0;
            foreach (Match m in mc) 
                outp.Add(new SettingToggleInfo(m.Groups[3].Value, m.Groups.Count >= 6 ? m.Groups[5].Value : "", m.Groups[1].Value.Replace('-', ' '), count++));
            return outp;
        }
        #endregion
        #endregion
    }
}
