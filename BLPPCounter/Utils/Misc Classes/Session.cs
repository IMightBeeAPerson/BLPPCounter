using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.List_Settings;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal class Session
    {
        public readonly Leaderboards Leaderboard;
        public readonly string UserId;
        private readonly List<Play> Scores;
        public readonly float InitialProfilePp;
        public readonly List<object> Info;

        public float GainedProfilePp => Scores.Aggregate(0.0f, (total, current) => total + current.ProfilePpGained);
        public int PlaysSet => Scores.Count;

        public Session(Leaderboards leaderboard, string userId, float initialProfilePp)
        {
            Leaderboard = leaderboard;
            UserId = userId;
            Scores = new List<Play>();
            InitialProfilePp = initialProfilePp;
            Info = new List<object>();
        }

        public void AddPlay(string mapName, string mapKey, BeatmapDifficulty diff, string mode, float rawPp, float profilePpGained, float oldPp = -1)
        {
            Scores.Add(new Play(mapName, mapKey, diff, mode, (float)Math.Round(rawPp, PluginConfig.Instance.DecimalPrecision), profilePpGained, (float)Math.Round(oldPp, PluginConfig.Instance.DecimalPrecision)));
            Info.Add(new SessionListInfo(Scores.Last()));
        }
    }
}
