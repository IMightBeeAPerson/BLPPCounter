using static GameplayModifiers;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using BLPPCounter.Settings.Configs;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;

namespace BLPPCounter.Utils.API_Handlers
{
    internal abstract class APIHandler
    {
        protected static readonly HttpClient client = new HttpClient
        {
            Timeout = new TimeSpan(0, 0, 3)
        };
        public static bool UsingDefault = false;

        public abstract bool CallAPI(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false);
        public static bool CallAPI_Static(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false)
        { //This is done to allow for default api calls that aren't to the leaderboard
            try
            {
                Plugin.Log.Debug("API Call: " + path);
                HttpResponseMessage hrm = client.GetAsync(new Uri(path)).Result;
                hrm.EnsureSuccessStatusCode();
                content = hrm.Content;
                return true;
            }
            catch (Exception e)
            {
                if (!quiet)
                {
                    Plugin.Log.Error($"API request failed\nPath: {path}\nError: {e.Message}");
                    Plugin.Log.Debug(e);
                }
                content = null;

                return false;
            }
        }
        
        public HttpContent CallAPI(string path, bool quiet = false, bool forceNoHeader = false)
        {
            CallAPI(path, out HttpContent content, quiet, forceNoHeader);
            return content;
        }
        public string CallAPI_String(string path, bool quiet = false, bool forceNoHeader = false) =>
            CallAPI(path, quiet, forceNoHeader)?.ReadAsStringAsync().Result;
        public byte[] CallAPI_Bytes(string path, bool quiet = false, bool forceNoHeader = false) =>
            CallAPI(path, quiet, forceNoHeader)?.ReadAsByteArrayAsync().Result;
        public abstract float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1);
        public abstract bool MapIsUsable(JToken diffData);
        public abstract bool AreRatingsNull(JToken diffData);
        public abstract string GetSongName(JToken diffData);
        public abstract string GetDiffName(JToken diffData);
        public abstract string GetLeaderboardId(JToken diffData);
        public abstract int GetMaxScore(JToken diffData);
        public abstract int GetMaxScore(string hash, int diffNum, string modeName);
        public abstract float[] GetRatings(JToken diffData);
        public abstract JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName);
        public abstract string GetHashData(string hash, int diffNum);
        public abstract string GetHash(JToken diffData);
        public abstract JToken GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false);
        public abstract float GetPP(JToken scoreData);
        public abstract int GetScore(JToken scoreData);
        public abstract float[] GetScoregraph(MapSelection ms);
        internal abstract void AddMap(Dictionary<string, Map> Data, string hash);
        public static APIHandler GetAPI(bool useDefault = false) => GetAPI(!useDefault ? PluginConfig.Instance.Leaderboard : PluginConfig.Instance.DefaultLeaderboard);
        public static APIHandler GetSelectedAPI() => GetAPI(UsingDefault);
        public static APIHandler GetAPI(Leaderboards leaderboard)
        {
            switch (leaderboard)
            {
                case Leaderboards.Beatleader:
                    return BLAPI.Instance;
                case Leaderboards.Scoresaber:
                    return SSAPI.Instance;
                case Leaderboards.Accsaber:
                    return APAPI.Instance;
                default:
                    return null;
            }
        }
    }
}
