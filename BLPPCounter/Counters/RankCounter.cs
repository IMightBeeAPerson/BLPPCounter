using Newtonsoft.Json.Linq;
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
using UnityEngine;
using static GameplayModifiers;
using BLPPCounter.Utils.Misc_Classes;

namespace BLPPCounter.Counters
{
    public class RankCounter : MyCounters
    {
        #region Static Variables
        public static int OrderNumber => 4;
        public static string DisplayName => "Rank";
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public static string DisplayHandler => DisplayName;
        private static PluginConfig PC => PluginConfig.Instance;

        /// <summary>
        /// fc, extraInfo, isNum1, pp, fcpp, rank, ppDiff, percentDiff, color, label
        /// </summary>
        private static Func<bool, bool, bool, float, float, int, float, float, string, string, string> displayRank;
        private static Func<Func<Dictionary<char, object>, string>> rankIniter;

        public static readonly Dictionary<string, char> MainAlias = new Dictionary<string, char>()
        {
            {"PP", 'x' },
            {"FCPP", 'y' },
            {"Rank", 'r' },
            {"PP Difference", 'd' },
            {"Acc Difference", 'p' },
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
                {'p', "The percent difference between your current acc and the person above you's acc" },
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
                { 'd', 1.1f },
                { 'p', 0.1f },
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
                { 'p', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 0.5f) } },
                { 'c', new (string, object)[4] { ("IsInteger", true), ("MinVal", 1), ("MaxVal", 100), ("IncrementVal", 1) } },
            }
            );
        #endregion
        #region Variables
        public override string Name => DisplayName;
        private float[] rankArr;
        private (float acc, float pp, SongSpeed speed, float modMult)[] mapData;
        private int ratingLen;
        #endregion
        #region Inits
        public RankCounter(TMP_Text display, MapSelection map) : base(display, map)
        {
            ratingLen = ratings.SelectedRatings.Length == 1 ? 0 : ratings.SelectedRatings.Length;
        }
        public override void SetupData(MapSelection map)
        {
            string songId = map.MapData.Item1;
            APIHandler api = APIHandler.GetSelectedAPI();
            mapData = api.GetScoregraph(map).GetAwaiter().GetResult();
            bool isUnranked = mapData[0].pp <= 0 || TheCounter.Leaderboard == Leaderboards.Accsaber;
            for (int i = 0; i < mapData.Length; i++)
            {
                if (!isUnranked && (TheCounter.Leaderboard != Leaderboards.Beatleader || mapData[i].speed == map.MapSpeed))
                    continue;
                if (TheCounter.Leaderboard == Leaderboards.Beatleader)
                {
                    float[] specificPps = calc.GetPpWithSummedPp(mapData[i].acc, HelpfulPaths.GetAllRatingsOfSpeed(map.MapData.diffData, calc, mapData[i].speed));
                    mapData[i] = (
                        BLCalc.Instance.GetAccDeflatedUnsafe(specificPps[0] + specificPps[1] + specificPps[2], PC.DecimalPrecision, ratings.SelectedRatings) / 100f,
                        (float)Math.Round(specificPps[3], PC.DecimalPrecision),
                        mapData[i].speed, mapData[i].modMult);
                }
                else mapData[i] = (mapData[i].acc, (float)Math.Round(calc.Inflate(calc.GetSummedPp(mapData[i].acc, ratings.SelectedRatings)), PC.DecimalPrecision), mapData[i].speed, mapData[i].modMult);
            }
            Array.Sort(mapData, (a,b) => (b.pp - a.pp) < 0 ? -1 : 1);
            rankArr = mapData.Select(t => t.pp).ToArray();
            Plugin.Log.Info($"[{string.Join(", ", mapData.Select(t => (t.acc, t.pp)))}]");
        }
        public override void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {
            base.ReinitCounter(display, ratingVals);
            ratingLen = ratings.SelectedRatings.Length == 1 ? 0 : ratings.SelectedRatings.Length;
        }
        public override void ReinitCounter(TMP_Text display, MapSelection map) 
        {
            ratingLen = Calculator.GetSelectedCalc().SelectRatings(map).Length;
            if (ratingLen == 1) ratingLen = 0;
            base.ReinitCounter(display, map);
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
            displayRank = (fc, extraInfo, isNum1, pp, fcpp, rank, ppDiff, percentDiff, color, label) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    {(char)1, fc }, {(char)2, extraInfo }, {(char)3, !isNum1 && extraInfo }, {(char)4, isNum1 && extraInfo }, {'x', pp}, {'y', fcpp },
                    {'r', rank}, {'d', ppDiff}, {'p', percentDiff }, {'c', color }, {'l',label}
                };
                return simple.Invoke(vals);
            };
        }
        public override void UpdateFormat() => InitTheFormat();
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
        private int GetRank(float pp) 
        { 
            int val = HelpfulMisc.FindInsertValueReverse(rankArr, pp);
            return Mathf.Clamp(val + 1, 1, Math.Min(PC.MinRank, mapData.Length));
        }
        #endregion
        #region Updates
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            bool displayFc = PluginConfig.Instance.PPFC && mistakes > 0;
            float[] ppVals = new float[(ratingLen + 1) * 2], temp;
            temp = calc.GetPpWithSummedPp(acc, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
            //Plugin.Log.Info($"ratings: {HelpfulMisc.Print(temp)}\ttemp: {HelpfulMisc.Print(temp)}");
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            if (displayFc)
            {
                temp = calc.GetPpWithSummedPp(fcPercent, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + temp.Length] = temp[i];
            }
            int rank = GetRank(ppVals[ratingLen]);
            float ppDiff = (float)Math.Abs(Math.Round(mapData[Math.Max(rank - 2, 0)].pp - ppVals[ratingLen], PluginConfig.Instance.DecimalPrecision));
            float accDiff = (float)Math.Abs(Math.Round((mapData[Math.Max(rank - 2, 0)].acc - acc) * 100f, PluginConfig.Instance.DecimalPrecision));
            string color = HelpfulFormatter.GetWeightedRankColor(rank);
            string text = "";
            //Plugin.Log.Info("PPVals: " + HelpfulMisc.Print(ppVals));
            if (PC.SplitPPVals && calc.RatingCount > 1)
                for (int i = 0; i < ratingLen; i++)
                    text += displayRank(displayFc, false, false, ppVals[i], ppVals[i + ratingLen],
                        rank, ppDiff, accDiff, color, TheCounter.CurrentLabels[i]) + "\n";
            text += displayRank(displayFc, PC.ExtraInfo, rank == 1, ppVals[ratingLen], ppVals[ratingLen * 2 + 1], rank, ppDiff, accDiff, color, TheCounter.CurrentLabels.Last());
            display.text = text;
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion

    }
}
