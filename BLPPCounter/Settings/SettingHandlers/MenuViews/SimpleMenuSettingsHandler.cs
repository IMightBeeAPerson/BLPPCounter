using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
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

        [UIComponent(nameof(UICustomizer))]
        private CustomCellListTableData UICustomizer;
        [UIValue(nameof(MenuHeight))]
        private int MenuHeight = SettingsHandler.MENU_HEIGHT;
        [UIValue(nameof(MenuAnchor))]
        private int MenuAnchor = SettingsHandler.MENU_ANCHOR;
        [UIValue(nameof(UISettings))]
        public List<object> UISettings { get; } = [];

        public SimpleMenuSettingsHandler()
        {
            AddChange = (id, newVal) => PC.SimpleMenuConfig[id] = newVal;
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
#if NEW_VERSION
            UICustomizer?.TableView.ReloadData(); // 1.37.0 and above
#else
            UICustomizer?.tableView.ReloadData(); // 1.34.2 and below
#endif
        }
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            List<SettingToggleInfo> outp = [];
            MatchCollection mc = HelpfulRegex.ConvertMenuRegex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource));
            if (PC.SimpleMenuConfig.Length != mc.Count)
                PC.SimpleMenuConfig = new BoolStorage(mc.Count, true);
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
