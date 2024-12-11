using Newtonsoft.Json.Linq;
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

namespace BLPPCounter.Counters
{
    public class ClanCounter: IMyCounters
    {
        #region Static Variables
        private static readonly HttpClient client = new HttpClient();
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
        public static readonly Dictionary<char, object> DefaultValues = new Dictionary<char, object>()
        {
            {(char)1, true },
            {(char)2, true },
            {'p', 543.21f },
            {'x', (-69.42f).ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT) },
            {'c', new Func<string>(() => "<color=#0F0>") },
            {'o', 654.32f },
            {'y', 42.69f.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT) },
            {'f', new Func<string>(() => "<color=#F00>") },
            {'l', " PP" },
            {'e', 1 },
            {'t', "Person" },
            {'m', new Func<string>(() => "<Insert formatted message here>") },
        };
        public static readonly Dictionary<char, object> WeightedDefaultValues = new Dictionary<char, object>()
        {
            {(char)1, true },
            {(char)2, true },
            {(char)3, true },
            {'e', 1 },
            {'c', new Func<string>(() => HelpfulFormatter.GetWeightedRankColor(3)) },
            {'r', 3 },
            {'x', -69.42f },
            {'p', 543.21f },
            {'l', " PP" },
            {'y', 42.69f },
            {'o', 654.32f },
            {'m', "<Insert a message here>" },
        };
        public static readonly Dictionary<char, object> MessageDefaultValues = new Dictionary<char, object>()
        {
            {'c', new Func<string>(() => "<color=#0F0>") },
            {'a', "95.85" },
            {'x', 114.14f },
            {'y', 321.23f },
            {'z', 69.42f },
            {'p', 543.21f },
            {'t', "Person" },
        };
        public static Func<string, string> QuickFormatClan => format => GetFormatClan(format, false).Invoke().Invoke(DefaultValues);
        public static Func<string, string> QuickFormatWeighted => format => GetFormatWeighted(format, false).Invoke().Invoke(WeightedDefaultValues);
        public static Func<string, string> QuickFormatMessage => format => GetFormatCustom(format, false).Invoke().Invoke(MessageDefaultValues);
        #endregion
        #region Variables
        public static string DisplayName => "Clan";
        public static string DisplayHandler => DisplayName;
        public static int OrderNumber => 3;
        public string Name => DisplayName;
        public string Mods { get; private set; }
        private TMP_Text display;
        private float accRating, passRating, techRating, nmAccRating, nmPassRating, nmTechRating;
        private float[] neededPPs, clanPPs;
        private int precision, setupStatus;
        private string message;
        private bool showRank;
        #endregion
        #region Init & Overrides
        public ClanCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            precision = pc.DecimalPrecision;
        }
        public ClanCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(map); }
        public void SetupData(MapSelection map) //setupStatus key: 0 = success, 1 = Map not ranked, 2 = Map already captured, 3 = load failed, 4 = map too hard to capture
        {
            setupStatus = 0;
            JToken mapData = map.MapData.Item2;
            if (int.Parse(mapData["status"].ToString()) != 3) { setupStatus = 1; goto theEnd; }
            //Plugin.Log.Info(mapData.ToString());
            string songId = map.MapData.Item1;
            //mods = mapData["PATH TO MODS"]
            Mods = "";
            nmPassRating = HelpfulPaths.GetRating(mapData, PPType.Pass);
            nmAccRating = HelpfulPaths.GetRating(mapData, PPType.Acc);
            nmTechRating = HelpfulPaths.GetRating(mapData, PPType.Tech);
            neededPPs = new float[6];
            neededPPs[3] = GetCachedPP(map);
            if (neededPPs[3] <= 0)
            {
                float[] ppVals = LoadNeededPp(songId, out bool mapCaptured);
                if (mapCaptured)
                {
                    Plugin.Log.Debug("Map already captured! No need to do anything else.");
                    setupStatus = 2;
                    goto theEnd;
                }
                if (ppVals == null) { setupStatus = 3; goto theEnd; }
                if (pc.MapCache > 0) mapCache.Add((map, ppVals));
            }
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
            if (mapCache.Count > pc.MapCache) Plugin.Log.Debug("Cache full! Making room..."); 
            while (mapCache.Count > pc.MapCache) mapCache.RemoveAt(0);
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
        public float[] LoadNeededPp(string mapId, out bool mapCaptured)
        {
            string id = Targeter.TargetID, check;
            mapCaptured = false;
            neededPPs = new float[6];
            check = RequestData($"https://api.beatleader.xyz/player/{id}");
            if (playerClanId < 0 && check.Length > 0) playerClanId = ParseId(JToken.Parse(check));
            check = RequestData($"{HelpfulPaths.BLAPI_CLAN}{mapId}?page=1&count=1");
            if (check.Length <= 0) return null;
            JToken clanData = JToken.Parse(check)["clanRanking"].Children().First();
            int clanId = -1;
            if (clanData.Count() > 0) clanId = (int)clanData["clan"]["id"]; else return null;
            mapCaptured = clanId <= 0 || clanId == playerClanId;
            float pp = (float)clanData["pp"];
            JEnumerable<JToken> scores = JToken.Parse(RequestClanLeaderboard(id, mapId))["associatedScores"].Children();
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
            clanPPs = clone.ToArray();
            Array.Sort(clanPPs, (a, b) => (int)Math.Round(b - a));
            neededPPs[3] = mapCaptured ? 0.0f : BLCalc.GetNeededPlay(actualPpVals, pp, playerScore);
            return clanPPs.Prepend(neededPPs[3]).ToArray();
        }
        public void ReinitCounter(TMP_Text display) { this.display = display; }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating) { 
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = pc.DecimalPrecision;
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
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
        #endregion
        #region API Requests
        private string RequestClanLeaderboard(string id, string mapId)
        {
            try
            {
                int clanId = playerClanId > 0 ? playerClanId : ParseId(JToken.Parse(client.GetStringAsync($"https://api.beatleader.xyz/player/{id}").Result));
                return client.GetStringAsync($"https://api.beatleader.xyz/leaderboard/clanRankings/{mapId}/clan/{clanId}?count=100&page=1").Result;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed! This is very sad.\nError: {e.Message}\nMap ID: {mapId}\tPlayer ID: {id}");
                Plugin.Log.Debug(e);
                return "";
            }
        }
        private static string RequestData(string path)
        {
            try
            {
                return client.GetStringAsync(new Uri(path)).Result;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Beat Leader API request in ClanCounter failed!\nPath: {path}\nError: {e.Message}");
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
        private static void FormatClan(string format) => clanIniter = GetFormatClan(format);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatClan(string format, bool applySettings = true)
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
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<string>)vals['c']).Invoke()}", "</color>");
                    if (vals.ContainsKey('f')) HelpfulFormatter.SurroundText(tokensCopy, 'f', $"{((Func<string>)vals['f']).Invoke()}", "</color>");
                    if (vals.ContainsKey('m')) HelpfulFormatter.SetText(tokensCopy, 'm', ((Func<string>)vals['m']).Invoke());
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                }, out string _, applySettings);
            
        }
        private static void FormatWeighted(string format) => weightedIniter = GetFormatWeighted(format);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatWeighted(string format, bool applySettings = true) //settings values are: 0 = displayFC, 1 = totPP, 2 = showRank
        {
            return HelpfulFormatter.GetBasicTokenParser(format, WeightedFormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!pc.ShowLbl) formattedTokens.SetText('l');
                },
                (tokens, tokensCopy, priority, vals) => {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<string>)vals['c']).Invoke()}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1'); 
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2'); 
                    if (!(bool)vals[(char)3]) HelpfulFormatter.SetText(tokensCopy, '3'); 
                }, out string _, applySettings);
            
        }
        private static void FormatCustom(string format) => customIniter = GetFormatCustom(format);
        private static Func<Func<Dictionary<char, object>, string>> GetFormatCustom(string format, bool applySettings = true)
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
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<string>)vals['c']).Invoke()}", "</color>");
                }, out string _, applySettings);
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
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] - neededPPs[i];
            if (displayFc)
            {
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] - neededPPs[i - 8];
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };
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
                        () => color(ppVals[i + 12]), ppVals[i + 12].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i + 8], labels[i], message) + "\n";
                display.text = text;
            }
            else
                display.text = displayClan.Invoke(displayFc, pc.ExtraInfo, mistakes, () => color(ppVals[7]), ppVals[7].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[3],
                    () => color(ppVals[15]), ppVals[15].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[11], labels[3], message) + "\n";
        }
        private void UpdateWeightedCounter(float acc, int mistakes, float fcPercent)
        {
            bool displayFc = pc.PPFC && mistakes > 0;
            float[] ppVals = new float[16]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            float weight = BLCalc.GetWeight(ppVals[3], clanPPs, out int rank);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] * weight;
            if (displayFc)
            {
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] * weight;
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " Weighted PP" };
            if (pc.SplitPPVals)
            {
                string text = "", color = HelpfulFormatter.GetWeightedRankColor(rank);
                for (int i = 0; i < 4; i++)
                    text += displayWeighted.Invoke(new bool[] { displayFc, pc.ExtraInfo && i == 3, showRank && i == 3 }, 
                        mistakes, () => color, $"{rank}", $"{ppVals[i + 4]}", ppVals[i], $"{ppVals[i + 12]}", ppVals[i + 8], labels[i], message) + "\n";
                display.text = text;
            }
            else
                display.text = displayWeighted.Invoke(new bool[] { displayFc, pc.ExtraInfo, showRank }, 
                    mistakes, () => HelpfulFormatter.GetWeightedRankColor(rank), $"{rank}", $"{ppVals[7]}", ppVals[3], $"{ppVals[15]}", ppVals[11], labels[3], message) + "\n";
        }
    }
    #endregion
}
