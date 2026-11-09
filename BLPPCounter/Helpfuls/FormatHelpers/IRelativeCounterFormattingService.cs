namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IRelativeCounterFormattingService : IFormatRelationContributor
    {
        bool UsesPpValues { get; }

        string Format(
            bool displayFullCombo,
            bool displayExtraInformation,
            int mistakes,
            string mistakeColor,
            float accuracyDifference,
            float ppDifference,
            float pp,
            float fullComboPpDifference,
            float fullComboPp,
            float targetAccuracy,
            string label,
            string target
        );
    }
}
