﻿using Newtonsoft.Json.Linq;
using System;
using System.IO;
using BLPPCounter.Utils;
using static GameplayModifiers;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.Http;
using IPA.Utilities;
using System.Text;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.API_Handlers;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulPaths
    {
        #region File Paths
        public static readonly string HOST_NAME = "BLPPCounter";
        public static readonly string PLAYLISTS = Path.Combine(Environment.CurrentDirectory, "Playlists");
        public static readonly string THE_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", HOST_NAME);
        public static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        public static readonly string BL_REPLAY_FOLDER = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "Replays");
        #endregion
        #region Resource Paths
        public static readonly string COUNTER_BSML = HOST_NAME + ".Settings.BSML.Settings.bsml";
        public static readonly string MENU_BSML = HOST_NAME + ".Settings.BSML.MenuSettings.bsml";
        public static readonly string SIMPLE_MENU_BSML = HOST_NAME + ".Settings.BSML.Test.bsml";
        public static readonly string SETTINGS_BSML = HOST_NAME + ".Settings.BSML.MainMenuSettings.bsml";
        public static readonly string PP_CALC_BSML = HOST_NAME + ".Settings.BSML.PpInfo.bsml";
        #endregion
        #region API Paths
        public static readonly string BLAPI = "https://api.beatleader.xyz/";
        public static readonly string BLAPI_HASH = "https://api.beatleader.xyz/leaderboards/hash/{0}"; //hash
        public static readonly string BLAPI_USERID = "https://api.beatleader.xyz/user/id/{0}"; //user_id
        public static readonly string BLAPI_CLAN = "https://api.beatleader.xyz/leaderboard/clanRankings/{0}"; //clan_id
        public static readonly string BLAPI_SCORE = "https://api.beatleader.xyz/score/8/{0}/{1}/{2}/{3}"; //user_id, hash, diff, mode || Ex: https://api.beatleader.xyz/score/8/76561198306905129/a3292aa17b782ee2a229800186324947a4ec9fee/Expert/Standard

        public static readonly string SSAPI = "https://scoresaber.com/api/";
        //UNRANKED: https://scoresaber.com/api/leaderboard/by-hash/bdacecbf446f0f066f4189c7fe1a81c6d3664b90/info?difficulty=5
        //RANKED: https://scoresaber.com/api/leaderboard/by-hash/7c44cdc1e33e2f5f929867b29ceb3860c3716ddc/info?difficulty=5
        /// <summary>Format in the order: hash value, "info" or "scores", diff number.</summary>
        public static readonly string SSAPI_HASH = "https://scoresaber.com/api/leaderboard/by-hash/{0}/{1}?difficulty={2}"; //hash, either "info" or "scores", diff_number
        public static readonly string SSAPI_USERID = "https://scoresaber.com/api/player/{0}/{1}"; //user_id, either "basic", "full", or "scores"
        public static readonly string SSAPI_DIFFS = "https://scoresaber.com/api/leaderboard/get-difficulties/{0}"; //hash
        public static readonly string SSAPI_LEADERBOARDID = "https://scoresaber.com/api/leaderboard/by-id/{0}/{1}"; //leaderboard_id, either "info" or "scores"

        public static readonly string TAOHABLE_API = "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/SongLibrary.json";
        public static readonly string TAOHABLE_META = "https://raw.githubusercontent.com/HypersonicSharkz/SmartSongSuggest/master/TaohSongSuggest/Configuration/InitialData/Files.meta";
        #endregion
        #region Directory Checks
        public static bool EnsureTaohableDirectoryExists()
        {
            string path = Path.Combine(UnityGame.InstallPath, "UserData", "PP Counter");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            path = Path.Combine(path, "TaohableData.json");
            if (!APIHandler.CallAPI_Static(TAOHABLE_META, out HttpContent content))
                throw new Exception("Taoh's meta file is not available. This either means internet is down or I need to go yell at someone.");
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
                JToken CurrentTaohHeaders = JToken.Parse(headerString);
                int SimpleComparer<T>(T i1, T i2) where T : IComparable => i1.CompareTo(i2);
                headersGood = HelpfulMisc.CompareStructValues<float>(TaohHeaders, CurrentTaohHeaders, "top10kVersion", SimpleComparer) <= 0
                    && HelpfulMisc.CompareStructValues<float>(TaohHeaders, CurrentTaohHeaders, "songLibraryVersion", SimpleComparer) <= 0
                    && HelpfulMisc.CompareValues(TaohHeaders, CurrentTaohHeaders, "top10kUpdated", item => DateTime.Parse(item), SimpleComparer) <= 0;
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
        public static readonly string TAOHABLE_DATA = Path.Combine(UnityGame.InstallPath, "UserData", "PP Counter", "TaohableData.json");
        public static readonly string HEADERS = Path.Combine(UnityGame.InstallPath, "UserData", "PP Counter", "Headers.json");
        public static float GetRating(JToken data, PPType type, SongSpeed mod = SongSpeed.Normal)
        {
            if (mod != SongSpeed.Normal) data = data["modifiersRating"]; //only BL uses more than one rating so this will work for now.
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), mod);
            return (float)(data?[path] ?? 0);
        }
        public static float GetRating(JToken data, PPType type, string modName)
        {
            if (!modName.Equals("")) data = data["modifiersRating"]; //only BL uses more than one rating so this will work for now.
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), modName);
            return (float)(data?[path] ?? 0);
        }
        public static float[] GetAllRatingsOfSpeed(JToken data, Calculator calc, SongSpeed mod = SongSpeed.Normal)
        { //star, acc, pass, tech
            float[] outp = calc.SelectRatings(GetRating(data, PPType.Star, mod), GetRating(data, PPType.Acc, mod), GetRating(data, PPType.Pass, mod), GetRating(data, PPType.Tech, mod));
            outp = outp.FilledWithDefaults() ? new float[0] : outp;
            return outp;
        }
        public static float[] GetAllRatings(JToken data, Calculator calc) =>
            GetAllRatingsOfSpeed(data, calc, SongSpeed.Slower).Union(GetAllRatingsOfSpeed(data, calc, SongSpeed.Normal)).Union(GetAllRatingsOfSpeed(data, calc, SongSpeed.Faster)).Union(GetAllRatingsOfSpeed(data, calc, SongSpeed.SuperFast)).ToArray();

        public static float GetMultiAmount(JToken data, string name)
        {
            if (!Calculator.GetSelectedCalc().UsesModifiers) return 1.0f;
            MatchCollection mc = Regex.Matches(data.TryEnter("difficulty")["modifierValues"].ToString(), "^\\s*\"(.+?)\": *(-?\\d(?:\\.\\d+)?).*$", RegexOptions.Multiline);
            //string val = mc.FirstOrDefault(m => m.Groups[1].Value.Equals(name))?.Groups[2].Value; // 1.37.0 and above
            string val = mc.OfType<Match>().FirstOrDefault(m => m.Groups[1].Value.Equals(name))?.Groups[2].Value; // 1.34.2 and below
            return val is null ? 0 : float.Parse(val);
        }
        public static float GetMultiAmounts(JToken data, string[] names) 
        {
            //Plugin.Log.Info(data.ToString());
            float outp = 1; 
            foreach (string n in names) outp += GetMultiAmount(data, n);
            return outp; 
        }
        #endregion
    }
}
