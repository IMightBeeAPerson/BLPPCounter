using System;
using System.IO;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulPaths
    {
        #region File Paths
        public const string HOST_NAME = "BLPPCounter";
        public static readonly string PLAYLISTS = Path.Combine(Environment.CurrentDirectory, "Playlists");
        public static readonly string THE_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "PP Counter");
        public static readonly string BL_CACHE_FILE = Path.Combine(
            Environment.CurrentDirectory,
            "UserData",
            "BeatLeader",
            "LeaderboardsCache"
        );
        public static readonly string BL_REPLAY_FOLDER = Path.Combine(
            Environment.CurrentDirectory,
            "UserData",
            "BeatLeader",
            "Replays"
        );
        public static readonly string BL_REPLAY_CACHE_FOLDER = Path.Combine(
            Environment.CurrentDirectory,
            "UserData",
            "BeatLeader",
            "ReplayerCache"
        );
        public static readonly string BL_REPLAY_HEADERS = Path.Combine(
            Environment.CurrentDirectory,
            "UserData",
            "BeatLeader",
            "ReplayHeadersCache"
        );
        public static readonly string TAOHABLE_DATA = Path.Combine(THE_FOLDER, "TaohableData.json");
        public static readonly string HEADERS = Path.Combine(THE_FOLDER, "Headers.json");
        public static readonly string PROFILE_DATA = Path.Combine(THE_FOLDER, "Profiles.json");
        public static readonly string TARGET_DATA = Path.Combine(THE_FOLDER, "ExtraTargets.json");
        #endregion
        #region Resource Paths
        public const string COUNTER_BSML = HOST_NAME + ".Settings.BSML.Settings.bsml";
        public const string MENU_BSML = HOST_NAME + ".Settings.BSML.MenuSettings.bsml";
        public const string SIMPLE_MENU_BSML = HOST_NAME + ".Settings.BSML.VariableSettingsContainer.bsml";
        public const string SETTINGS_BSML = HOST_NAME + ".Settings.BSML.MainMenuSettings.bsml";
        public const string PP_CALC_BSML = HOST_NAME + ".Settings.BSML.PpInfo.bsml";
        #endregion
        #region API Paths
        public const string BLAPI = "https://api.beatleader.com/";
        public const string BLAPI_HASH = "https://api.beatleader.com/leaderboards/hash/{0}"; //hash
        public const string BLAPI_HASH_FULL = "https://api.beatleader.com/v5/scores/{0}/{1}/{2}?page={3}&count={4}"; //hash, diff, mode, page, count
        public const string BLAPI_USERID = "https://api.beatleader.com/player/{0}?stats=false"; //user_id
        public const string BLAPI_USERID_FULL = "https://api.beatleader.com/player/{0}"; //user_id
        public const string BLAPI_CLAN = "https://api.beatleader.com/leaderboard/clanRankings/{0}"; //clan_id
        public const string BLAPI_CLAN_PLAYERS = "https://api.beatleader.com/clan/{0}?count={1}"; //clan_name (the 4 letter one), count
        public const string BLAPI_SCORE = "https://api.beatleader.com/score/general/{0}/{1}/{2}/{3}"; //user_id, hash, diff, mode || Ex: https://api.beatleader.com/score/general/76561198306905129/a3292aa17b782ee2a229800186324947a4ec9fee/Expert/Standard
        public const string BLAPI_PLAYERSCORES =
            "https://api.beatleader.com/player/{0}/scores/compact?sortBy=pp&page={1}&count={2}&scoreStatus=0&leaderboardContext=general"; //user_id, page, count
        public const string BLAPI_FOLLOWERS =
            "https://api.beatleader.com/player/{0}/followers?page={1}&count={2}&type=following"; //user_id, page, count

        //public const string BLAPI_FOLLOWERS = "https://api.beatleader.com/players?page={0}&count={1}&leaderboardContext=general&friends=true"; //page, count
        public const string BLAPI_PLAYER_FILTER =
            "https://api.beatleader.com/players?page={0}&count={1}&leaderboardContext=general"; //page, count
        public const string BLAPI_SCOREVALUE = "https://api.beatleader.com/player/{0}/scorevalue/{1}/{2}/{3}"; //user_id, hash, diff, mode

        public const string SSAPI = "https://scoresaber.com/api/";

        //UNRANKED: https://scoresaber.com/api/leaderboard/by-hash/bdacecbf446f0f066f4189c7fe1a81c6d3664b90/info?difficulty=5
        //RANKED: https://scoresaber.com/api/leaderboard/by-hash/7c44cdc1e33e2f5f929867b29ceb3860c3716ddc/info?difficulty=5
        //Score: https://scoresaber.com/api/player/76561198306905129/scores?limit=1&sort=top&page=1
        /// <summary>Format in the order: hash value, "info" or "scores", diff number.</summary>
        public const string SSAPI_HASH = "https://scoresaber.com/api/leaderboard/by-hash/{0}/{1}?difficulty={2}"; //hash, either "info" or "scores", diff_number
        public const string SSAPI_HASH_PAGED =
            "https://scoresaber.com/api/leaderboard/by-hash/{0}/{1}?difficulty={2}&page={3}"; //hash, either "info" or "scores", diff_number, page
        public const string SSAPI_USERID = "https://scoresaber.com/api/player/{0}/{1}"; //user_id, either "basic", "full", or "scores"
        public const string SSAPI_DIFFS = "https://scoresaber.com/api/leaderboard/get-difficulties/{0}"; //hash
        public const string SSAPI_LEADERBOARDID = "https://scoresaber.com/api/leaderboard/by-id/{0}/{1}"; //leaderboard_id, either "info" or "scores"
        public const string SSAPI_PLAYERSCORES =
            "https://scoresaber.com/api/player/{0}/scores?limit={2}&sort=top&page={1}"; //user_id, page, count
        public const string SSAPI_PLAYER_FILTER = "https://scoresaber.com/api/players?page={0}"; //page (count is always 50, sorted by rank)

        /* accsaber.com API is currently very unstable and prone to 500 errors, so I'm not using it for now. If it becomes more stable in the future, I can add it back in. For now, the endpoints are here for reference if I want to add it back in later.
        //No documentation here, doc at https://github.com/accsaber/accsaber-plugin/blob/main/EndpointResearch/ENDPOINTS.md
        //Or find it yourself here: https://github.com/accsaber/accsaber-backend/blob/main/accsaber-api/src/main/kotlin/de/ixsen/accsaber/api/controllers/PlayerController.kt
        public const string APAPI = "https://api.accsaber.com/";
        public const string APAPI_LEADERBOARDID = "https://api.accsaber.com/ranked-maps/{0}"; //Scoresaber_id
        public const string APAPI_PLAYERID = "https://api.accsaber.com/players/{0}"; //user_id
        public const string APAPI_SCORES = "https://api.accsaber.com/players/{0}/scores?page={1}&pageSize={2}"; //user_id, page, count
        public const string APAPI_CATEGORY_SCORES = "https://api.accsaber.com/players/{0}/{1}/scores"; //user_id, accsaber category (true, standard, tech)
        public const string APAPI_RECENT_SCORE = "https://api.accsaber.com/players/{0}/recent-scores?pageSize=1"; //user_id
        //*/

        //Docs: https://api.accsaberreloaded.com/v1/docs
        // Category ID: b0000000-0000-0000-0000-000000000003 for Tech, 2 = Standard, 1 = True, none for overall.
        // Score endpoint example: https://api.accsaberreloaded.com/v1/users/76561198306905129/scores/by-hash/2a579bb1a3efa58af7640f9663c972ee84fea44a?difficulty=EXPERT&characteristic=Standard
        // Diff endpoint example: https://api.accsaberreloaded.com/v1/maps/hash/2A579BB1A3EFA58AF7640F9663C972EE84FEA44A?difficulty=EXPERT
        public const string APAPI = "https://api.accsaberreloaded.com/";
        public const string APAPI_PLAYERID = "https://api.accsaberreloaded.com/v1/users/{0}/statistics"; //user_id
        public const string APAPI_PLAYERID_CATEGORY =
            "https://api.accsaberreloaded.com/v1/users/{0}/statistics?category={1}"; //user_id, category (overall, true_acc, standard_acc, tech_acc)
        public const string APAPI_SCORE =
            "https://api.accsaberreloaded.com/v1/users/{0}/scores/by-hash/{1}?difficulty={2}&characteristic=Standard"; //user_id, hash, difficulty IN CAPS
        public const string APAPI_SCORES = "https://api.accsaberreloaded.com/v1/users/{0}/scores?page={1}&size={2}"; //user_id, page (zero indexed), count
        public const string APAPI_CATEGORY_SCORES =
            "https://api.accsaberreloaded.com/v1/users/{0}/scores?categoryId={1}&page={2}&size={3}"; // user_id, category_id, page (zero indexed), count
        public const string APAPI_HASH = "https://api.accsaberreloaded.com/v1/maps/hash/{0}"; //hash
        public const string APAPI_HASH_DIFF = "https://api.accsaberreloaded.com/v1/maps/hash/{0}?difficulty={1}"; //hash, difficulty IN CAPS
        public const string APAPI_LEADERBOARD_DIFF =
            "https://api.accsaberreloaded.com/v1/maps/difficulties/{0}/scores?page={1}&size={2}"; //diff_id, page (zero indexed), count

        //Docs: https://api.beatsaver.com/docs/index.html
        public const string BSAPI = "https://api.beatsaver.com/";
        public const string BSAPI_MAPID = "https://api.beatsaver.com/maps/id/{0}";
        public const string BSAPI_HASH = "https://api.beatsaver.com/maps/hash/{0}";

        public const string TAOHABLE_API =
            "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/SongLibrary.json";
        public const string TAOHABLE_META =
            "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/Files.meta";
        #endregion
    }
}
