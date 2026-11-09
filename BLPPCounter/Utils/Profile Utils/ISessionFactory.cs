using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal interface ISessionFactory
    {
        Session Create(Leaderboards leaderboard, string userId, float initialProfilePp);
    }
}
