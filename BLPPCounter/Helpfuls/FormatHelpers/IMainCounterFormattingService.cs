using System.Collections.Generic;
using System.Text;
using BLPPCounter.Utils.Containers;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IMainCounterFormattingService : IFormatRelationContributor
    {
        bool DisplayUsable { get; }

        bool TargetUsable { get; }

        bool PercentNeededUsable { get; }

        string FormatTarget(string name, string modifiers);

        string FormatPercentNeeded(
            string color,
            float accuracy,
            float passPp,
            float accuracyPp,
            float technologyPp,
            float totalPp
        );

        void AppendPpText(
            bool displayFc,
            StringBuilder output,
            PPHandler ppValues,
            int mistakes,
            int displayRatingCount,
            IReadOnlyList<string> labels
        );
    }
}
