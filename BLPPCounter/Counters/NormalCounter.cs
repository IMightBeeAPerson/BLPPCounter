using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using TMPro;

namespace BLPPCounter.Counters
{
    public class NormalCounter: IMyCounters
    {
        public static string DisplayName => "Normal";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 0;
        public static bool SSUsable => true;
        public string Name => DisplayName;
        private TMP_Text display;
        private float accRating, passRating, techRating, starRating;
        private int precision;

        #region Init
        public NormalCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.starRating = starRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public NormalCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }

        public void ReinitCounter(TMP_Text display) { this.display = display; }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating) 
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = PluginConfig.Instance.DecimalPrecision;
        }

        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            this.display = display;
            passRating = map.PassRating;
            accRating = map.AccRating;
            techRating = map.TechRating;
            starRating = map.StarRating;
        }
        public void SetupData(MapSelection map) { }
        public void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0, ss = PluginConfig.Instance.UsingSS;
            float[] ppVals = new float[ss ? 2 : 8];
            if (ss)
            {
                ppVals[0] = SSCalc.GetPP(acc, starRating);
                if (displayFc) ppVals[1] = SSCalc.GetPP(fcPercent, starRating);
            }
            else
            {
                (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
                ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
                if (displayFc)
                {
                    (ppVals[4], ppVals[5], ppVals[6]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                    ppVals[7] = BLCalc.Inflate(ppVals[4] + ppVals[5] + ppVals[6]);
                }
                for (int i = 0; i < ppVals.Length; i++)
                    ppVals[i] = (float)Math.Round(ppVals[i], precision);
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            TheCounter.UpdateText(displayFc, display, ppVals, mistakes);
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent) { }
        #endregion
    }
}
