using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace PleaseWork.Settings
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }
        public virtual string ClanTextFormat { get; set; } = "&:c&s&x&: &:p($)&: &l";//&c = color, &s = sign, &x = modified number, &p = normal pp, &l = label
        public virtual bool SplitPPVals { get; set; } = false;
        public virtual string PPType { get; set; } = "Normal";
        public virtual int DecimalPrecision { get; set; } = 2;
        public virtual double FontSize { get; set; } = 3;
        public virtual bool ClanWithNormal { get => PPType.Equals("Clan w/ normal"); }
        public virtual bool RelativeWithNormal { get => PPType.Equals("Relative w/ normal"); }
        public virtual bool ShowLbl { get; set; } = true;
        public virtual bool PPFC { get; set; } = false;
        public virtual string Target { get; set; } = "None";
        public virtual bool ShowEnemy { get; set; } = true;
        public virtual bool LocalReplay { get; set; } = false;
        public virtual string CaptureType { get; set; } = "Percentage";
        public virtual int MapCache { get; set; } = 10;
        public virtual double ClanPercentCeil { get; set; } = 99;
        public virtual bool CeilEnabled { get; set; } = true;
        public virtual string ChosenPlaylist { get; set; } = "";
    }
}
