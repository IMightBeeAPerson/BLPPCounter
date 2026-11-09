using System;
using System.Collections.Generic;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Counters
{
    internal sealed class CounterLabelProvider : ICounterLabelProvider
    {
        private static readonly string[] Labels = [" Acc PP", " Pass PP", " Tech PP", " PP"];

        private readonly PluginConfig _config;

        public CounterLabelProvider(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IReadOnlyList<string> GetLabels(Calculator calculator, Leaderboards leaderboard)
        {
            if (calculator is null)
            {
                throw new ArgumentNullException(nameof(calculator));
            }

            int count = calculator.DisplayRatingCount;

            if (count <= 0)
                return [];

            string leaderboardPrefix = _config.LeaderInLabel ? calculator.Label : "";

            string[] result = new string[count];

            for (int index = 0; index < count; index++)
            {
                if (_config.LeaderInLabel && leaderboard != Leaderboards.Accsaber)
                {
                    int labelIndex = count == 1 ? Labels.Length - 1 : Math.Min(index, Labels.Length - 1);

                    result[index] = leaderboardPrefix + Labels[labelIndex];
                }
                else
                {
                    result[index] = leaderboardPrefix;
                }
            }

            return result;
        }
    }
}
