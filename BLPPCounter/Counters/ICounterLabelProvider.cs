using System.Collections.Generic;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Counters
{
    internal interface ICounterLabelProvider
    {
        IReadOnlyList<string> GetLabels(Calculator calculator, Leaderboards leaderboard);
    }
}
