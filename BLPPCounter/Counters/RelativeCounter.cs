using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatLeader.Models.Replay;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using Newtonsoft.Json.Linq;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Counters
{
    public sealed class RelativeCounter : MyCounters
    {
        #region Variables
        public static int OrderNumber => 2;

        public static string DisplayName => "Relative";

        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        public static string DisplayHandler => DisplayName;

        private BLAPI _beatLeaderApi = null!;
        private ILeaderboardApiProvider _apiProvider = null!;
        private ITargeter _targeter = null!;
        private IRelativeCounterFormattingService _formatting = null!;
        private IMainCounterFormattingService _mainFormatting = null!;
        private IBackgroundTaskRunner _taskRunner = null!;
        private PathUtils _pathUtils = null!;
        private BLCalc _beatLeaderCalculator = null!;
        private ILocalReplayService _localReplayService = null!;

        private Task<bool> _setupTask = Task.FromResult(false);

        private float _accuracyToBeat;
        private float _staticAccuracyToBeat;
        private PPContainer _replayPpValues;
        private RatingContainer _replayRatings;

        private float _replayScore;
        private float _maximumReplayScore;

        private int _replayCombo;
        private int _bombs;
        private int _replayMistakes;
        private int _catchUpNotes;

        private Replay? _bestReplay;
        private NoteEvent[]? _noteArray;
        private Queue<WallEvent>? _wallArray;

        private bool _useReplay;
        private bool _caughtUp;
        private bool _usingModifiedAccuracy;

        private string _mistakeColor = "";

        public override string Name => DisplayName;

        public string ReplayMods { get; private set; } = "";

        #endregion
        #region Init

        [Inject]
        private void ConstructRelativeCounter(
            BLAPI beatLeaderApi,
            ILeaderboardApiProvider apiProvider,
            ITargeter targeter,
            IRelativeCounterFormattingService formatting,
            IMainCounterFormattingService mainFormatting,
            IBackgroundTaskRunner taskRunner,
            PathUtils pathUtils,
            BLCalc beatLeaderCalculator,
            ILocalReplayService localReplayService
        )
        {
            _beatLeaderApi = beatLeaderApi ?? throw new ArgumentNullException(nameof(beatLeaderApi));

            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _formatting = formatting ?? throw new ArgumentNullException(nameof(formatting));

            _mainFormatting = mainFormatting ?? throw new ArgumentNullException(nameof(mainFormatting));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));

            _pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));

            _beatLeaderCalculator =
                beatLeaderCalculator ?? throw new ArgumentNullException(nameof(beatLeaderCalculator));

            _localReplayService = localReplayService ?? throw new ArgumentNullException(nameof(localReplayService));
        }

        public RelativeCounter()
        {
            ResetReplayState();
        }

        private async Task<JToken?> SetupReplayDataAsync(MapSelection map, CancellationToken cancellationToken)
        {
            string mode = CurrentLeaderboard == Leaderboards.Beatleader ? map.Mode : Profile.DEFAULT_MODE;

            byte[]? replayData = null;
            JToken? scoreData = null;

            if (pc.LocalReplays)
            {
                try
                {
                    string? replayName = await _localReplayService
                        .GetReplayNameAsync(
                            _targeter.Target.TargetID,
                            map.Hash,
                            mode,
                            map.Difficulty.ToString(),
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (!BetterString.IsNullOrWhiteSpace(replayName))
                    {
                        string path =
                            _localReplayService.GetReplayPath(replayName)
                            ?? throw new InvalidOperationException(
                                "The replay header references a " + "missing local file."
                            );

                        replayData = await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);

                        if (!pc.LocalReplaysOnly)
                        {
                            string? scoreValue = await _beatLeaderApi
                                .CallAPI_String(
                                    string.Format(
                                        HelpfulPaths.BLAPI_SCOREVALUE,
                                        _targeter.Target.TargetID,
                                        map.Hash,
                                        map.Difficulty,
                                        mode
                                    ),
                                    quiet: true,
                                    ct: cancellationToken
                                )
                                .ConfigureAwait(false);

                            if (
                                int.TryParse(scoreValue, out int onlineScore)
                                && ReplayDecoder.TryDecodeReplayInfo(replayData, out ReplayInfo? replayInfo)
                                && replayInfo is not null
                                && replayInfo.score < onlineScore
                            )
                            {
                                Plugin.Log.Warn(
                                    "The local replay is older than the " + "online score. Using the online replay."
                                );

                                replayData = null;
                            }
                        }

                        if (
                            replayData is not null
                            && ReplayDecoder.TryDecodeReplay(replayData, out Replay? localReplay)
                            && localReplay is not null
                        )
                        {
                            _bestReplay = localReplay;

                            if (!HelpfulMisc.HasNonSpeedMods(localReplay.info.modifiers))
                            {
                                int maximumScore = HelpfulMath.GetMaxScoreFromNotes(localReplay.notes);

                                if (maximumScore > 0)
                                {
                                    float accuracy = localReplay.info.score / (float)maximumScore;

                                    scoreData = new JObject
                                    {
                                        { "accuracy", accuracy },
                                        { "pp", calc.Inflate(calc.GetSummedPp(accuracy)) },
                                        { "modifiers", localReplay.info.modifiers },
                                    };
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Warn("There was an error loading the local replay.");

                    Plugin.Log.Debug(exception);

                    replayData = null;
                    _bestReplay = null;
                }
            }

            if (pc.LocalReplaysOnly && (scoreData is null || replayData is null))
            {
                _useReplay = false;
                return null;
            }

            scoreData ??= await _beatLeaderApi
                .GetScoreData(
                    _targeter.Target.TargetID,
                    map.Hash,
                    map.Difficulty.ToString(),
                    mode,
                    quiet: true,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (replayData is null)
            {
                if (scoreData is null)
                {
                    _useReplay = false;
                    return null;
                }

                string? replayUrl = scoreData.Value<string>("replay");

                if (BetterString.IsNullOrWhiteSpace(replayUrl))
                {
                    Plugin.Log.Warn("BeatLeader returned no replay URL.");

                    _useReplay = false;
                    return null;
                }

                replayData = await _beatLeaderApi
                    .CallAPI_Bytes(replayUrl, quiet: true, ct: cancellationToken)
                    .ConfigureAwait(false);

                if (replayData is null || !ReplayDecoder.TryDecodeReplay(replayData, out _bestReplay))
                {
                    Plugin.Log.Warn("The online replay could not be decoded.");

                    _useReplay = false;
                    return null;
                }
            }

            if (
                _bestReplay is null
                && (!ReplayDecoder.TryDecodeReplay(replayData, out _bestReplay) || _bestReplay is null)
            )
            {
                _useReplay = false;
                return null;
            }

            _noteArray = [.. _bestReplay.notes];

            _wallArray = new Queue<WallEvent>(_bestReplay.walls);

            ReplayMods = _bestReplay.info.modifiers.ToUpperInvariant();

            _usingModifiedAccuracy = false;

            if (CurrentLeaderboard == Leaderboards.Beatleader)
            {
                SongSpeed speed = HelpfulMisc.GetSongSpeed(ReplayMods);

                _replayRatings = _pathUtils.GetAllRatingsOfSpeed(map.MapData.Data, Leaderboards.Beatleader, speed);

                string[] modifierNames =
                [
                    .. ReplayMods
                        .ToLowerInvariant()
                        .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                        .Where(modifier => modifier is not "fs" and not "sf" and not "ss" and not "nf"),
                ];

                float modifierMultiplier = _pathUtils.GetMultiAmounts(map.MapData.Data, modifierNames);

                _replayRatings.MultiplyRatings(modifierMultiplier);

                _usingModifiedAccuracy = pc.ReplayMods && !ratings.Equals(_replayRatings);
            }
            else
            {
                _replayRatings = ratings;
            }

            _replayPpValues = new PPContainer(calc.DisplayRatingCount, 0f, precision: pc.DecimalPrecision);

            return scoreData;
        }

        private async Task<bool> SetupAndCatchUpAsync(MapSelection map, CancellationToken cancellationToken)
        {
            bool initialized = await SetupDataAsync(map, cancellationToken).ConfigureAwait(false);

            if (!initialized)
                return false;

            cancellationToken.ThrowIfCancellationRequested();

            CatchUpReplay();
            return true;
        }

        private async Task<bool> SetupDataAsync(MapSelection map, CancellationToken cancellationToken)
        {
            try
            {
                await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

                JToken? playerData = pc.UseReplay
                    ? await SetupReplayDataAsync(map, cancellationToken).ConfigureAwait(false)
                    : await _apiProvider
                        .Get(CurrentLeaderboard)
                        .GetScoreData(
                            _targeter.Target.TargetID,
                            map.Hash,
                            map.Difficulty.ToString(),
                            CurrentLeaderboard == Leaderboards.Beatleader
                                ? map.Mode
                                : Utils.Profile_Utils.Profile.DEFAULT_MODE,
                            quiet: true,
                            ct: cancellationToken
                        )
                        .ConfigureAwait(false);

                if (playerData is null)
                {
                    Plugin.Log.Warn(
                        "Relative counter cannot load because the " + "target has no score or replay for this map."
                    );

                    return FailSetup();
                }

                float targetPp = playerData.Value<float?>("pp") ?? playerData.Value<float?>("ap") ?? 0f;

                if (targetPp > 0f)
                {
                    _accuracyToBeat = calc.GetAcc(targetPp, ratings, pc.DecimalPrecision);
                }
                else
                {
                    float? accuracy = playerData.Value<float?>("accuracy");

                    if (accuracy is null)
                    {
                        Plugin.Log.Warn("Relative counter score data contains " + "neither PP nor accuracy.");

                        return FailSetup();
                    }

                    RatingContainer sourceRatings =
                        CurrentLeaderboard == Leaderboards.Beatleader
                            ? _pathUtils.GetAllRatingsOfSpeed(
                                map.MapData.Data,
                                Leaderboards.Beatleader,
                                HelpfulMisc.GetSongSpeed(playerData.Value<string>("modifiers"))
                            )
                            : ratings;

                    float sourcePp = calc.GetSummedPp(accuracy.Value, sourceRatings);

                    _accuracyToBeat = calc.GetAccDeflated(sourcePp, ratings, pc.DecimalPrecision);
                }

                _staticAccuracyToBeat = _accuracyToBeat;

                ResetReplayProgress();
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("There was an error loading the target's " + $"replay:\n{exception}");

                return FailSetup();
            }
        }
        #endregion
        #region Overrides
        public override void SetupData(MapSelection map, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _useReplay = pc.UseReplay;
            _caughtUp = false;
            _catchUpNotes = 0;

            ResetReplayState();

            ppHandler = new PPHandler(
                ratings,
                calc,
                pc.DecimalPrecision,
                2,
                (rating, accuracy, in main, ref toChange, _) =>
                    PPContainer.SubtractFast(in main, in _replayPpValues, ref toChange)
            )
            {
                UpdateFCEnabled = pc.PPFC,
                UpdatePPEnabled = _formatting.UsesPpValues,
            };

            ppHandler.UpdateFC += (fullComboAccuracy, values, actions, multiplier) =>
            {
                values[2].SetValues(calc.GetPpWithSummedPp(fullComboAccuracy, pc.DecimalPrecision));

                actions(0, fullComboAccuracy, in values[2], ref values[3], multiplier);
            };

            ppHandler.UpdateMistakes += mistakes =>
                _mistakeColor = HelpfulFormatter.NumberToColor(_replayMistakes - mistakes);

            _mistakeColor = HelpfulFormatter.NumberToColor(1);

            _setupTask = SetupAndCatchUpAsync(map, cancellationToken);

            _taskRunner.Forget(_setupTask, "Initialize relative counter replay");
        }

        public override void ReinitCounter()
        {
            ResetReplayProgress();
        }

        public override void ReinitCounter(RatingContainer ratingValues)
        {
            ResetReplayProgress();
        }

        public override void ReinitCounter(MapSelection map)
        {
            ResetReplayProgress();
        }

        public override void UpdateFormat()
        {
            ppHandler.UpdatePPEnabled = _formatting.UsesPpValues;
        }

        #endregion
        #region Helper Functions
        private void ResetReplayState()
        {
            _replayPpValues = default;
            _replayRatings = default;

            _bestReplay = null;
            _noteArray = null;
            _wallArray = null;

            ReplayMods = "";

            ResetReplayProgress();
        }

        #endregion
        #region Helper Functions

        private static async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
        {
            const int bufferSize = 81920;

            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                useAsync: true
            );

            byte[] data = new byte[stream.Length];

            int offset = 0;

            while (offset < data.Length)
            {
                int read = await stream
                    .ReadAsync(data, offset, data.Length - offset, cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                    break;

                offset += read;
            }

            if (offset == data.Length)
                return data;

            Array.Resize(ref data, offset);

            return data;
        }

        private void ResetReplayProgress()
        {
            _bombs = 0;
            _replayMistakes = 0;
            _replayScore = 0f;
            _replayCombo = 0;
            _maximumReplayScore = 0f;
        }

        private bool FailSetup()
        {
            Plugin.Log.Warn($"Relative counter setup failed. Configured " + $"fallback counter: {pc.RelativeDefault}.");

            // Counter replacement will move behind an injected gameplay
            // runtime service when TheCounter's remaining static control
            // methods are removed.
            TheCounter.CancelCounter();

            return false;
        }

        #endregion
        #region Updates
        private void CatchUpReplay()
        {
            if (_catchUpNotes == 0)
            {
                _caughtUp = true;
                return;
            }

            if (!_useReplay || _noteArray is null)
            {
                _replayPpValues.SetValues(calc.GetPpWithSummedPp(_accuracyToBeat / 100f));

                _caughtUp = true;
                return;
            }

            Plugin.Log.Info("Catching the target replay up to real time.");

            int notesToProcess = Math.Min(_catchUpNotes, _noteArray.Length);

            for (int notes = 1; notes <= notesToProcess; notes++)
            {
                int index = notes + _bombs - 1;

                if (index < 0 || index >= _noteArray.Length)
                {
                    break;
                }

                NoteEvent replayNote = _noteArray[index];

                switch (replayNote.eventType)
                {
                    case NoteEventType.good:
                        _maximumReplayScore +=
                            Calculator.GetMaxCutScore(replayNote) * HelpfulMath.ClampedMultiplierForNote(notes);

                        _replayCombo++;

                        _replayScore +=
                            Calculator.GetCutScore(replayNote) * HelpfulMath.ClampedMultiplierForNote(_replayCombo);

                        break;

                    case NoteEventType.bomb:
                        _replayCombo = HelpfulMath.DecreaseMultiplier(_replayCombo);

                        _bombs++;
                        _replayMistakes++;
                        notes--;
                        break;

                    default:
                        _maximumReplayScore +=
                            Calculator.GetMaxCutScore(replayNote) * HelpfulMath.ClampedMultiplierForNote(notes);

                        _replayCombo = HelpfulMath.DecreaseMultiplier(_replayCombo);

                        _replayMistakes++;
                        break;
                }
            }

            if (_maximumReplayScore > 0f)
            {
                _replayPpValues.SetValues(calc.GetPpWithSummedPp(_replayScore / _maximumReplayScore, _replayRatings));
            }

            _caughtUp = true;
        }

        private void UpdateBest(int notes, NoteData noteData)
        {
            if (!_useReplay)
            {
                if (notes <= 1)
                {
                    _replayPpValues.SetValues(calc.GetPpWithSummedPp(_accuracyToBeat / 100f));
                }

                return;
            }

            if (notes < 1 || _noteArray is null || notes + _bombs - 1 >= _noteArray.Length)
            {
                return;
            }

            NoteEvent replayNote = _noteArray[notes + _bombs - 1];

            while (_wallArray is not null && _wallArray.Count > 0 && _wallArray.Peek().spawnTime < replayNote.spawnTime)
            {
                if (_wallArray.Dequeue().energy < 1f)
                {
                    _replayCombo = HelpfulMath.DecreaseMultiplier(_replayCombo);

                    _replayMistakes++;
                }
            }

#if NEW_VERSION
            NoteData.ScoringType scoringType = TheCounter.HandleWeirdNoteBehaviour(noteData);
#else
            NoteData.ScoringType scoringType = noteData.scoringType;
#endif

            switch (replayNote.eventType)
            {
                case NoteEventType.good:
                    _maximumReplayScore +=
                        notes < 14
                            ? Calculator.GetNoteScoreDefinition(scoringType).maxCutScore
                                * HelpfulMath.ClampedMultiplierForNote(notes)
                            : Calculator.GetNoteScoreDefinition(scoringType).maxCutScore;

                    _replayCombo++;

                    _replayScore +=
                        Calculator.GetCutScore(replayNote) * HelpfulMath.ClampedMultiplierForNote(_replayCombo);

                    break;

                case NoteEventType.bomb:
                    _replayCombo = HelpfulMath.DecreaseMultiplier(_replayCombo);

                    _bombs++;
                    _replayMistakes++;

                    UpdateBest(notes, noteData);

                    return;

                default:
                    _maximumReplayScore +=
                        notes < 14
                            ? Calculator.GetNoteScoreDefinition(scoringType).maxCutScore
                                * HelpfulMath.ClampedMultiplierForNote(notes)
                            : Calculator.GetNoteScoreDefinition(scoringType).maxCutScore;

                    _replayCombo = HelpfulMath.DecreaseMultiplier(_replayCombo);

                    _replayMistakes++;
                    break;
            }

            if (_maximumReplayScore <= 0f)
                return;

            _replayPpValues.SetValues(calc.GetPpWithSummedPp(_replayScore / _maximumReplayScore, _replayRatings));

            _accuracyToBeat = _usingModifiedAccuracy
                ? _beatLeaderCalculator.GetAccDeflatedUnsafe(
                    _replayPpValues.AccPP + _replayPpValues.PassPP + _replayPpValues.TechPP,
                    pc.DecimalPrecision,
                    ratings.SelectedRatings,
                    _accuracyToBeat / 100f
                )
                : (float)Math.Round(_replayScore / _maximumReplayScore * 100f, pc.DecimalPrecision);
        }

        public override void UpdateCounterInternal(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            if (!_setupTask.IsCompletedSuccessfully || !_caughtUp || currentNote is null)
            {
                return;
            }

            UpdateBest(notes, currentNote);

            ppHandler.Update(accuracy, mistakes, fullComboAccuracy);

            float accuracyDifference = (float)Math.Round(accuracy * 100f, pc.DecimalPrecision) - _accuracyToBeat;

            if (float.IsNaN(accuracyDifference))
                accuracyDifference = 0f;

            float replayAccuracy = pc.DynamicAcc && _useReplay ? _accuracyToBeat : _staticAccuracyToBeat;

            if (float.IsNaN(replayAccuracy))
                replayAccuracy = 0f;

            string target =
                pc.ShowEnemy && _targeter.IsInitialized && pc.Target != Targeter.NO_TARGET
                    ? _mainFormatting.FormatTarget(
                        _targeter.Target.TargetName.ClampString(pc.MaxNameLength),
                        ReplayMods
                    )
                    : "";

            if (pc.SplitPPVals && calc.RatingCount > 1)
            {
                int count = Math.Min(calc.DisplayRatingCount, CurrentLabels.Count);

                for (int index = 0; index < count; index++)
                {
                    outpText.AppendLine(
                        _formatting.Format(
                            ppHandler.DisplayFC,
                            pc.ExtraInfo && index == count - 1,
                            mistakes,
                            _mistakeColor,
                            accuracyDifference,
                            ppHandler[1, index],
                            ppHandler[0, index],
                            ppHandler[3, index],
                            ppHandler[2, index],
                            replayAccuracy,
                            CurrentLabels[index],
                            target
                        )
                    );
                }

                return;
            }

            string label = CurrentLabels.Count == 0 ? "" : CurrentLabels[CurrentLabels.Count - 1];

            outpText.AppendLine(
                _formatting.Format(
                    ppHandler.DisplayFC,
                    pc.ExtraInfo,
                    mistakes,
                    _mistakeColor,
                    accuracyDifference,
                    ppHandler[1],
                    ppHandler[0],
                    ppHandler[3],
                    ppHandler[2],
                    replayAccuracy,
                    label,
                    target
                )
            );
        }

        public override void SoftUpdate(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            if (_setupTask.IsCompletedSuccessfully)
                return;

            _catchUpNotes = notes;
        }

        public override void CounterComplete(float finalAcc, float finalFCAcc, int totalNotes, int totalMistakes)
        {
            if (totalNotes + _bombs == _noteArray?.Length)
                TheCounter.ClearCounter();
        }
        #endregion
    }
}
