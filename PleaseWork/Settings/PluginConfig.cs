using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace PleaseWork.Settings
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }
        public virtual bool SplitPPVals { get; set; } = false;
        public virtual bool ProgressPP { get; set; } = false;
        public virtual int DecimalPrecision { get; set; } = 2;
        public virtual double FontSize { get; set; } = 2.5;
        public virtual bool Relative { get; set; } = false;
        public virtual bool ShowLbl { get; set; } = true;
        public virtual bool PPFC { get; set; } = false;
    }
}
