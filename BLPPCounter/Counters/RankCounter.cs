using Newtonsoft.Json.Linq;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace BLPPCounter.Counters
{
    public class RankCounter : IMyCounters
    {
        #region Static Variables
        public static int OrderNumber => 4;
        public static string DisplayName => "Rank";
        public static string DisplayHandler => DisplayName;
        public static string API_PATH => "leaderboard/{0}/scoregraph"; //replace 0 with a BL leaderboard idea (ex: a40531)
        public static string API_PATH2 => "leaderboard/scores/{0}?count={1}"; //replace 0 with a BL leaderboard idea (ex: a40531), replace 1 with the amount of people to request
        private static PluginConfig pc => PluginConfig.Instance;

        private static Func<bool, bool, float, float, int, float, string, string, string> displayRank;
        private static Func<Func<Dictionary<char, object>, string>> rankIniter;
        #endregion
        #region Variables
        public string Name => DisplayName;
        private int precision;
        private float accRating, passRating, techRating;
        private TMP_Text display;
        private float[] mapPP;
        private int MaxAmountOfPeople => pc.MinRank;
        #endregion
        #region Inits
        public RankCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public RankCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(map); }
        public void SetupData(MapSelection map)
        {
            string songId = map.MapData.Item1;
            if (map.IsRanked)
            {
                TheCounter.CallAPI(string.Format(API_PATH, songId), out string data);
                mapPP = JToken.Parse(data).Children().Select(a => (float)Math.Round((double)a["pp"], precision)).ToArray();
            }
            else
            {
                TheCounter.CallAPI(string.Format(API_PATH2, songId, MaxAmountOfPeople), out string data);
                JToken mapData = map.MapData.Item2;
                int maxScore = (int)mapData["maxScore"];
                float acc = (float)mapData["accRating"], pass = (float)mapData["passRating"], tech = (float)mapData["techRating"];
                mapPP = JToken.Parse(data)["scores"].Children().Select(a => 
                (float)Math.Round((double)BLCalc.Inflate(BLCalc.GetPpSum(HelpfulMath.BackCalcAcc((int)a["modifiedScore"], maxScore), acc, pass, tech)), precision)).ToArray();
            }
            Array.Sort(mapPP);//, (a,b) => (int)(b - a));
            Plugin.Log.Info($"[{string.Join(", ", mapPP)}]");
        }
        public void ReinitCounter(TMP_Text display) => this.display = display;
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
            ReinitCounter(display, map.PassRating, map.AccRating, map.TechRating);
            SetupData(map); 
        }
        #endregion
        #region Helper Methods
        public static void FormatTheFormat(string format)
        {
            rankIniter = HelpfulFormatter.GetBasicTokenParser(format,
                new Dictionary<string, char>()
                {
                    {"PP", 'x' },
                    {"FCPP", 'y' },
                    {"Rank", 'r' },
                    {"PP Difference", 'd' },
                    {"Label", 'l' },
                    {"Rank Color", 'c' }
                }, DisplayName,
                formattedTokens =>
                {
                    if (!pc.ShowLbl) formattedTokens.SetText('l');
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{vals['c']}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                });
        }
        public static void InitTheFormat()
        {
            var simple = rankIniter.Invoke();
            displayRank = (fc, totPp, pp, fcpp, rank, ppDiff, color, label) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    {(char)1, fc }, {(char)2, totPp }, {'x', pp}, {'y', fcpp }, {'r', rank}, {'d', ppDiff}, {'c', color }, {'l',label}
                };
                return simple.Invoke(vals);
            };
        }
        public void UpdateFormat() => InitTheFormat();
        public static bool InitFormat()
        {
            if (rankIniter == null) FormatTheFormat(pc.FormatSettings.RankTextFormat);
            if (displayRank == null && rankIniter != null) InitTheFormat();
            return displayRank != null;
        }
        private int GetRank(float pp) { int val = Array.BinarySearch(mapPP, pp); return mapPP.Length - (val >= 0 ? val - 1 : Math.Abs(val) - 2); }
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
            int rank = GetRank(ppVals[3]);
            float ppDiff = rank == 1 ? 0 : (float)Math.Round((rank < mapPP.Length + 1 ? mapPP[mapPP.Length - rank + 1] : mapPP[0]) - ppVals[3], precision);
            string color = HelpfulFormatter.GetWeightedRankColor(rank);
            if (pc.SplitPPVals)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += displayRank(displayFc, pc.ExtraInfo && i == 3, ppVals[i], ppVals[i + 4], rank, ppDiff, color, TheCounter.Labels[i]) + "\n";
                display.text = text;
            }
            else
                display.text = displayRank(displayFc, pc.ExtraInfo, ppVals[3], ppVals[7], rank, ppDiff, color, TheCounter.Labels[3]);
        }
        #endregion

    }
}
