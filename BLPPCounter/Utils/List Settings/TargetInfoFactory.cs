using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils.Misc_Classes;
using Zenject;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class TargetInfoFactory
    {
        private readonly ITargeter _targeter;
        private readonly LazyInject<SettingsHandler> _settingsHandler;

        public TargetInfoFactory(ITargeter targeter, LazyInject<SettingsHandler> settingsHandler)
        {
            _targeter = targeter;
            _settingsHandler = settingsHandler;
        }

        public TargetInfo Create(string displayName, string id, params TargetRank[] ranks)
        {
            return new TargetInfo(_targeter, _settingsHandler.Value, displayName, id, ranks);
        }
    }
}
