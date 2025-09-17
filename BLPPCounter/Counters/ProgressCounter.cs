using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Helpfuls;
using System;
using TMPro;
using BLPPCounter.Utils.API_Handlers;

namespace BLPPCounter.Counters
{
    public class ProgressCounter: IMyCounters
    {
        public static string DisplayName => "Progressive";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 1;
        public string Name => DisplayName;
        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        private TMP_Text display;
        private float accRating, passRating, techRating, starRating;
        private int precision, totalNotes;
        private Calculator calc;
        #region Init
        public ProgressCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.starRating = starRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            calc = Calculator.GetSelectedCalc();
        }
        public ProgressCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }
        #endregion
        #region Overrides
        public void ReinitCounter(TMP_Text display) { this.display = display; calc = Calculator.GetSelectedCalc(); }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = PluginConfig.Instance.DecimalPrecision;
            calc = Calculator.GetSelectedCalc();
        }

        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            this.display = display; 
            totalNotes = HelpfulMath.NotesForMaxScore(APIHandler.GetSelectedAPI().GetMaxScore(map.MapData.Item2));
            passRating = map.PassRating;
            accRating = map.AccRating;
            techRating = map.TechRating;
            calc = Calculator.GetSelectedCalc();
        }
        public void SetupData(MapSelection map)
        {
            totalNotes = HelpfulMath.NotesForMaxScore(APIHandler.GetSelectedAPI().GetMaxScore(map.MapData.Item2));
            calc = Calculator.GetSelectedCalc();
        }
        public void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            float[] ratings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            float[] ppVals = new float[(ratings.Length + 1) * 2], temp;
            temp = calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings);
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            if (displayFc)
            {
                temp = calc.GetPpWithSummedPp(fcPercent, PluginConfig.Instance.DecimalPrecision, ratings);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + temp.Length] = temp[i];
            }
            float mult = notes / (float)totalNotes;
            mult = Math.Min(1, mult);
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i] * mult, precision);
            TheCounter.UpdateText(displayFc, display, ppVals, mistakes);
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
