using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Models.MapModels;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using CountersPlus.Counters.Custom;
using IPA.Utilities.Async;
using TMPro;
using Zenject;
using static GameplayModifiers;
#if NEW_VERSION
#else
using BLPPCounter.Utils.Special_Utils;
using UnityEngine;
#endif

namespace BLPPCounter
{
    public class TheCounter : BasicCustomCounter
    {
        #region Dependencies
#if NEW_VERSION
        private BeatmapLevel beatmap = null!;
        private BeatmapKey beatmapDiff;
#else
        private IDifficultyBeatmap beatmap = null!;
#endif
        private GameplayModifiers mods = null!;
        private ScoreController sc = null!;
        private BeatmapObjectManager bomb = null!;
        private PlayerHeadAndObstacleInteraction wall = null!;
        private LeaderboardSelector selector = null!;
        private PathUtils pathUtils = null!;
        private IMapRepository mapRepository = null!;
        private BLCalc beatLeaderCalculator = null!;
        private IBackgroundTaskRunner taskRunner = null!;
        private PluginConfig pc = null!;
        private ILeaderboardApiProvider apiProvider = null!;
        private ICounterCatalog counterCatalog = null!;
        private ICounterFactory counterFactory = null!;
        private IFormatRegistry formatRegistry = null!;
        private RatingContainer.Factory ratingFactory = null!;
        private MapSelection.Factory mapFactory = null!;

#if NEW_VERSION
        [Inject]
        private void Construct(
            BeatmapLevel beatmapLevel,
            BeatmapKey beatmapKey,
            GameplayModifiers gameplayModifiers,
            ScoreController scoreController,
            BeatmapObjectManager beatmapObjectManager,
            PlayerHeadAndObstacleInteraction obstacleInteraction,
            PluginConfig pluginConfig,
            ILeaderboardApiProvider apiProvider,
            LeaderboardSelector leaderboardSelector,
            PathUtils pathUtils,
            IMapRepository mapRepository,
            BLCalc beatLeaderCalculator,
            IBackgroundTaskRunner taskRunner,
            ICounterCatalog counterCatalog,
            ICounterFactory counterFactory,
            IFormatRegistry formatRegistry,
            RatingContainer.Factory ratingFactory,
            MapSelection.Factory mapFactory
        )
        {
            beatmap = beatmapLevel ?? throw new ArgumentNullException(nameof(beatmapLevel));
            beatmapDiff = beatmapKey;

            ConstructShared(
                gameplayModifiers,
                scoreController,
                beatmapObjectManager,
                obstacleInteraction,
                pluginConfig,
                apiProvider,
                leaderboardSelector,
                pathUtils,
                mapRepository,
                beatLeaderCalculator,
                taskRunner,
                counterCatalog,
                counterFactory,
                formatRegistry,
                ratingFactory,
                mapFactory
            );
        }
#else
        [Inject]
        private void Construct(
            IDifficultyBeatmap difficultyBeatmap,
            GameplayModifiers gameplayModifiers,
            ScoreController scoreController,
            BeatmapObjectManager beatmapObjectManager,
            PlayerHeadAndObstacleInteraction obstacleInteraction,
            PluginConfig pluginConfig,
            ILeaderboardApiProvider apiProvider,
            LeaderboardSelector leaderboardSelector,
            PathUtils pathUtils,
            IMapRepository mapRepository,
            BLCalc beatLeaderCalculator,
            IBackgroundTaskRunner taskRunner,
            ICounterCatalog counterCatalog,
            ICounterFactory counterFactory,
            IFormatRegistry formatRegistry,
            RatingContainer.Factory ratingFactory,
            MapSelection.Factory mapFactory
        )
        {
            beatmap = difficultyBeatmap ?? throw new ArgumentNullException(nameof(difficultyBeatmap));

            ConstructShared(
                gameplayModifiers,
                scoreController,
                beatmapObjectManager,
                obstacleInteraction,
                pluginConfig,
                apiProvider,
                leaderboardSelector,
                pathUtils,
                mapRepository,
                beatLeaderCalculator,
                taskRunner,
                counterCatalog,
                counterFactory,
                formatRegistry,
                ratingFactory,
                mapFactory
            );
        }
#endif

        private void ConstructShared(
            GameplayModifiers gameplayModifiers,
            ScoreController scoreController,
            BeatmapObjectManager beatmapObjectManager,
            PlayerHeadAndObstacleInteraction obstacleInteraction,
            PluginConfig pluginConfig,
            ILeaderboardApiProvider apiProvider,
            LeaderboardSelector leaderboardSelector,
            PathUtils pathUtils,
            IMapRepository mapRepository,
            BLCalc beatLeaderCalculator,
            IBackgroundTaskRunner taskRunner,
            ICounterCatalog counterCatalog,
            ICounterFactory counterFactory,
            IFormatRegistry formatRegistry,
            RatingContainer.Factory ratingFactory,
            MapSelection.Factory mapFactory
        )
        {
            mods = gameplayModifiers ?? throw new ArgumentNullException(nameof(gameplayModifiers));
            sc = scoreController ?? throw new ArgumentNullException(nameof(scoreController));
            bomb = beatmapObjectManager ?? throw new ArgumentNullException(nameof(beatmapObjectManager));
            wall = obstacleInteraction ?? throw new ArgumentNullException(nameof(obstacleInteraction));
            pc = pluginConfig ?? throw new ArgumentNullException(nameof(pluginConfig));
            this.apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
            selector = leaderboardSelector ?? throw new ArgumentNullException(nameof(leaderboardSelector));
            this.pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));
            this.mapRepository = mapRepository ?? throw new ArgumentNullException(nameof(mapRepository));
            this.beatLeaderCalculator =
                beatLeaderCalculator ?? throw new ArgumentNullException(nameof(beatLeaderCalculator));
            this.taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
            this.counterCatalog = counterCatalog ?? throw new ArgumentNullException(nameof(counterCatalog));
            this.counterFactory = counterFactory ?? throw new ArgumentNullException(nameof(counterFactory));
            this.formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
            this.ratingFactory = ratingFactory ?? throw new ArgumentNullException(nameof(ratingFactory));
            this.mapFactory = mapFactory ?? throw new ArgumentNullException(nameof(mapFactory));
        }
        #endregion

        #region Static Variables
        public static string DisplayName => MainCounterFormattingService.CounterName;

        private int leaderboardIndex;
        private int lastLeaderboardIndex;

        internal static MapSelection LastMap;
        internal static GameplayModifiers? LastMods;
        public static MyCounters? theCounter { get; private set; }
        public static bool SettingChanged { get; set; }

        private static readonly TimeLooper TimeLooper = new();
        private static string lastTarget = Targeter.NO_TARGET;
        private static Action? ForceOff;

        private Task initTask = Task.CompletedTask;
        private CancellationTokenSource? initTaskCanceller;
        private int initializationGeneration;

        private bool counterPaused;
        private bool formattingSubscribed;
        private bool notifiersSubscribed;

#if !NEW_VERSION
        private static HashSet<SliderKey> sliderMap = [];
#endif
        #endregion

        #region Variables
        private Leaderboards Leaderboard =>
            pc.LeaderboardsInUse.Count <= leaderboardIndex ? default : pc.LeaderboardsInUse[leaderboardIndex];

        private bool LastLeaderboard => pc.LeaderboardsInUse.Count - 1 <= leaderboardIndex;

        private TMP_Text? display;
        private bool enabled;
        private bool checkedLastIndex;

        private RatingContainer ratings;
        private int notes;
        private int comboNotes;
        private int mistakes;
        private int totalHitscore;
        private int maxHitscore;
        private int fcTotalHitscore;
        private int fcMaxHitscore;
        private float acc;
        private float fcAcc;
        private string? mode;
        private string? hash;
        private NoteData? currentNote;
        #endregion

        #region Initialization Lifecycle
        private static async Task DisposeInitializationAsync(
            Task initializationTask,
            CancellationTokenSource cancellationSource
        )
        {
            try
            {
                await initializationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
            {
                Plugin.Log.Debug("PP counter initialization was cancelled.");
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("PP counter initialization ended with an error during shutdown:\n" + exception);
            }
            finally
            {
                cancellationSource.Dispose();
            }
        }

        private void CancelAndObserveInitialization()
        {
            CancellationTokenSource? previousCancellationSource = initTaskCanceller;
            Task previousInitializationTask = initTask;

            initTaskCanceller = null;
            initTask = Task.CompletedTask;

            if (previousCancellationSource is null)
            {
                return;
            }

            previousCancellationSource.Cancel();

            taskRunner.Forget(
                DisposeInitializationAsync(previousInitializationTask, previousCancellationSource),
                "Finish cancelled PP counter initialization"
            );
        }

        private bool IsCurrentInitialization(int generation, CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested
                && generation == Volatile.Read(ref initializationGeneration);
        }

        private Task RunOnMainThread(Action action)
        {
            return UnityMainThreadTaskScheduler.Factory.StartNew(action);
        }

        private Task<T> RunOnMainThread<T>(Func<T> function)
        {
            return UnityMainThreadTaskScheduler.Factory.StartNew(function);
        }

        public override void CounterInit()
        {
            CancelAndObserveInitialization();

            int generation = Interlocked.Increment(ref initializationGeneration);

            enabled = false;
            checkedLastIndex = false;
            counterPaused = false;
            leaderboardIndex = lastLeaderboardIndex;

            ForceOff = () => ForceTurnOff();

            SubscribeToFormatChanges();
            counterCatalog.ValidateConfiguration();

            if (!counterCatalog.IsUsable)
            {
                Plugin.Log.Critical("There is no usable counter!");
                return;
            }

            notes = 0;
            comboNotes = 0;
            mistakes = 0;
            totalHitscore = 0;
            maxHitscore = 0;
            fcTotalHitscore = 0;
            fcMaxHitscore = 0;
            acc = 0f;
            fcAcc = 0f;
            currentNote = null;

#if !NEW_VERSION
            sliderMap = [];
#endif

            ChangeNotifiers(true);

            display = CanvasUtility.CreateTextFromSettings(Settings);
            display.fontSize = pc.FontSize;
            display.text = "Loading...";

            initTaskCanceller = new CancellationTokenSource();
            CancellationToken cancellationToken = initTaskCanceller.Token;

            initTask = AsyncCounterInit(cancellationToken, generation);
            taskRunner.Forget(initTask, "Initialize gameplay PP counter");
        }

        public override void CounterDestroy()
        {
            Plugin.Log.Debug($"There were {notes} note(s) handled.");

            try
            {
                Interlocked.Increment(ref initializationGeneration);

                UnsubscribeFromFormatChanges();
                ChangeNotifiers(false);

                if (enabled)
                {
                    lastLeaderboardIndex = leaderboardIndex;

                    if (pc.UpdateAfterTime)
                    {
                        taskRunner.Forget(TimeLooper.End(), "Stop PP counter time loop");
                    }

                    theCounter?.CounterComplete(acc, fcAcc, notes, mistakes);
                }

                enabled = false;
                theCounter = null;
                ForceOff = null;

                CancelAndObserveInitialization();
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("There was an issue turning the counter off!");
                Plugin.Log.Debug(exception);
                theCounter = null;
                ForceOff = null;
            }
        }

        internal void ForceTurnOff(string errorText = "")
        {
            Interlocked.Increment(ref initializationGeneration);

            enabled = false;
            counterPaused = false;

            if (pc.UpdateAfterTime)
            {
                taskRunner.Forget(TimeLooper.End(), "Stop disabled PP counter time loop");
            }

            display?.SetText(errorText);
            ChangeNotifiers(false);

            theCounter = null;

            // Cancellation is cooperative. Do not dispose the CTS here because this
            // method can be called from inside the currently executing initialization task.
            initTaskCanceller?.Cancel();
        }

        internal static void CancelCounter()
        {
            ForceOff?.Invoke();
        }

        public static void ClearCounter()
        {
            LastMap = default;
            LastMods = null;
            theCounter = null;
            lastTarget = Targeter.NO_TARGET;
            SettingChanged = false;
        }
        #endregion

        #region Map Selection
        public MapSelection GetDifficulty(
            Map map,
            BeatmapDifficulty difficulty,
            Leaderboards leaderboard,
            string mode = Profile.DEFAULT_MODE,
            GameplayModifiers? gameplayModifiers = null,
            bool quiet = false
        )
        {
            MapDiffModel? difficultyData = map.Get(mode, difficulty);

            if (
                difficultyData is null
                || !SetupMapData(difficultyData.Data, leaderboard, out float[]? loadedRatings, gameplayModifiers, quiet)
                || loadedRatings is null
            )
            {
                return default;
            }

            return mapFactory.Create(
                map,
                difficulty,
                mode,
                gameplayModifiers?.songSpeed ?? SongSpeed.Normal,
                leaderboard,
                loadedRatings
            );
        }

        public static string SelectMode(string? mainMode, Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Scoresaber => Map.SS_MODE_NAME,
                Leaderboards.Accsaber => Map.AP_MODE_NAME,
                _ => BetterString.IsNullOrWhiteSpace(mainMode) ? Profile.DEFAULT_MODE : mainMode,
            };
        }
        #endregion

        #region Counter Construction
        [MemberNotNullWhen(true, nameof(theCounter))]
        private bool InitCounter(CancellationToken cancellationToken = default)
        {
            if (display is null)
            {
                Plugin.Log.Warn("Cannot init counter when the display is null!");
                return false;
            }

            MyCounters? created = CreateCounter(pc.PPType, display, cancellationToken);

            if (created is null)
            {
                Plugin.Log.Warn("The created counter is null!");
                return false;
            }

            theCounter = created;
            return true;
        }

        private MyCounters? CreateCounter(
            string displayName,
            TMP_Text counterDisplay,
            CancellationToken cancellationToken = default
        )
        {
            if (LastMap.Equals(default))
            {
                Plugin.Log.Warn("Cannot create a counter before the current map selection has been initialized.");
                return null;
            }

            return counterFactory.Create(displayName, counterDisplay, LastMap, Leaderboard, cancellationToken);
        }

        internal void ReplaceCurrentCounter(string newCounter, TMP_Text counterDisplay)
        {
            try
            {
                if (pc.UpdateAfterTime)
                {
                    TimeLooper.Pause();
                }

                counterPaused = true;

                MyCounters? replacement = CreateCounter(newCounter, counterDisplay);

                if (replacement is null)
                {
                    Plugin.Log.Error("Error changing counter: the replacement counter is null.");
                    CancelCounter();
                    return;
                }

                theCounter = replacement;
                counterPaused = false;

                if (pc.UpdateAfterTime)
                {
                    TimeLooper.Resume();
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Error changing counter:\n{exception}");
                CancelCounter();
            }
        }
        #endregion

        #region Map Loading
        private async Task<bool> SetupMapDataAsync(CancellationToken cancellationToken, int generation)
        {
#if NEW_VERSION
            string characteristic = beatmapDiff.beatmapCharacteristic.serializedName;
            BeatmapDifficulty difficulty = beatmapDiff.difficulty;
            string levelId = beatmap.levelID;
#else
            string characteristic = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            BeatmapDifficulty difficulty = beatmap.difficulty;
            string levelId = beatmap.level.levelID;
#endif

            if (!IsCurrentInitialization(generation, cancellationToken))
            {
                return false;
            }

            string selectedMode = SelectMode(characteristic, Leaderboard);
            string[] levelIdParts = levelId.Split('_');

            if (levelIdParts.Length < 3)
            {
                Plugin.Log.Warn($"Could not extract a map hash from level ID \"{levelId}\".");
                return false;
            }

            string selectedHash = Map.NormalizeHash(levelIdParts[2]);

            try
            {
                Map? map = await mapRepository
                    .GetAsync(
                        selectedHash,
                        selectedMode,
                        Leaderboard,
                        forceRemoteLoad: pc.UseUnranked && Leaderboard == Leaderboards.Beatleader,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (!IsCurrentInitialization(generation, cancellationToken))
                {
                    return false;
                }

                if (map is null)
                {
                    Plugin.Log.Warn("The selected map is not available in the map repository.");
                    return false;
                }

                if (!map.TryGet(selectedMode, difficulty, out MapDiffModel? mapDifficulty))
                {
                    Plugin.Log.Warn(
                        $"Mode \"{selectedMode}\" and difficulty \"{difficulty}\" were not found for map {selectedHash}."
                    );
                    return false;
                }

                if (!SetupMapData(mapDifficulty.Data))
                {
                    return false;
                }

                mode = selectedMode;
                hash = selectedHash;

                Plugin.Log.Info($"Map Hash: {hash}");
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn($"Failed to load map {selectedHash} for {Leaderboard}.");
                Plugin.Log.Debug(exception);
                return false;
            }
        }

        private bool SetupMapData(ICacheModel data)
        {
            if (!SetupMapData(data, Leaderboard, out float[]? loadedRatings, mods))
            {
                return false;
            }

            ratings = ratingFactory.Create(Leaderboard, loadedRatings);
            return true;
        }

        private async Task<bool> APIAvoidanceModeAsync(CancellationToken cancellationToken, int generation)
        {
            if (mode is null || display is null || theCounter is null || LastMap.Equals(default))
            {
                return false;
            }

            Plugin.Log.Debug("API avoidance mode is active.");

            Map? map = await mapRepository
                .GetAsync(LastMap.Hash, mode, Leaderboard, forceRemoteLoad: false, cancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentInitialization(generation, cancellationToken) || map is null)
            {
                return false;
            }

#if NEW_VERSION
            MapSelection currentMap = mapFactory.Create(map, beatmapDiff.difficulty, mode, mods.songSpeed, ratings);
#else
            MapSelection currentMap = mapFactory.Create(map, beatmap.difficulty, mode, mods.songSpeed, ratings);
#endif

            if (!currentMap.IsUsable)
            {
                return false;
            }

            (bool ratingDifference, bool difficultyDifference) = currentMap.GetDifference(LastMap);

            if (Leaderboard == Leaderboards.Beatleader && (ratingDifference || difficultyDifference))
            {
                await beatLeaderCalculator.SetCurve(LastMap.MapData.SongId, LastMap.MapSpeed, apiProvider);

                if (!IsCurrentInitialization(generation, cancellationToken))
                {
                    return false;
                }
            }

            await RunOnMainThread(() =>
            {
                if (!IsCurrentInitialization(generation, cancellationToken) || theCounter is null)
                {
                    return;
                }

                if (difficultyDifference)
                {
                    theCounter.ReinitCounter(display, currentMap);
                }
                else if (ratingDifference)
                {
                    theCounter.ReinitCounter(display, ratings);
                }
                else
                {
                    theCounter.ReinitCounter(display);
                }

                LastMap = currentMap;
            });

            return IsCurrentInitialization(generation, cancellationToken);
        }
        #endregion

        #region Async Initialization
        private async Task AsyncCounterInit(
            CancellationToken cancellationToken,
            int generation,
            bool ignoreLast = false
        )
        {
            if (!IsCurrentInitialization(generation, cancellationToken))
            {
                return;
            }

            if (leaderboardIndex == lastLeaderboardIndex && checkedLastIndex)
            {
                goto Failed;
            }

            if (Leaderboard == default)
            {
                Plugin.Log.Warn("Leaderboard reached default state, disabling.");
                ForceTurnOff();
                lastLeaderboardIndex = 0;
                return;
            }

            Leaderboards currentLeaderboard = Leaderboard;
            Plugin.Log.Info($"Attempting to load {currentLeaderboard}...");

            try
            {
                enabled = await RunOnMainThread(() => SetupMapDataAsync(cancellationToken, generation)).Unwrap();

                if (!IsCurrentInitialization(generation, cancellationToken))
                {
                    return;
                }

                if (!enabled)
                {
                    Plugin.Log.Warn(
                        "Map data failed to load, most likely because the selected difficulty is unranked."
                    );
                    goto Failed;
                }

                if (hash is null || mode is null)
                {
                    Plugin.Log.Warn("Counter map identity was not initialized.");
                    goto Failed;
                }

                if (hash.EndsWith("wip", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log.Warn("WIP map detected; disabling the counter.");
                    ForceTurnOff();
                    return;
                }

                if (
                    !LastMap.Equals(default)
                    && !hash.Equals(LastMap.Hash, StringComparison.OrdinalIgnoreCase)
                    && !ignoreLast
                    && lastLeaderboardIndex != 0
                    && lastLeaderboardIndex == leaderboardIndex
                )
                {
                    Plugin.Log.Info("Ignoring the last leaderboard because the selected map changed.");

                    leaderboardIndex = 0;
                    await AsyncCounterInit(cancellationToken, generation, ignoreLast: true);
                    return;
                }

                bool counterChanged = !string.Equals(theCounter?.Name, pc.PPType, StringComparison.Ordinal);

                bool requiresNewCounter =
                    theCounter is null
                    || SettingChanged
                    || counterChanged
                    || LastMap.Equals(default)
                    || !hash.Equals(LastMap.Hash, StringComparison.OrdinalIgnoreCase)
                    || !lastTarget.Equals(pc.Target, StringComparison.Ordinal);

                if (requiresNewCounter)
                {
                    Map? map = await mapRepository
                        .GetAsync(
                            hash,
                            mode,
                            currentLeaderboard,
                            forceRemoteLoad: pc.UseUnranked && currentLeaderboard == Leaderboards.Beatleader,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (!IsCurrentInitialization(generation, cancellationToken))
                    {
                        return;
                    }

                    if (map is null)
                    {
                        Plugin.Log.Warn("The map repository returned no map.");
                        goto Failed;
                    }

                    bool counterInitialized = await RunOnMainThread(() =>
                    {
#if NEW_VERSION
                        MapSelection selection = mapFactory.Create(
                            map,
                            beatmapDiff.difficulty,
                            mode,
                            mods.songSpeed,
                            ratings
                        );
#else
                        MapSelection selection = mapFactory.Create(
                            map,
                            beatmap.difficulty,
                            mode,
                            mods.songSpeed,
                            ratings
                        );
#endif

                        if (!selection.IsUsable || !IsCurrentInitialization(generation, cancellationToken))
                        {
                            return false;
                        }

                        LastMap = selection;
                        LastMods = mods;

                        return InitCounter(cancellationToken);
                    });

                    if (!counterInitialized)
                    {
                        Plugin.Log.Warn("The selected counter failed to initialize.");
                        goto Failed;
                    }

                    SettingChanged = false;

                    if (currentLeaderboard == Leaderboards.Beatleader)
                    {
                        await beatLeaderCalculator.SetCurve(LastMap.MapData.SongId, LastMap.MapSpeed, apiProvider);

                        if (!IsCurrentInitialization(generation, cancellationToken))
                        {
                            return;
                        }
                    }
                }
                else if (
                    !await RunOnMainThread(async () => await APIAvoidanceModeAsync(cancellationToken, generation))
                        .Unwrap()
                )
                {
                    goto Failed;
                }

                if (!IsCurrentInitialization(generation, cancellationToken))
                {
                    return;
                }

                await RunOnMainThread(() =>
                {
                    if (!IsCurrentInitialization(generation, cancellationToken))
                    {
                        return;
                    }

                    lastTarget = pc.Target;

                    theCounter?.UpdateFormat();

                    if (pc.UpdateAfterTime)
                    {
                        SetTimeLooper();
                    }

                    display?.SetText("Loaded!");

                    if (!pc.UpdateAfterTime && notes < 1)
                    {
                        theCounter?.UpdateCounter(1f, 0, 0, 1f, null);
                    }
                });

                if (!IsCurrentInitialization(generation, cancellationToken))
                {
                    return;
                }

                Plugin.Log.Debug("Counter loaded successfully.");

                if (pc.UpdateAfterTime)
                {
                    await TimeLooper.Start().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Plugin.Log.Debug("Counter initialization was cancelled.");
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"The counter failed to initialize: {exception.Message}\nSource: {exception.Source}");
                Plugin.Log.Debug(exception);

                if (LastLeaderboard)
                {
                    ForceTurnOff();
                }
            }

            return;

            Failed:
            if (!IsCurrentInitialization(generation, cancellationToken))
            {
                return;
            }

            if (!LastLeaderboard || (!checkedLastIndex && pc.LeaderboardsInUse.Count > 1))
            {
                if (leaderboardIndex == lastLeaderboardIndex && !checkedLastIndex)
                {
                    leaderboardIndex = lastLeaderboardIndex == 0 ? 1 : 0;
                    checkedLastIndex = true;
                }
                else
                {
                    leaderboardIndex +=
                        leaderboardIndex + 1 == lastLeaderboardIndex && !checkedLastIndex && !ignoreLast ? 2 : 1;
                }

                await AsyncCounterInit(cancellationToken, generation, ignoreLast);
                return;
            }

            ForceTurnOff();
        }
        #endregion

        #region Event Calls
        private void OnNoteScored(ScoringElement scoringElement)
        {
            try
            {
                OnNoteScoredInternal(scoringElement);
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("The counter encountered a fatal error, shutting down.");
                ForceTurnOff("Fatal error!\nPlease report this!");
                Plugin.Log.Debug(exception);

                if (exception is NullReferenceException or ArgumentNullException)
                {
                    Plugin.Log.Debug(
                        $"NULL CHECKS: theCounter null? {theCounter is null} || "
                            + $"scoringElement null? {scoringElement is null} || "
                            + $"scoringElement.NoteData null? {scoringElement?.noteData is null}"
                    );
                }
            }
            finally
            {
                if (pc.UpdateAfterTime)
                {
                    TimeLooper.Resume();
                }
            }
        }

        private void OnNoteScoredInternal(ScoringElement scoringElement)
        {
            if (scoringElement.noteData.gameplayType == NoteData.GameplayType.Bomb)
            {
                return;
            }

            NoteData.ScoringType scoringType = scoringElement.noteData.scoringType;
            currentNote = scoringElement.noteData;

            if (scoringType == NoteData.ScoringType.Ignore)
            {
                goto Finish;
            }

            notes++;

            if (scoringType != NoteData.ScoringType.NoScore)
            {
                comboNotes++;
            }

            int cutScore = scoringElement.cutScore;
            int maximumCutScore = scoringElement.maxPossibleCutScore;

            int offset = HandleWeirdNoteBehaviour(currentNote, maximumCutScore);

            if (offset != 0)
            {
                cutScore += offset;
                maximumCutScore += offset;
            }

            maxHitscore += maximumCutScore * HelpfulMath.MultiplierForNote(notes);

            if (cutScore > 0)
            {
                fcMaxHitscore += maximumCutScore * HelpfulMath.MultiplierForNote(notes);
                totalHitscore += cutScore * HelpfulMath.MultiplierForNote(comboNotes);
                fcTotalHitscore += cutScore * HelpfulMath.MultiplierForNote(notes);
            }
            else
            {
                OnMiss();
            }

            acc = maxHitscore <= 0 ? 0f : (float)totalHitscore / maxHitscore;
            fcAcc = fcMaxHitscore <= 0 ? 0f : (float)fcTotalHitscore / fcMaxHitscore;

            Finish:
            if (!initTask.IsCompleted || counterPaused || theCounter is null)
            {
                return;
            }

            theCounter.SoftUpdate(acc, notes, mistakes, fcAcc, currentNote);

            if (!pc.UpdateAfterTime)
            {
                theCounter.UpdateCounter(acc, notes, mistakes, fcAcc, currentNote);
            }
        }

#if !NEW_VERSION
        private void OnSliderSpawn(SliderController sliderController)
        {
            if (!sliderController.sliderData.hasHeadNote)
            {
                return;
            }

            sliderMap.Add(KeyFromSliderData(sliderController.sliderData, true));
        }
#endif

        private void OnBombHit(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            if (noteController.noteData.gameplayType == NoteData.GameplayType.Bomb)
            {
                OnMiss();
            }
        }

        private void OnWallHit(ObstacleController obstacleController)
        {
            OnMiss();
        }

        private void OnMiss()
        {
            // Bombs and walls can occur before the first scoreable note. They are
            // still gameplay mistakes and must be counted.
            comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
            mistakes++;
        }

        private void SetTimeLooper()
        {
            TimeLooper.DelayMs = Math.Max(1, (int)(pc.UpdateTime * 1000));

            int currentNotes = 0;

            TimeLooper.SetAction(() =>
            {
                if (currentNotes == notes)
                {
                    return true;
                }

                int capturedNotes = notes;
                int capturedMistakes = mistakes;
                float capturedAcc = acc;
                float capturedFcAcc = fcAcc;
                NoteData? capturedCurrentNote = currentNote;

                _ = RunOnMainThread(() =>
                    theCounter?.UpdateCounter(
                        capturedAcc,
                        capturedNotes,
                        capturedMistakes,
                        capturedFcAcc,
                        capturedCurrentNote
                    )
                );

                currentNotes = capturedNotes;
                return false;
            });
        }
        #endregion

        #region Helper Methods
        private void SubscribeToFormatChanges()
        {
            if (formattingSubscribed)
            {
                return;
            }

            formatRegistry.FormatsRebuilt += HandleFormatsRebuilt;
            formattingSubscribed = true;
        }

        private void UnsubscribeFromFormatChanges()
        {
            if (!formattingSubscribed)
            {
                return;
            }

            formatRegistry.FormatsRebuilt -= HandleFormatsRebuilt;
            formattingSubscribed = false;
        }

        private void HandleFormatsRebuilt()
        {
            try
            {
                theCounter?.UpdateFormat();
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("The active counter failed to apply rebuilt formatters:\n" + exception);
            }
        }

        private void ChangeNotifiers(bool subscribe)
        {
            if (subscribe)
            {
                if (notifiersSubscribed)
                {
                    return;
                }

                sc.scoringForNoteFinishedEvent += OnNoteScored;
                wall.headDidEnterObstacleEvent += OnWallHit;
                bomb.noteWasCutEvent += OnBombHit;

#if !NEW_VERSION
                bomb.sliderWasSpawnedEvent += OnSliderSpawn;
#endif

                notifiersSubscribed = true;
                return;
            }

            if (!notifiersSubscribed)
            {
                return;
            }

            sc.scoringForNoteFinishedEvent -= OnNoteScored;
            wall.headDidEnterObstacleEvent -= OnWallHit;
            bomb.noteWasCutEvent -= OnBombHit;

#if !NEW_VERSION
            bomb.sliderWasSpawnedEvent -= OnSliderSpawn;
#endif

            notifiersSubscribed = false;
        }

        public static int HandleWeirdNoteBehaviour(NoteData note, int maxCutscore)
        {
            return ScoreModel.GetNoteScoreDefinition(HandleWeirdNoteBehaviour(note)).maxCutScore - maxCutscore;
        }

        public static NoteData.ScoringType HandleWeirdNoteBehaviour(NoteData note)
        {
#if NEW_VERSION
            if (note.gameplayType == NoteData.GameplayType.BurstSliderHead && note.isArcHead)
#endif
#if NEWER_VERSION
            {
                return note.isArcTail ? NoteData.ScoringType.ArcHeadArcTail : NoteData.ScoringType.ArcHead;
            }
#elif NEW_VERSION
            {
                return note.isArcTail
                    ? (NoteData.ScoringType)BLCalc.ExtendedScoringType.ArcHeadArcTail
                    : (NoteData.ScoringType)2;
            }
#else
            bool isSliderHead = false;
            SliderKey sliderKey = KeyFromNoteData(note);

            if (sliderMap.Contains(sliderKey))
            {
                sliderMap.Remove(sliderKey);
                isSliderHead = true;
            }

            if (note.gameplayType == NoteData.GameplayType.BurstSliderHead && isSliderHead)
            {
                note.ChangeToSliderHead();
            }
#endif

            return note.scoringType;
        }

#if !NEW_VERSION
        private static SliderKey KeyFromNoteData(NoteData noteData)
        {
            return new SliderKey(
                Mathf.RoundToInt(noteData.time * 1000f),
                noteData.lineIndex,
                noteData.noteLineLayer,
                noteData.colorType,
                noteData.cutDirection
            );
        }

        private static SliderKey KeyFromSliderData(SliderData sliderData, bool useHead)
        {
            return useHead
                ? new SliderKey(
                    Mathf.RoundToInt(sliderData.time * 1000f),
                    sliderData.headLineIndex,
                    sliderData.headLineLayer,
                    sliderData.colorType,
                    sliderData.headCutDirection
                )
                : new SliderKey(
                    Mathf.RoundToInt(sliderData.tailTime * 1000f),
                    sliderData.tailLineIndex,
                    sliderData.tailLineLayer,
                    sliderData.colorType,
                    sliderData.tailCutDirection
                );
        }
#endif
        #endregion

        #region Ratings
        /// <summary>
        /// Given the difficulty <paramref name="data"/>, <paramref name="gameplayModifiers"/>, and
        /// <paramref name="leaderboard"/>, find what the star ratings are with as few API calls as possible.
        /// </summary>
        /// <param name="data">The difficulty data of the map.</param>
        /// <param name="leaderboard">The leaderboard in which the map is being checked on.</param>
        /// <param name="loadedRatings">the ratings of the map. Outputs in the order of star, acc, pass, tech.</param>
        /// <param name="gameplayModifiers">What mods are used in the map (defaults to null for no mods).</param>
        /// <param name="quiet">Whether or not for this function to print out the ratings to read.</param>
        /// <returns>Whether or not the ratings were loaded successfully.</returns>
        public bool SetupMapData(
            ICacheModel data,
            Leaderboards leaderboard,
            [NotNullWhen(true)] out float[]? loadedRatings,
            GameplayModifiers? gameplayModifiers = null,
            bool quiet = false
        )
        {
            if (data is null || (data is BlCacheEntry && leaderboard != Leaderboards.Beatleader))
            {
                loadedRatings = null;
                return false;
            }

            switch (leaderboard)
            {
                case Leaderboards.Scoresaber:
                {
                    if (data is not TaohCache taohCache)
                    {
                        loadedRatings = null;
                        return false;
                    }

                    float stars = taohCache.StarScoreSaber.GetValueOrDefault();
                    loadedRatings = [stars];

                    if (!quiet)
                    {
                        Plugin.Log.Info($"Stars: {stars}");
                    }

                    return stars > 0f;
                }

                case Leaderboards.Accsaber:
                {
                    if (data is not TaohCache taohCache)
                    {
                        loadedRatings = null;
                        return false;
                    }

                    float complexity = taohCache.ComplexityAccSaber.GetValueOrDefault();
                    loadedRatings = [complexity];

                    if (!quiet)
                    {
                        Plugin.Log.Info($"Complexity: {complexity}");
                    }

                    return complexity > 0f;
                }

                case Leaderboards.Beatleader:
                {
                    SongSpeed speed = gameplayModifiers?.songSpeed ?? SongSpeed.Normal;
                    float multiplier = pathUtils.GetStarMultiplier(data, gameplayModifiers);

                    float stars = pathUtils.GetRating(data, PPType.Star, leaderboard, speed);
                    float accuracyRating = pathUtils.GetRating(data, PPType.Acc, leaderboard, speed) * multiplier;
                    float passRating = pathUtils.GetRating(data, PPType.Pass, leaderboard, speed) * multiplier;
                    float technologyRating = pathUtils.GetRating(data, PPType.Tech, leaderboard, speed) * multiplier;

                    loadedRatings = [stars, accuracyRating, passRating, technologyRating];

                    if (!quiet)
                    {
                        string modifierName = HelpfulMisc.GetModifierShortname(speed).ToUpperInvariant();

                        string prefix = modifierName.Length == 0 ? "" : modifierName + " ";

                        Plugin.Log.Info(
                            $"{prefix}Stars: {stars}\n"
                                + $"{prefix}Acc Rating: {accuracyRating}\n"
                                + $"{prefix}Pass Rating: {passRating}\n"
                                + $"{prefix}Tech Rating: {technologyRating}"
                        );
                    }

                    return technologyRating > 0f;
                }

                default:
                    loadedRatings = null;
                    return false;
            }
        }
        #endregion
    }
}
