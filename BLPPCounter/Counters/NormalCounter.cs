using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Misc_Classes;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public class NormalCounter(TMP_Text display, MapSelection map, CancellationToken ct) : MyCounters(display, map, ct)
    {
        public static string DisplayName => "Normal";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 0;
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public override string Name => DisplayName;

        #region Init
        public override void SetupData(MapSelection map, CancellationToken ct) 
        {
            ppHandler = new PPHandler(ratings, calc, PluginConfig.Instance.DecimalPrecision, 1)
            {
                UpdateFCEnabled = PluginConfig.Instance.PPFC
            };
            ppHandler.UpdateFC += (acc, extraVals, extraCalls) => extraVals[1].SetValues(calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings));
        }
        public override void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            ppHandler.Update(acc, mistakes, fcPercent);
            TheCounter.UpdateText(ppHandler.DisplayFC, display, ppHandler, mistakes);
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
