using BeatSaberMarkupLanguage.Attributes;

namespace PleaseWork.Settings
{
    class SettingsHandler
    {
        [UIValue("SplitVals")]
        public bool splitPPVals
        {
            get => PluginConfig.Instance.splitPPVals;
            set
            {
                PluginConfig.Instance.splitPPVals = value;
            }
        }
    }
}