using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Containers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using BLPPCounter.Utils.Misc_Classes;

namespace BLPPCounter.Counters
{
    public class ClanCounter(TMP_Text display, MapSelection map, CancellationToken ct) : MyCounters(display, map, ct)
    {
        #region Static Variables
        private static int playerClanId = -1;
        private static readonly List<(MapSelection, float[])> mapCache = []; //Map, clan pp vals, acc vals
        private static PluginConfig PC => PluginConfig.Instance;
        private static Func<FormatWrapper, string> displayClan, displayWeighted, displayCustom;
        private static Func<Func<FormatWrapper, string>> clanIniter, weightedIniter, customIniter;
        private static FormatWrapper clanWrapper, weightedWrapper, customWrapper;
        public static readonly Dictionary<string, char> FormatAlias = new()
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
        public static readonly Dictionary<string, char> WeightedFormatAlias = new()
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
        public static readonly Dictionary<string, char> MessageFormatAlias = new()
        {
            {"Color", 'c' },
            {"Accuracy", 'a' },
            {"Tech PP", 'x' },
            {"Acc PP", 'y' },
            {"Pass PP", 'z' },
            {"PP", 'p' },
            { "Target", 't' }
        };
        internal static readonly FormatRelation ClanFormatRelation = new("Main Format", DisplayName,
            PC.FormatSettings.ClanTextFormat, str => PC.FormatSettings.ClanTextFormat = str, FormatAlias,
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
            new FormatWrapper(new Dictionary<char, object>()
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
            }), HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(6)
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
        internal static readonly FormatRelation WeightedFormatRelation = new("Weighted Format", DisplayName,
            PC.FormatSettings.WeightedTextFormat, str => PC.FormatSettings.WeightedTextFormat = str, WeightedFormatAlias,
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
                { 'm', "This will show a message if the counter is used on a map that isn't perfectly ideal for the weighted counter or that the weighted counter can't be used on." }
            }, str => { var hold = GetFormatWeighted(str, out string errorStr, false); return (hold, errorStr); },
            new FormatWrapper(new Dictionary<char, object>(12)
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
            }), HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(4)
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
        internal static readonly FormatRelation MessageFormatRelation = new("Custom Message Format", DisplayName,
            PC.MessageSettings.ClanMessage, str => PC.MessageSettings.ClanMessage = str, MessageFormatAlias,
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
            new FormatWrapper(new Dictionary<char, object>(7)
            {
                {'c', new Func<object>(() => "#0F0") },
                {'a', 95.85 },
                {'x', 114.14f },
                {'y', 321.23f },
                {'z', 69.42f },
                {'p', 543.21f },
                {'t', "Person" }
            }), HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(3)
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
        private static bool displayPP;
        #endregion
        #region Variables
        public static string DisplayName => "Clan";
        public static string DisplayHandler => DisplayName;
        public static int OrderNumber => 3;
        public static Leaderboards ValidLeaderboards => Leaderboards.Beatleader;
        public override string Name => DisplayName;
        public string Mods { get; private set; }
        private RatingContainer nmRatings;
        private float[] clanPPs;
        private PPContainer neededPPs;
        private float neededAcc;
        private int setupStatus;
        private string message;
        private bool showRank;
        #endregion
        #region Init & Overrides
        public override void SetupData(MapSelection map, CancellationToken ct) //setupStatus key: 0 = success, 1 = Map not ranked, 2 = Map already captured, 3 = load failed, 4 = map too hard to capture
        {
            setupStatus = 0;
            JToken mapData = map.MapData.diffData;
            if (int.Parse(mapData["status"].ToString()) != 3) { setupStatus = 1; goto theEnd; }
            string songId = map.MapData.songId;
            Mods = "";
            nmRatings = HelpfulPaths.GetAllRatingsOfSpeed(mapData, calc);
            neededPPs = new PPContainer(calc.DisplayRatingCount, GetCachedPP(map), precision: PC.DecimalPrecision);
            if (neededPPs.TotalPP <= 0)
            {
                float[] ppVals = LoadNeededPp(songId, out bool mapCaptured, out _, ref playerClanId);
                neededPPs.TotalPP = ppVals[0];
                clanPPs = [.. ppVals.Skip(1)];
                if (mapCaptured)
                {
                    Plugin.Log.Debug("Map already captured! No need to do anything else.");
                    setupStatus = 2;
                    goto theEnd;
                }
                if (ppVals == null) { setupStatus = 3; goto theEnd; }
                if (PC.MapCache > 0) mapCache.Add((map, ppVals));
            }
            neededAcc = calc.GetAcc(neededPPs.TotalPP, PC.DecimalPrecision, ratings.Ratings);
            float[] temp = calc.GetPp(neededAcc / 100f, nmRatings.Ratings);
            neededPPs.AccPP = temp[0];
            neededPPs.PassPP = temp[1];
            neededPPs.TechPP = temp[2];
            if (mapCache.Count > PC.MapCache)
            {
                Plugin.Log.Debug("Cache full! Making room...");
                do mapCache.RemoveAt(0); while (mapCache.Count > PC.MapCache);
            }
            if (PC.CeilEnabled && neededAcc >= PC.ClanPercentCeil) setupStatus = 4;
        theEnd:
            switch (setupStatus)
            {
                case 1: message = PC.MessageSettings.MapUnrankedMessage; break;
                case 2: message = PC.MessageSettings.MapCapturedMessage; break;
                case 3: message = PC.MessageSettings.LoadFailedMessage; break;
                case 4: message = PC.MessageSettings.MapUncapturableMessage; break;
            }
            showRank = PC.ShowRank && setupStatus != 1 && setupStatus != 3;
            ppHandler = new PPHandler(ratings, calc, PC.DecimalPrecision, 2, (rating, acc, in main, ref toChange) => PPContainer.SubtractFast(in main, in neededPPs, ref toChange))
            {
                UpdateFCEnabled = PC.PPFC
            };
            ppHandler.UpdateFC += (fcAcc, vals, actions) =>
            {
                vals[2].SetValues(calc.GetPpWithSummedPp(fcAcc, PC.DecimalPrecision));
                actions(0, fcAcc, in vals[2], ref vals[3]);
            };
        }
        public static async Task<(float[] clanPP, bool mapCaptured, string owningClan, int playerClanId)> LoadNeededPp(string mapId, int playerClanId, CancellationToken ct = default)
        {
            string id = Targeter.TargetID, check;
            bool mapCaptured = false;
            string owningClan = "None";
            check = await BLAPI.Instance.CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID_FULL, id), ct: ct).ConfigureAwait(false);
            if (playerClanId < 0 && check.Length > 0) playerClanId = ParseId(JToken.Parse(check));
            check = await BLAPI.Instance.CallAPI_String($"{string.Format(HelpfulPaths.BLAPI_CLAN, mapId)}?page=1&count=1", ct: ct).ConfigureAwait(false);
            if (check.Length == 0) return (null, mapCaptured, owningClan, playerClanId);
            JToken clanData = JToken.Parse(check);
            if ((int)clanData["difficulty"]["status"] != 3) return (null, mapCaptured, owningClan, playerClanId); //Map isn't ranked
            clanData = clanData["clanRanking"].Children().First();
            owningClan = clanData["clan"]["tag"].ToString();
            int clanId = -1;
            if (clanData.Count() > 0) clanId = (int)clanData["clan"]["id"]; else return (null, mapCaptured, owningClan, playerClanId);
            mapCaptured = clanId <= 0 || clanId == playerClanId;
            float pp = (float)clanData["pp"];
            check = await RequestClanLeaderboard(id, mapId, playerClanId, ct);
            if (check.Length == 0) return (new float[1] { pp }, mapCaptured, owningClan, playerClanId); //No scores are set, so player must capture it by themselves.
            JEnumerable<JToken> scores = JToken.Parse(check)["associatedScores"].Children();
            List<float> actualPpVals = [];
            float playerScore = 0.0f;
            foreach (JToken score in scores)
            {
                if (score["playerId"].ToString().Equals(id))
                    playerScore = (float)score["pp"];
                actualPpVals.Add((float)score["pp"]);
            }
            List<float> clone = [.. actualPpVals];
            clone.Remove(playerScore);
            float[] clanPPs = clone.ToArray();
            Array.Sort(clanPPs, (a, b) => (int)Math.Round(b - a));
            float neededPp = mapCaptured ? 0.0f : BLCalc.Instance.GetNeededPlay(actualPpVals, pp, playerScore);
            return (clanPPs.Prepend(neededPp).ToArray(), mapCaptured, owningClan, playerClanId);
        }
        public static float[] LoadNeededPp(string mapId, out bool mapCaptured, out string owningClan, ref int playerClanId, CancellationToken ct = default)
        {
            float[] outp;
            (outp, mapCaptured, owningClan, playerClanId) = LoadNeededPp(mapId, playerClanId, ct).GetAwaiter().GetResult();
            return outp;
        }
        public static float[] LoadNeededPp(string mapId, out bool mapCaptured, out string owningClan, CancellationToken ct = default) 
        {
            float[] outp;
            (outp, mapCaptured, owningClan, _) = LoadNeededPp(mapId, -1, ct).GetAwaiter().GetResult(); 
            return outp;
        }
        public override void ReinitCounter(TMP_Text display, RatingContainer ratingVals) {
            base.ReinitCounter(display, ratingVals);
            neededAcc = calc.GetAcc(neededPPs.TotalPP, PC.DecimalPrecision, ratings.Ratings);
            //Plugin.Log.Info($"Read Percent: {neededAcc}, Calc Percent: {neededAcc / 100f}");
            float[] temp = calc.GetPp(neededAcc / 100f, nmRatings.Ratings);
            neededPPs.AccPP = temp[0];
            neededPPs.PassPP = temp[1];
            neededPPs.TechPP = temp[2];
        }
        public override void UpdateFormat() => UpdateFormats();
        public static bool InitFormat()
        {
            if (clanIniter == null || weightedIniter == null || (PC.ShowClanMessage && customIniter == null)) FormatTheFormat();
            UpdateFormats();
            return displayClan != null && displayWeighted != null && TheCounter.TargetUsable && TheCounter.PercentNeededUsable && (!PC.ShowClanMessage || displayCustom != null);
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
        private static async Task<string> RequestClanLeaderboard(string id, string mapId, int playerClanId, CancellationToken ct = default)
        {
            try
            {
                int clanId = playerClanId > 0 ? playerClanId : ParseId(await BLAPI.Instance.CallAPI_String($"{HelpfulPaths.BLAPI}player/{id}", forceNoHeader: true, ct: ct).ConfigureAwait(false));
                return await BLAPI.Instance.CallAPI_String(string.Format(HelpfulPaths.BLAPI_CLAN + "/clan/{1}?count=100&page=1", mapId, clanId), forceNoHeader: true, ct: ct).ConfigureAwait(false);
            } catch (TaskCanceledException e)
            {
                Plugin.Log.Warn("RequestClanLeaderboard failed due to CancellationToken being invoked.");
                Plugin.Log.Debug(e);
                return "";
            }
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
            FormatClan(PC.FormatSettings.ClanTextFormat);
            FormatWeighted(PC.FormatSettings.WeightedTextFormat);
            FormatCustom(PC.MessageSettings.ClanMessage);
        }
        public static void UpdateFormats()
        {
            InitClan();
            InitWeighted();
            InitCustom();
        }
        private static void FormatClan(string format) => clanIniter = GetFormatClan(format, out string _);
        private static Func<Func<FormatWrapper, string>> GetFormatClan(string format, out string errorMessage, bool applySettings = true)
        {
            var outp = HelpfulFormatter.GetBasicTokenParser(format, FormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!PC.ShowLbl) formattedTokens.SetText('l');
                    if (!PC.Target.Equals(Targeter.NO_TARGET) && PC.ShowEnemy)
                    {
                        string theMods = "";
                        if (TheCounter.theCounter is ClanCounter cc) theMods = cc.Mods;
                        formattedTokens.MakeTokenConstant('t', TheCounter.TargetFormatter(PC.Target, theMods));
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
                }, out errorMessage, out HelpfulFormatter.TokenInfo[] arr, applySettings);

            HashSet<char> ppSymbols = ['x', 'p', 'c'];
            displayPP = arr.Any(token => token.Usage > HelpfulFormatter.TokenUsage.Never && ppSymbols.Contains(token.Token));

            return outp;
        }
        private static void FormatWeighted(string format) => weightedIniter = GetFormatWeighted(format, out string _);
        private static Func<Func<FormatWrapper, string>> GetFormatWeighted(string format, out string errorMessage, bool applySettings = true)
        {//settings values are: 0 = displayFC, 1 = totPP, 2 = showRank
            return HelpfulFormatter.GetBasicTokenParser(format, WeightedFormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!PC.ShowLbl) formattedTokens.SetText('l');
                },
                (tokens, tokensCopy, priority, vals) => {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1'); 
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2'); 
                    if (!(bool)vals[(char)3]) HelpfulFormatter.SetText(tokensCopy, '3'); 
                }, out errorMessage, out _, applySettings);
        }
        private static void FormatCustom(string format) => customIniter = GetFormatCustom(format, out string _);
        private static Func<Func<FormatWrapper, string>> GetFormatCustom(string format, out string errorMessage, bool applySettings = true)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, MessageFormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!PC.Target.Equals(Targeter.NO_TARGET) && PC.ShowEnemy)
                        formattedTokens.SetText('t', PC.Target); 
                        else formattedTokens.SetText('t');
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                }, out errorMessage, out _, applySettings);
        }
        private static void InitClan()
        {
            displayClan = clanIniter.Invoke();
            clanWrapper = new FormatWrapper((typeof(bool), (char)1), (typeof(bool), (char)2), (typeof(int), 'e'), (typeof(Func<string>), 'c'), (typeof(string), 'x'), (typeof(float), 'p'),
                (typeof(string), 'l'), (typeof(Func<string>), 'f'), (typeof(string), 'y'), (typeof(float), 'o'), (typeof(Func<string>), 'm'));
        }
        private static string DisplayClan(bool fc, bool totPp, int mistakes, Func<string> color, string modPp, float regPp,
            Func<string> fcColor, string fcModPp, float fcRegPp, string label, Func<string> message)
        {
            clanWrapper.SetValues(
                ((char)1, fc), ((char)2, totPp), ('e', mistakes), ('c', color), ('x', modPp), ('p', regPp), ('l', label), ('f', fcColor), ('y', fcModPp), ('o', fcRegPp),
                ('m', message)
            );
            return displayClan.Invoke(clanWrapper);
        }
        private static void InitWeighted()
        {
            displayWeighted = weightedIniter.Invoke();
            weightedWrapper = new FormatWrapper((typeof(bool), (char)1), (typeof(bool), (char)2), (typeof(bool), (char)3), (typeof(int), 'e'), (typeof(Func<string>), 'c'),
                (typeof(string), 'r'), (typeof(string), 'x'), (typeof(float), 'p'), (typeof(string), 'l'), (typeof(string), 'y'), (typeof(float), 'o'), (typeof(string), 'm'));
        }
        private static string DisplayWeighted(bool[] settings, int mistakes, Func<string> rankColor, string rank, string modPp, float regPp,
            string fcModPp, float fcRegPp, string label, string message)
        {
            weightedWrapper.SetValues(('e', mistakes), ('c', rankColor), ('r', rank), ('x', modPp), ('p', regPp),
                    ('l', label), ('y', fcModPp), ('o', fcRegPp), ('m', message), ((char)1, settings[0]), ((char)2, settings[1]), ((char)3, settings[2]));
            return displayWeighted.Invoke(weightedWrapper);
        }
        private static void InitCustom()
        {
            displayCustom = customIniter.Invoke();
            customWrapper = new FormatWrapper((typeof(Func<string>), 'c'), (typeof(float), 'a'), (typeof(float), 'x'), (typeof(float), 'y'),
                (typeof(float), 'z'), (typeof(float), 'p'));
        }
        private static string DisplayCustom(Func<string> color, float acc, float accPP, float passPP, float techPP, float pp)
        {
            customWrapper.SetValues(('c', color), ('a', acc), ('y', accPP), ('z', passPP), ('x', techPP), ('p', pp));
            return displayCustom.Invoke(customWrapper);
        }
        public static void AddToCache(MapSelection map, float[] vals) => mapCache.Add((map, vals));
        #endregion
        #region Updates
        //public override void UpdatePP(float acc)
        //{
        //    calc.SetPp(acc, ppVals, 0, PC.DecimalPrecision);
        //    for (int i = 0; i < ratingLen; i++)
        //        ppVals[i + ratingLen] = (float)Math.Round(ppVals[i] - neededPPs[i], PC.DecimalPrecision);
        //}
        //public override void UpdateFCPP(float fcPercent)
        //{
        //    calc.SetPp(fcPercent, ppVals, ratingLen * 2, PC.DecimalPrecision);
        //    for (int i = 0; i < ratingLen; i++)
        //        ppVals[i + ratingLen * 3] = (float)Math.Round(ppVals[i + ratingLen * 2] - neededPPs[i], PC.DecimalPrecision);
        //}
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            if (setupStatus > 0)
            {
                UpdateWeightedCounter(acc, mistakes, fcPercent);
                return;
            }
            
            ppHandler.Update(acc, mistakes, fcPercent);
            //Plugin.Log.Info("ppVals: " + HelpfulMisc.Print(ppHandler));

            string color(float num) => PC.UseGrad ? HelpfulFormatter.NumberToGradient(num) : HelpfulFormatter.NumberToColor(num);
            string message()
            {
                Func<Func<string>, float, float, float, float, float, string> func = PC.ShowClanMessage ? DisplayCustom : TheCounter.PercentNeededFormatter;
                return func.Invoke(() => color(ppHandler.GetPPGroup(0).TotalPP - neededPPs.TotalPP),
                neededAcc, neededPPs.AccPP, neededPPs.PassPP, neededPPs.TechPP, neededPPs.TotalPP);
            }
            if (PC.SplitPPVals && calc.RatingCount > 1)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += DisplayClan(ppHandler.DisplayFC, PC.ExtraInfo && i == 3, mistakes, () => color(ppHandler[1, i]), ppHandler[1, i].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppHandler[0, i],
                        () => color(ppHandler[3, i]), ppHandler[3, i].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppHandler[2, i], TheCounter.CurrentLabels[i], message) + "\n";
                display.text = text;
            }
            else
                display.text = DisplayClan(ppHandler.DisplayFC, PC.ExtraInfo, mistakes, () => color(ppHandler[1, 3]), ppHandler[1, 3].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppHandler[0, 3],
                    () => color(ppHandler[3, 3]), ppHandler[3, 3].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppHandler[2, 3], TheCounter.CurrentLabels.Last(), message) + "\n";
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        private void UpdateWeightedCounter(float acc, int mistakes, float fcPercent)
        {
            bool displayFc = PC.PPFC && mistakes > 0;
            float[] ppVals = new float[16]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            float[] temp = calc.GetPp(acc);
            for (int i = 0; i < temp.Length; i++)
                ppVals[i] = temp[i];
            ppVals[3] = calc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            float weight = BLCalc.Instance.GetWeight(ppVals[3], clanPPs, out int rank);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] * weight;
            if (displayFc)
            {
                temp = calc.GetPp(acc);
                for (int i = 0; i < temp.Length; i++)
                    ppVals[i + 8] = temp[i];
                ppVals[11] = calc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] * weight;
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], PC.DecimalPrecision);
            string ppLabel = " Weighted PP";
            if (PC.SplitPPVals && calc.RatingCount > 1)
            {
                string text = "", color = HelpfulFormatter.GetWeightedRankColor(rank);
                for (int i = 0; i < 4; i++)
                    text += DisplayWeighted([displayFc, PC.ExtraInfo && i == 3, showRank && i == 3], 
                        mistakes, () => color, $"{rank}", $"{ppVals[i + 4]}", ppVals[i], $"{ppVals[i + 12]}", ppVals[i + 8], i == 3 ? ppLabel : TheCounter.CurrentLabels[i], message) + "\n";
                display.text = text;
            }
            else
                display.text = DisplayWeighted([displayFc, PC.ExtraInfo, showRank], 
                    mistakes, () => HelpfulFormatter.GetWeightedRankColor(rank), $"{rank}", $"{ppVals[7]}", ppVals[3], $"{ppVals[15]}", ppVals[11], ppLabel, message) + "\n";
        }
    }
    #endregion
}
