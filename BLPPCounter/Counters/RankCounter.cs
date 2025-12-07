using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Containers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using static GameplayModifiers;

namespace BLPPCounter.Counters
{
    public class RankCounter(TMP_Text display, MapSelection map, CancellationToken ct) : MyCounters(display, map, ct)
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
        private static Func<FormatWrapper, string> displayRank;
        private static Func<Func<FormatWrapper, string>> rankIniter;
        private static FormatWrapper rankWrapper;
        private static bool displayPP;

        public static readonly Dictionary<string, char> MainAlias = new()
        {
            {"PP", 'x' },
            {"FCPP", 'y' },
            {"Rank", 'r' },
            {"PP Difference", 'd' },
            {"Acc Difference", 'p' },
            {"Label", 'l' },
            {"Rank Color", 'c' }
        };
        internal static readonly FormatRelation MainRelation = new("Main Format", DisplayName,
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
            new FormatWrapper(new Dictionary<char, object>()
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
            }), HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>()
            {
                {'r', 0 },
                {'c', 1 }
            },
            [
                FormatRelation.CreateFunc("#{0}", "{0}"),
                FormatRelation.CreateFunc<int>(num => HelpfulFormatter.GetWeightedRankColor(num) + '#' + num, num => HelpfulFormatter.GetWeightedRankColor(num))
            ], new Dictionary<char, IEnumerable<(string, object)>>()
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

        #endregion
        #region Inits
        public override void SetupData(MapSelection map, CancellationToken ct)
        {
            string songId = map.MapData.songId;
            APIHandler api = APIHandler.GetSelectedAPI();
            mapData = api.GetScoregraph(map, ct).GetAwaiter().GetResult();
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
            rankArr = [.. mapData.Select(t => t.pp)];
            //Plugin.Log.Debug($"[{string.Join(", ", mapData.Select(t => (t.acc, t.pp)))}]");
            ppHandler = new PPHandler(ratings, calc, PC.DecimalPrecision, 1)
            {
                UpdateFCEnabled = PC.PPFC,
                UpdatePPEnabled = displayPP
            };
            ppHandler.UpdateFC += (fcAcc, vals, actions) => vals[1].SetValues(calc.GetPpWithSummedPp(fcAcc, PC.DecimalPrecision));
        }
        public override void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {
            base.ReinitCounter(display, ratingVals);
        }
        public override void ReinitCounter(TMP_Text display, MapSelection map) 
        {
            base.ReinitCounter(display, map);
        }
        #endregion
        #region Helper Methods
        public static void FormatTheFormat(string format) => rankIniter = GetTheFormat(format, out _);
        public static Func<Func<FormatWrapper, string>> GetTheFormat(string format, out string errorStr)
        {
            var outp = HelpfulFormatter.GetBasicTokenParser(format, MainAlias, DisplayName,
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
                }, out errorStr, out HelpfulFormatter.TokenInfo[] arr);

            HashSet<char> ppSymbols = ['x', 'd'];
            displayPP = arr.Any(token => token.Usage > HelpfulFormatter.TokenUsage.Never && ppSymbols.Contains(token.Token));

            return outp;
        }
        public static void InitTheFormat()
        {
            displayRank = rankIniter.Invoke();
            rankWrapper = new FormatWrapper((typeof(bool), (char)1), (typeof(bool), (char)2), (typeof(bool), (char)3), (typeof(bool), (char)4), (typeof(float), 'x'), (typeof(float), 'y'),
                (typeof(int), 'r'), (typeof(float), 'd'), (typeof(float), 'p'), (typeof(string), 'c'), (typeof(string), 'l'));
        }
        private static string DisplayRank(bool fc, bool extraInfo, bool isNum1, float pp, float fcpp, int rank, float ppDiff, float percentDiff, string color, string label)
        {
            rankWrapper.SetValues(((char)1, fc), ((char)2, extraInfo), ((char)3, !isNum1 && extraInfo), ((char)4, isNum1 && extraInfo), ('x', pp), ('y', fcpp),
                    ('r', rank), ('d', ppDiff), ('p', percentDiff), ('c', color), ('l', label));
            return displayRank.Invoke(rankWrapper);
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
            ppHandler.Update(acc, mistakes, fcPercent);

            int rank = GetRank(ppHandler.GetPPGroup(0).TotalPP);
            float ppDiff = (float)Math.Abs(Math.Round(mapData[Math.Max(rank - 2, 0)].pp - ppHandler.GetPPGroup(0).TotalPP, PluginConfig.Instance.DecimalPrecision));
            float accDiff = (float)Math.Abs(Math.Round((mapData[Math.Max(rank - 2, 0)].acc - acc) * 100f, PluginConfig.Instance.DecimalPrecision));
            string color = HelpfulFormatter.GetWeightedRankColor(rank);
            string text = "";
            //Plugin.Log.Info("PPVals: " + HelpfulMisc.Print(ppVals));
            if (PC.SplitPPVals && calc.RatingCount > 1)
                for (int i = 0; i < calc.RatingCount; i++)
                    text += DisplayRank(ppHandler.DisplayFC, false, false, ppHandler[0, i], ppHandler[0, i],
                        rank, ppDiff, accDiff, color, TheCounter.CurrentLabels[i]) + "\n";
            text += DisplayRank(ppHandler.DisplayFC, PC.ExtraInfo, rank == 1, ppHandler[0], ppHandler[1], rank, ppDiff, accDiff, color, TheCounter.CurrentLabels.Last());
            display.text = text;
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion

    }
}
