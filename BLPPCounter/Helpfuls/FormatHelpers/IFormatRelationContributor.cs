using System.Collections.Generic;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IFormatRelationContributor
    {
        IReadOnlyList<FormatRelation> Relations { get; }

        void Rebuild(FormatRelation relation);

        void RebuildAll();
    }
}
