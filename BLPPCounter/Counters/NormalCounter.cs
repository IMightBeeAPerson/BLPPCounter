﻿using BLPPCounter.CalculatorStuffs;
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
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public string Name => DisplayName;
        private TMP_Text display;
        private int precision;
        private float[] ppContainer;
        private Calculator Calc;

        #region Init
        public NormalCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            Calc = Calculator.GetSelectedCalc();
            ppContainer = Calc.SelectRatings(starRating, accRating, passRating, techRating);
        }
        public NormalCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }

        public void ReinitCounter(TMP_Text display) { this.display = display; }

        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating) 
        {
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            Calc = Calculator.GetSelectedCalc();
            ppContainer = Calc.SelectRatings(starRating, accRating, passRating, techRating);
        }

        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            this.display = display;
            Calc = Calculator.GetSelectedCalc();
            ppContainer = Calc.SelectRatings(map.StarRating, map.AccRating, map.PassRating, map.TechRating);
        }
        public void SetupData(MapSelection map) { }
        public void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            float[] ppVals = new float[Calc.DisplayRatingCount * 2];
            Calc.SetPp(acc, ppVals, 0, precision, ppContainer);
            Calc.SetPp(fcPercent, ppVals, Calc.DisplayRatingCount, precision, ppContainer);
            TheCounter.UpdateText(PluginConfig.Instance.PPFC && mistakes > 0, display, ppVals, mistakes);
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent) { }
        #endregion
    }
}
