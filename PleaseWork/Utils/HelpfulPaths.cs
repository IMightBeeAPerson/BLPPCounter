using System;
using System.IO;

namespace PleaseWork.Utils
{
    public static class HelpfulPaths
    {
        public static readonly string PLAYLISTS = Path.Combine(Environment.CurrentDirectory, "Playlists");
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BL_REPLAY_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "Replays");
        public static readonly string BLAPI_HASH = "https://api.beatleader.xyz/leaderboards/hash/";
        public static readonly string BLAPI_USERID = "https://api.beatleader.xyz/user/id";
        public static readonly string BLAPI_CLAN = "https://api.beatleader.xyz/leaderboard/clanRankings/";
    }
}
