using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using PleaseWork.Settings.FormatSettings;
using PleaseWork.Utils;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace PleaseWork.Settings
{
    internal class PluginConfig
    {
        #region Non-Settings Variables
        public static PluginConfig Instance { get; set; }
        public virtual TokenFormatSettings TokenSettings { get; set; } = new TokenFormatSettings();
        public virtual MessageSettings MessageSettings { get; set; } = new MessageSettings();
        public virtual TextFormatSettings FormatSettings { get; set; } = new TextFormatSettings();
        #endregion
        #region General Settings
        public virtual int DecimalPrecision { get; set; } = 2;
        public virtual double FontSize { get; set; } = 3;
        public virtual bool ShowLbl { get; set; } = true;
        public virtual bool PPFC { get; set; } = false;
        public virtual bool SplitPPVals { get; set; } = false;
        public virtual bool ExtraInfo { get; set; } = false;
        public virtual bool UseGrad { get; set; } = true;
        public virtual int GradVal { get; set; } = 100;
        public virtual string PPType { get; set; } = "Normal";
        #endregion
        #region Clan Counter Settings
        public virtual bool ShowClanMessage { get; set; } = false;
        public virtual int MapCache { get; set; } = 10;
        public virtual double ClanPercentCeil { get; set; } = 99;
        public virtual bool CeilEnabled { get; set; } = true;
        #endregion
        #region Relative Counter Settings
        public virtual bool ShowRank { get; set; } = true;
        public virtual string RelativeDefault { get; set; } = "Normal";
        #endregion
        #region Rank Counter Settings
        public virtual int MinRank { get; set; } = 100;
        public virtual int MaxRank { get; set; } = 0;
        public virtual bool AdaptableRank { get; set; } = true;
        #endregion
        #region Target Settings
        public virtual bool ShowEnemy { get; set; } = true;
        public virtual string Target { get; set; } = Targeter.NO_TARGET;

        //The below list is not in order so that in the config file there is nothing below this that gets obstructed.
        [UseConverter(typeof(ListConverter<CustomTarget>))]
        public virtual List<CustomTarget> CustomTargets { get; set; } = new List<CustomTarget>();
        #endregion
        #region Unused Code
        //public virtual bool LocalReplay { get; set; } = false;
        //public virtual string ChosenPlaylist { get; set; } = "";
        #endregion
        #region Menu Settings - Main
        public virtual bool SimpleUI { get; set; } = true;
        #endregion
    }
}
