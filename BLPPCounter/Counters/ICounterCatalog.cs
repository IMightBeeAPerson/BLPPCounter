using System.Collections.Generic;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Counters
{
    internal interface ICounterCatalog
    {
        bool IsUsable { get; }

        IReadOnlyList<CounterDescriptor> Counters { get; }

        IReadOnlyList<string> GetDisplayNames(Leaderboards leaderboard);

        bool TryGet(string displayName, out CounterDescriptor? descriptor);

        CounterDescriptor Get(string displayName);

        void ValidateConfiguration();
    }
}
