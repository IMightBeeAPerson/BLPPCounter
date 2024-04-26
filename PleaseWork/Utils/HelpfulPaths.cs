using System;
using System.IO;

namespace PleaseWork.Utils
{
    public static class HelpfulPaths
    {
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BLAPI_HASH = "https://api.beatleader.xyz/leaderboards/hash/";
        public static readonly string BLAPI_USERID = "https://api.beatleader.xyz/user/id";
        public static readonly string BLAPI_MAP_DUMP = "https://api.beatleader.xyz/songsuggest/songs";
    }
}
