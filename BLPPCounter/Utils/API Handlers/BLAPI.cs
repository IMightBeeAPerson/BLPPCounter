using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using static AlphabetScrollInfo;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class BLAPI: APIHandler
    {
        private static readonly Throttler Throttle = new Throttler(100, 15);
        internal static BLAPI Instance { get; private set; } = new BLAPI();
        private BLAPI() { }
        public override bool CallAPI(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.beatleader";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.BLAPI + path;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader)) 
                Throttle.Call();
            return CallAPI_Static(path, out content, quiet, forceNoHeader);
        }
        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            diffData = diffData["difficulty"];
            if (speed != SongSpeed.Normal) diffData = diffData["modifiersRating"];
            return new float[3] {
                (float)diffData[HelpfulMisc.AddModifier("accRating", speed)] * modMult,
                (float)diffData[HelpfulMisc.AddModifier("passRating", speed)] * modMult,
                (float)diffData[HelpfulMisc.AddModifier("techRating", speed)] * modMult
                };
        }
        public override string GetSongName(JToken diffData) => diffData["song"]["name"].ToString();
        public override string GetDiffName(JToken diffData) => diffData["difficulty"]["difficultyName"].ToString();
        public override string GetLeaderboardId(JToken diffData) => diffData["id"].ToString();
        public override string GetHash(JToken diffData) => diffData["song"]["hash"].ToString();
        public override int GetMaxScore(JToken diffData) => (int)(diffData["difficulty"]?["maxScore"] ?? diffData["maxScore"]);
        public override int GetMaxScore(string hash, int diffNum, string modeName) => 
            GetMaxScore(SelectSpecificDiff(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash))), diffNum, modeName));
        public override float[] GetRatings(JToken diffData) => new float[3] { (float)diffData["difficulty"]["accRating"], (float)diffData["difficulty"]["passRating"], (float)diffData["difficulty"]["techRating"] };
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName)
        {
            //Plugin.Log.Info(diffData.ToString());
            //Plugin.Log.Info(diffData["leaderboards"].ToString());
            return diffData["leaderboards"].Children().First(t => ((int)t["difficulty"]["value"]) == diffNum && ((string)t["difficulty"]["modeName"]).Equals(modeName));
        }
        public override bool MapIsUsable(JToken diffData) => !(diffData is null) && HelpfulMisc.StatusIsUsable((int)diffData["difficulty"]["status"]);
        public override bool AreRatingsNull(JToken diffData) =>
            diffData["difficulty"]["modifiersRating"] is null || diffData["difficulty"]["modifiersRating"].ToString().Length == 0;
        public override string GetHashData(string hash, int diffNum) =>
            CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash));
        public override JToken GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false)
        {
            string outp = CallAPI_String(string.Format(HelpfulPaths.BLAPI_SCORE, userId, hash, diff, mode), quiet);
            if (outp is null || outp.Length == 0) return null;
            return JToken.Parse(outp);
        }
        public override float GetPP(JToken scoreData) => (float)scoreData["pp"];
        public override int GetScore(JToken scoreData) => (int)scoreData["modifiedScore"];
        public override float[] GetScoregraph(MapSelection ms)
        {
            if (ms.IsUsable)
            {
                string data = CallAPI_String($"leaderboard/{ms.MapData.Item1}/scoregraph");
                return JToken.Parse(data).Children().Select(a => (float)Math.Round((double)a["pp"], PluginConfig.Instance.DecimalPrecision)).ToArray();
            } else
            {
                string data = CallAPI_String($"leaderboard/scores/{ms.MapData.Item1}?count={PluginConfig.Instance.MinRank}");
                JToken mapData = ms.MapData.Item2;
                float maxScore = (int)mapData["maxScore"];
                float acc = (float)mapData["accRating"], pass = (float)mapData["passRating"], tech = (float)mapData["techRating"];
                return JToken.Parse(data)["scores"].Children().Select(a =>
                (float)Math.Round(BLCalc.Instance.Inflate(BLCalc.Instance.GetSummedPp((int)a["modifiedScore"] / maxScore, acc, pass, tech)), PluginConfig.Instance.DecimalPrecision)).ToArray();
            }
        }
        public override float[] GetScores(string userId, int count)
        {
            const int MaxCountToPage = 100;
            List<float> outp = new List<float>();
            int pageNum = 1;
            while (count >= MaxCountToPage)
            {
                outp.AddRange(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.BLAPI_PLAYERSCORES, userId, pageNum, MaxCountToPage)))?["data"].Children().Select(token => (float)token["score"]["pp"]).ToArray());
                count -= MaxCountToPage;
                pageNum++;
            }
            if (count > 0)
                outp.AddRange(JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.BLAPI_PLAYERSCORES, userId, pageNum, count)))?["data"].Children().Select(token => (float)token["score"]["pp"]).ToArray());
            return outp.ToArray();
        }
        internal override void AddMap(Dictionary<string, Map> Data, string hash)
        {
            try
            {
                JToken dataToken = JObject.Parse(CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)));
                JEnumerable<JToken> mapTokens = dataToken["song"]["difficulties"].Children();
                string songId = (string)dataToken["song"]["id"];
                foreach (JToken mapToken in mapTokens)
                {
                    Map map = new Map(hash, songId + mapToken["value"] + mapToken["mode"], mapToken);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding BL map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
