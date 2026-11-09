using System;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;

namespace BLPPCounter.Counters
{
    internal sealed class CounterDescriptor
    {
        public CounterDescriptor(
            Type counterType,
            string displayName,
            string displayHandler,
            int orderNumber,
            Leaderboards validLeaderboards
        )
        {
            if (counterType is null)
            {
                throw new ArgumentNullException(nameof(counterType));
            }

            if (!typeof(MyCounters).IsAssignableFrom(counterType) || counterType.IsAbstract)
            {
                throw new ArgumentException(
                    $"{counterType.FullName} is not a concrete " + $"{nameof(MyCounters)} implementation.",
                    nameof(counterType)
                );
            }

            if (BetterString.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A counter display name is required.", nameof(displayName));
            }

            if (BetterString.IsNullOrWhiteSpace(displayHandler))
            {
                throw new ArgumentException("A counter display handler is required.", nameof(displayHandler));
            }

            CounterType = counterType;
            DisplayName = displayName;
            DisplayHandler = displayHandler;
            OrderNumber = orderNumber;
            ValidLeaderboards = validLeaderboards;
        }

        public Type CounterType { get; }

        public string DisplayName { get; }

        public string DisplayHandler { get; }

        public int OrderNumber { get; }

        public Leaderboards ValidLeaderboards { get; }

        public bool Supports(Leaderboards leaderboard)
        {
            return (ValidLeaderboards & leaderboard) != Leaderboards.None;
        }
    }
}
