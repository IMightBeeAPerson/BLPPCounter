using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace PleaseWork.Settings
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }
        public virtual bool SplitPPVals { get; set; } = false;
        public virtual string PPType { get; set; } = "Normal";
        public virtual bool ProgressPP { get => PPType.Equals("Progressive"); }
        public virtual int DecimalPrecision { get; set; } = 2;
        public virtual double FontSize { get; set; } = 3;
        public virtual bool Relative { get => PPType.Equals("Relative"); }
        public virtual bool RelativeWithNormal { get => PPType.Equals("Relative w/ normal"); }
        public virtual bool ShowLbl { get; set; } = true;
        public virtual bool PPFC { get; set; } = false;
    }
}
