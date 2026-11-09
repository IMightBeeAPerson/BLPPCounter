namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IRankCounterFormattingService : IFormatRelationContributor
    {
        bool UsesPpValues { get; }

        string Format(
            bool displayFullCombo,
            bool displayExtraInformation,
            bool isFirstPlace,
            float pp,
            float fullComboPp,
            int rank,
            string nextPlayerName,
            float ppDifference,
            float accuracyDifference,
            string rankColor,
            string label
        );
    }
}
