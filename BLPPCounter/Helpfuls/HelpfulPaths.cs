﻿using Newtonsoft.Json.Linq;
using System;
using System.IO;
using BLPPCounter.Utils;
using static GameplayModifiers;
using System.Text.RegularExpressions;
using System.Linq;

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
        #region API Paths
        public static readonly string BLAPI = "https://api.beatleader.xyz/";
        public static readonly string BLAPI_HASH = "https://api.beatleader.xyz/leaderboards/hash/";
        public static readonly string BLAPI_USERID = "https://api.beatleader.xyz/user/id";
        public static readonly string BLAPI_CLAN = "https://api.beatleader.xyz/leaderboard/clanRankings/";
        #endregion
        #region Resource Paths
        public static readonly string COUNTER_BSML = HOST_NAME + ".Settings.BSML.Settings.bsml";
        public static readonly string MENU_BSML = HOST_NAME + ".Settings.BSML.MenuSettings.bsml";
        public static readonly string SIMPLE_MENU_BSML = HOST_NAME + ".Settings.BSML.SimpleMenuSettings.bsml";
        public static readonly string SETTINGS_BSML = HOST_NAME + ".Settings.BSML.MainMenuSettings.bsml";
        public static readonly string PP_CALC_BSML = HOST_NAME + ".Settings.BSML.PpInfo.bsml";
        #endregion
        #region Json Paths
        public static float GetRating(JToken data, PPType type, SongSpeed mod = SongSpeed.Normal)
        {
            if (mod != SongSpeed.Normal) data = data["modifiersRating"];
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), mod);
            return (float)data[path];
        }
        public static float GetRating(JToken data, PPType type, string modName)
        {
            if (!modName.Equals("")) data = data["modifiersRating"];
            string path = HelpfulMisc.AddModifier(HelpfulMisc.PPTypeToRating(type), modName);
            return (float)data[path];
        }
        public static float[] GetAllRatingsOfSpeed(JToken data, SongSpeed mod = SongSpeed.Normal) =>
            new float[4] { GetRating(data, PPType.Acc, mod), GetRating(data, PPType.Pass, mod), GetRating(data, PPType.Tech, mod), GetRating(data, PPType.Star, mod) };
        public static float[] GetAllRatings(JToken data) =>
            GetAllRatingsOfSpeed(data, SongSpeed.Slower).Union(GetAllRatingsOfSpeed(data, SongSpeed.Normal)).Union(GetAllRatingsOfSpeed(data, SongSpeed.Faster)).Union(GetAllRatingsOfSpeed(data, SongSpeed.SuperFast)).ToArray();

        public static float GetMultiAmount(JToken data, string name)
        {
            MatchCollection mc = Regex.Matches(data["modifierValues"].ToString(), "^\\s*\"(.+?)\": *(-?\\d(?:\\.\\d+)?).*$", RegexOptions.Multiline);
            string val = mc.FirstOrDefault(m => m.Groups[1].Value.Equals(name))?.Groups[2].Value;
            return val is null ? 0 : float.Parse(val);
        }
        public static float GetMultiAmounts(JToken data, string[] names) 
        { 
            float outp = 1; 
            foreach (string n in names) outp += GetMultiAmount(data, n);
            return outp; 
        }
        #endregion
    }
}
