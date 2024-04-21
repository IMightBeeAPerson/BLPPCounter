using BeatSaberMarkupLanguage.Attributes;

namespace PleaseWork.Settings
{
    class SettingsHandler
    {
        [UIValue("SplitVals")]
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
    }
}