using BeatSaberMarkupLanguage.Attributes;
using PleaseWork.Counters;
using PleaseWork.Utils;
using System.Collections.Generic;

namespace PleaseWork.Settings
{
    class SettingsHandler
    {
        [UIValue("SplitVals")]
        public bool SplitPPVals
        {
            get => PluginConfig.Instance.SplitPPVals;
            set => PluginConfig.Instance.SplitPPVals = value;
        }
        [UIValue("DecimalPrecision")]
        public int DecimalPrecision
        {
            get => PluginConfig.Instance.DecimalPrecision;
            set => PluginConfig.Instance.DecimalPrecision = value;
        }
        [UIValue("FontSize")]
        public double FontSize
        {
            get => PluginConfig.Instance.FontSize;
            set => PluginConfig.Instance.FontSize = value;
        }
        [UIValue("TypesOfPP")]
        public List<object> TypesOfPP => Plugin.BLInstalled ?
            new List<object>() { "Normal", "Progressive", "Relative", "Relative w/ normal", "Clan PP", "Clan w/ normal" } :
            new List<object>() { "Normal", "Progressive" };
        [UIValue("PPType")]
        public string PPType
        {
            get => PluginConfig.Instance.PPType;
            set => PluginConfig.Instance.PPType = value;
        }
        [UIValue("ShowLbl")]
        public bool ShowLbl
        {
            get => PluginConfig.Instance.ShowLbl;
            set => PluginConfig.Instance.ShowLbl = value;
        }
        [UIValue("PPFC")]
        public bool PPFC
        {
            get => PluginConfig.Instance.PPFC;
            set => PluginConfig.Instance.PPFC = value;
        }
        [UIValue("Target")]
        public string Target
        {
            get => PluginConfig.Instance.Target;
            set => PluginConfig.Instance.Target = value;
        }
        [UIValue("toTarget")]
        public List<object> ToTarget => Targeter.clanNames;
        [UIValue("ShowEnemy")]
        public bool ShowEnemy
        {
            get => PluginConfig.Instance.ShowEnemy;
            set => PluginConfig.Instance.ShowEnemy = value;
        }
        [UIValue("LocalReplay")]
        public bool LocalReplay
        {
            get => PluginConfig.Instance.LocalReplay;
            set => PluginConfig.Instance.LocalReplay = value;
        }
        [UIValue("MapCashe")]
        public int MapCashe
        {
            get => PluginConfig.Instance.MapCashe;
            set => PluginConfig.Instance.MapCashe = value;
        }
        [UIAction("ClearCashe")]
        void ClearCashe() => ClanCounter.ClearCashe();
        [UIValue("ClanPercentCeil")]
        public double ClanPercentCeil
        {
            get => PluginConfig.Instance.ClanPercentCeil;
            set => PluginConfig.Instance.ClanPercentCeil = value;
        }[UIValue("CeilEnabled")]
        public bool CeilEnabled
        {
            get => PluginConfig.Instance.CeilEnabled;
            set => PluginConfig.Instance.CeilEnabled = value;
        }
    }
}