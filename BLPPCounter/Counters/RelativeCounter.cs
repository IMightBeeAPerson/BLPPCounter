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
using BLPPCounter.Utils.API_Handlers;
using BeatLeader.Models.AbstractReplay;
using System.Threading.Tasks;
using System.Threading;

namespace BLPPCounter.Counters
{
    public class RelativeCounter: IMyCounters
    {
        #region Static Variables
        public static int OrderNumber => 2;
        public static string DisplayName => "Relative";
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public static string DisplayHandler => DisplayName;
        private static Func<bool, bool, int, string, string, string, float, string, string, float, float, string, string> displayFormatter;
        public static Type[] FormatterTypes => displayFormatter.GetType().GetGenericArguments();
        private static Func<Func<Dictionary<char, object>, string>> displayIniter;
        private static PluginConfig PC => PluginConfig.Instance;
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
            PC.FormatSettings.RelativeTextFormat, str => PC.FormatSettings.RelativeTextFormat = str, FormatAlias,
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
        private static Task SetupTask = Task.CompletedTask;

        #endregion
        #region Variables
        public string Name => DisplayName;
        public string ReplayMods { get; private set; }

        private TMP_Text display;
        private float accRating, passRating, techRating, starRating, accToBeat, staticAccToBeat;
        private float[] replayPPVals;
        private float[] replayRatings;
        private float replayScore, maxReplayScore;
        private int replayCombo;
        private Replay bestReplay;
        private BeatLeader.Models.Replay.NoteEvent[] noteArray;
        private Queue<BeatLeader.Models.Replay.WallEvent> wallArray;
        private int precision, bombs;
        private IMyCounters backup;
        private bool failed, useReplay;
        private Calculator calc;
        private Leaderboards leaderboard;
        private float[] selectedRatings, ppVals;
        private bool caughtUp, usingModdedAcc;
        private int catchUpNotes, displayNum;
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
            useReplay = PC.UseReplay;
            precision = PC.DecimalPrecision;
            calc = Calculator.GetSelectedCalc(); //only need to set it here bc when Calculator changes, this class instance gets remade.
            selectedRatings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            displayNum = calc.DisplayRatingCount;
            leaderboard = Calculator.UsingDefault ? PC.DefaultLeaderboard : PC.Leaderboard;
            ppVals = new float[displayNum * 4]; //16 for bl (displayRatingCount bc gotta store total pp as well)
            ResetVars();
        }
        public RelativeCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating, map.StarRating) { SetupData(map); }
        private void SetupReplayData(MapSelection map, JToken data)
        {
            if (data is null)
            {
                data = BLAPI.Instance.GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true).GetAwaiter().GetResult();
                if (data is null)
                {
                    useReplay = false;
                    return;
                }
            }
            string replay = (string)data["replay"];
            byte[] replayData = BLAPI.Instance.CallAPI_Bytes(replay, true).Result ?? throw new Exception("The replay link from the API is bad! (replay link failed to return data)");
            ReplayDecoder.TryDecodeReplay(replayData, out bestReplay);
            noteArray = bestReplay.notes.ToArray();
            wallArray = new Queue<BeatLeader.Models.Replay.WallEvent>(bestReplay.walls);
            ReplayMods = bestReplay.info.modifiers.ToLower();
            usingModdedAcc = false;
            if (leaderboard == Leaderboards.Beatleader)
            {
                Match hold = Regex.Match(ReplayMods, "(fs|sf|ss),?");
                SongSpeed mod = hold.Success ? HelpfulMisc.GetModifierFromShortname(hold.Groups[1].Value) : SongSpeed.Normal;
                data = data["difficulty"];
                float replayMult = HelpfulPaths.GetMultiAmounts(data, Regex.Replace(Regex.Replace(ReplayMods, "fs|sf|ss|nf", ""), hold.Value, "").Split(','));
                replayRatings = new float[3];
                replayRatings[0] = HelpfulPaths.GetRating(data, PPType.Acc, mod) * replayMult;
                replayRatings[1] = HelpfulPaths.GetRating(data, PPType.Pass, mod) * replayMult;
                replayRatings[2] = HelpfulPaths.GetRating(data, PPType.Tech, mod) * replayMult;
                for (int i = 0; i < replayRatings.Length; i++)
                    if (replayRatings[i] != selectedRatings[i])
                    {
                        usingModdedAcc = true;
                        break;
                    }
                usingModdedAcc &= PC.ReplayMods;
            }
            else
                replayRatings = selectedRatings;
            replayPPVals = new float[replayRatings.Length + 1];
            ReplayMods = ReplayMods.ToUpper();
        }
        #endregion
        #region Overrides
        public void SetupData(MapSelection map)
        {
            caughtUp = false;
            catchUpNotes = 0;
            Task.Run(async () =>
            {
                SetupTask = SetupDataAsync(map);
                await SetupTask;
                CatchupBest();
                if (catchUpNotes == 0)
                    UpdateCounter(1, 0, 0, 1, null);
            });
        }
        private async Task SetupDataAsync(MapSelection map)
        {
            try
            {
                Plugin.Log.Info($"Data: {HelpfulMisc.Print(new object[] { Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), PC.Leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true })}");
                JToken playerData = await APIHandler.GetSelectedAPI().GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), PC.Leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true).ConfigureAwait(false);
                if (playerData is null)
                {
                    Plugin.Log.Warn("Relative counter cannot be loaded due to the player never having played this map before! (API didn't return the corrent status)");
                    goto Failed;
                }
                //Only BL has useable replays, so only send data if this is a BL replay.
                if (PC.UseReplay) SetupReplayData(map, leaderboard == Leaderboards.Beatleader ? playerData : null);
                if ((float)playerData["pp"] is float thePP && thePP > 0)
                    accToBeat = calc.GetAcc(thePP, PC.DecimalPrecision, selectedRatings);
                else
                {
                    string[] hold = ((string)playerData["modifiers"]).ToUpper().Split(',');
                    float acc = (float)playerData["accuracy"];
                    float ppToBeat;
                    //Ignore how janky this line is below, i'll fix later if I feel like it.
                    if (leaderboard == Leaderboards.Beatleader)
                    {
                        string modName = hold.Length == 0 ? "" : hold.FirstOrDefault(a => a.Equals("SF") || a.Equals("FS") || a.Equals("SS")) ?? "";
                        playerData = playerData["difficulty"];
                        modName = modName.ToLower();
                        float passStar = HelpfulPaths.GetRating(playerData, PPType.Pass, modName);
                        float techStar = HelpfulPaths.GetRating(playerData, PPType.Tech, modName);
                        float accStar = HelpfulPaths.GetRating(playerData, PPType.Acc, modName);
                        ppToBeat = calc.GetSummedPp(acc, accStar, passStar, techStar);
                    }
                    else ppToBeat = calc.GetSummedPp(acc, selectedRatings);
                    accToBeat = calc.GetAccDeflated(ppToBeat, PC.DecimalPrecision, selectedRatings);
                }
                staticAccToBeat = accToBeat;
                if (!failed) ResetVars();
                return;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("There was an error loading the replay of the player.");
                Plugin.Log.Warn(e.Message);
                Plugin.Log.Debug(e);
            }
            Failed:
            Plugin.Log.Warn($"Defaulting to {PC.RelativeDefault.ToLower()} counter.");
            failed = true;
            if (!PC.RelativeDefault.Equals(Targeter.NO_TARGET))
            {
                backup = TheCounter.InitCounter(PC.RelativeDefault, display);
                if (catchUpNotes < 1) backup.UpdateCounter(1, 0, 0, 1, null);
            }
            else
                TheCounter.CancelCounter();
        }
        public void ReinitCounter(TMP_Text display)
        {
            this.display = display;
            if (failed)
            {
                if (backup is null)
                    TheCounter.CancelCounter();
                else backup.ReinitCounter(display, passRating, accRating, techRating, starRating);
            }
            else ResetVars();
        }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating, float starRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            this.starRating = starRating;
            precision = PC.DecimalPrecision;
            calc = Calculator.GetSelectedCalc(); //only need to set it here bc when Calculator changes, this class instance gets remade.
            selectedRatings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            displayNum = calc.DisplayRatingCount;
            leaderboard = Calculator.UsingDefault ? PC.DefaultLeaderboard : PC.Leaderboard;
            ppVals = new float[displayNum * 4]; //16 for bl (displayRatingCount bc gotta store total pp as well)
            if (failed)
            {
                if (backup is null)
                    TheCounter.CancelCounter();
                else backup.ReinitCounter(display, passRating, accRating, techRating, starRating);
            }
            else ResetVars();
        }
        public void ReinitCounter(TMP_Text display, MapSelection map)
        { 
            this.display = display;
            passRating = map.PassRating;
            accRating = map.AccRating; 
            techRating = map.TechRating;
            starRating = map.StarRating;
            selectedRatings = calc.SelectRatings(starRating, accRating, passRating, techRating);
            failed = false;
            SetupData(map); 
        }
        public void UpdateFormat() => InitDefaultFormat();
        public static bool InitFormat()
        {
            if (displayIniter == null && TheCounter.TargetUsable) FormatTheFormat(PC.FormatSettings.RelativeTextFormat);
            if (displayFormatter == null && displayIniter != null) InitDefaultFormat();
            return displayFormatter != null && TheCounter.TargetUsable;
        }
        public static void ResetFormat()
        {
            displayIniter = null;
            displayFormatter = null;
        }
        private void ResetVars()
        {
            bombs = 0;
            replayScore = 0;
            replayCombo = 0;
            maxReplayScore = 0;
        }
        #endregion
        #region Helper Functions
        public static Func<Func<Dictionary<char, object>, string>> GetTheFormat(string format, out string errorMessage, bool applySettings = true)
        {
            return HelpfulFormatter.GetBasicTokenParser(format, FormatAlias, DisplayName,
                formattedTokens =>
                {
                    if (!PC.ShowLbl) formattedTokens.SetText('l');
                    if (!PC.Target.Equals(Targeter.NO_TARGET) && PC.ShowEnemy)
                    {
                        string theMods = "";
                        if (TheCounter.theCounter is RelativeCounter rc2) theMods = rc2.ReplayMods;
                        formattedTokens.MakeTokenConstant('t', TheCounter.TargetFormatter.Invoke(PC.Target, theMods));
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
        private void CatchupBest()
        {
            if (catchUpNotes == 0)
            {
                caughtUp = true;
                return;
            }
            if (!useReplay)
            {
                replayPPVals = calc.GetPpWithSummedPp(accToBeat / 100.0f, selectedRatings);
                caughtUp = true;
                return;
            }
            BeatLeader.Models.Replay.NoteEvent note;
            int notes = 1;
            for (; notes <= catchUpNotes; notes++)
            {
                note = noteArray[notes + bombs - 1];
                switch (note.eventType)
                {
                    case NoteEventType.good:
                        maxReplayScore += notes < 14 ? BLCalc.GetMaxCutScore(note) * HelpfulMath.ClampedMultiplierForNote(notes) : BLCalc.GetMaxCutScore(note);
                        replayCombo++;
                        replayScore += BLCalc.GetCutScore(note) * HelpfulMath.ClampedMultiplierForNote(replayCombo);
                        break;
                    case NoteEventType.bomb:
                        replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                        bombs++;
                        notes--;
                        continue;
                    default:
                        maxReplayScore += notes < 14 ? BLCalc.GetMaxCutScore(note) * HelpfulMath.ClampedMultiplierForNote(notes) : BLCalc.GetMaxCutScore(note); 
                        replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                        break;

                }
            }
            replayPPVals = calc.GetPpWithSummedPp(replayScore / maxReplayScore, replayRatings);
            if (catchUpNotes > notes)
            {
                Plugin.Log.Info($"Catch up too slow, trying again (notes = {notes}, catchUpNotes = {catchUpNotes}");
                CatchupBest();
            }
            caughtUp = true;
        }
        private void UpdateBest(int notes, NoteData noteData)
        {
            if (!useReplay)
            {
                if (notes <= 1) //this value is constant, no need to update every note hit.
                    replayPPVals = calc.GetPpWithSummedPp(accToBeat / 100.0f, selectedRatings);
                return;
            } //Past here will be treating it as if the leaderboard selected is beatleader, as that is the source of the replay.
            if (notes < 1) return;
            BeatLeader.Models.Replay.NoteEvent note = noteArray[notes + bombs - 1];
            while (wallArray.Count() > 0 && wallArray.Peek().spawnTime < note.spawnTime)
            {
                if (wallArray.Dequeue().energy < 1.0f)
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
            }
#if NEW_VERSION
            NoteData.ScoringType scoringType = TheCounter.HandleWeirdNoteBehaviour(noteData);
#else
            NoteData.ScoringType scoringType = noteData.scoringType;
#endif
            switch (note.eventType)
            {
                case NoteEventType.good:
                    maxReplayScore += notes < 14 ? BLCalc.GetNoteScoreDefinition(scoringType).maxCutScore * HelpfulMath.ClampedMultiplierForNote(notes) : BLCalc.GetNoteScoreDefinition(scoringType).maxCutScore;
                    replayCombo++;
                    replayScore += BLCalc.GetCutScore(note) * HelpfulMath.ClampedMultiplierForNote(replayCombo);
                    break;
                case NoteEventType.bomb:
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                    bombs++;
                    UpdateBest(notes, noteData);
                    return;
                default:
                    maxReplayScore += notes < 14 ? BLCalc.GetNoteScoreDefinition(scoringType).maxCutScore * HelpfulMath.ClampedMultiplierForNote(notes) : BLCalc.GetNoteScoreDefinition(scoringType).maxCutScore;
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                    break;

            }
            //Plugin.Log.Info($"Note #{notes} ({scoringType}): {BLCalc.GetCutScore(note)} / {ScoreModel.GetNoteScoreDefinition(scoringType).maxCutScore}");
            replayPPVals = calc.GetPpWithSummedPp(replayScore / maxReplayScore, replayRatings);
            accToBeat = usingModdedAcc ? BLCalc.Instance.GetAccDeflatedUnsafe(replayPPVals[0] + replayPPVals[1] + replayPPVals[2], true, PC.DecimalPrecision, selectedRatings, accToBeat / 100.0f, 100) : (float)Math.Round(replayScore / maxReplayScore * 100.0f, PC.DecimalPrecision);
        }
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            if (failed)
            {
                backup?.UpdateCounter(acc, notes, mistakes, fcPercent, currentNote);
                return;
            }
            if (!SetupTask.IsCompleted || !caughtUp) return;
            bool displayFc = PC.PPFC && mistakes > 0, showLbl = PC.ShowLbl;
            
            float[] temp = calc.GetPpWithSummedPp(acc, selectedRatings);
            for (int i = 0; i < temp.Length; i++)
            {
                ppVals[i] = temp[i];
                ppVals[i + temp.Length] = temp[i] - replayPPVals[i];
            }
            if (displayFc)
            {
                temp = calc.GetPpWithSummedPp(fcPercent, selectedRatings);
                for (int i = temp.Length * 2; i < temp.Length * 3; i++)
                {
                    ppVals[i] = temp[i - temp.Length * 2];
                    ppVals[i + temp.Length] = ppVals[i] - replayPPVals[i - temp.Length * 2];
                }
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string target = PC.ShowEnemy ? PC.Target : Targeter.NO_TARGET;
            string color(float num) => PC.UseGrad ? HelpfulFormatter.NumberToGradient(num) : HelpfulFormatter.NumberToColor(num);
            float accDiff = (float)Math.Round(acc * 100.0f, PC.DecimalPrecision) - accToBeat;
            if (float.IsNaN(accDiff)) accDiff = 0f;
            //else if (!useReplay) accDiff -= accToBeat;
            float replayAcc = PC.DynamicAcc && useReplay ? accToBeat : staticAccToBeat;
            if (float.IsNaN(replayAcc)) replayAcc = 0f;
            if (PC.SplitPPVals && calc.RatingCount > 1)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += displayFormatter.Invoke(displayFc, PC.ExtraInfo && i == 3, mistakes, accDiff.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), color(ppVals[i + displayNum]), ppVals[i + displayNum].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i],
                        color(ppVals[i + displayNum * 3]), ppVals[i + displayNum * 3].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i + displayNum * 2], replayAcc, TheCounter.CurrentLabels[i]) + "\n";
                display.text = text;
            }
            else
                display.text = displayFormatter.Invoke(displayFc, PC.ExtraInfo, mistakes, accDiff.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), color(ppVals[displayNum * 2 - 1]), ppVals[displayNum * 2 - 1].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[displayNum - 1],
                    color(ppVals[displayNum * 4 - 1]), ppVals[displayNum * 4 - 1].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[displayNum * 3 - 1], replayAcc, TheCounter.CurrentLabels.Last()) + "\n";
        }
        public void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            if (!SetupTask.IsCompleted || !caughtUp)
            {
                catchUpNotes = notes;
                if (!caughtUp) //This check is done twice because we are dealing with multi thread communication.
                    return;
            }
            if (!failed) UpdateBest(notes, currentNote);
        }
#endregion
    }
}
