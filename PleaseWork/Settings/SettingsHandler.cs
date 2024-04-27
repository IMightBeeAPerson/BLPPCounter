using BeatSaberMarkupLanguage.Attributes;
using PleaseWork.Utils;
using System.Collections.Generic;

namespace PleaseWork.Settings
{
    class SettingsHandler
    {
        [UIValue("splitVals")]
        public bool SplitPPVals
        {
            get => PluginConfig.Instance.SplitPPVals;
            set => PluginConfig.Instance.SplitPPVals = value;
        }
        [UIValue("decimalPrecision")]
        public int DecimalPrecision
        {
            get => PluginConfig.Instance.DecimalPrecision;
            set => PluginConfig.Instance.DecimalPrecision = value;
        }
        [UIValue("fontSize")]
        public double FontSize
        {
            get => PluginConfig.Instance.FontSize;
            set => PluginConfig.Instance.FontSize = value;
        }
        [UIValue("typesOfPP")]
        public List<object> TypesOfPP => Plugin.BLInstalled ? new List<object>() { "Normal", "Progressive", "Relative", "Relative w/ normal" } : new List<object>() { "Normal", "Progressive" };
        [UIValue("PPType")]
        public string PPType
        {
            get => PluginConfig.Instance.PPType;
            set => PluginConfig.Instance.PPType = value;
        }
        [UIValue("showLbl")]
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
    }
}