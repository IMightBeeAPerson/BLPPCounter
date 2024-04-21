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
    }
}
