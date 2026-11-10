using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Safety;
using UnityEngine;
using Zenject;
using static BLPPCounter.Utils.API_Handlers.APIHandler;

namespace BLPPCounter.Counters
{
    public sealed class RankCounter : MyCounters
    {
        private ILeaderboardApiProvider _apiProvider = null!;

        private PathUtils _pathUtils = null!;

        private BLCalc _beatLeaderCalculator = null!;

        private IRankCounterFormattingService _formatting = null!;

        private IBackgroundTaskRunner _taskRunner = null!;

        private HelpfulFormatter _formatter = null!;

        private Task _setupTask = Task.CompletedTask;

        private float[] _rankValues = [];
        private ScoregraphInfo[] _scoregraph = [];

        private bool _setupFailed;
        private bool _setupErrorLogged;

        public static int OrderNumber => 4;

        public static string DisplayName => "Rank";

        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        public static string DisplayHandler => DisplayName;

        public override string Name => DisplayName;

        [Inject]
        private void ConstructRankCounter(
            ILeaderboardApiProvider apiProvider,
            PathUtils pathUtils,
            BLCalc beatLeaderCalculator,
            IRankCounterFormattingService formatting,
            IBackgroundTaskRunner taskRunner,
            HelpfulFormatter formatter
        )
        {
            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            _pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));

            _beatLeaderCalculator =
                beatLeaderCalculator ?? throw new ArgumentNullException(nameof(beatLeaderCalculator));

            _formatting = formatting ?? throw new ArgumentNullException(nameof(formatting));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));

            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public override void SetupData(MapSelection map, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _setupFailed = false;
            _setupErrorLogged = false;
            _rankValues = [];
            _scoregraph = [];

            ppHandler = new PPHandler(ratings, calc, pc.DecimalPrecision, 1)
            {
                UpdateFCEnabled = pc.PPFC,
                UpdatePPEnabled = _formatting.UsesPpValues,
            };

            ppHandler.UpdateFC += (fullComboAccuracy, values, _, _) =>
                values[1].SetValues(calc.GetPpWithSummedPp(fullComboAccuracy, pc.DecimalPrecision));

            _setupTask = InitializeScoregraphAsync(map, cancellationToken);

            _taskRunner.Forget(_setupTask, "Initialize rank counter scoregraph");
        }

        public override void UpdateFormat()
        {
            ppHandler.UpdatePPEnabled = _formatting.UsesPpValues;
        }

        public override void UpdateCounterInternal(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            if (!ScoregraphIsReady())
                return;

            ppHandler.Update(accuracy, mistakes, fullComboAccuracy);

            float currentValue = calc.UsesModifiers ? ppHandler.GetPPGroup(0).TotalPP : accuracy;

            int rank = GetRank(currentValue);

            ScoregraphInfo rankData = GetRankData(rank);

            float ppDifference = (float)
                Math.Abs(Math.Round(rankData.PP - ppHandler.GetPPGroup(0).TotalPP, pc.DecimalPrecision));

            float accuracyDifference = (float)
                Math.Abs(Math.Round((rankData.Acc - accuracy) * 100f, pc.DecimalPrecision));

            string rankColor = _formatter.GetWeightedRankColor(rank);

            if (pc.SplitPPVals && calc.RatingCount > 1)
            {
                int count = Math.Min(calc.RatingCount, CurrentLabels.Count);

                for (int index = 0; index < count; index++)
                {
                    outpText.AppendLine(
                        _formatting.Format(
                            ppHandler.DisplayFC,
                            displayExtraInformation: false,
                            isFirstPlace: false,
                            ppHandler[0, index],
                            ppHandler[1, index],
                            rank,
                            rankData.PlayerName,
                            ppDifference,
                            accuracyDifference,
                            rankColor,
                            CurrentLabels[index]
                        )
                    );
                }
            }

            string totalLabel = CurrentLabels.Count == 0 ? "" : CurrentLabels[CurrentLabels.Count - 1];

            outpText.AppendLine(
                _formatting.Format(
                    ppHandler.DisplayFC,
                    pc.ExtraInfo,
                    rank == 1,
                    ppHandler[0],
                    ppHandler[1],
                    rank,
                    rankData.PlayerName,
                    ppDifference,
                    accuracyDifference,
                    rankColor,
                    totalLabel
                )
            );
        }

        public override void SoftUpdate(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        ) { }

        private async Task InitializeScoregraphAsync(MapSelection map, CancellationToken cancellationToken)
        {
            try
            {
                APIHandler api = _apiProvider.Get(CurrentLeaderboard);

                ScoregraphInfo[] scoregraph = await api.GetScoregraph(map, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (scoregraph.Length == 0)
                {
                    throw new InvalidOperationException("The leaderboard returned an empty " + "scoregraph.");
                }

                bool isUnranked = scoregraph[0].PP <= 0f || CurrentLeaderboard == Leaderboards.Accsaber;

                if (calc.UsesModifiers || isUnranked)
                {
                    RecalculateScoregraph(scoregraph, map, isUnranked, cancellationToken);
                }

                Array.Sort(scoregraph, CompareScoregraphEntries);

                _scoregraph = scoregraph;

                _rankValues = [.. scoregraph.Select(entry => calc.UsesModifiers ? entry.PP : entry.Acc)];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _setupFailed = true;

                Plugin.Log.Error($"Error setting up Rank Counter for " + $"{map.MapData.SongId}:\n{exception}");
            }
        }

        private void RecalculateScoregraph(
            ScoregraphInfo[] scoregraph,
            MapSelection map,
            bool isUnranked,
            CancellationToken cancellationToken
        )
        {
            for (int index = 0; index < scoregraph.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ScoregraphInfo entry = scoregraph[index];

                if (!isUnranked && (CurrentLeaderboard != Leaderboards.Beatleader || entry.Speed == map.MapSpeed))
                {
                    continue;
                }

                if (CurrentLeaderboard == Leaderboards.Beatleader)
                {
                    RatingContainer speedRatings = _pathUtils.GetAllRatingsOfSpeed(
                        map.MapData.Data,
                        Leaderboards.Beatleader,
                        entry.Speed
                    );

                    float[] specificPp = calc.GetPpWithSummedPp(entry.Acc, speedRatings);

                    float deflatedPp = specificPp.Take(3).Sum();

                    float adjustedAccuracy =
                        _beatLeaderCalculator.GetAccDeflatedUnsafe(
                            deflatedPp,
                            pc.DecimalPrecision,
                            ratings.GetRatings(CurrentLeaderboard)
                        ) / 100f;

                    float adjustedPp = specificPp.Length > 3 ? specificPp[3] : calc.Inflate(deflatedPp);

                    entry.ChangePP(adjustedAccuracy, (float)Math.Round(adjustedPp, pc.DecimalPrecision));
                }
                else
                {
                    entry.ChangePP(
                        entry.Acc,
                        (float)
                            Math.Round(
                                calc.Inflate(calc.GetSummedPp(entry.Acc, ratings.GetRatings(CurrentLeaderboard))),
                                pc.DecimalPrecision
                            )
                    );
                }

                scoregraph[index] = entry;
            }
        }

        private int CompareScoregraphEntries(ScoregraphInfo first, ScoregraphInfo second)
        {
            float difference = calc.UsesModifiers ? second.PP - first.PP : second.Acc - first.Acc;

            if (Mathf.Approximately(difference, 0f))
            {
                return 0;
            }

            return difference < 0f ? -1 : 1;
        }

        private bool ScoregraphIsReady()
        {
            if (_setupTask.Status == TaskStatus.RanToCompletion)
            {
                return !_setupFailed && _scoregraph.Length > 0 && _rankValues.Length > 0;
            }

            if (_setupTask.IsFaulted && !_setupErrorLogged)
            {
                _setupErrorLogged = true;

                Plugin.Log.Error("Rank counter scoregraph setup failed:\n" + _setupTask.Exception);
            }

            return false;
        }

        private int GetRank(float value)
        {
            int index = HelpfulMisc.FindInsertValueReverse(_rankValues, value);

            int maximumRank = Math.Min(pc.MinRank, _scoregraph.Length);

            if (maximumRank < 1)
                maximumRank = _scoregraph.Length;

            return Mathf.Clamp(index + 1, 1, Math.Max(1, maximumRank));
        }

        private ScoregraphInfo GetRankData(int rank)
        {
            int index = Math.Max(rank - 2, 0);

            index = Math.Min(index, _scoregraph.Length - 1);

            return _scoregraph[index];
        }
    }
}
