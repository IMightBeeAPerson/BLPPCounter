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
    public class ProgressCounter: IMyCounters
    {
        public string Name { get => "Progressive"; }

        private TMP_Text display;
        private float accRating, passRating, techRating;
        private int precision, totalNotes;
        #region Init
        public ProgressCounter(TMP_Text display, float accRating, float passRating, float techRating, int totalNotes)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            this.totalNotes = totalNotes;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
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

        public void ReinitCounter(TMP_Text display, string hash, string diff, string mode, string mapData) { this.display = display; }
        public void SetupData(string id, string hash, string diff, string mode, string mapData)
        {
            
        }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int badNotes, int fcScore)
        {
            bool displayFc = PluginConfig.Instance.PPFC && badNotes > 0;
            float[] ppVals = new float[displayFc ? 8 : 4];
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            if (displayFc)
            {
                float fcAcc = fcScore / (float)HelpfulMath.MaxScoreForNotes(notes);
                if (float.IsNaN(fcAcc)) fcAcc = 1;
                (ppVals[4], ppVals[5], ppVals[6]) = BLCalc.GetPp(fcAcc, accRating, passRating, techRating);
                ppVals[7] = BLCalc.Inflate(ppVals[4] + ppVals[5] + ppVals[6]);
            }
            float mult = notes / (float)totalNotes;
            mult = Math.Min(1, mult);
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i] * mult, precision);
            TheCounter.UpdateText(displayFc, display, ppVals);
        }
        #endregion
    }
}
