namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IClanCounterFormattingService : IFormatRelationContributor
    {
        bool UsesMainPpValues { get; }

        bool UsesWeightedPpValues { get; }

        string FormatMain(
            bool displayFullCombo,
            bool displayExtraInformation,
            int mistakes,
            float ppDifference,
            float pp,
            float fullComboPpDifference,
            float fullComboPp,
            string label,
            string target,
            string message
        );

        string FormatWeighted(
            bool displayFullCombo,
            bool displayExtraInformation,
            bool showRank,
            int mistakes,
            string rankColor,
            int rank,
            float weightedPp,
            float pp,
            float weightedFullComboPp,
            float fullComboPp,
            string label,
            string message
        );

        string FormatMessage(
            string color,
            float accuracy,
            float accuracyPp,
            float passPp,
            float technologyPp,
            float totalPp,
            string target
        );
    }
}
