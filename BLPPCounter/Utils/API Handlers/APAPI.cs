using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class APAPI: APIHandler
    {
        private static readonly Throttler Throttle = new Throttler(400, 60);
        internal static APAPI Instance { get; private set; } = new APAPI();
        private APAPI() { }
        public override bool CallAPI(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "accsaber";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.APAPI + path;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                Throttle.Call();
            return CallAPI_Static(path, out content, quiet, forceNoHeader);
        }
        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1) => new float[1] { (float)diffData["complexity"] };
        public override float[] GetRatings(JToken diffData) => new float[1] { (float)diffData["complexity"] };
        public override string GetSongName(JToken diffData) => diffData["songName"].ToString();
        public override string GetDiffName(JToken diffData) => diffData["difficulty"].ToString();
        public override string GetLeaderboardId(JToken diffData) => diffData["leaderboardId"].ToString();
        public override string GetHash(JToken diffData) => diffData["songHash"].ToString();
        public override bool MapIsUsable(JToken diffData) => !(diffData is null) && GetRatings(diffData)[0] > 0;
        public override bool AreRatingsNull(JToken diffData) => diffData["complexity"] is null;
        public override int GetMaxScore(JToken diffData) => (int)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, diffData["leaderboardId"], "info")))["maxScore"];
        public override int GetMaxScore(string hash, int diffNum, string modeName) => GetMaxScore(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)))["difficulty"]);
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName) => diffData;
        public override string GetHashData(string hash, int diffNum)
        {
            string id = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)))["id"].ToString();
            return CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, id), true);
        }
        public override JToken GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false) => 
            SSAPI.Instance.GetScoreData(userId, hash, diff, mode, quiet);
        public override float GetPP(JToken scoreData)
        {
            float acc = (float)scoreData["accuracy"];
            float complexity = (float)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, scoreData["id"].ToString()), true))["complexity"];
            return APCalc.Instance.GetPp(acc, complexity)[0];
        }
        public override int GetScore(JToken scoreData) => (int)scoreData["baseScore"];
        public override float[] GetScoregraph(MapSelection ms) => SSAPI.Instance.GetScoregraph(ms);
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
                    JToken mapInfo = JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, songId)));
                    Map map = Map.ConvertAPToTaoh(hash, songId, mapInfo);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding AP map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
