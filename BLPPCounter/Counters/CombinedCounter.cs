using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace BLPPCounter.Counters
{
    public class CombinedCounter : IMyCounters
    {
        public static string DisplayName => "Combined";
        public static int OrderNumber => 5;
        public static string DisplayHandler => DisplayName;
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public string Name => DisplayName;

        private TMP_Text display;
        private int precision;
        private float[] ppContainer;
        private PluginConfig PC => PluginConfig.Instance;

        #region Init
        public CombinedCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            ppContainer = Calculator.GetSelectedCalc().SelectRatings(starRating, accRating, passRating, techRating);
        }
        public CombinedCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }

        public void ReinitCounter(TMP_Text display) { this.display = display; }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating)
        {
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            ppContainer = Calculator.GetSelectedCalc().SelectRatings(starRating, accRating, passRating, techRating);
        }

        public void ReinitCounter(TMP_Text display, MapSelection map)
        {
            this.display = display;
            ppContainer = Calculator.GetSelectedCalc().SelectRatings(map.StarRating, map.AccRating, map.PassRating, map.TechRating);
        }
        public void SetupData(MapSelection map) { }
        public void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            Calculator calc = Calculator.GetSelectedCalc();
            float[] ppVals = new float[calc.DisplayRatingCount * 2];
            calc.SetPp(acc, ppVals, 0, precision, ppContainer);
            calc.SetPp(fcPercent, ppVals, calc.DisplayRatingCount, precision, ppContainer);
            TheCounter.GetUpdateText(displayFc, ppVals, mistakes);
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent) { }
        #endregion
    }
}
