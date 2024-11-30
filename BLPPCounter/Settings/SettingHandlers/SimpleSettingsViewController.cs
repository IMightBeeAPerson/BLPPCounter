using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Parser;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class SimpleSettingsViewController: BSMLResourceViewController
    {
        public override string ResourceName { get; } = HelpfulPaths.SIMPLE_MENU_BSML;
        public static SimpleSettingsViewController Instance { get; set; } = new SimpleSettingsViewController();
        public bool TabLoaded { get; private set; } = false;

        public void ChangeMenuTab(bool loadContents = true)
        {
            if (loadContents) GameplaySetup.Instance.RemoveTab("BL PP Counter");
            if (PluginConfig.Instance.SimpleUI)
            {
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.SIMPLE_MENU_BSML, SettingsHandler.Instance);
                //if (loadContents) UpdateMenuTabContents();
            }
            else
                GameplaySetup.Instance.AddTab("BL PP Counter", HelpfulPaths.MENU_BSML, SettingsHandler.Instance);
            return;
        }
        /*private BSMLParserParams UpdateMenuTabContents()
        {
            TabLoaded = true;
            //Plugin.Log.Info(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), "BLPPCounter.Settings.BSML.SimpleMenuSettings.bsml"));
            string content = ConvertMenu();
            //Log.Info(content);
            Plugin.Log.Info(SettingsHandler.Instance.Body == null ? "Body is null" : "Body is not null");
            if (SettingsHandler.Instance.Body == null) return null;
            return BSMLParser.Instance.Parse(content, SettingsHandler.Instance.Body, SettingsHandler.Instance);
            //Log.Info(SettingsHandler.Instance.Container == null ? "Container is null" : "Container is not null");

        }
        private string ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<(?:[^ ]+-setting|text|button) [^>]*\\/>$";
            string outp = "<vertical>\n";
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (PluginConfig.Instance.SimpleMenuConfig.Count != mc.Count) return "<vertical/>";
            int count = 0;
            foreach (Match match in mc)
            {
                if (PluginConfig.Instance.SimpleMenuConfig[count++]) outp += "\t" + match.Value + "\n";
            }
            return outp + "</vertical>";
        }//*/
    }
}
