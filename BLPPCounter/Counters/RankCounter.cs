﻿using Newtonsoft.Json.Linq;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using BLPPCounter.Utils.List_Settings;
using BeatLeader.Replayer;
using BLPPCounter.Utils.API_Handlers;

namespace BLPPCounter.Counters
{
    public class RankCounter : IMyCounters
    {
        #region Static Variables
        public static int OrderNumber => 4;
        public static string DisplayName => "Rank";
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public static string DisplayHandler => DisplayName;
        private static PluginConfig PC => PluginConfig.Instance;

        private static Func<bool, bool, bool, float, float, int, float, string, string, string> displayRank;
        private static Func<Func<Dictionary<char, object>, string>> rankIniter;

        public static readonly Dictionary<string, char> MainAlias = new Dictionary<string, char>()
        {
            {"PP", 'x' },
            {"FCPP", 'y' },
            {"Rank", 'r' },
            {"PP Difference", 'd' },
            {"Label", 'l' },
            {"Rank Color", 'c' }
        };
        internal static readonly FormatRelation MainRelation = new FormatRelation("Main Format", DisplayName,
            PC.FormatSettings.RankTextFormat, str => PC.FormatSettings.RankTextFormat = str, MainAlias, 
            new Dictionary<char, string>()
            {
                {'x', "The unmodified PP number" },
                {'y', "The unmodified PP number if the map was FC'ed" },
                {'r', "The rank you would be on the leaderboard if the map ended right then" },
                {'d', "The modified PP number, shows how much pp to go up one rank on the leaderboard" },
                {'l', "The label (ex: PP, Tech PP, etc)" },
                {'c', "The color of the rank (set in settings)" }
            }, str => { var hold = GetTheFormat(str, out string errorStr); return (hold, errorStr); },
            new Dictionary<char, object>()
            {
                {(char)1, true },
                {(char)2, true },
                {(char)3, true },
                {(char)4, true },
                { 'x', 543.21f },
                { 'y', 654.32f },
                { 'r', 3 },
                { 'd', 0.1f },
                { 'l', "PP" },
                { 'c', 3 }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>()
            {
                {'r', 0 },
                {'c', 1 }
            }, new Func<object, bool, object>[]
            {
                FormatRelation.CreateFunc("#{0}", "{0}"),
                FormatRelation.CreateFunc<int>(num => HelpfulFormatter.GetWeightedRankColor(num) + '#' + num, num => HelpfulFormatter.GetWeightedRankColor(num))
            }, new Dictionary<char, IEnumerable<(string, object)>>()
            {
                { 'x', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'y', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'r', new (string, object)[4] { ("IsInteger", true), ("MinVal", 1), ("MaxVal", 100), ("IncrementVal", 1) } },
                { 'd', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 1.5f) } },
                { 'c', new (string, object)[4] { ("IsInteger", true), ("MinVal", 1), ("MaxVal", 100), ("IncrementVal", 1) } },
            }
            );
        #endregion
        #region Variables
        public string Name => DisplayName;
        private int precision;
        private float accRating, passRating, techRating, starRating;
        private TMP_Text display;
        private float[] mapPP, ratings;
        private int ratingLen;
        private Calculator calc;
        #endregion
        #region Inits
        public RankCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.starRating = starRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
            calc = Calculator.GetSelectedCalc();
            ratings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            ratingLen = ratings.Length == 1 ? 0 : ratings.Length;
        }
        public RankCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }
        public void SetupData(MapSelection map)
        {
            string songId = map.MapData.Item1;
            APIHandler api = APIHandler.GetSelectedAPI();
            mapPP = api.GetScoregraph(map);
            Array.Sort(mapPP);//, (a,b) => (int)(b - a));
            //Plugin.Log.Debug($"[{string.Join(", ", mapPP)}]");
        }
        public void ReinitCounter(TMP_Text display) => this.display = display;
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = PluginConfig.Instance.DecimalPrecision;
            calc = Calculator.GetSelectedCalc();
            ratings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            ratingLen = ratings.Length == 1 ? 0 : ratings.Length;
        }
        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { 
            ReinitCounter(display, map.PassRating, map.AccRating, map.TechRating, map.StarRating);
            SetupData(map); 
        }
        #endregion
        #region Helper Methods
        public static void FormatTheFormat(string format) => rankIniter = GetTheFormat(format, out _);
        public static Func<Func<Dictionary<char, object>, string>> GetTheFormat(string format, out string errorStr)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, MainAlias, DisplayName,
                formattedTokens =>
                {
                    if (!PC.ShowLbl) formattedTokens.SetText('l');
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{vals['c']}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                    if (!(bool)vals[(char)3]) HelpfulFormatter.SetText(tokensCopy, '3');
                    if (!(bool)vals[(char)4]) HelpfulFormatter.SetText(tokensCopy, '4');
                }, out errorStr);
        }
        public static void InitTheFormat()
        {
            var simple = rankIniter.Invoke();
            displayRank = (fc, extraInfo, isNum1, pp, fcpp, rank, ppDiff, color, label) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    {(char)1, fc }, {(char)2, extraInfo }, {(char)3, !isNum1 && extraInfo }, {(char)4, isNum1 && extraInfo }, {'x', pp}, {'y', fcpp },
                    {'r', rank}, {'d', ppDiff}, {'c', color }, {'l',label}
                };
                return simple.Invoke(vals);
            };
        }
        public void UpdateFormat() => InitTheFormat();
        public static bool InitFormat()
        {
            if (rankIniter == null) FormatTheFormat(PC.FormatSettings.RankTextFormat);
            if (displayRank == null && rankIniter != null) InitTheFormat();
            return displayRank != null;
        }
        public static void ResetFormat() 
        {
            rankIniter = null;
            displayRank = null;
        }
        private int GetRank(float pp) { int val = Array.BinarySearch(mapPP, pp); return mapPP.Length - (val >= 0 ? val - 1 : Math.Abs(val) - 2); }
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            float[] ppVals = new float[(ratingLen + 1) * 2], temp;
            temp = calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings);
            //Plugin.Log.Info($"ratings: {HelpfulMisc.Print(temp)}\ttemp: {HelpfulMisc.Print(temp)}");
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            if (displayFc)
            {
                temp = calc.GetPpWithSummedPp(fcPercent, PluginConfig.Instance.DecimalPrecision, ratings);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + temp.Length] = temp[i];
            }
            int rank = GetRank(ppVals[ratingLen]);
            float ppDiff = (float)Math.Abs(Math.Round(mapPP[mapPP.Length + 1 - Math.Max(2, Math.Min(rank, mapPP.Length + 1))] - ppVals[ratingLen], precision));
            string color = HelpfulFormatter.GetWeightedRankColor(rank);
            string text = "";
            //Plugin.Log.Info("PPVals: " + HelpfulMisc.Print(ppVals));
            if (PC.SplitPPVals)
                for (int i = 0; i < ratingLen; i++)
                    text += displayRank(displayFc, false, false, ppVals[i], ppVals[i + ratingLen],
                        rank, ppDiff, color, TheCounter.GetLabel(i)) + "\n";
            text += displayRank(displayFc, PC.ExtraInfo, rank == 1, ppVals[ratingLen], ppVals[ratingLen * 2 + 1], rank, ppDiff, color, TheCounter.GetLabel(3));
            display.text = text;
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent) { }
        #endregion

    }
}
