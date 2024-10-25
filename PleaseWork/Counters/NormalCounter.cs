using BeatLeader.Models;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace PleaseWork.Counters
{
    internal class NormalCounter: IMyCounters
    {
        public static string DisplayName => "Normal";
        public static int OrderNumber => 0;
        public string Name => DisplayName;
        public bool Usable => TheCounter.FormatUsable;
        private TMP_Text display;
        private float accRating, passRating, techRating;
        private int precision;

        #region Init
        public NormalCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public NormalCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(map); }

        public void ReinitCounter(TMP_Text display) { this.display = display; }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating) 
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = PluginConfig.Instance.DecimalPrecision;
        }

        public void ReinitCounter(TMP_Text display, MapSelection map) { this.display = display; passRating = map.PassRating; accRating = map.AccRating; techRating = map.TechRating; }
        public void SetupData(MapSelection map) { }
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
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            TheCounter.UpdateText(displayFc, display, ppVals, mistakes);
        }
        #endregion
    }
}
