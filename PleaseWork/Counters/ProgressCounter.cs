using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using PleaseWork.Helpfuls;
using System;
using TMPro;

namespace PleaseWork.Counters
{
    public class ProgressCounter: IMyCounters
    {
        public static string DisplayName => "Progressive";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 1;
        public string Name => DisplayName;

        private TMP_Text display;
        private float accRating, passRating, techRating;
        private int precision, totalNotes;
        #region Init
        public ProgressCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public ProgressCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(map); }
        #endregion
        #region Overrides
        public void ReinitCounter(TMP_Text display) { this.display = display; }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = PluginConfig.Instance.DecimalPrecision;
        }

        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            this.display = display; 
            totalNotes = HelpfulMath.NotesForMaxScore(int.Parse(map.MapData.Item2["maxScore"].ToString()));
            passRating = map.PassRating; accRating = map.AccRating; techRating = map.TechRating;
        }
        public void SetupData(MapSelection map)
        {
            totalNotes = HelpfulMath.NotesForMaxScore(int.Parse(map.MapData.Item2["maxScore"].ToString()));
        }
        public void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            float[] ppVals = new float[8];
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            if (displayFc)
            {
                (ppVals[4], ppVals[5], ppVals[6]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                ppVals[7] = BLCalc.Inflate(ppVals[4] + ppVals[5] + ppVals[6]);
            }
            float mult = notes / (float)totalNotes;
            mult = Math.Min(1, mult);
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i] * mult, precision);
            TheCounter.UpdateText(displayFc, display, ppVals, mistakes);
        }
        #endregion
    }
}
