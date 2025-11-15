using CountersPlus.ConfigModels;

namespace BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers
{
    public class CounterSettingsHandler : ConfigModel
    {
        public static CounterSettingsHandler Instance { get; private set; } = new CounterSettingsHandler();
    }
}
