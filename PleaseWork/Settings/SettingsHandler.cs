using BeatSaberMarkupLanguage.Attributes;

namespace PleaseWork.Settings
{
    class SettingsHandler
    {
        [UIValue("splitVals")]
        public bool SplitPPVals
        {
            get => PluginConfig.Instance.SplitPPVals;
            set
            {
                PluginConfig.Instance.SplitPPVals = value;
            }
        }
        [UIValue("PPP")]
        public bool ProgressPP
        {
            get => PluginConfig.Instance.ProgressPP;
            set
            {
                PluginConfig.Instance.ProgressPP = value;
            }
        }
        [UIValue("decimalPrecision")]
        public int DecimalPrecision
        {
            get => PluginConfig.Instance.DecimalPrecision;
            set
            {
                PluginConfig.Instance.DecimalPrecision = value;
            }
        }
        [UIValue("fontSize")]
        public double FontSize
        {
            get => PluginConfig.Instance.FontSize;
            set
            {
                PluginConfig.Instance.FontSize = value;
            }
        }
        /*[UIValue("relative")]
        public bool Relative
        {
            get => PluginConfig.Instance.Relative;
            set
            {
                PluginConfig.Instance.Relative = value;
            }
        }*/
        [UIValue("showLbl")]
        public bool ShowLbl
        {
            get => PluginConfig.Instance.ShowLbl;
            set
            {
                PluginConfig.Instance.ShowLbl = value;
            }
        }
        [UIValue("PPFC")]
        public bool PPFC
        {
            get => PluginConfig.Instance.PPFC;
            set
            {
                PluginConfig.Instance.PPFC = value;
            }
        }
    }
}