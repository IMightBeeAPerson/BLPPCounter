using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Containers;
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

        private float totalNotes;
        #region Init
        public ProgressCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            ppHandler = new PPHandler(ratings, calc, PluginConfig.Instance.DecimalPrecision, 2, (rating, acc, in main, ref toChange, mult) => PPContainer.MultiplyFast(ref toChange, mult))
            {
                UpdateFCEnabled = PluginConfig.Instance.PPFC
            };
            ppHandler.UpdateFC += (acc, extraVals, extraCalls, _) =>
            {
                extraVals[2].SetValues(calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings));
                extraCalls(0, acc, in extraVals[2], ref extraVals[3], _);
            };
        }
        #endregion
        #region Overrides
        public override void ReinitCounter(MapSelection map) 
        { 
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
        public override void UpdateCounterInternal(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            ppHandler.Update(acc, mistakes, fcPercent, Math.Min(1, notes / totalNotes));
            TheCounter.UpdateText(ppHandler.DisplayFC, outpText, ppHandler, mistakes);
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
