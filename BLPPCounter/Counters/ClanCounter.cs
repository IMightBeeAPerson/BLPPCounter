﻿using Newtonsoft.Json.Linq;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Helpfuls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using TMPro;
using SiraUtil.Affinity;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.API_Handlers;

namespace BLPPCounter.Counters
{
    public class ClanCounter: IMyCounters
    {
        #region Static Variables
        private static int playerClanId = -1;
        private static readonly List<(MapSelection, float[])> mapCache = new List<(MapSelection, float[])>(); //Map, clan pp vals, acc vals
        private static PluginConfig pc => PluginConfig.Instance;
        private static Func<bool, bool, int, Func<string>, string, float, Func<string>, string, float, string, Func<string>, string> displayClan;
        private static Func<bool[], int, Func<string>, string, string, float, string, float, string, string, string> displayWeighted;
        private static Func<Func<string>, float, float, float, float, float, string> displayCustom;
        private static Func<Func<Dictionary<char, object>, string>> clanIniter, weightedIniter, customIniter;
        public static readonly Dictionary<string, char> FormatAlias = new Dictionary<string, char>()
        {
            { "PP", 'p' },
            { "PP Difference", 'x' },
            { "Color", 'c' },
            { "FCPP", 'o' },
            { "FCPP Difference", 'y' },
            { "FC Color", 'f' },
            { "Label", 'l' },
            { "Mistakes", 'e' },
            { "Target", 't' },
            { "Message", 'm' }
        };
        public static readonly Dictionary<string, char> WeightedFormatAlias = new Dictionary<string, char>()
        {
            { "Mistakes", 'e' },
            { "Rank Color", 'c' },
            { "Rank", 'r' },
            { "PP Difference", 'x' },
            { "PP", 'p' },
            { "Label", 'l' },
            { "FCPP Difference", 'y' },
            { "FCPP", 'o' },
            { "Message", 'm' }
        };
        public static readonly Dictionary<string, char> MessageFormatAlias = new Dictionary<string, char>()
        {
            {"Color", 'c' },
            {"Accuracy", 'a' },
            {"Tech PP", 'x' },
            {"Acc PP", 'y' },
            {"Pass PP", 'z' },
            {"PP", 'p' },
            { "Target", 't' }
        };
        internal static readonly FormatRelation ClanFormatRelation = new FormatRelation("Main Format", DisplayName,
            pc.FormatSettings.ClanTextFormat, str => pc.FormatSettings.ClanTextFormat = str, FormatAlias,
            new Dictionary<char, string>()
            {
                { 'p', "The unmodified PP number" },
                { 'x', "The modified PP number (plus/minus value)" },
                { 'c', "Must use as a group value, and will color everything inside group" },
                { 'o', "The unmodified PP number if the map was FC'ed" },
                { 'y', "The modified PP number if the map was FC'ed" },
                { 'f', "Must use as a group value, and will color everything inside group" },
                { 'l', "The label (ex: PP, Tech PP, etc)" },
                { 'e', "The amount of mistakes made in the map. This includes bomb and wall hits" },
                { 't', "This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target" },
                { 'm', "This shows either the clan message or percent needed message depending on user settings. The idea of this message is to show what percent is needed to capture the map." }
            }, str => { var hold = GetFormatClan(str, out string errorStr, false); return (hold, errorStr); },
            new Dictionary<char, object>()
            {
                { (char)1, true },
                { (char)2, true },
                { 'p', 543.21f },
                { 'x', -69.42f },
                { 'c', new Func<object>(() => "#0F0") },
                { 'o', 654.32f },
                { 'y', 42.69f },
                { 'f', new Func<object>(() => "#F00") },
                { 'l', " PP" },
                { 'e', 1 },
                { 't', "Person" },
                { 'm', new Func<object>(() => 95.0f) }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(6)
            {
                {'x', 0 },
                {'y', 0 },
                {'c', 1 },
                {'f', 1 },
                {'t', 2 },
                {'m', 3 }
            }, new Func<object, bool, object>[4]
            {
                FormatRelation.CreateFunc<float>(
                    outp => $"<color={(outp > 0 ? "green" : "red")}>" + outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT),
                    outp => outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT)),
                FormatRelation.CreateFuncWithWrapper("<color={0}>{0}", "<color={0}>"),
                FormatRelation.CreateFunc("Targeting <color=red>{0}</color>"),
                FormatRelation.CreateFuncWithWrapper("{0}%", "Get {0}% for ___ PP!")
            }, new Dictionary<char, IEnumerable<(string, object)>>(5)
            {
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'o', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'x', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 5) } },
                { 'y', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 5) } },
                { 'm', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 100), ("IncrementVal", 1) } }
            }, new (char, string)[2]
            {
                ((char)1, "Has a miss"),
                ((char)2, "Is bottom of text")
            }
            );
        internal static readonly FormatRelation WeightedFormatRelation = new FormatRelation("Weighted Format", DisplayName,
            pc.FormatSettings.WeightedTextFormat, str => pc.FormatSettings.WeightedTextFormat = str, WeightedFormatAlias,
            new Dictionary<char, string>()
            {
                { 'e', "The amount of mistakes made in the map. This includes bomb and wall hits" },
                { 'c', "The color based off of what rank you are out of your clan, must be used inside of a group and will color everything in the group" },
                { 'r', "The rank you are in the clan at that current moment" },
                { 'x', "The modified PP number if the map was FC'ed" },
                { 'p', "The unmodified PP number" },
                { 'l', "The label (ex: PP, Tech PP, etc)" },
                { 'y', "The modified PP number if the map was FC'ed" },
                { 'o', "The unmodified PP number if the map was FC'ed" },
                { 'm', "This will show a message if the counter is used on a map that isn't perfectly ideal for the weighted counter or that the weighted counter can't be used on. The message will say the reason for why this isn't ideal" }
            }, str => { var hold = GetFormatWeighted(str, out string errorStr, false); return (hold, errorStr); },
            new Dictionary<char, object>(12)
            {
                {(char)1, true },
                {(char)2, true },
                {(char)3, true },
                {'e', 1 },
                {'c', new Func<object>(() => 3) },
                {'r', 3 },
                {'x', -69.42f },
                {'p', 543.21f },
                {'l', " PP" },
                {'y', 42.69f },
                {'o', 654.32f },
                {'m', "<Insert a message here>" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(4)
            {
                { 'c', 0 },
                { 'r', 1 },
                { 'x', 2 },
                { 'y', 2 },
            }, new Func<object, bool, object>[3]
            {
                FormatRelation.CreateFuncWithWrapper<int>(a => $"{HelpfulFormatter.GetWeightedRankColor(a)}{a}",
                    a => () => HelpfulFormatter.GetWeightedRankColor(a)),
                FormatRelation.CreateFunc("#{0}", "{0}"),
                FormatRelation.CreateFunc<float>(
                    outp => $"<color={(outp > 0 ? "green" : "red")}>" + outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT),
                    outp => outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT))
            }, new Dictionary<char, IEnumerable<(string, object)>>(5)
            {
                { 'c', new (string, object)[4] { ("IsInteger", true), ("MinVal", 1), ("MaxVal", 100), ("IncrementVal", 1) } },
                { 'r', new (string, object)[4] { ("IsInteger", true), ("MinVal", 1), ("MaxVal", 100), ("IncrementVal", 1) } },
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'o', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                { 'x', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 5) } },
                { 'y', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 5) } },
            }, new (char, string)[3]
            {
                ((char)1, "Has a miss"),
                ((char)2, "Is bottom of text"),
                ((char)3, "Show Rank Info")
            }
            );
        internal static readonly FormatRelation MessageFormatRelation = new FormatRelation("Custom Message Format", DisplayName,
            pc.MessageSettings.ClanMessage, str => pc.MessageSettings.ClanMessage = str, MessageFormatAlias,
            new Dictionary<char, string>()
            {
                { 'c', "Must use as a group value, and will color everything inside group" },
                { 'a', "The accuracy needed to capture the map" },
                { 'x', "The tech PP needed" },
                { 'y', "The accuracy PP needed" },
                { 'z', "The pass PP needed" },
                { 'p', "The total PP number needed to capture the map" },
                { 't', "This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target" }
            }, str => { var hold = GetFormatCustom(str, out string errorStr, false); return (hold, errorStr); },
            new Dictionary<char, object>(7)
            {
                {'c', new Func<object>(() => "#0F0") },
                {'a', 95.85 },
                {'x', 114.14f },
                {'y', 321.23f },
                {'z', 69.42f },
                {'p', 543.21f },
                {'t', "Person" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(3)
            {
                {'c', 0 },
                {'a', 1 },
                {'t', 2 }
            }, new Func<object, bool, object>[3]
            {
                FormatRelation.CreateFuncWithWrapper("<color={0}>{0}", "<color={0}>"),
                FormatRelation.CreateFunc("{0}%", "{0}"),
                FormatRelation.CreateFunc("Targeting <color=red>{0}</color>", "{0}")
            }, new Dictionary<char, IEnumerable<(string, object)>>(5)
            {
                { 'a', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 100), ("IncrementVal", 1.5f), } },
                { 'x', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'y', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'z', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } }
            }
            );
        #endregion
        #region Variables
        public static string DisplayName => "Clan";
        public static string DisplayHandler => DisplayName;
        public static int OrderNumber => 3;
        public static Leaderboards ValidLeaderboards => Leaderboards.Beatleader;
        public string Name => DisplayName;
        public string Mods { get; private set; }
        private TMP_Text display;
        private float accRating, passRating, techRating, starRating, nmAccRating, nmPassRating, nmTechRating;
        private float[] neededPPs, clanPPs;
        private int precision, setupStatus;
        private string message;
        private bool showRank;
        private BLCalc calc;
        #endregion
        #region Init & Overrides
        public ClanCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.starRating = starRating;
            this.display = display;
            precision = pc.DecimalPrecision;
            calc = BLCalc.Instance;
        }
        public ClanCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }
        public void SetupData(MapSelection map) //setupStatus key: 0 = success, 1 = Map not ranked, 2 = Map already captured, 3 = load failed, 4 = map too hard to capture
        {
            setupStatus = 0;
            JToken mapData = map.MapData.Item2;
            if (int.Parse(mapData["status"].ToString()) != 3) { setupStatus = 1; goto theEnd; }
            string songId = map.MapData.Item1;
            Mods = "";
            nmPassRating = HelpfulPaths.GetRating(mapData, PPType.Pass);
            nmAccRating = HelpfulPaths.GetRating(mapData, PPType.Acc);
            nmTechRating = HelpfulPaths.GetRating(mapData, PPType.Tech);
            neededPPs = new float[6];
            neededPPs[3] = GetCachedPP(map);
            if (neededPPs[3] <= 0)
            {
                float[] ppVals = LoadNeededPp(songId, out bool mapCaptured, out _, ref playerClanId);
                neededPPs = new float[6];
                neededPPs[3] = ppVals[0];
                clanPPs = ppVals.Skip(1).ToArray();
                if (mapCaptured)
                {
                    Plugin.Log.Debug("Map already captured! No need to do anything else.");
                    setupStatus = 2;
                    goto theEnd;
                }
                if (ppVals == null) { setupStatus = 3; goto theEnd; }
                if (pc.MapCache > 0) mapCache.Add((map, ppVals));
            }
            neededPPs[5] = calc.GetAcc(neededPPs[3], pc.DecimalPrecision, calc.SelectRatings(starRating, accRating, passRating, techRating));
            neededPPs[4] = neededPPs[5] / 100.0f;
            float[] temp = calc.GetPp(neededPPs[4], calc.SelectRatings(starRating, nmAccRating, nmPassRating, nmTechRating));
            for (int i=0;i<temp.Length;i++) //temp length should be less than or equal to 3
                neededPPs[i] = temp[i];
            if (mapCache.Count > pc.MapCache)
            {
                Plugin.Log.Debug("Cache full! Making room...");
                do mapCache.RemoveAt(0); while (mapCache.Count > pc.MapCache);
            }
            if (pc.CeilEnabled && neededPPs[5] >= pc.ClanPercentCeil) setupStatus = 4;
        theEnd:
            switch (setupStatus)
            {
                case 1: message = pc.MessageSettings.MapUnrankedMessage; break;
                case 2: message = pc.MessageSettings.MapCapturedMessage; break;
                case 3: message = pc.MessageSettings.LoadFailedMessage; break;
                case 4: message = pc.MessageSettings.MapUncapturableMessage; break;
            }
            showRank = pc.ShowRank && setupStatus != 1 && setupStatus != 3;
        }
        public static float[] LoadNeededPp(string mapId, out bool mapCaptured, out string owningClan, ref int playerClanId)
        {
            string id = Targeter.TargetID, check;
            mapCaptured = false;
            owningClan = "None";
            check = BLAPI.Instance.CallAPI_String($"https://api.beatleader.xyz/player/{id}");
            if (playerClanId < 0 && check.Length > 0) playerClanId = ParseId(JToken.Parse(check));
            check = BLAPI.Instance.CallAPI_String($"{string.Format(HelpfulPaths.BLAPI_CLAN, mapId)}?page=1&count=1");
            if (check.Length == 0) return null;
            JToken clanData = JToken.Parse(check);
            if ((int)clanData["difficulty"]["status"] != 3) return null; //Map isn't ranked
            clanData = clanData["clanRanking"].Children().First();
            owningClan = clanData["clan"]["tag"].ToString();
            int clanId = -1;
            if (clanData.Count() > 0) clanId = (int)clanData["clan"]["id"]; else return null;
            mapCaptured = clanId <= 0 || clanId == playerClanId;
            float pp = (float)clanData["pp"];
            check = RequestClanLeaderboard(id, mapId, playerClanId);
            if (check.Length == 0) return new float[1] { pp }; //No scores are set, so player must capture it by themselves.
            JEnumerable<JToken> scores = JToken.Parse(check)["associatedScores"].Children();
            List<float> actualPpVals = new List<float>();
            float playerScore = 0.0f;
            foreach (JToken score in scores)
            {
                if (score["playerId"].ToString().Equals(id))
                    playerScore = (float)score["pp"];
                actualPpVals.Add((float)score["pp"]);
            }
            List<float> clone = new List<float>(actualPpVals);
            clone.Remove(playerScore);
            float[] clanPPs = clone.ToArray();
            Array.Sort(clanPPs, (a, b) => (int)Math.Round(b - a));
            float neededPp = mapCaptured ? 0.0f : BLCalc.Instance.GetNeededPlay(actualPpVals, pp, playerScore);
            return clanPPs.Prepend(neededPp).ToArray();
        }
        public static float[] LoadNeededPp(string mapId, out bool mapCaptured, out string owningClan) { int no = -1; return LoadNeededPp(mapId, out mapCaptured, out owningClan, ref no); }
        public void ReinitCounter(TMP_Text display) { this.display = display; }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating) { 
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = pc.DecimalPrecision;
            neededPPs[5] = calc.GetAcc(neededPPs[3], pc.DecimalPrecision, calc.SelectRatings(starRating, accRating, passRating, techRating));
            neededPPs[4] = neededPPs[5] / 100.0f;
            Plugin.Log.Info($"Read Percent: {neededPPs[5]}, Calc Percent: {neededPPs[4]}");
            float[] temp = calc.GetPp(neededPPs[4], calc.SelectRatings(starRating, nmAccRating, nmPassRating, nmTechRating));
            for (int i = 0; i < temp.Length; i++) //temp length should be less than or equal to 3
                neededPPs[i] = temp[i];
        }
        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { this.display = display; passRating = map.PassRating; accRating = map.AccRating; techRating = map.TechRating; SetupData(map); }
        public void UpdateFormat() => UpdateFormats();
        public static bool InitFormat()
        {
            if (clanIniter == null || weightedIniter == null || (pc.ShowClanMessage && customIniter == null)) FormatTheFormat();
            UpdateFormats();
            return displayClan != null && displayWeighted != null && TheCounter.TargetUsable && TheCounter.PercentNeededUsable && (!pc.ShowClanMessage || displayCustom != null);
        }
        public static void ResetFormat()
        {
            clanIniter = null;
            weightedIniter = null; 
            customIniter = null;
            displayClan = null;
            displayWeighted = null;
            displayCustom = null;
        }
        #endregion
        #region API Requests
        private static string RequestClanLeaderboard(string id, string mapId, int playerClanId)
        {
            int clanId = playerClanId > 0 ? playerClanId : ParseId(APIHandler.GetSelectedAPI().CallAPI_String($"{HelpfulPaths.BLAPI}player/{id}", forceNoHeader: true));
            return APIHandler.GetSelectedAPI().CallAPI_String($"{HelpfulPaths.BLAPI}leaderboard/clanRankings/{mapId}/clan/{clanId}?count=100&page=1", forceNoHeader: true);
        }
        #endregion
        #region Helper Functions
        private static int ParseId(JToken playerData)
        {
            JEnumerable<JToken> clans = playerData["clans"].Children();
            if (clans.Count() <= 1) return clans.Count() == 1 ? (int)clans.First()["id"] : -1;
            string clan = playerData["clanOrder"].ToString().Split(',')[0];
            foreach (JToken token in clans)
                if (token["tag"].ToString().Equals(clan))
                    return (int)token["id"];
            return -1;
        }
        public static void ClearCache() => mapCache.Clear();
        private float GetCachedPP(MapSelection map)
        {
            foreach ((MapSelection, float[]) pair in mapCache)
                if (pair.Item1.Equals(map)) {
                    clanPPs = pair.Item2.Skip(1).ToArray();
                    Plugin.Log.Debug($"PP: {pair.Item2[0]}");
                    return pair.Item2[0];
                }
            return -1.0f;
        } 
        private static void FormatTheFormat()
        {
            FormatClan(pc.FormatSettings.ClanTextFormat);
            FormatWeighted(pc.FormatSettings.WeightedTextFormat);
            FormatCustom(pc.MessageSettings.ClanMessage);
        }
        public static void UpdateFormats()
        {
            InitClan();
            InitWeighted();
            InitCustom();
        }
        private static void FormatClan(string format) => clanIniter = GetFormatClan(format, out string _);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatClan(string format, out string errorMessage, bool applySettings = true)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, FormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!pc.ShowLbl) formattedTokens.SetText('l');
                    if (!pc.Target.Equals(Targeter.NO_TARGET) && pc.ShowEnemy)
                    {
                        string theMods = "";
                        if (TheCounter.theCounter is ClanCounter cc) theMods = cc.Mods;
                        formattedTokens.MakeTokenConstant('t', TheCounter.TargetFormatter.Invoke(pc.Target, theMods));
                    }
                    else { formattedTokens.SetText('t'); formattedTokens.MakeTokenConstant('t'); }
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                    if (vals.ContainsKey('f')) HelpfulFormatter.SurroundText(tokensCopy, 'f', $"{((Func<object>)vals['f']).Invoke()}", "</color>");
                    if (vals.ContainsKey('m')) HelpfulFormatter.SetText(tokensCopy, 'm', ((Func<object>)vals['m']).Invoke().ToString());
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                }, out errorMessage, applySettings);
            
        }
        private static void FormatWeighted(string format) => weightedIniter = GetFormatWeighted(format, out string _);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatWeighted(string format, out string errorMessage, bool applySettings = true)
        {//settings values are: 0 = displayFC, 1 = totPP, 2 = showRank
            return HelpfulFormatter.GetBasicTokenParser(format, WeightedFormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!pc.ShowLbl) formattedTokens.SetText('l');
                },
                (tokens, tokensCopy, priority, vals) => {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1'); 
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2'); 
                    if (!(bool)vals[(char)3]) HelpfulFormatter.SetText(tokensCopy, '3'); 
                }, out errorMessage, applySettings);
        }
        private static void FormatCustom(string format) => customIniter = GetFormatCustom(format, out string _);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatCustom(string format, out string errorMessage, bool applySettings = true)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, MessageFormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!pc.Target.Equals(Targeter.NO_TARGET) && pc.ShowEnemy)
                        formattedTokens.SetText('t', pc.Target); 
                        else formattedTokens.SetText('t');
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                }, out errorMessage, applySettings);
        }
        private static void InitClan()
        {
            var simple = clanIniter.Invoke();
            displayClan = (fc, totPp, mistakes, color, modPp, regPp, fcCol, fcModPp, fcRegPp, label, message) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    { (char)1, fc }, {(char)2, totPp }, {'e', mistakes }, { 'c', color }, {'x',  modPp }, {'p', regPp }, {'l', label }, { 'f', fcCol }, { 'y', fcModPp }, { 'o', fcRegPp },
                    {'m', message }
                };
                return simple.Invoke(vals);
            };
        }
        private static void InitWeighted()
        {
            var simple = weightedIniter.Invoke();
            displayWeighted = (settings, mistakes, rankColor, rank, modPp, regPp, fcModPp, fcRegPp, label, message) =>
                simple.Invoke(new Dictionary<char, object>() {{'e', mistakes }, {'c', rankColor }, {'r', rank }, {'x',  modPp }, {'p', regPp },
                    {'l', label }, { 'y', fcModPp }, { 'o', fcRegPp }, {'m', message }, { (char)1, settings[0] }, {(char)2, settings[1] }, {(char)3, settings[2] }});
        }
        private static void InitCustom()
        {
            var simple = customIniter.Invoke();
            displayCustom = (color, acc, passpp, accpp, techpp, pp) => simple.Invoke(new Dictionary<char, object>()
            { { 'c', color }, { 'a', acc }, { 'x', techpp }, { 'y', accpp }, { 'z', passpp }, { 'p', pp } });
        }
        public static void AddToCache(MapSelection map, float[] vals) => mapCache.Add((map, vals));      
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent)
        {
            if (setupStatus > 0)
            {
                UpdateWeightedCounter(acc, mistakes, fcPercent);
                return;
            }
            bool displayFc = pc.PPFC && mistakes > 0;
            float[] ppVals = new float[16]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            float[] temp = calc.GetPp(acc, accRating, passRating, techRating);
            for (int i = 0; i < temp.Length; i++) 
                ppVals[i] = temp[i];
            ppVals[3] = calc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] - neededPPs[i];
            if (displayFc)
            {
                temp = calc.GetPp(acc, accRating, passRating, techRating);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + 8] = temp[i];
                ppVals[11] = calc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] - neededPPs[i - 8];
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string color(float num) => pc.UseGrad ? HelpfulFormatter.NumberToGradient(num) : HelpfulFormatter.NumberToColor(num);
            string message()
            {
                var func = pc.ShowClanMessage ? displayCustom : TheCounter.PercentNeededFormatter;
                return func.Invoke(() => color(ppVals[3] - neededPPs[3]),
                neededPPs[5], neededPPs[0], neededPPs[1], neededPPs[2], (float)Math.Round(neededPPs[3], precision));
            }
            if (pc.SplitPPVals)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += displayClan.Invoke(displayFc, pc.ExtraInfo && i == 3, mistakes, () => color(ppVals[i + 4]), ppVals[i + 4].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i],
                        () => color(ppVals[i + 12]), ppVals[i + 12].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i + 8], TheCounter.GetLabel(i), message) + "\n";
                display.text = text;
            }
            else
                display.text = displayClan.Invoke(displayFc, pc.ExtraInfo, mistakes, () => color(ppVals[7]), ppVals[7].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[3],
                    () => color(ppVals[15]), ppVals[15].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[11], TheCounter.GetLabel(3), message) + "\n";
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent) { }
        private void UpdateWeightedCounter(float acc, int mistakes, float fcPercent)
        {
            bool displayFc = pc.PPFC && mistakes > 0;
            float[] ppVals = new float[16]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            float[] temp = calc.GetPp(acc, accRating, passRating, techRating);
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            ppVals[3] = calc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            float weight = calc.GetWeight(ppVals[3], clanPPs, out int rank);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] * weight;
            if (displayFc)
            {
                temp = calc.GetPp(acc, accRating, passRating, techRating);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + 8] = temp[i];
                ppVals[11] = calc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] * weight;
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string ppLabel = " Weighted PP";
            if (pc.SplitPPVals)
            {
                string text = "", color = HelpfulFormatter.GetWeightedRankColor(rank);
                for (int i = 0; i < 4; i++)
                    text += displayWeighted.Invoke(new bool[] { displayFc, pc.ExtraInfo && i == 3, showRank && i == 3 }, 
                        mistakes, () => color, $"{rank}", $"{ppVals[i + 4]}", ppVals[i], $"{ppVals[i + 12]}", ppVals[i + 8], i == 3 ? ppLabel : TheCounter.GetLabel(i), message) + "\n";
                display.text = text;
            }
            else
                display.text = displayWeighted.Invoke(new bool[] { displayFc, pc.ExtraInfo, showRank }, 
                    mistakes, () => HelpfulFormatter.GetWeightedRankColor(rank), $"{rank}", $"{ppVals[7]}", ppVals[3], $"{ppVals[15]}", ppVals[11], ppLabel, message) + "\n";
        }
    }
    #endregion
}
