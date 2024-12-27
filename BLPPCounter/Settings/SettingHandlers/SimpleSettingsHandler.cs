using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.GameplaySetup;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using static BLPPCounter.Utils.SimpleMenuInfo;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class SimpleSettingsHandler
    {
#pragma warning disable CS0649
        #region Static Variables
        public static SimpleSettingsHandler Instance { get; private set; } = new SimpleSettingsHandler();
        #endregion
        #region UI Variables
#pragma warning disable IDE0051
        [UIComponent("UIList")]
        private CustomCellListTableData ccltd;
        [UIValue(nameof(UIElements))]
        public List<object> UIElements { get; } = new List<object>();
        private bool loaded = false;
        [UIValue(nameof(HasNotLoaded))]
        private bool HasNotLoaded => !loaded;
        #endregion
        #region Init Functions
        [UIAction("#post-parse")]
        private void LoadElements()
        {
            if (loaded) return;
            loaded = true;
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<([^ ]+-setting|text|button)[^>]*\\/>$";
            List<object> outp = new List<object>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            bool loadData = PluginConfig.Instance.SimpleMenuConfigLength == mc.Count;
            if (loadData) SimpleMenuSettingsHandler.Instance.LoadMenu();
            foreach (Match match in mc)
            {
                string type = match.Groups[1].Value;
                Dictionary<string, string> values = new Dictionary<string, string>();
                MatchCollection vals = Regex.Matches(match.Value, "[^ ]+=(['\"]).*?\\1");
                foreach (Match match2 in vals)
                {
                    string[] splitVals = match2.Value.Replace("\"", "").Replace("'", "").Split('=');
                    values.Add(splitVals[0], splitVals[1]);
                }
                if (loadData && !SimpleMenuSettingsHandler.Instance.UISettings.Any(a => a is SettingToggleInfo sti && sti.Usable && sti.Text.Equals(values["text"])))
                    continue;
                SimpleMenuInfo toAdd = null;
                if (UsableSetting(type))
                    if (values.TryGetValue("value", out string value)) toAdd = InitUsingSettingsType(type, value, SettingsHandler.Instance);
                    else toAdd = new SimpleMenuInfo(type, SettingsHandler.Instance);
                if (toAdd == null) continue;
                foreach (string name in Enum.GetNames(typeof(UsableAttributes)))
                {
                    if (values.TryGetValue(name.Replace('_', '-'), out string val))
                        toAdd.SetAttribute(Enum.Parse<UsableAttributes>(name), val);
                }
                outp.Add(toAdd);
            }
            UIElements.AddRange(outp.Cast<object>());
            ccltd.TableView.ReloadData();
            //Plugin.Log.Info("Simple Tab Settings Loaded!");
            //Plugin.Log.Info(string.Join("\n", UIElements));
        }
        public void ReloadTab() { UIElements.Clear(); loaded = false; LoadElements(); }
        public void ChangeMenuTab(bool removeTab = true)
        {
            if (removeTab) GameplaySetup.Instance.RemoveTab("BL PP Counter");
            if (PluginConfig.Instance.SimpleUI)
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, this);
            else
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, SettingsHandler.Instance);
        }
#pragma warning restore IDE0051
        #endregion
    }
}
