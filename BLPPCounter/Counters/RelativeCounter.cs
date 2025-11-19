using BeatLeader.Models.Replay;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;

namespace BLPPCounter.Counters
{
    public class RelativeCounter: MyCounters
    {
        #region Static Variables
        public static int OrderNumber => 2;
        public static string DisplayName => "Relative";
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public static string DisplayHandler => DisplayName;
        //private static Func<bool, bool, int, string, string, string, string, float, string, string, float, float, string, string> displayFormatter;
        private static Func<FormatWrapper, string> displayFormatter;
        private static Func<Func<FormatWrapper, string>> displayIniter;
        private static FormatWrapper displayWrapper;
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
                    { "Mistakes", 'e' },
                    { "Mistake Color", 'z' }
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
                { 'e', "This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target" },
                { 'z', "Color for mistakes compared to your replay mistakes" }
            }, str => { var hold = GetTheFormat(str, out string errorStr, false); return (hold, errorStr); },
            new FormatWrapper(new Dictionary<char, object>(13)
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
                {'t', "Person" },
                {'z', "yellow" }
            }), HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(7)
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
        public override string Name => DisplayName;
        public string ReplayMods { get; private set; }

        private float accToBeat, staticAccToBeat;
        private float[] replayPPVals;
        private RatingContainer replayRatings;
        private float replayScore, maxReplayScore;
        private int replayCombo;
        private Replay bestReplay;
        private NoteEvent[] noteArray;
        private Queue<WallEvent> wallArray;
        private int bombs, replayMistakes;
        private MyCounters backup;
        private bool failed, useReplay;
        private Leaderboards leaderboard;
        private float[] ppVals;
        private bool caughtUp, usingModdedAcc;
        private int catchUpNotes, displayNum;
        #endregion
        #region Init
        public RelativeCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            failed = false;
            useReplay = PC.UseReplay;
            calc = Calculator.GetSelectedCalc(); //only need to set it here bc when Calculator changes, this class instance gets remade.
            displayNum = calc.DisplayRatingCount;
            leaderboard = TheCounter.Leaderboard;
            ppVals = new float[displayNum * 4]; //16 for bl (displayRatingCount bc gotta store total pp as well)
            ResetVars();
        }
        private async Task<JToken> SetupReplayData(MapSelection map, CancellationToken ct = default)
        {
            string mode = leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard";
            byte[] replayData = null;
            JToken data = null;

            if (PC.LocalReplays) {
                string replayName = LocalReplayHandler.GetReplayName(Targeter.TargetID, map.Map.Hash, mode, map.Difficulty.ToString());//, data["song"]["name"].ToString());
                if (replayName != null)
                {
                    //Plugin.Log.Info($"Loading local replay for player {Targeter.TargetName} at path: {replayName}");
                    string path = Path.Combine(HelpfulPaths.BL_REPLAY_FOLDER, replayName);
                    try
                    {
                        replayData = File.Exists(path) ? File.ReadAllBytes(path) : File.ReadAllBytes(Path.Combine(HelpfulPaths.BL_REPLAY_CACHE_FOLDER, replayName));
                        ReplayDecoder.TryDecodeReplay(replayData, out bestReplay);
                        if (!HelpfulMisc.HasNonSpeedMods(bestReplay.info.modifiers))
                        {
                            float acc = (float)bestReplay.info.score / HelpfulMath.GetMaxScoreFromNotes(bestReplay.notes);
                            data = new JObject
                            {
                                { "accuracy", acc },
                                { "pp", calc.Inflate(calc.GetSummedPp(acc)) },
                                { "modifiers", bestReplay.info.modifiers }
                            };
                        } 
                    } catch (Exception e)
                    {
                        Plugin.Log.Warn($"There was an error loading the local replay at path: {path}");
                        Plugin.Log.Warn(e.Message);
                        Plugin.Log.Debug(e);
                    }
                }
            }

            if (data is null)
                data = await BLAPI.Instance.GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), mode, true, ct);
            if (replayData is null)
            {
                if (data is null)
                {
                    useReplay = false;
                    return null;
                }
                replayData = await BLAPI.Instance.CallAPI_Bytes(data["replay"].ToString(), true, ct: ct) ?? throw new Exception("The replay link from the API is bad! (replay link failed to return data)");
                ReplayDecoder.TryDecodeReplay(replayData, out bestReplay);
            }

            noteArray = bestReplay.notes.ToArray();
            wallArray = new Queue<WallEvent>(bestReplay.walls);
            ReplayMods = bestReplay.info.modifiers.ToUpper();
            usingModdedAcc = false;
            if (leaderboard == Leaderboards.Beatleader)
            {
                var (mod, replayMult) = HelpfulMisc.ParseModifiers(ReplayMods, map.MapData.diffData);
                //replayRatings = RatingContainer.GetContainer(leaderboard, HelpfulPaths.GetAllRatingsOfSpeed(data, calc, mod).Select(num => num * replayMult).ToArray());
                replayRatings = HelpfulPaths.GetAllRatingsOfSpeed(map.MapData.diffData, calc, mod);
                replayRatings.MultiplyRatings(replayMult);
                usingModdedAcc = PC.ReplayMods && !ratings.Equals(replayRatings);
            }
            else
                replayRatings = ratings;
            //Plugin.Log.Info($"Replay Ratings: \n{replayRatings}");
            replayPPVals = new float[calc.DisplayRatingCount];
            return data;
        }
        #endregion
        #region Overrides
        public override void SetupData(MapSelection map, CancellationToken ct)
        {
            caughtUp = false;
            catchUpNotes = 0;
            Task.Run(async () =>
            {
                SetupTask = SetupDataAsync(map, ct);
                await SetupTask;
                CatchupBest();
                if (catchUpNotes == 0)
                    UpdateCounter(1, 0, 0, 1, null);
            }, ct);
        }
        private async Task SetupDataAsync(MapSelection map, CancellationToken ct)
        {
            try
            {
                //Plugin.Log.Info($"Data: {HelpfulMisc.Print(new object[] { Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true })}");
                JToken playerData = null;
                if (PC.UseReplay) playerData = await SetupReplayData(map, ct);
                if (playerData is null)
                    playerData = await APIHandler.GetSelectedAPI().GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true).ConfigureAwait(false);
                if (playerData is null)
                {
                    Plugin.Log.Warn("Relative counter cannot be loaded due to the player never having played this map before! (API didn't return the corrent status)");
                    goto Failed;
                }
                if ((float)playerData["pp"] is float thePP && thePP > 0)
                    accToBeat = calc.GetAcc(thePP, ratings, PC.DecimalPrecision);
                else
                {
                    accToBeat = calc.GetAccDeflated(calc.GetSummedPp((float)playerData["accuracy"],
                        leaderboard == Leaderboards.Beatleader ?
                        HelpfulPaths.GetAllRatingsOfSpeed(map.MapData.diffData, HelpfulMisc.GetSongSpeed(playerData["modifiers"].ToString())) : ratings),
                        ratings, PC.DecimalPrecision);
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
        public override void ReinitCounter(TMP_Text display)
        {
            base.ReinitCounter(display);
            if (failed)
            {
                if (backup is null)
                    TheCounter.CancelCounter();
                else backup.ReinitCounter(display);
            }
            else ResetVars();
        }
        public override void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {
            base.ReinitCounter(display, ratingVals);
            displayNum = calc.DisplayRatingCount;
            leaderboard = TheCounter.Leaderboard;
            ppVals = new float[displayNum * 4]; //16 for bl (displayRatingCount bc gotta store total pp as well)
            if (failed)
            {
                if (backup is null)
                    TheCounter.CancelCounter();
                else backup.ReinitCounter(display, ratingVals);
            }
            else ResetVars();
        }
        public override void ReinitCounter(TMP_Text display, MapSelection map)
        { 
            failed = false;
            base.ReinitCounter(display, map);
        }
        public override void UpdateFormat() => InitDefaultFormat();
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
            replayMistakes = 0;
            replayScore = 0;
            replayCombo = 0;
            maxReplayScore = 0;
        }
        #endregion
        #region Helper Functions
        public static Func<Func<FormatWrapper, string>> GetTheFormat(string format, out string errorMessage, bool applySettings = true)
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
                    HelpfulFormatter.SurroundText(tokensCopy, 'z', $"{vals['z']}", "</color>");
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1');
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2');
                }, out errorMessage, applySettings);//this is one line of code lol
        }
        public static void FormatTheFormat(string format) => displayIniter = GetTheFormat(format, out string _);
        public static void InitDefaultFormat()
        {
            displayFormatter = displayIniter.Invoke();
            displayWrapper = new FormatWrapper((typeof(bool), (char)1), (typeof(bool), (char)2), (typeof(int), 'e'), (typeof(string), 'z'),
                (typeof(string), 'd'), (typeof(string), 'c'), (typeof(string), 'x'), (typeof(float), 'p'),
                (typeof(string), 'f'), (typeof(string), 'y'), (typeof(float), 'o'), (typeof(float), 'a'),
                (typeof(string), 'l'));
        }
        private string DisplayFormatter(bool fc, bool totPp, int mistakes, string missColor, string accDiff, string color, string modPp, float regPp,
            string fcCol, string fcModPp, float fcRegPp, float acc, string label)
        {
            displayWrapper.SetValues(((char)1, fc), ((char)2, totPp), ('e', mistakes), ('d', accDiff), ('c', color), ('x', modPp), ('p', regPp),
                ('f', fcCol), ('y', fcModPp), ('o', fcRegPp), ('a', acc), ('l', label), ('z', missColor));
            return displayFormatter.Invoke(displayWrapper);
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
                replayPPVals = calc.GetPpWithSummedPp(accToBeat / 100.0f);
                caughtUp = true;
                return;
            }
            NoteEvent note;
            int notes = 1;
            for (; notes <= catchUpNotes; notes++)
            {
                note = noteArray[notes + bombs - 1];
                switch (note.eventType)
                {
                    case NoteEventType.good:
                        maxReplayScore += notes < 14 ? Calculator.GetMaxCutScore(note) * HelpfulMath.ClampedMultiplierForNote(notes) : Calculator.GetMaxCutScore(note);
                        replayCombo++;
                        replayScore += Calculator.GetCutScore(note) * HelpfulMath.ClampedMultiplierForNote(replayCombo);
                        break;
                    case NoteEventType.bomb:
                        replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                        bombs++;
                        replayMistakes++;
                        notes--;
                        continue;
                    default:
                        maxReplayScore += notes < 14 ? Calculator.GetMaxCutScore(note) * HelpfulMath.ClampedMultiplierForNote(notes) : Calculator.GetMaxCutScore(note); 
                        replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                        replayMistakes++;
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
                    replayPPVals = calc.GetPpWithSummedPp(accToBeat / 100.0f);
                return;
            } //Past here will be treating it as if the leaderboard selected is beatleader, as that is the source of the replay.
            if (notes < 1 || notes + bombs - 1 >= noteArray.Length) return;
            NoteEvent note = noteArray[notes + bombs - 1];
            while (wallArray.Count() > 0 && wallArray.Peek().spawnTime < note.spawnTime)
                if (wallArray.Dequeue().energy < 1.0f)
                {
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                    replayMistakes++;
                }
#if NEW_VERSION
            NoteData.ScoringType scoringType = TheCounter.HandleWeirdNoteBehaviour(noteData);
#else
            NoteData.ScoringType scoringType = noteData.scoringType;
#endif
            switch (note.eventType)
            {
                case NoteEventType.good:
                    maxReplayScore += notes < 14 ? Calculator.GetNoteScoreDefinition(scoringType).maxCutScore * HelpfulMath.ClampedMultiplierForNote(notes) : Calculator.GetNoteScoreDefinition(scoringType).maxCutScore;
                    replayCombo++;
                    replayScore += Calculator.GetCutScore(note) * HelpfulMath.ClampedMultiplierForNote(replayCombo);
                    break;
                case NoteEventType.bomb:
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                    bombs++;
                    replayMistakes++;
                    UpdateBest(notes, noteData);
                    return;
                default:
                    maxReplayScore += notes < 14 ? Calculator.GetNoteScoreDefinition(scoringType).maxCutScore * HelpfulMath.ClampedMultiplierForNote(notes) : Calculator.GetNoteScoreDefinition(scoringType).maxCutScore;
                    replayCombo = HelpfulMath.DecreaseMultiplier(replayCombo);
                    replayMistakes++;
                    break;

            }
            //Plugin.Log.Info($"Note #{notes} ({scoringType}): {BLCalc.GetCutScore(note)} / {ScoreModel.GetNoteScoreDefinition(scoringType).maxCutScore}");
            //Plugin.Log.Info($"Note #{notes}: {replayScore} / {maxReplayScore} ({Math.Round(replayScore / maxReplayScore * 100f, PC.DecimalPrecision)}%)");
            replayPPVals = calc.GetPpWithSummedPp(replayScore / maxReplayScore, replayRatings);
            accToBeat = usingModdedAcc ? BLCalc.Instance.GetAccDeflatedUnsafe(replayPPVals[0] + replayPPVals[1] + replayPPVals[2], PC.DecimalPrecision, ratings.SelectedRatings, accToBeat / 100.0f) : (float)Math.Round(replayScore / maxReplayScore * 100.0f, PC.DecimalPrecision);
        }
        public override void UpdatePP(float acc)
        {
            float[] temp = calc.GetPpWithSummedPp(acc, PC.DecimalPrecision);
            for (int i = 0; i < temp.Length; i++)
            {
                ppVals[i] = temp[i];
                ppVals[i + temp.Length] = (float)Math.Round(temp[i] - replayPPVals[i], PC.DecimalPrecision);
            }
        }
        public override void UpdateFCPP(float fcPercent)
        {
            float[] temp = calc.GetPpWithSummedPp(fcPercent);
            for (int i = temp.Length * 2; i < temp.Length * 3; i++)
            {
                ppVals[i] = (float)Math.Round(temp[i - temp.Length * 2], PC.DecimalPrecision);
                ppVals[i + temp.Length] = (float)Math.Round(ppVals[i] - replayPPVals[i - temp.Length * 2]);
            }
        }
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            if (failed)
            {
                backup?.UpdateCounter(acc, notes, mistakes, fcPercent, currentNote);
                return;
            }
            if (!SetupTask.IsCompleted || !caughtUp) return;
            bool displayFc = PC.PPFC && mistakes > 0, showLbl = PC.ShowLbl;

            UpdatePP(acc);
            if (displayFc) UpdateFCPP(fcPercent);

            string color(float num) => PC.UseGrad ? HelpfulFormatter.NumberToGradient(num) : HelpfulFormatter.NumberToColor(num);
            string missColor(int miss, int replayMiss) => HelpfulFormatter.NumberToColor(miss == 0 && replayMiss == 0 ? 1 : replayMiss - miss);
            float accDiff = (float)Math.Round(acc * 100.0f, PC.DecimalPrecision) - accToBeat;
            if (float.IsNaN(accDiff)) accDiff = 0f;
            //else if (!useReplay) accDiff -= accToBeat;
            float replayAcc = PC.DynamicAcc && useReplay ? accToBeat : staticAccToBeat;
            if (float.IsNaN(replayAcc)) replayAcc = 0f;
            if (PC.SplitPPVals && calc.RatingCount > 1)
            {
                string text = "";
                for (int i = 0; i < 4; i++)
                    text += DisplayFormatter(displayFc, PC.ExtraInfo && i == 3, mistakes, missColor(mistakes, replayMistakes), accDiff.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), color(ppVals[i + displayNum]), ppVals[i + displayNum].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i],
                        color(ppVals[i + displayNum * 3]), ppVals[i + displayNum * 3].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[i + displayNum * 2], replayAcc, TheCounter.CurrentLabels[i]) + "\n";
                display.text = text;
            }
            else
                display.text = DisplayFormatter(displayFc, PC.ExtraInfo, mistakes, missColor(mistakes, replayMistakes), accDiff.ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), color(ppVals[displayNum * 2 - 1]), ppVals[displayNum * 2 - 1].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[displayNum - 1],
                    color(ppVals[displayNum * 4 - 1]), ppVals[displayNum * 4 - 1].ToString(HelpfulFormatter.NUMBER_TOSTRING_FORMAT), ppVals[displayNum * 3 - 1], replayAcc, TheCounter.CurrentLabels.Last()) + "\n";
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
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
