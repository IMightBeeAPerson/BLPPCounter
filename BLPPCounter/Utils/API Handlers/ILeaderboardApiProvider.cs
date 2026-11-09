using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.API_Handlers
{
    internal interface ILeaderboardApiProvider
    {
        APIHandler Get(Leaderboards leaderboard);

        BLAPI BeatLeader { get; }

        SSAPI ScoreSaber { get; }

        APAPI AccSaber { get; }
    }
}
