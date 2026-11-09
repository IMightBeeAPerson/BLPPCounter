using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Misc_Classes;
using Zenject;

namespace BLPPCounter.Counters
{
    internal sealed class CounterCatalog : ICounterCatalog, IInitializable
    {
        private readonly PluginConfig _config;
        private readonly IReadOnlyList<CounterDescriptor> _counters;
        private readonly Dictionary<string, CounterDescriptor> _countersByDisplayName;

        public CounterCatalog(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            CounterDescriptor[] counters =
            [
                new(
                    typeof(NormalCounter),
                    NormalCounter.DisplayName,
                    NormalCounter.DisplayHandler,
                    NormalCounter.OrderNumber,
                    NormalCounter.ValidLeaderboards
                ),
                new(
                    typeof(ProgressCounter),
                    ProgressCounter.DisplayName,
                    ProgressCounter.DisplayHandler,
                    ProgressCounter.OrderNumber,
                    ProgressCounter.ValidLeaderboards
                ),
                new(
                    typeof(RelativeCounter),
                    RelativeCounter.DisplayName,
                    RelativeCounter.DisplayHandler,
                    RelativeCounter.OrderNumber,
                    RelativeCounter.ValidLeaderboards
                ),
                new(
                    typeof(ClanCounter),
                    ClanCounter.DisplayName,
                    ClanCounter.DisplayHandler,
                    ClanCounter.OrderNumber,
                    ClanCounter.ValidLeaderboards
                ),
                new(
                    typeof(RankCounter),
                    RankCounter.DisplayName,
                    RankCounter.DisplayHandler,
                    RankCounter.OrderNumber,
                    RankCounter.ValidLeaderboards
                ),
            ];

            _counters = [.. counters.OrderBy(descriptor => descriptor.OrderNumber)];

            _countersByDisplayName = _counters.ToDictionary(
                descriptor => descriptor.DisplayName,
                StringComparer.Ordinal
            );
        }

        public bool IsUsable =>
            _counters.Count > 0 && _config.LeaderboardsInUse.Any(leaderboard => GetDisplayNames(leaderboard).Count > 0);

        public IReadOnlyList<CounterDescriptor> Counters => _counters;

        public void Initialize()
        {
            ValidateConfiguration();
        }

        public IReadOnlyList<string> GetDisplayNames(Leaderboards leaderboard)
        {
            if (leaderboard == Leaderboards.None)
                return [];

            return
            [
                .. _counters
                    .Where(descriptor => descriptor.Supports(leaderboard))
                    .Select(descriptor => descriptor.DisplayName),
            ];
        }

        public bool TryGet(string displayName, out CounterDescriptor? descriptor)
        {
            if (BetterString.IsNullOrWhiteSpace(displayName))
            {
                descriptor = null;
                return false;
            }

            return _countersByDisplayName.TryGetValue(displayName, out descriptor);
        }

        public CounterDescriptor Get(string displayName)
        {
            if (!TryGet(displayName, out CounterDescriptor? descriptor))
            {
                throw new KeyNotFoundException(
                    $"\"{displayName}\" is not a registered "
                        + "counter. Registered counters: "
                        + string.Join(", ", _countersByDisplayName.Keys)
                );
            }

            return descriptor;
        }

        public void ValidateConfiguration()
        {
            RemoveInvalidLeaderboards();

            if (_config.LeaderboardsInUse.Count == 0)
            {
                Leaderboards replacement = FindFirstSupportedLeaderboard();

                if (replacement == Leaderboards.None)
                {
                    Plugin.Log.Critical("No registered counter supports any " + "leaderboard.");

                    return;
                }

                _config.LeaderboardsInUse.Add(replacement);

                Plugin.Log.Warn("No usable leaderboard was configured. " + $"Added {replacement}.");
            }

            Leaderboards selectedLeaderboard = _config.LeaderboardsInUse[0];

            IReadOnlyList<string> available = GetDisplayNames(selectedLeaderboard);

            if (available.Count == 0)
            {
                Plugin.Log.Critical($"No counter supports configured leaderboard " + $"{selectedLeaderboard}.");

                return;
            }

            if (
                !_countersByDisplayName.TryGetValue(_config.PPType, out CounterDescriptor? selected)
                || !selected.Supports(selectedLeaderboard)
            )
            {
                _config.PPType = available[0];
            }

            if (
                BetterString.IsNullOrWhiteSpace(_config.RelativeDefault)
                || (
                    _config.RelativeDefault != Targeter.NO_TARGET
                    && (
                        !_countersByDisplayName.TryGetValue(
                            _config.RelativeDefault,
                            out CounterDescriptor? relativeDefault
                        )
                        || !relativeDefault.Supports(selectedLeaderboard)
                        || relativeDefault.CounterType == typeof(RelativeCounter)
                    )
                )
            )
            {
                _config.RelativeDefault =
                    available.FirstOrDefault(displayName =>
                        _countersByDisplayName[displayName].CounterType != typeof(RelativeCounter)
                    ) ?? Targeter.NO_TARGET;
            }
        }

        private void RemoveInvalidLeaderboards()
        {
            HashSet<Leaderboards> seen = [];

            for (int index = _config.LeaderboardsInUse.Count - 1; index >= 0; index--)
            {
                Leaderboards leaderboard = _config.LeaderboardsInUse[index];

                bool usable =
                    leaderboard != Leaderboards.None && seen.Add(leaderboard) && GetDisplayNames(leaderboard).Count > 0;

                if (!usable)
                {
                    _config.LeaderboardsInUse.RemoveAt(index);
                }
            }
        }

        private Leaderboards FindFirstSupportedLeaderboard()
        {
            foreach (Leaderboards leaderboard in Enum.GetValues(typeof(Leaderboards)))
            {
                if (leaderboard is Leaderboards.None or Leaderboards.All)
                {
                    continue;
                }

                if (GetDisplayNames(leaderboard).Count > 0)
                {
                    return leaderboard;
                }
            }

            return Leaderboards.None;
        }
    }
}
