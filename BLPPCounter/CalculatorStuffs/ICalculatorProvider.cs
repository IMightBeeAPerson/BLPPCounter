using BLPPCounter.Utils.Enums;

namespace BLPPCounter.CalculatorStuffs
{
    internal interface ICalculatorProvider
    {
        Calculator Get(Leaderboards leaderboard);
    }
}
