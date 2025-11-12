using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
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
        private float[] ppVals;
        #region Init
        public ProgressCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            ppVals = new float[calc.DisplayRatingCount * 2];
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
            float[] temp;
            temp = calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            if (displayFc)
            {
                temp = calc.GetPpWithSummedPp(fcPercent, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + temp.Length] = temp[i];
            }
            float mult = notes / (float)totalNotes;
            mult = Math.Min(1, mult);
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i] * mult, PluginConfig.Instance.DecimalPrecision);
            TheCounter.UpdateText(displayFc, display, ppVals, mistakes);
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
