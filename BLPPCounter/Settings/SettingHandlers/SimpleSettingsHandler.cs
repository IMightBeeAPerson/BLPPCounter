using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Parser;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class SimpleSettingsHandler
    {
#pragma warning disable CS0649, IDE0051, IDE0044
        #region Static Variables
        public static SimpleSettingsHandler Instance { get; private set; } = new SimpleSettingsHandler();
        private static readonly HashSet<string> NonSettingTags = new HashSet<string>(2) { "settings-container", "vertical", "horizontal", "modal", "custom-list" };
        #endregion
        #region UI & Normal Variables
#if !NEW_VERSION
        private bool loadData = true;
#endif
        [UIObject(nameof(Container))] private GameObject Container;
#endregion
        #region Init Functions
        [UIAction("#post-parse")]
        private void LoadElements()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = @"(?<=\s)<\/?([A-z\-]+)[^>]*>(?=[^<]*?$)(?!\z)";
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
#if NEW_VERSION
            bool loadData = PluginConfig.Instance.SimpleMenuConfig.Length == mc.Count(m => !NonSettingTags.Contains(m.Groups[1].Value) && !m.Value.Contains('~')); //1.37.0 and above
            //Plugin.Log.Info($"Real Count: {PluginConfig.Instance.SimpleMenuConfig.Length}, counted count: {mc.Count(m => !NonSettingTags.Contains(m.Groups[1].Value) && !m.Value.Contains('~'))}");
#else
            int count = 0;
            foreach (Match m in mc)
                if (!NonSettingTags.Contains(m.Groups[1].Value) && !m.Value.Contains('~')) count++;
            //Plugin.Log.Info($"Real Count: {PluginConfig.Instance.SimpleMenuConfig.Length}, counted count: {count}");
            loadData &= PluginConfig.Instance.SimpleMenuConfig.Length == count; //1.34.2 and below
#endif
            if (loadData) SimpleMenuSettingsHandler.Instance.LoadMenu();
            string huh = "";
            Dictionary<string, bool> usable = new Dictionary<string, bool>();
            foreach (SettingToggleInfo sti in SimpleMenuSettingsHandler.Instance.UISettings.Cast<SettingToggleInfo>())
                usable[sti.Text] = sti.Usable;
            foreach (Match m in mc)
                if (!loadData || NonSettingTags.Contains(m.Groups[1].Value) || m.Value.Contains('~') || (loadData && usable[Regex.Match(m.Value, "text=['\"]([^'\"]+)").Groups[1].Value]))
                    huh += m.Value + '\n';
#if NEW_VERSION
            BSMLParser.Instance.Parse(huh, Container, SettingsHandler.Instance);
#else
            BSMLParser.instance.Parse(huh, Container, SettingsHandler.Instance);
#endif
            //Plugin.Log.Info("Simple Settings has been loaded.");
        }
        public void ChangeMenuTab(bool removeTab = true)
        {
#if NEW_VERSION
            if (removeTab) GameplaySetup.Instance.RemoveTab("BL PP Counter");
            if (PluginConfig.Instance.SimpleUI)
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, this);
            else
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, SettingsHandler.Instance);  // 1.37.0 and above
#else
            if (removeTab) 
            { //Remove tab doesn't work very well in 1.29, so instead clear tab and redo the xml.
                if (Container is null || Container.transform is null) return;
                Container.transform.DetachChildren();
                loadData = PluginConfig.Instance.SimpleUI;
                LoadElements();
                return;
            }
            if (PluginConfig.Instance.SimpleUI)
                GameplaySetup.instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, this);
            else
                GameplaySetup.instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, SettingsHandler.Instance);  // 1.34.2 and below
#endif
        }
        #endregion
    }
}
