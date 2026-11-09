using System;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Safety;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Counters
{
    public sealed class ClanCounter : MyCounters
    {
        private IClanService _clanService = null!;
        private IClanCounterFormattingService _formatting = null!;

        private IMainCounterFormattingService _mainFormatting = null!;

        private ITargeter _targeter = null!;
        private IBackgroundTaskRunner _taskRunner = null!;
        private PathUtils _pathUtils = null!;
        private BLCalc _beatLeaderCalculator = null!;

        private Task _setupTask = Task.CompletedTask;

        private RatingContainer _normalRatings;
        private float[] _clanPpValues = [];

        private PPContainer _neededPpValues;
        private PPHandler? _weightedPpHandler;

        private float _neededAccuracy;
        private string _message = "";
        private bool _showRank;

        private ClanCounterMode _mode = ClanCounterMode.Loading;

        public static string DisplayName => "Clan";

        public static string DisplayHandler => DisplayName;

        public static int OrderNumber => 3;

        public static Leaderboards ValidLeaderboards => Leaderboards.Beatleader;

        public override string Name => DisplayName;

        public string Mods { get; private set; } = "";

        [Inject]
        private void ConstructClanCounter(
            IClanService clanService,
            IClanCounterFormattingService formatting,
            IMainCounterFormattingService mainFormatting,
            ITargeter targeter,
            IBackgroundTaskRunner taskRunner,
            PathUtils pathUtils,
            BLCalc beatLeaderCalculator
        )
        {
            _clanService = clanService ?? throw new ArgumentNullException(nameof(clanService));

            _formatting = formatting ?? throw new ArgumentNullException(nameof(formatting));

            _mainFormatting = mainFormatting ?? throw new ArgumentNullException(nameof(mainFormatting));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));

            _pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));

            _beatLeaderCalculator =
                beatLeaderCalculator ?? throw new ArgumentNullException(nameof(beatLeaderCalculator));
        }

        public override void SetupData(MapSelection map, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Mods = "";
            _message = "";
            _showRank = false;
            _mode = ClanCounterMode.Loading;
            _clanPpValues = [];

            _normalRatings = _pathUtils.GetAllRatingsOfSpeed(
                map.MapData.Data,
                Leaderboards.Beatleader,
                SongSpeed.Normal
            );

            _neededPpValues = new PPContainer(calc.DisplayRatingCount, 0f, precision: pc.DecimalPrecision);

            InitializeMainHandler();
            InitializeWeightedHandler();

            _setupTask = InitializeAsync(map, cancellationToken);

            _taskRunner.Forget(_setupTask, "Initialize clan counter");
        }

        public override void ReinitCounter(RatingContainer ratingValues)
        {
            if (_mode != ClanCounterMode.Capture || _neededPpValues.TotalPP <= 0f)
            {
                return;
            }

            CalculateNeededAccuracy();
        }

        public override void ReinitCounter(MapSelection map)
        {
            _normalRatings = _pathUtils.GetAllRatingsOfSpeed(
                map.MapData.Data,
                Leaderboards.Beatleader,
                SongSpeed.Normal
            );
        }

        public override void UpdateFormat()
        {
            ppHandler.UpdatePPEnabled = _formatting.UsesMainPpValues;

            _weightedPpHandler?.UpdatePPEnabled = _formatting.UsesWeightedPpValues;
        }

        public override void UpdateCounterInternal(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            if (_setupTask.Status != TaskStatus.RanToCompletion || _mode == ClanCounterMode.Loading)
            {
                return;
            }

            if (_mode != ClanCounterMode.Capture)
            {
                UpdateWeightedCounter(accuracy, mistakes, fullComboAccuracy);

                return;
            }

            ppHandler.Update(accuracy, mistakes, fullComboAccuracy);

            string message = CreateCaptureMessage();

            string target = CreateTargetMessage();

            if (pc.SplitPPVals && calc.RatingCount > 1)
            {
                int count = Math.Min(calc.DisplayRatingCount, CurrentLabels.Count);

                for (int index = 0; index < count; index++)
                {
                    outpText.AppendLine(
                        _formatting.FormatMain(
                            ppHandler.DisplayFC,
                            pc.ExtraInfo && index == count - 1,
                            mistakes,
                            ppHandler[1, index],
                            ppHandler[0, index],
                            ppHandler[3, index],
                            ppHandler[2, index],
                            CurrentLabels[index],
                            target,
                            message
                        )
                    );
                }

                return;
            }

            string label = CurrentLabels.Count == 0 ? "" : CurrentLabels[CurrentLabels.Count - 1];

            outpText.AppendLine(
                _formatting.FormatMain(
                    ppHandler.DisplayFC,
                    pc.ExtraInfo,
                    mistakes,
                    ppHandler[1],
                    ppHandler[0],
                    ppHandler[3],
                    ppHandler[2],
                    label,
                    target,
                    message
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

        private async Task InitializeAsync(MapSelection map, CancellationToken cancellationToken)
        {
            try
            {
                ClanCaptureResult result = await _clanService
                    .GetCaptureAsync(map, cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                _clanPpValues = [.. result.ClanPpValues];

                switch (result.Status)
                {
                    case ClanCaptureStatus.Success:
                        InitializeCaptureMode(result);
                        break;

                    case ClanCaptureStatus.AlreadyCaptured:
                        InitializeWeightedMode(ClanCounterMode.Captured, pc.MessageSettings.MapCapturedMessage);
                        break;

                    case ClanCaptureStatus.Unranked:
                        InitializeWeightedMode(ClanCounterMode.Unranked, pc.MessageSettings.MapUnrankedMessage);
                        break;

                    default:
                        InitializeWeightedMode(ClanCounterMode.LoadFailed, pc.MessageSettings.LoadFailedMessage);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Clan counter setup failed:\n{exception}");

                InitializeWeightedMode(ClanCounterMode.LoadFailed, pc.MessageSettings.LoadFailedMessage);
            }
        }

        private void InitializeCaptureMode(ClanCaptureResult result)
        {
            if (result.NeededPp <= 0f)
            {
                InitializeWeightedMode(ClanCounterMode.Captured, pc.MessageSettings.MapCapturedMessage);

                return;
            }

            _neededPpValues.TotalPP = result.NeededPp;

            CalculateNeededAccuracy();

            if (pc.CeilEnabled && _neededAccuracy >= pc.ClanPercentCeil)
            {
                InitializeWeightedMode(ClanCounterMode.Uncapturable, pc.MessageSettings.MapUncapturableMessage);

                return;
            }

            _message = "";
            _showRank = pc.ShowRank;
            _mode = ClanCounterMode.Capture;
        }

        private void InitializeWeightedMode(ClanCounterMode mode, string message)
        {
            _mode = mode;
            _message = message;

            _showRank = pc.ShowRank && mode is not ClanCounterMode.Unranked and not ClanCounterMode.LoadFailed;
        }

        private void CalculateNeededAccuracy()
        {
            _neededAccuracy = calc.GetAcc(_neededPpValues.TotalPP, pc.DecimalPrecision, ratings.Ratings);

            float[] values = calc.GetPp(_neededAccuracy / 100f, _normalRatings.Ratings);

            if (values.Length > 0)
                _neededPpValues.AccPP = values[0];

            if (values.Length > 1)
                _neededPpValues.PassPP = values[1];

            if (values.Length > 2)
                _neededPpValues.TechPP = values[2];
        }

        private void InitializeMainHandler()
        {
            ppHandler = new PPHandler(
                ratings,
                calc,
                pc.DecimalPrecision,
                2,
                (rating, accuracy, in main, ref toChange, _) =>
                    PPContainer.SubtractFast(in main, in _neededPpValues, ref toChange)
            )
            {
                UpdateFCEnabled = pc.PPFC,
                UpdatePPEnabled = _formatting.UsesMainPpValues,
            };

            ppHandler.UpdateFC += (fullComboAccuracy, values, actions, multiplier) =>
            {
                values[2].SetValues(calc.GetPpWithSummedPp(fullComboAccuracy, pc.DecimalPrecision));

                actions(0, fullComboAccuracy, in values[2], ref values[3], multiplier);
            };
        }

        private void InitializeWeightedHandler()
        {
            _weightedPpHandler = new PPHandler(
                ratings,
                calc,
                pc.DecimalPrecision,
                2,
                (rating, accuracy, in main, ref toChange, weight) => PPContainer.MultiplyFast(ref toChange, weight)
            )
            {
                UpdateFCEnabled = pc.PPFC,
                UpdatePPEnabled = _formatting.UsesWeightedPpValues,
            };

            _weightedPpHandler.UpdateFC += (fullComboAccuracy, values, actions, weight) =>
            {
                values[2].SetValues(calc.GetPpWithSummedPp(fullComboAccuracy, pc.DecimalPrecision));

                actions(0, fullComboAccuracy, in values[2], ref values[3], weight);
            };
        }

        private string CreateCaptureMessage()
        {
            float difference = ppHandler.GetPPGroup(0).TotalPP - _neededPpValues.TotalPP;

            string color = pc.UseGrad
                ? HelpfulFormatter.NumberToGradient(difference)
                : HelpfulFormatter.NumberToColor(difference);

            if (pc.ShowClanMessage)
            {
                return _formatting.FormatMessage(
                    color,
                    _neededAccuracy,
                    _neededPpValues.AccPP,
                    _neededPpValues.PassPP,
                    _neededPpValues.TechPP,
                    _neededPpValues.TotalPP,
                    CreateTargetMessage()
                );
            }

            return _mainFormatting.FormatPercentNeeded(
                color,
                _neededAccuracy,
                _neededPpValues.PassPP,
                _neededPpValues.AccPP,
                _neededPpValues.TechPP,
                _neededPpValues.TotalPP
            );
        }

        private string CreateTargetMessage()
        {
            if (!pc.ShowEnemy || pc.Target == Targeter.NO_TARGET || !_targeter.IsInitialized)
            {
                return "";
            }

            return _mainFormatting.FormatTarget(_targeter.Target.TargetName.ClampString(pc.MaxNameLength), Mods);
        }

        private void UpdateWeightedCounter(float accuracy, int mistakes, float fullComboAccuracy)
        {
            if (_weightedPpHandler is null)
                return;

            PPContainer partial = _weightedPpHandler.PartialUpdate(accuracy, mistakes);

            float weight = _beatLeaderCalculator.GetWeight(partial.TotalPP, _clanPpValues, out int rank);

            _weightedPpHandler.FinishPartialUpdate(accuracy, fullComboAccuracy, weight);

            string rankColor = HelpfulFormatter.GetWeightedRankColor(rank);

            const string weightedLabel = " Weighted PP";

            if (pc.SplitPPVals && calc.RatingCount > 1)
            {
                int count = Math.Min(calc.DisplayRatingCount, CurrentLabels.Count);

                for (int index = 0; index < count; index++)
                {
                    bool isTotal = index == count - 1;

                    outpText.AppendLine(
                        _formatting.FormatWeighted(
                            _weightedPpHandler.DisplayFC,
                            pc.ExtraInfo && isTotal,
                            _showRank && isTotal,
                            mistakes,
                            rankColor,
                            rank,
                            _weightedPpHandler[1, index],
                            _weightedPpHandler[0, index],
                            _weightedPpHandler[3, index],
                            _weightedPpHandler[2, index],
                            isTotal ? weightedLabel : CurrentLabels[index],
                            _message
                        )
                    );
                }

                return;
            }

            outpText.AppendLine(
                _formatting.FormatWeighted(
                    _weightedPpHandler.DisplayFC,
                    pc.ExtraInfo,
                    _showRank,
                    mistakes,
                    rankColor,
                    rank,
                    _weightedPpHandler[1],
                    _weightedPpHandler[0],
                    _weightedPpHandler[3],
                    _weightedPpHandler[2],
                    weightedLabel,
                    _message
                )
            );
        }

        private enum ClanCounterMode
        {
            Loading,
            Capture,
            Captured,
            Unranked,
            LoadFailed,
            Uncapturable,
        }
    }
}
