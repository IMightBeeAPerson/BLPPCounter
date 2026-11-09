using System;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.Utils.API_Handlers
{
    internal sealed class LeaderboardApiProvider : ILeaderboardApiProvider
    {
        public LeaderboardApiProvider(BLAPI beatLeader, SSAPI scoreSaber, APAPI accSaber)
        {
            BeatLeader = beatLeader ?? throw new ArgumentNullException(nameof(beatLeader));

            ScoreSaber = scoreSaber ?? throw new ArgumentNullException(nameof(scoreSaber));

            AccSaber = accSaber ?? throw new ArgumentNullException(nameof(accSaber));
        }

        public BLAPI BeatLeader { get; }

        public SSAPI ScoreSaber { get; }

        public APAPI AccSaber { get; }

        public APIHandler Get(Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Beatleader => BeatLeader,
                Leaderboards.Scoresaber => ScoreSaber,
                Leaderboards.Accsaber => AccSaber,

                _ => throw new ArgumentOutOfRangeException(
                    nameof(leaderboard),
                    leaderboard,
                    "A single supported leaderboard is required."
                ),
            };
        }
    }
}
