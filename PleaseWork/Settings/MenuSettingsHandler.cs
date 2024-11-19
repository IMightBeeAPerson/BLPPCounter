using BeatSaberMarkupLanguage.Attributes;
namespace PleaseWork.Settings
{
    public class MenuSettingsHandler
    {
        #region Variables
        private static PluginConfig pc => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; private set; } = new MenuSettingsHandler();
        #endregion
        #region Main Settings
        [UIValue("SimpleUI")]
        public bool SimpleUI
        {
            get => pc.SimpleUI;
            set  {
                pc.SimpleUI = value;
                Plugin.Instance.ChangeMenuTab();
            }
        }
        #endregion

    }
}
