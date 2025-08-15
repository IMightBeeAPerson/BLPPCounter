using BeatLeader.Utils;
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
using System.Threading.Tasks;
using static AlphabetScrollInfo;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class BLAPI: APIHandler
    {
        internal static readonly Throttler Throttle = new Throttler(100, 15);
        internal static BLAPI Instance { get; private set; } = new BLAPI();
        private BLAPI() { }
        public override (bool, HttpContent) CallAPI(string path, bool quiet = false, bool forceNoHeader = false)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.beatleader";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.BLAPI + path;
            Throttler t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return CallAPI_Static(path, t, quiet).Result;
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
        public override (string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId)[] GetScores(string userId, int count)
        {
            return GetScores(
                userId,
                count,
                HelpfulPaths.BLAPI_PLAYERSCORES,
                "data",
                token =>
                {
                    string mapID = token["leaderboard"]["id"].ToString();
                    string cleanMapId = mapID.Contains('x') ? mapID.Substring(0, mapID.IndexOf('x')) : mapID.Substring(0, mapID.Length - 2);
                    return (
                        token["leaderboard"]["songHash"].ToString(),
                        Map.FromValue((int)token["leaderboard"]["difficulty"]),
                        (float)token["score"]["pp"],
                        cleanMapId
                    );
                },
                (data, repData) =>
                {
                    if (repData is null || repData.Equals(string.Empty)) return (data, data.MapName);
                    data.MapName = repData;
                    return (data, data.MapName);
                },
                "name",
                throttler: Throttle
            ).Result;
        }
        public override float GetProfilePP(string userId)
        {
            return (float)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID, userId)))?["pp"];
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
