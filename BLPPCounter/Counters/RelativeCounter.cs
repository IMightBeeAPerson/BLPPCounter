using BeatLeader.Models.Replay;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Containers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;

namespace BLPPCounter.Counters
{
    public class RelativeCounter: MyCounters
    {
        #region Static Variables
        public static int OrderNumber => 2;
        public static string DisplayName => "Relative";
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public static string DisplayHandler => DisplayName;
        private static string format;
        private static Formatter formatter;
        private static Printer displayFormatter;
        private static FormatWrapper displayWrapper;
        private static PluginConfig PC => PluginConfig.Instance;
        public static readonly Dictionary<string, char> FormatAlias = new()
                {
                    { "Acc Difference", 'd' },
                    { "PP Difference", 'x' },
                    { "PP", 'p' },
                    { "Label", 'l' },
                    { "FCPP Difference", 'y' },
                    { "FCPP", 'o' },
                    { "Accuracy", 'a' },
                    { "Target", 't' },
                    { "Mistakes", 'e' },
                    { "Mistake Color", 'z' }
                };
        internal static readonly FormatRelation DefaultFormatRelation = new("Main Format", DisplayName,
            PC.FormatSettings.RelativeTextFormat, str => PC.FormatSettings.RelativeTextFormat = str, FormatAlias,
            new Dictionary<char, string>()
            {
                { 'd', "This will show the difference in percentage at the current moment between you and the replay you're comparing against" },
                { 'x', "The unmodified PP number" },
                { 'p', "The modified PP number (plus/minus value)" },
                { 'l', "Must use as a group value, and will color everything inside group" },
                { 'y', "The modified PP number if the map was FC'ed" },
                { 'o', "The unmodified PP number if the map was FC'ed" },
                { 'a', "The label (ex: PP, Tech PP, etc)" },
                { 't', "The amount of mistakes made in the map. This includes bomb and wall hits" },
                { 'e', "This will either be the targeting message or nothing, depending on if the user has enabled show enemies and has selected a target" },
                { 'z', "Color for mistakes compared to your replay mistakes" }
            }, FormatRelation.FormatDisplayer(SetupDefaultFormatter, FormatAlias),
            new FormatWrapper(new Dictionary<char, object>()
            {
                {(char)1, true },
                {(char)2, true },
                {'e', 1 },
                {'d', 0.1f },
                {'x', -30.5f },
                {'p', 543.21f },
                {'y', 21.21f },
                {'o', 654.32f },
                {'a', 99.54f },
                {'l', "PP" },
                {'t', "Person" },
                {'z', "yellow" }
            }), Tokens.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(2)
            {
                {'a', 0 },
                {'t', 1 }
            },
            [
                FormatRelation.CreateFunc("{0}%", "{0}"),
                FormatRelation.CreateFunc("Targeting <color=red>{0}</color>")
            ],
            new Dictionary<char, IEnumerable<(string, object)>>(6)
            { //default values: IsInteger = false, MinVal = -1.0f, MaxVal = -1.0f, IncrementVal = -1.0f
                { 'd', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 1.5f), } },
                { 'x', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10), } },
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'y', new (string, object)[3] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10), } },
                { 'o', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'a', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 100), ("IncrementVal", 0.5f), } }
            },
            [
                ((char)1, "Has a miss"),
                ((char)2, "Is bottom of text")
            ]
            );
        private static Task SetupTask = Task.CompletedTask;
        private static bool displayPP;

        #endregion
        #region Variables
        public override string Name => DisplayName;
        public string ReplayMods { get; private set; } = "";

        private float accToBeat, staticAccToBeat;
        private PPContainer replayPPVals;
        private RatingContainer replayRatings;
        private float replayScore, maxReplayScore;
        private int replayCombo;
        private Replay bestReplay;
        private NoteEvent[] noteArray;
        private Queue<WallEvent> wallArray;
        private int bombs, replayMistakes;
        private MyCounters backup;
        private bool failed, useReplay;
        private bool caughtUp, usingModdedAcc;
        private int catchUpNotes;
        private string missColor;
        #endregion
        #region Init
        public RelativeCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            failed = false;
            useReplay = PC.UseReplay;
            //displayNum = calc.DisplayRatingCount;
            ResetVars();
        }
        private async Task<JToken> SetupReplayData(MapSelection map, CancellationToken ct = default)
        {
            string mode = calc.Leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard";
            byte[] replayData = null;
            JToken data = null;

            if (PC.LocalReplays) {
                try
                {
                    string replayName = LocalReplayHandler.GetReplayName(Targeter.TargetID, map.Map.Hash, mode, map.Difficulty.ToString());
                    if (replayName is not null)
                    {
                        //Plugin.Log.Info($"Loading local replay for player {Targeter.TargetName} at path: {replayName}");
                        string path = LocalReplayHandler.GetReplayPath(replayName) ?? throw new Exception("The local file found in header does not exist.");
                        replayData = File.ReadAllBytes(path);
                        if (!PC.LocalReplaysOnly)
                        {
                            //check if the replay is outdated
                            if (int.TryParse(await BLAPI.Instance.CallAPI_String(string.Format(HelpfulPaths.BLAPI_SCOREVALUE, Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), mode), ct: ct), out int score) &&
                                ReplayDecoder.TryDecodeReplayInfo(replayData, out ReplayInfo info) &&
                                info.score < score)
                            {
                                Plugin.Log.Warn("Local replay is outdated compared to online score, using online replay instead.");
                                replayData = null;
                                goto skip; //outdated replay, use online one instead
                            }
                        }
                        ReplayDecoder.TryDecodeReplay(replayData, out bestReplay);
                        //Plugin.Log.Info("Non speed mods: " + HelpfulMisc.HasNonSpeedMods(bestReplay.info.modifiers));
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
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Warn("There was an error loading the local replay.");
                    Plugin.Log.Warn(e.Message);
                    Plugin.Log.Debug(e);
                }
            }
        skip:
            if (PC.LocalReplaysOnly && (data is null || replayData is null))
            {
                useReplay = false;
                return null;
            }

            data ??= await BLAPI.Instance.GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), mode, true, ct);
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

            noteArray = [.. bestReplay.notes];

            //Plugin.Log.Info($"Replay Score: {bestReplay.info.score}, Max Score: {HelpfulMath.MaxScoreForNotes(bestReplay.notes.Count)}, Accuracy: {(float)bestReplay.info.score / HelpfulMath.MaxScoreForNotes(bestReplay.notes.Count) * 100f}%\n{new string('-', 30)}");
            //Plugin.Log.Info($"Calculated Replay Score: {HelpfulMath.GetTotalScoreFromNotes(noteArray)}, Calculated Max Score: {HelpfulMath.GetMaxScoreFromNotes(noteArray)}, Calculated Accuracy: {HelpfulMath.GetAccuracyFromNotes(noteArray)}");

            wallArray = new Queue<WallEvent>(bestReplay.walls);
            ReplayMods = bestReplay.info.modifiers.ToUpper();
            usingModdedAcc = false;
            if (calc.Leaderboard == Leaderboards.Beatleader)
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
            replayPPVals = new PPContainer(calc.DisplayRatingCount, 0f, precision: PC.DecimalPrecision);
            return data;
        }
        #endregion
        #region Overrides
        public override void SetupData(MapSelection map, CancellationToken ct)
        {
            caughtUp = false;
            catchUpNotes = 0;
            ppHandler = new PPHandler(ratings, calc, PC.DecimalPrecision, 2, (rating, acc, in main, ref toChange, _) => PPContainer.SubtractFast(in main, in replayPPVals, ref toChange))
            {
                UpdateFCEnabled = PC.PPFC,
                UpdatePPEnabled = displayPP
            };
            ppHandler.UpdateFC += (fcAcc, vals, actions, _) =>
            {
                vals[2].SetValues(calc.GetPpWithSummedPp(fcAcc, PC.DecimalPrecision));
                actions(0, fcAcc, in vals[2], ref vals[3], _);
            };
            ppHandler.UpdateMistakes += (mistakes) =>
            {
                missColor = HelpfulFormatter.NumberToColor(replayMistakes - mistakes);
            };
            missColor = HelpfulFormatter.NumberToColor(1);
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
                //Plugin.Log.Info($"Data: {HelpfulMisc.Print(new object[] { Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), calc.Leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true })}");
                JToken playerData = PC.UseReplay ? 
                    await SetupReplayData(map, ct) :
                    await APIHandler.GetSelectedAPI().GetScoreData(Targeter.TargetID, map.Map.Hash, map.Difficulty.ToString(), calc.Leaderboard == Leaderboards.Beatleader ? map.Mode : "Standard", true).ConfigureAwait(false);
                if (playerData is null)
                {
                    Plugin.Log.Warn("Relative counter cannot be loaded due to the player never having played this map before! (API didn't return the corrent status and/or local replay doesn't exist)");
                    goto Failed;
                }
                if ((float)playerData["pp"] is float thePP && thePP > 0)
                    accToBeat = calc.GetAcc(thePP, ratings, PC.DecimalPrecision);
                else
                {
                    accToBeat = calc.GetAccDeflated(calc.GetSummedPp((float)playerData["accuracy"],
                        calc.Leaderboard == Leaderboards.Beatleader ?
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
                backup = TheCounter.InitCounter(PC.RelativeDefault, Display);
                if (catchUpNotes < 1) backup.UpdateCounter(1, 0, 0, 1, null);
            }
            else
                TheCounter.CancelCounter();
        }
        public new void ReinitCounter(TMP_Text display)
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
        public new void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {
            base.ReinitCounter(display, ratingVals);
            if (failed)
            {
                if (backup is null)
                    TheCounter.CancelCounter();
                else backup.ReinitCounter(display, ratingVals);
            }
            else ResetVars();
        }
        public override void ReinitCounter(MapSelection map)
        { 
            failed = false;
        }
        public override void UpdateFormat() => InitDefaultFormat();
        public static bool InitFormat()
        {
            if (formatter is null && TheCounter.TargetUsable) FormatTheFormat(PC.FormatSettings.RelativeTextFormat);
            return displayFormatter != null && TheCounter.TargetUsable;
        }
        public static void ResetFormat()
        {
            formatter = null;
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
        public static void FormatTheFormat(string format)
        {
            RelativeCounter.format = format;
            InitDefaultFormat();
        }
        public static void InitDefaultFormat()
        {
            displayWrapper ??= GetDefaultWrapper();

            formatter = SetupDefaultFormatter(format, displayWrapper, FormatAlias);

            displayFormatter = formatter.GetOutput();

            displayPP = displayFormatter.UsedKeys.ContainsAny('p', 'x');
        }
        public static FormatWrapper GetDefaultWrapper() => new((typeof(bool), (char)1), (typeof(bool), (char)2), (typeof(int), 'e'), (typeof(string), 'z'),
                (typeof(string), 'd'), (typeof(float), 'x'), (typeof(float), 'p'), (typeof(float), 'y'), (typeof(float), 'o'), (typeof(float), 'a'), (typeof(string), 'l'));
        internal static Formatter SetupDefaultFormatter(string format, FormatWrapper values = null, Dictionary<string, char> alias = null)
        {
            Formatter outp = new(TokenParser.ParseTokens(format, alias), values ?? GetDefaultWrapper());

            if (!PC.ShowLbl) outp.SetTokenToConstantValue('l');
            if (!PC.Target.Equals(Targeter.NO_TARGET) && PC.ShowEnemy)
            {
                string theMods = "";
                if (TheCounter.theCounter is RelativeCounter rc2) theMods = rc2.ReplayMods;
                outp.SetTokenToConstantValue('t', TheCounter.TargetFormatter(PC.Target.ClampString(PC.MaxNameLength), theMods));
            }
            else outp.SetTokenToConstantValue('t');

            outp.SurroundTokens("$", "</color>", 'z');
            outp.FlagTokensForToString('x', 'y', 'd');
            outp.PromiseValueForAllTokens();

            return outp;
        }
        private string DisplayFormatter(bool fc, bool totPp, int mistakes, string missColor, float accDiff, float modPp, float regPp,
            float fcModPp, float fcRegPp, float acc, string label)
        {
            displayWrapper.SetValues(((char)1, fc), ((char)2, totPp), ('e', mistakes), ('d', accDiff), ('x', modPp), ('p', regPp),
                ('y', fcModPp), ('o', fcRegPp), ('a', acc), ('l', label), ('z', missColor));
            return displayFormatter.Print();
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
                replayPPVals.SetValues(calc.GetPpWithSummedPp(accToBeat / 100.0f));
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
            replayPPVals.SetValues(calc.GetPpWithSummedPp(replayScore / maxReplayScore, replayRatings));
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
                    replayPPVals.SetValues(calc.GetPpWithSummedPp(accToBeat / 100.0f));
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
            replayPPVals.SetValues(calc.GetPpWithSummedPp(replayScore / maxReplayScore, replayRatings));
            accToBeat = usingModdedAcc ? BLCalc.Instance.GetAccDeflatedUnsafe(replayPPVals.AccPP + replayPPVals.PassPP + replayPPVals.TechPP, PC.DecimalPrecision, ratings.SelectedRatings, accToBeat / 100.0f) : (float)Math.Round(replayScore / maxReplayScore * 100.0f, PC.DecimalPrecision);
        }
        public override void UpdateCounterInternal(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            if (failed)
            {
                backup?.UpdateCounter(acc, notes, mistakes, fcPercent, currentNote);
                return;
            }
            if (!SetupTask.IsCompleted || !caughtUp) return;

            ppHandler.Update(acc, mistakes, fcPercent);

            float accDiff = (float)Math.Round(acc * 100.0f, PC.DecimalPrecision) - accToBeat;
            if (float.IsNaN(accDiff)) accDiff = 0f;
            //else if (!useReplay) accDiff -= accToBeat;
            float replayAcc = PC.DynamicAcc && useReplay ? accToBeat : staticAccToBeat;
            //Plugin.Log.Info("ppVals: " + HelpfulMisc.Print(ppHandler));
            if (float.IsNaN(replayAcc)) replayAcc = 0f;
            if (PC.SplitPPVals && calc.RatingCount > 1)
            {
                for (int i = 0; i < 4; i++)
                    outpText.AppendLine(DisplayFormatter(ppHandler.DisplayFC, PC.ExtraInfo && i == 3, mistakes, missColor, accDiff, ppHandler[1, i], ppHandler[0, i],
                        ppHandler[3, i], ppHandler[2, i], replayAcc, TheCounter.CurrentLabels[i]));
            }
            else
                outpText.AppendLine(DisplayFormatter(ppHandler.DisplayFC, PC.ExtraInfo, mistakes, missColor, accDiff, ppHandler[1], ppHandler[0],
                    ppHandler[3], ppHandler[2], replayAcc, TheCounter.CurrentLabels.Last()));
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
