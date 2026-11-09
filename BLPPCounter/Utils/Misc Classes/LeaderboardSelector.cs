using System;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal sealed class LeaderboardSelector
    {
        private readonly ILeaderboardApiProvider _apiProvider;
        private readonly ICalculatorProvider _calculatorProvider;

        public LeaderboardSelector(ILeaderboardApiProvider apiProvider, ICalculatorProvider calculatorProvider)
        {
            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            _calculatorProvider = calculatorProvider ?? throw new ArgumentNullException(nameof(calculatorProvider));
        }

        public APIHandler GetAPI(Leaderboards leaderboard)
        {
            return _apiProvider.Get(leaderboard);
        }

        public Calculator GetCalc(Leaderboards leaderboard)
        {
            return _calculatorProvider.Get(leaderboard);
        }
    }
}
