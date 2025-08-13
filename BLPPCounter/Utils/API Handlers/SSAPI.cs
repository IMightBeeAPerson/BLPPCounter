using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class SSAPI : APIHandler
    {
        private static readonly Throttler Throttle = new Throttler(50, 10);
        internal static SSAPI Instance { get; private set; } = new SSAPI();
        private SSAPI() { }
        public override bool CallAPI(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "scoresaber";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.SSAPI + path;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader)) 
                Throttle.Call();
            return CallAPI_Static(path, out content, quiet, forceNoHeader);
        }
        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1) =>
            new float[1] { (float)diffData["stars"] };
        public override string GetSongName(JToken diffData) => diffData["songName"].ToString();
        public override string GetDiffName(JToken diffData) => diffData["difficulty"]["difficultyRaw"].ToString().Substring(1).Split('_')[0];
        public override string GetLeaderboardId(JToken diffData) => diffData["id"].ToString();
        public override bool MapIsUsable(JToken diffData) => !(diffData is null) && GetRatings(diffData)[0] > 0;
        public override bool AreRatingsNull(JToken diffData) => diffData["stars"] is null;
        public override int GetMaxScore(JToken diffData)
        {
            int score = (int)(diffData["maxScore"] ?? 0);
            if (score > 0) return score;
            return (int)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, diffData["scoreSaberID"], "info")))["maxScore"];
        }
        public override int GetMaxScore(string hash, int diffNum, string modeName) => GetMaxScore(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum))));
        public override float[] GetRatings(JToken diffData) => new float[1] { (float)diffData["stars"] };
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName) => diffData;
        public override string GetHashData(string hash, int diffNum) =>
            CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum));
        public override string GetHash(JToken diffData) => diffData["songHash"].ToString();
        public override JToken GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false)
        {
            diff = Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff)) + "";
            string name = CallAPI_String(string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic"), quiet);
            if (name is null || JToken.Parse(name)["name"] is null) return null;
            name = (string)JToken.Parse(name)["name"];
            string outp = CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "scores", diff) + "&search=" + name, quiet);
            if (outp is null || outp.Length == 0) return null;
            JObject tokenOutp = JToken.Parse(outp)["scores"].Children().FirstOrDefault(token => token["leaderboardPlayerInfo"]["name"].ToString().Equals(name)) as JObject;
            if (tokenOutp is null) return null;
            JToken mapInfo = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diff, quiet)));
            tokenOutp.Property("id").AddAfterSelf(new JProperty("maxScore", (int)mapInfo["maxScore"]));
            tokenOutp.Property("maxScore").AddAfterSelf(new JProperty("accuracy", (float)tokenOutp["modifiedScore"] / (float)tokenOutp["maxScore"]));
            tokenOutp["id"] = (int)mapInfo["id"];
            return tokenOutp;
        }
        public override float GetPP(JToken scoreData) => (float)scoreData["pp"];
        public override int GetScore(JToken scoreData) => (int)scoreData["modifiedScore"];
        public override (string, BeatmapDifficulty, float)[] GetScores(string userId, int count)
        {
            const int MaxCountToPage = 100;
            int pageNum = 1;
            List<(string, BeatmapDifficulty, float)> outp = new List<(string, BeatmapDifficulty, float)>();
            while (count >= MaxCountToPage)
            {
                outp.AddRange(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_PLAYERSCORES, userId, MaxCountToPage, pageNum)))?["playerScores"].Children().Select(token => (token["leaderboard"]["songName"].ToString(), Map.FromValue((int)token["leaderboard"]["difficulty"]["difficulty"]),(float)token["score"]["pp"])));
                count -= MaxCountToPage;
                pageNum++;
            }
            if (count > 0)
                outp.AddRange(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_PLAYERSCORES, userId, count, pageNum)))?["playerScores"].Children().Select(token => (token["leaderboard"]["songName"].ToString(), Map.FromValue((int)token["leaderboard"]["difficulty"]["difficulty"]), (float)token["score"]["pp"])));
            return outp.ToArray();
        }
        public override float GetProfilePP(string userId)
        {
            return (float)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic")))?["pp"];
        }
        public override float[] GetScoregraph(MapSelection ms)
        {
            IEnumerable<float> pps = new List<float>();
            string path = string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "scores", Map.FromDiff(ms.Difficulty));
            const int pages = 5;
            if (ms.IsUsable)
            {
                for (int i = 1; i < pages; i++)
                    pps = pps.Union(JToken.Parse(CallAPI_String(path + "&page=" + i))["scores"].Children().Select(token => (float)token["pp"]));
            }
            else
            {
                JToken mapData = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "hash", Map.FromDiff(ms.Difficulty))));
                int maxScore = (int)mapData["maxScore"];
                for (int i = 1; i < pages; i++)
                    pps = pps.Union(JToken.Parse(CallAPI_String(path + "&page=" + i))["scores"].Children().Select(token => (float)token["modifiedScore"] / maxScore));
            }
            return pps.ToArray();
        }
        internal override void AddMap(Dictionary<string, Map> Data, string hash)
        {
            try
            {
                JEnumerable<JToken> diffs = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_DIFFS, hash))).Children();
                Stack<int> ids = new Stack<int>(diffs.Count());
                foreach (JToken diff in diffs)
                    ids.Push((int)diff["leaderboardId"]);
                while (ids.Count > 0)
                {
                    string songId = ids.Pop().ToString();
                    JToken mapInfo = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, songId, "info")));
                    if (!(bool)mapInfo["ranked"])
                    {
                        Plugin.Log.Warn("SS map cannot be added to cache as it is not ranked.");
                        return;
                    }
                    Map map = Map.ConvertSSToTaoh(hash, songId, mapInfo);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding SS map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
