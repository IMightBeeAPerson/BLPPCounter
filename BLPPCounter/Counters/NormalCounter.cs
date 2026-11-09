using System.Threading;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;

namespace BLPPCounter.Counters
{
    public sealed class NormalCounter : MyCounters
    {
        public static string DisplayName => "Normal";

        public static string DisplayHandler => MainCounterFormattingService.CounterName;

        public static int OrderNumber => 0;

        public static Leaderboards ValidLeaderboards => Leaderboards.All;

        public override string Name => DisplayName;

        public override void SetupData(MapSelection map, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ppHandler = new PPHandler(ratings, calc, pc.DecimalPrecision, 1) { UpdateFCEnabled = pc.PPFC };

            ppHandler.UpdateFC += (accuracy, extraValues, _, _) =>
                extraValues[1].SetValues(calc.GetPpWithSummedPp(accuracy, pc.DecimalPrecision, ratings));
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
            ppHandler.Update(accuracy, mistakes, fullComboAccuracy);

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
    }
}
