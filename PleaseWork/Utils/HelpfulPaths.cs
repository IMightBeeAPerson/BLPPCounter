using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PleaseWork.Utils
{
    public static class HelpfulPaths
    {
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BLAPI_HASH = "http://api.beatleader.xyz/leaderboards/hash/";
        public static readonly string BLAPI_MAP_DUMP = "https://api.beatleader.xyz/songsuggest/songs";
    }
}
