using System;
using TMPro;
using BeatLeader.Models.Replay;
using System.Net.Http;
using BLPPCounter.Settings.Configs;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils;
using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using static GameplayModifiers;
using System.Text.RegularExpressions;
using BLPPCounter.Utils.List_Settings;

namespace BLPPCounter.Counters
{
    public class RelativeCounter: IMyCounters
    {
        #region Static Variables
        public static int OrderNumber => 2;
        public static string DisplayName => "Relative";
        public static bool SSUsable => false;
        public static string DisplayHandler => DisplayName;
        private static Func<bool, bool, int, float, string, string, float, string, string, float, float, string, string> displayFormatter;
        public static Type[] FormatterTypes => displayFormatter.GetType().GetGenericArguments();
        private static Func<Func<Dictionary<char, object>, string>> displayIniter;
        private static PluginConfig pc => PluginConfig.Instance;
        public static readonly Dictionary<string, char> FormatAlias = new Dictionary<string, char>()
                {
                    { "Acc Difference", 'd' },
                    { "Color", 'c' },
                    { "PP Difference", 'x' },
                    { "PP", 'p' },
                    { "Label", 'l' },
                    { "FC Color", 'f' },
                    { "FCPP Difference", 'y' },
                    { "FCPP", 'o' },
                    { "Accuracy", 'a' },
                    { "Target", 't' },
                    { "Mistakes", 'e' }
                };
        internal static readonly FormatRelation DefaultFormatRelation = new FormatRelation("Main Format", DisplayName,
            pc.FormatSettings.RelativeTextFormat, str => pc.FormatSettings.RelativeTextFormat = str, FormatAlias,
            new Dictionary<char, string>()
            {
                { 'd', "This will show the difference in percentage at the current moment between you and the replay you're comparing against" },
                { 'c', "This is the accuracy needed to beat your or your target's previous score" },
                { 'x', "The unmodified PP number" },
                { 'p', "The modified PP number (plus/minus value)" },
                { 'l', "Must use as a group value, and will color everything inside group" },
                { 'f', "The unmodified PP number if the map was FC'ed" },
                { 'y', "The modified PP number if the map was FC'ed" },
                { 'o', "Must use as a group value, and will color everything inside group" },
                { 'a', "The label (ex: PP, Tech PP, etc)" },
                { 't', "The amount of mistakes made in the map. This includes bomb and wall hits" },
                { 'e', "This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target" }
            }, str => { var hold = GetTheFormat(str, out string errorStr, false); return (hold, errorStr); },
            new Dictionary<char, object>(13)
            {
                {(char)1, true },
                {(char)2, true },
                {'e', 1 },
                {'d', 0.1f },
                {'c', "green" },
                {'x', -30.5f },
                {'p', 543.21f },
                {'f', "red" },
                {'y', 21.21f },
                {'o', 654.32f },
                {'a', 99.54f },
                {'l', "PP" },
                {'t', "Person" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(7)
            {
                {'c', 0 },
                {'x', 1 },
                {'f', 0 },
                {'y', 1 },
                {'a', 2 },
                {'t', 3 }
            }, new Func<object, bool, object>[4] 
            { 
                FormatRelation.CreateFunc("<color={0}>{0}", "<color={0}>"),
                FormatRelation.CreateFunc<float>(
                    outp => $"<color={(outp > 0 ? "green" : "red")}>" + outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT),
                    outp => outp.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT)),
                FormatRelation.CreateFunc("{0}%", "{0}"),
                FormatRelation.CreateFunc("Targeting <color=red>{0}</color>")
            },
            new Dictionary<char, IEnumerable<(string, object)>>(6)
            { //default values: IsInteger = false, MinVal = -1.0f, MaxVal = -1.0f, IncrementVal = -1.0f
                { 'd', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 1.5f), } },
                { 'x', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10), } },
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'y', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10), } },
                { 'o', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'a', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 100), ("IncrementVal", 0.5f), } }
            }, new (char, string)[2]
            {
                ((char)1, "Has a miss"),
                ((char)2, "Is bottom of text")
            }
            );

        #endregion
        #region Variables
        public string Name => DisplayName;
        public string ReplayMods { get; private set; }

        private TMP_Text display;
        private float accRating, passRating, techRating, starRating, accToBeat;
        private float[] best; //pass, acc, tech, total, replay pass rating, replay acc rating, replay tech rating, current score, current combo
        private Replay bestReplay;
        private NoteEvent[] noteArray;
        private int precision;
        private IMyCounters backup;
        private bool failed;
        #endregion
        #region Init
        public RelativeCounter(TMP_Text display, float accRating, float passRating, float techRating, float starRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.starRating = starRating;
            this.display = display;
            failed = false;
            precision = pc.DecimalPrecision;
        }
        public RelativeCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }
        private void SetupReplayData(JToken data)
        {
            //Plugin.Log.Debug(data.ToString());
            string replay = (string)data["replay"];
            ReplayDecoder.TryDecodeReplay(HelpfulPaths.CallAPI(replay).ReadAsByteArrayAsync().Result, out bestReplay);
            noteArray = bestReplay.notes.ToArray();
            ReplayMods = bestReplay.info.modifiers.ToLower();
            Match hold = Regex.Match(ReplayMods, "(fs|sf|ss),?");
            SongSpeed mod = hold.Success ? HelpfulMisc.GetModifierFromShortname(hold.Groups[1].Value) : SongSpeed.Normal;
            data = data["difficulty"];
            float replayMult = HelpfulPaths.GetMultiAmounts(data, Regex.Replace(ReplayMods, hold.Value, "").Split(','));
            best[4] = HelpfulPaths.GetRating(data, PPType.Pass, mod) * replayMult;
            best[5] = HelpfulPaths.GetRating(data, PPType.Acc, mod) * replayMult;
            best[6] = HelpfulPaths.GetRating(data, PPType.Tech, mod) * replayMult;
            ReplayMods = ReplayMods.ToUpper();
        }
        #endregion
        #region Overrides
        public void SetupData(MapSelection map)
        {
            try
            {
                string check = HelpfulPaths.CallAPI(string.Format(HelpfulPaths.BLAPI_SCORE, Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), map.Mode), true).ReadAsStringAsync().Result;
                if (check != default && check.Length > 0)
                {
                    JToken playerData = JObject.Parse(check);
                    best = new float[9];
                    SetupReplayData(playerData);
                    if ((float)playerData["pp"] is float thePP && thePP > 0)
                        accToBeat = (float)Math.Round(BLCalc.GetAcc(accRating, passRating, techRating, thePP) * 100.0f, pc.DecimalPrecision);
                    else
                    {
                        string[] hold = ((string)playerData["modifiers"]).ToUpper().Split(',');
                        //Ignore how janky this line is below, i'll fix later if I feel like it.
                        string modName = hold.Length == 0 ? "" : hold.FirstOrDefault(a => a.Equals("SF") || a.Equals("FS") || a.Equals("SS")) ?? "";
                        float acc = (float)playerData["accuracy"];
                        playerData = playerData["difficulty"];
                        modName = modName.ToLower();
                        float passStar = HelpfulPaths.GetRating(playerData, PPType.Pass, modName);
                        float techStar = HelpfulPaths.GetRating(playerData, PPType.Tech, modName);
                        float accStar = HelpfulPaths.GetRating(playerData, PPType.Acc, modName);
                        var moreHold = BLCalc.GetPpSum(acc, accStar, passStar, techStar);
                        accToBeat = (float)Math.Round(BLCalc.GetAccDeflated(accRating, passRating, techRating, moreHold) * 100.0f, pc.DecimalPrecision);
                    }
                }
            }
            catch (Exception e)
            {
                if (e.InnerException is HttpRequestException && e.InnerException.Message.Substring(0, 3).Equals("404"))
                    Plugin.Log.Warn("Relative counter cannot be loaded due to the player never having played this map before! (API return status 404)");
                else
                {
                    Plugin.Log.Warn("There was an error loading the replay of the player, most likely they have never played the map before.");
                    Plugin.Log.Debug(e);
                }
                Plugin.Log.Warn($"Defaulting to {pc.RelativeDefault.ToLower()} counter.");
                failed = true;
                backup = TheCounter.InitCounter(pc.RelativeDefault, display);
                return;
            }
            if (!failed)
            {
                if (best != null && best.Length >= 9)
                    best[7] = best[8] = 0;
            }
        }
        public void ReinitCounter(TMP_Text display)
        {
            this.display = display;
            if (failed) backup.ReinitCounter(display);
            if (!failed && best != null && best.Length >= 9)
                best[7] = best[8] = 0;
        }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = pc.DecimalPrecision;
            if (failed) backup.ReinitCounter(display, passRating, accRating, techRating, starRating);
            else if (best != null && best.Length >= 9)
                best[7] = best[8] = 0;
        }
        public void ReinitCounter(TMP_Text display, MapSelection map)
        { this.display = display; passRating = map.PassRating; accRating = map.AccRating; techRating = map.TechRating; failed = false; SetupData(map); }
        public void UpdateFormat() => InitDefaultFormat();
        public static bool InitFormat()
        {
            if (displayIniter == null && TheCounter.TargetUsable) FormatTheFormat(pc.FormatSettings.RelativeTextFormat);
            if (displayFormatter == null && displayIniter != null) InitDefaultFormat();
            return displayFormatter != null && TheCounter.TargetUsable;
        }
        public static void ResetFormat()
        {
            displayIniter = null;
            displayFormatter = null;
        }
        #endregion
        #region API Calls
        //https://api.beatleader.xyz/score/8/76561198306905129/98470c673d1702c5030487085120ad6f24828d6c/Expert/Standard
        #endregion
        #region Helper Functions
        public static Func<Func<Dictionary<char, object>, string>> GetTheFormat(string format, out string errorMessage, bool applySettings = true)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, FormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!pc.ShowLbl) formattedTokens.SetText('l');
                    if (!pc.Target.Equals(Targeter.NO_TARGET) && pc.ShowEnemy)
                    {
                        string theMods = "";
                        if (TheCounter.theCounter is RelativeCounter rc2) theMods = rc2.ReplayMods;
                        formattedTokens.MakeTokenConstant('t', TheCounter.TargetFormatter.Invoke(pc.Target, theMods));
                    }
                    else { formattedTokens.SetText('t'); formattedTokens.MakeTokenConstant('t'); }
                },
                (tokens, tokensCopy, priority, vals) =>
                {
                    HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{vals['c']}", "</color>");
                    HelpfulFormatter.SurroundText(tokensCopy, 'f', $"{vals['f']}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                }, out errorMessage, applySettings);//this is one line of code lol
        }
        public static void FormatTheFormat(string format) => displayIniter = GetTheFormat(format, out string _);
        public static void InitDefaultFormat()
        {
            var simple = displayIniter.Invoke();
            displayFormatter = (fc, totPp, mistakes, accDiff, color, modPp, regPp, fcCol, fcModPp, fcRegPp, acc, label) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    { (char)1, fc }, {(char)2, totPp }, {'e', mistakes }, {'d', accDiff }, { 'c', color }, {'x',  modPp }, {'p', regPp },
                    { 'f', fcCol }, { 'y', fcModPp }, { 'o', fcRegPp }, {'a', acc }, {'l', label }
                };
                return simple.Invoke(vals);
            };
        }
        #endregion
        #region Updates
        private void UpdateBest(int notes)
        {
            if (notes < 1) return;
            NoteEvent note = noteArray[notes-1];
            if (note.eventType == NoteEventType.good)
            {
                best[8]++;
                best[7] += BLCalc.GetCutScore(note.noteCutInfo) * HelpfulMath.MultiplierForNote((int)Math.Round(best[8]));
            }
            else
            {
                best[8] = HelpfulMath.DecreaseMultiplier((int)Math.Round(best[8]));
            }

            (best[0], best[1], best[2]) = BLCalc.GetPp(best[7] / HelpfulMath.MaxScoreForNotes(notes), best[5], best[4], best[6]);
            best[3] = BLCalc.Inflate(best[0] + best[1] + best[2]);
        }
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPrecent)
        {
            if (failed)
            {
                backup.UpdateCounter(acc, notes, mistakes, fcPrecent);
                return;
            }
            bool displayFc = pc.PPFC && mistakes > 0, showLbl = pc.ShowLbl;
            UpdateBest(notes);
            float[] ppVals = new float[16];
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] - best[i];
            if (displayFc)
            {
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcPrecent, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] - best[i - 8];
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string target = pc.ShowEnemy ? pc.Target : Targeter.NO_TARGET;
            string color(float num) => pc.UseGrad ? HelpfulFormatter.NumberToGradient(num) : HelpfulFormatter.NumberToColor(num);
            float accDiff = (float)Math.Round((acc - best[7] / HelpfulMath.MaxScoreForNotes(notes)) * 100.0f, pc.DecimalPrecision);
            if (float.IsNaN(accDiff)) accDiff = 0f;
            if (pc.SplitPPVals)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += displayFormatter.Invoke(displayFc, pc.ExtraInfo && i == 3, mistakes, accDiff, color(ppVals[i + 4]), ppVals[i + 4].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i],
                        color(ppVals[i + 12]), ppVals[i + 12].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i + 8], accToBeat, TheCounter.Labels[i]) + "\n";
                display.text = text;
            }
            else
                display.text = displayFormatter.Invoke(displayFc, pc.ExtraInfo, mistakes, accDiff, color(ppVals[7]), ppVals[7].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[3],
                    color(ppVals[15]), ppVals[15].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[11], accToBeat, TheCounter.Labels[3]) + "\n";
        }
        #endregion
    }
}
