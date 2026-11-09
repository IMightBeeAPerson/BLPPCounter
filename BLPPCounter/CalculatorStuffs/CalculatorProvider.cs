using System;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.CalculatorStuffs
{
    internal sealed class CalculatorProvider : ICalculatorProvider
    {
        private readonly BLCalc _beatLeaderCalculator;
        private readonly SSCalc _scoreSaberCalculator;
        private readonly APCalc _accSaberCalculator;

        public CalculatorProvider(BLCalc beatLeaderCalculator, SSCalc scoreSaberCalculator, APCalc accSaberCalculator)
        {
            _beatLeaderCalculator =
                beatLeaderCalculator ?? throw new ArgumentNullException(nameof(beatLeaderCalculator));

            _scoreSaberCalculator =
                scoreSaberCalculator ?? throw new ArgumentNullException(nameof(scoreSaberCalculator));

            _accSaberCalculator = accSaberCalculator ?? throw new ArgumentNullException(nameof(accSaberCalculator));
        }

        public Calculator Get(Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Beatleader => _beatLeaderCalculator,
                Leaderboards.Scoresaber => _scoreSaberCalculator,
                Leaderboards.Accsaber => _accSaberCalculator,

                _ => throw new ArgumentOutOfRangeException(
                    nameof(leaderboard),
                    leaderboard,
                    "A single supported leaderboard is required."
                ),
            };
        }
    }
}
