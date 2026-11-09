using System;
using System.Threading;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Models;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;

namespace BLPPCounter.Counters
{
    public sealed class ProgressCounter : MyCounters
    {
        private float _totalNotes;

        public static string DisplayName => "Progressive";

        public static string DisplayHandler => MainCounterFormattingService.CounterName;

        public static int OrderNumber => 1;

        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        public override string Name => DisplayName;

        public override void ReinitCounter(MapSelection map)
        {
            _totalNotes = GetTotalNotes(map);
        }

        public override void SetupData(MapSelection map, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _totalNotes = GetTotalNotes(map);

            ppHandler = new PPHandler(
                ratings,
                calc,
                pc.DecimalPrecision,
                2,
                (rating, accuracy, in PPContainer main, ref PPContainer toChange, multiplier) =>
                    PPContainer.MultiplyFast(ref toChange, multiplier)
            )
            {
                UpdateFCEnabled = pc.PPFC,
            };

            ppHandler.UpdateFC += (accuracy, extraValues, extraCalls, multiplier) =>
            {
                extraValues[2].SetValues(calc.GetPpWithSummedPp(accuracy, pc.DecimalPrecision, ratings));

                extraCalls(0, accuracy, in extraValues[2], ref extraValues[3], multiplier);
            };
        }

        public override void UpdateFormat() { }

        public override void UpdateCounterInternal(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            float progress = _totalNotes <= 0f ? 1f : Math.Min(1f, notes / _totalNotes);

            ppHandler.Update(accuracy, mistakes, fullComboAccuracy, progress);

            MainFormatting.AppendPpText(
                ppHandler.DisplayFC,
                outpText,
                ppHandler,
                mistakes,
                calc.DisplayRatingCount,
                CurrentLabels
            );
        }

        public override void SoftUpdate(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        ) { }

        private static float GetTotalNotes(MapSelection map)
        {
            if (map.MapData.Data is not BlCacheEntry beatLeaderData)
            {
                return 0f;
            }

            return HelpfulMath.NotesForMaxScore(beatLeaderData.DifficultyInfo.MaxScore);
        }
    }
}
