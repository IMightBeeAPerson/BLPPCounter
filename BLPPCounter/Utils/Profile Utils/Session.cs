using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal class Session(Leaderboards leaderboard, string userId, float initialProfilePp)
    {
        public readonly Leaderboards Leaderboard = leaderboard;
        public readonly string UserId = userId;
        private readonly List<Play> Scores = [];
        public readonly float InitialProfilePp = initialProfilePp;
        public readonly List<object> Info = [];
        private readonly AsyncLock locker = new();

        public float GainedProfilePp => Scores.Aggregate(0.0f, (total, current) => total + current.ProfilePpGained);
        public int PlaysSet => Scores.Count;

        public async Task<uint> AddPlay(string mapName, string hash, string mapKey, BeatmapDifficulty diff, string mode, float rawPp, uint rank, float profilePpGained, float oldPp = -1)
        {
            AsyncLock.Releaser theLock = await locker.LockAsync();
            using (theLock)
            {
                try
                {
                    Scores.Add(new Play(mapName, mapKey, diff, mode, (float)Math.Round(rawPp, PluginConfig.Instance.DecimalPrecision), rank, profilePpGained, (float)Math.Round(oldPp, PluginConfig.Instance.DecimalPrecision)));
                    Info.Add(new SessionListInfo(Scores.Last()));
                    if (rank == 0)
                        rank = await HandleRank(hash, diff.ToString(), mode);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error($"Error adding play.\n{e}");
                }
            }
            return rank;
        }

        private async Task<uint> HandleRank(string hash, string diff, string mode)
        {
            APIHandler api = APIHandler.GetAPI(Leaderboard);
            JToken scoreData = await api.GetScoreData(UserId, hash, diff, mode).ConfigureAwait(false);
            uint rank = (uint)api.GetRank(scoreData);
            Play p = Scores.Last();
            p.Rank = rank;
            Scores[Scores.Count - 1] = p;
            return rank;
        }
    }
}
