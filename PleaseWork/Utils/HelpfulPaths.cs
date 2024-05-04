using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace PleaseWork.Utils
{
    public static class HelpfulPaths
    {
        #region File Paths
        public static readonly string PLAYLISTS = Path.Combine(Environment.CurrentDirectory, "Playlists");
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BL_REPLAY_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "Replays");
        #endregion
        #region API Paths
        public static readonly string BLAPI_HASH = "https://api.beatleader.xyz/leaderboards/hash/";
        public static readonly string BLAPI_USERID = "https://api.beatleader.xyz/user/id";
        public static readonly string BLAPI_CLAN = "https://api.beatleader.xyz/leaderboard/clanRankings/";
        #endregion
        #region Json Paths
        public static float GetRating(JToken data, PPType type, double speed = 1.0) => GetRating(data, type, HelpfulMisc.SpeedToModifier(speed));
        public static float GetRating(JToken data, PPType type, Modifier mod)
        {
            if (mod != Modifier.None) data = data["modifiersRating"];
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), mod);
            return (float)data[path];
        }
        #endregion
    }
}
