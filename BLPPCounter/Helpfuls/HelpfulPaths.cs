using Newtonsoft.Json.Linq;
using System;
using System.IO;
using BLPPCounter.Utils;
using static GameplayModifiers;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.Http;
using System.Text;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Containers;
using System.Collections.Generic;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulPaths
    {
        #region File Paths
        public static readonly string HOST_NAME = "BLPPCounter";
        public static readonly string PLAYLISTS = Path.Combine(Environment.CurrentDirectory, "Playlists");
        public static readonly string THE_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "PP Counter");
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BL_REPLAY_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "Replays");
        public static readonly string BL_REPLAY_CACHE_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "ReplayerCache");
        public static readonly string BL_REPLAY_HEADERS = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "ReplayHeadersCache");
        public static readonly string TAOHABLE_DATA = Path.Combine(THE_FOLDER, "TaohableData.json");
        public static readonly string HEADERS = Path.Combine(THE_FOLDER, "Headers.json");
        public static readonly string PROFILE_DATA = Path.Combine(THE_FOLDER, "Profiles.json");
        public static readonly string TARGET_DATA = Path.Combine(THE_FOLDER, "ExtraTargets.json");
        #endregion
        #region Resource Paths
        public static readonly string COUNTER_BSML = HOST_NAME + ".Settings.BSML.Settings.bsml";
        public static readonly string MENU_BSML = HOST_NAME + ".Settings.BSML.MenuSettings.bsml";
        public static readonly string SIMPLE_MENU_BSML = HOST_NAME + ".Settings.BSML.VariableSettingsContainer.bsml";
        public static readonly string SETTINGS_BSML = HOST_NAME + ".Settings.BSML.MainMenuSettings.bsml";
        public static readonly string PP_CALC_BSML = HOST_NAME + ".Settings.BSML.PpInfo.bsml";
        #endregion
        #region API Paths
        public static readonly string BLAPI = "https://api.beatleader.com/";
        public static readonly string BLAPI_HASH = "https://api.beatleader.com/leaderboards/hash/{0}"; //hash
        public static readonly string BLAPI_USERID = "https://api.beatleader.com/player/{0}?stats=false"; //user_id
        public static readonly string BLAPI_USERID_FULL = "https://api.beatleader.com/player/{0}"; //user_id
        public static readonly string BLAPI_CLAN = "https://api.beatleader.com/leaderboard/clanRankings/{0}"; //clan_id
        public static readonly string BLAPI_CLAN_PLAYERS = "https://api.beatleader.com/clan/{0}?count={1}"; //clan_name (the 4 letter one), count
        public static readonly string BLAPI_SCORE = "https://api.beatleader.com/score/general/{0}/{1}/{2}/{3}"; //user_id, hash, diff, mode || Ex: https://api.beatleader.com/score/general/76561198306905129/a3292aa17b782ee2a229800186324947a4ec9fee/Expert/Standard
        public static readonly string BLAPI_PLAYERSCORES = "https://api.beatleader.com/player/{0}/scores/compact?sortBy=pp&page={1}&count={2}&scoreStatus=0&leaderboardContext=general"; //user_id, page, count
        public static readonly string BLAPI_FOLLOWERS = "https://api.beatleader.com/player/{0}/followers?page={1}&count={2}&type=following"; //user_id, page, count
        //public static readonly string BLAPI_FOLLOWERS = "https://api.beatleader.com/players?page={0}&count={1}&leaderboardContext=general&friends=true"; //page, count
        public static readonly string BLAPI_PLAYER_FILTER = "https://api.beatleader.com/players?page={0}&count={1}&leaderboardContext=general"; //page, count
        public static readonly string BLAPI_SCOREVALUE = "https://api.beatleader.com/player/{0}/scorevalue/{1}/{2}/{3}"; //user_id, hash, diff, mode

        public static readonly string SSAPI = "https://scoresaber.com/api/";
        //UNRANKED: https://scoresaber.com/api/leaderboard/by-hash/bdacecbf446f0f066f4189c7fe1a81c6d3664b90/info?difficulty=5
        //RANKED: https://scoresaber.com/api/leaderboard/by-hash/7c44cdc1e33e2f5f929867b29ceb3860c3716ddc/info?difficulty=5
        /// <summary>Format in the order: hash value, "info" or "scores", diff number.</summary>
        public static readonly string SSAPI_HASH = "https://scoresaber.com/api/leaderboard/by-hash/{0}/{1}?difficulty={2}"; //hash, either "info" or "scores", diff_number
        public static readonly string SSAPI_USERID = "https://scoresaber.com/api/player/{0}/{1}"; //user_id, either "basic", "full", or "scores"
        public static readonly string SSAPI_DIFFS = "https://scoresaber.com/api/leaderboard/get-difficulties/{0}"; //hash
        public static readonly string SSAPI_LEADERBOARDID = "https://scoresaber.com/api/leaderboard/by-id/{0}/{1}"; //leaderboard_id, either "info" or "scores"
        public static readonly string SSAPI_PLAYERSCORES = "https://scoresaber.com/api/player/{0}/scores?limit={2}&sort=top&page={1}"; //user_id, page, count
        public static readonly string SSAPI_PLAYER_FILTER = "https://scoresaber.com/api/players?page={0}"; //page (count is always 50, sorted by rank)

        //No documentation here, doc at https://github.com/accsaber/accsaber-plugin/blob/main/EndpointResearch/ENDPOINTS.md
        //Or find it yourself here: https://github.com/accsaber/accsaber-backend/blob/main/accsaber-api/src/main/kotlin/de/ixsen/accsaber/api/controllers/PlayerController.kt
        public static readonly string APAPI = "https://api.accsaber.com/"; 
        public static readonly string APAPI_LEADERBOARDID = "https://api.accsaber.com/ranked-maps/{0}"; //Scoresaber_id
        public static readonly string APAPI_PLAYERID = "https://api.accsaber.com/players/{0}"; //user_id
        public static readonly string APAPI_SCORES = "https://api.accsaber.com/players/{0}/scores?page={1}&pageSize={2}"; //user_id, page, count
        public static readonly string APAPI_CATEGORY_SCORES = "https://api.accsaber.com/players/{0}/{1}/scores"; //user_id, accsaber category (true, standard, tech)
        public static readonly string APAPI_RECENT_SCORE = "https://api.accsaber.com/players/{0}/recent-scores?pageSize=1"; //user_id

        //Docs: https://api.beatsaver.com/docs/index.html
        public static readonly string BSAPI = "https://api.beatsaver.com/";
        public static readonly string BSAPI_MAPID = "https://api.beatsaver.com/maps/id/{0}";
        public static readonly string BSAPI_HASH = "https://api.beatsaver.com/maps/hash/{0}";

        public static readonly string TAOHABLE_API = "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/SongLibrary.json";
        public static readonly string TAOHABLE_META = "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/Files.meta";
        #endregion
        #region Directory Checks
        public static bool EnsureTaohableDirectoryExists()
        {
            string path = THE_FOLDER;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            path = Path.Combine(path, "TaohableData.json");
            (bool succeeded, HttpContent content) = APIHandler.CallAPI_Static(TAOHABLE_META).Result;
            if (!succeeded)
                throw new Exception("Taoh's meta file is not available. This means either internet is down or I need to go yell at someone.");
            JToken TaohHeaders = JToken.Parse(content.ReadAsStringAsync().Result);
            bool headersGood = true;
            if (!File.Exists(HEADERS)) using (FileStream fs = File.Create(HEADERS))
                {
                    byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                    fs.Write(headerInfo, 0, headerInfo.Length);
                    return false;
                } 
            else
            {
                string headerString = File.ReadAllText(HEADERS);
                if (headerString is null || headerString.Length == 0) using (FileStream fs = File.OpenWrite(HEADERS))
                    {
                        byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                        fs.Write(headerInfo, 0, headerInfo.Length);
                        return false;
                    }
                JToken CurrentTaohHeaders = null;
                try
                {
                    CurrentTaohHeaders = JToken.Parse(headerString);
                    int SimpleComparer<T>(T i1, T i2) where T : IComparable => i1.CompareTo(i2);
                    headersGood = HelpfulMisc.CompareStructValues<float>(TaohHeaders, CurrentTaohHeaders, "top10kVersion", SimpleComparer) <= 0
                        && HelpfulMisc.CompareStructValues<float>(TaohHeaders, CurrentTaohHeaders, "songLibraryVersion", SimpleComparer) <= 0
                        && HelpfulMisc.CompareValues(TaohHeaders, CurrentTaohHeaders, "top10kUpdated", item => DateTime.Parse(item), SimpleComparer) <= 0;
                } catch (Exception e)
                {
                    Plugin.Log.Warn("Oh No! Current Taoh Headers are not parseable! Automatically overriding the problem file.\n" + e);
                    File.Delete(HEADERS);
                    headersGood = false;
                }
                if (!headersGood)
                    using (FileStream fs = File.OpenWrite(HEADERS))
                    {
                        byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                        fs.Write(headerInfo, 0, headerInfo.Length);
                    }
            }
            return headersGood && File.Exists(path);
        }
        #endregion

        #region Json Paths
        public static float GetRating(JObject data, PPType type, SongSpeed mod = SongSpeed.Normal)
        {
            if (data is null) return 0;
            if (mod != SongSpeed.Normal && data["modifiersRating"] is not null) data = data["modifiersRating"] as JObject; //only BL uses more than one rating so this will work for now.
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), mod);
            //Below is a workaround for how Taoh formats his data.
            if (mod == SongSpeed.Normal && type == PPType.Star && data[path] is null) return (float)(data["star" + HelpfulMisc.ToCapName(PpInfoTabHandler.Instance.CurrentLeaderboard)] ?? 0);
            return (float)(data[path] ?? 0);
        }
        public static float GetRating(JObject data, PPType type, string modName)
        {
            if (!modName.Equals("") && data?["modifiersRating"] is not null) data = data["modifiersRating"] as JObject; //only BL uses more than one rating so this will work for now.
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), modName);
            return (float)(data?[path] ?? 0);
        }
        public static RatingContainer GetAllRatingsOfSpeed(JObject data, Calculator calc, SongSpeed mod = SongSpeed.Normal)
        { //star, acc, pass, tech
            float[] outp = [.. Enum.GetValues(typeof(PPType)).Cast<PPType>().Select(type => GetRating(data, type, mod))];
            return RatingContainer.GetContainer(calc?.Leaderboard ?? Leaderboards.None, outp);
        }
        public static RatingContainer GetAllRatingsOfSpeed(JObject data, SongSpeed mod = SongSpeed.Normal) => GetAllRatingsOfSpeed(data, null, mod);
        public static RatingContainer[] GetAllRatings(JObject data, Calculator calc)
        {
            List<RatingContainer> outp = new(4); //length of the SongSpeed Enum.
            foreach (SongSpeed s in HelpfulMisc.OrderedSpeeds)
                outp.Add(GetAllRatingsOfSpeed(data, calc, s));
            return [.. outp];
        }
        public static RatingContainer[] GetAllRatings(JObject data) => GetAllRatings(data, null);
        public static float GetMultiAmount(JObject data, string name)
        {
            if (!Calculator.GetSelectedCalc().UsesModifiers) return 1.0f;
            MatchCollection mc = Regex.Matches(data.TryEnter("difficulty")["modifierValues"].ToString(), @"^\s*""(.+?)"": *(-?\d(?:\.\d+)?).*$", RegexOptions.Multiline);
#if NEW_VERSION
            string val = mc.FirstOrDefault(m => m.Groups[1].Value.Equals(name))?.Groups[2].Value; // 1.37.0 and above
#else
            string val = mc.OfType<Match>().FirstOrDefault(m => m.Groups[1].Value.Equals(name))?.Groups[2].Value; // 1.34.2 and below
#endif
            return val is null ? 0 : float.Parse(val);
        }
        public static Dictionary<string, float> GetMultiAmounts(JObject data)
        {
            MatchCollection mc = Regex.Matches(data.TryEnter("difficulty")["modifierValues"].ToString(), @"^\s*""(.+?)"": *(-?\d(?:\.\d+)?).*$", RegexOptions.Multiline);
            Dictionary<string, float> multiAmounts = new(mc.Count - 1);
            foreach (Match m in mc)
            {
                if (m.Groups[1].Value.Equals("modifierId")) continue;
                multiAmounts.Add(m.Groups[1].Value, float.Parse(m.Groups[2].Value));
            }
            return multiAmounts;
        }
        public static float GetMultiAmounts(JObject data, string[] names) 
        {
            //Plugin.Log.Info(data.ToString());
            float outp = 1;
            Dictionary<string, float> vals = GetMultiAmounts(data);
            foreach (string n in names) outp += vals.TryGetValue(n, out float val) ? val : 0;
            return outp; 
        }
#endregion
    }
}
