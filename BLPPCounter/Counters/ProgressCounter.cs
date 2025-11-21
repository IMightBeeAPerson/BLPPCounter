using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Misc_Classes;
using System;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public class ProgressCounter: MyCounters
    {
        public static string DisplayName => "Progressive";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 1;
        public override string Name => DisplayName;
        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        private int totalNotes;
        private float mult;
        #region Init
        public ProgressCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            ppHandler = new PPHandler(ratings, calc, PluginConfig.Instance.DecimalPrecision, 2, (rating, acc, in main, ref toChange) => PPContainer.MultiplyFast(ref toChange, mult))
            {
                UpdateFCEnabled = PluginConfig.Instance.PPFC
            };
            ppHandler.UpdateFC += (acc, extraVals, extraCalls) =>
            {
                extraVals[2].SetValues(calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings));
                extraCalls(0, acc, in extraVals[2], ref extraVals[3]);
            };
        }
        #endregion
        #region Overrides
        public override void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            base.ReinitCounter(display, map);
            totalNotes = HelpfulMath.NotesForMaxScore(APIHandler.GetSelectedAPI().GetMaxScore(map.MapData.diffData));
        }
        public override void SetupData(MapSelection map, CancellationToken ct)
        {
            totalNotes = HelpfulMath.NotesForMaxScore(APIHandler.GetSelectedAPI().GetMaxScore(map.MapData.diffData));
        }
        public override void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            mult = Math.Min(1, notes / (float)totalNotes);

            ppHandler.Update(acc, mistakes, fcPercent);

            TheCounter.UpdateText(displayFc, display, ppHandler, mistakes);
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
