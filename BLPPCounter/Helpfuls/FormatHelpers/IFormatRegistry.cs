using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal interface IFormatRegistry
    {
        event Action? FormatsRebuilt;

        IReadOnlyDictionary<(string FormatName, string CounterName), FormatRelation> Relations { get; }

        IReadOnlyList<string> CounterNames { get; }

        FormatRelation Get(string formatName, string counterName);

        bool TryGet(string formatName, string counterName, [NotNullWhen(true)] out FormatRelation? relation);

        IReadOnlyList<FormatRelation> GetForCounter(string counterName);

        void Rebuild(FormatRelation relation);

        void RebuildAll();

        void ReapplyAliases();
    }
}
