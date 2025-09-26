using BeatLeader.Utils;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Misc_Classes;
using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Threading;
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
        public override string API_HASH => HelpfulPaths.BLAPI_HASH;
        public override async Task<(bool, HttpContent)> CallAPI(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.beatleader";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.BLAPI + path;
            Throttler t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return await CallAPI_Static(path, t, quiet, maxRetries).ConfigureAwait(false);
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
        public override string GetDiffName(JToken diffData) => (diffData["difficulty"]?["difficultyName"] ?? diffData["difficultyName"]).ToString();
        public override string GetLeaderboardId(JToken diffData) => diffData["id"].ToString();
        public override string GetHash(JToken diffData) => diffData["song"]["hash"].ToString();
        public override int GetMaxScore(JToken diffData) => (int)(diffData["difficulty"]?["maxScore"] ?? diffData["maxScore"]);
        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName) => 
            GetMaxScore(SelectSpecificDiff(JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false)), diffNum, modeName));
        public override float[] GetRatings(JToken diffData)
        {
            if (!(diffData["difficulty"] is null)) diffData = diffData["difficulty"];
            return new float[3] { (float)diffData["accRating"], (float)diffData["passRating"], (float)diffData["techRating"] };
        }
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName)
        {
            //Plugin.Log.Info(diffData.ToString());
            //Plugin.Log.Info(diffData["leaderboards"].ToString());
            return diffData["leaderboards"].Children().First(t => ((int)t["difficulty"]["value"]) == diffNum && ((string)t["difficulty"]["modeName"]).Equals(modeName));
        }
        public override bool MapIsUsable(JToken diffData)
        {
            if (diffData is null) return false;
            if (!(diffData["difficulty"] is null)) diffData = diffData["difficulty"];
            return HelpfulMisc.StatusIsUsable((int)diffData["status"]);
        }
        public override bool AreRatingsNull(JToken diffData)
        {
            if (!(diffData["difficulty"] is null)) diffData = diffData["difficulty"];
            return diffData["modifiersRating"] is null || diffData["modifiersRating"].ToString().Length == 0;
        }
        public override async Task<string> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false);
        public override async Task<JToken> GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false)
        {
            string outp = await CallAPI_String(string.Format(HelpfulPaths.BLAPI_SCORE, userId, hash, diff, mode), quiet, maxRetries: 1).ConfigureAwait(false);
            if (outp is null || outp.Length == 0) return null;
            return JToken.Parse(outp);
        }
        public override float GetPP(JToken scoreData) => (float)scoreData["pp"];
        public override int GetScore(JToken scoreData) => (int)scoreData["modifiedScore"];
        public override async Task<(float acc, float pp, SongSpeed speed, float modMult)[]> GetScoregraph(MapSelection ms)
        {
            if (ms.IsUsable)
            {
                string data = await CallAPI_String($"leaderboard/{ms.MapData.Item1}/scoregraph").ConfigureAwait(false);
                return JToken.Parse(data).Children().Select(a => {
                    var (speed, modMult) = HelpfulMisc.ParseModifiers(a["modifiers"].ToString(), ms.MapData.Item2);
                    return ((float)a["accuracy"] / 100f, (float)Math.Round((float)a["pp"], PluginConfig.Instance.DecimalPrecision), speed, modMult);
                }).ToArray();
            } else
            {
                string data = await CallAPI_String($"leaderboard/scores/{ms.MapData.Item1}?count={PluginConfig.Instance.MinRank}").ConfigureAwait(false);
                JToken mapData = ms.MapData.Item2;
                float maxScore = (int)mapData["maxScore"];
                float acc = (float)mapData["accRating"], pass = (float)mapData["passRating"], tech = (float)mapData["techRating"];
                return JToken.Parse(data)["scores"].Children().Select(a => {
                    var (speed, modMult) = HelpfulMisc.ParseModifiers(a["modifiers"].ToString(), ms.MapData.Item2);
                    return (
                    (float)a["modifiedScore"] / (float)mapData["maxScore"],
                    (float)Math.Round(BLCalc.Instance.Inflate(BLCalc.Instance.GetSummedPp((int)a["modifiedScore"] / maxScore, acc, pass, tech)), PluginConfig.Instance.DecimalPrecision),
                    speed, modMult
                    );
                    }).ToArray();
            }
        }
        public override async Task<Play[]> GetScores(string userId, int count)
        {
            return await GetScores(
                userId,
                count,
                HelpfulPaths.BLAPI_PLAYERSCORES,
                "data",
                false,
                token =>
                {
                    string mapID = token["leaderboard"]["id"].ToString();
                    string cleanMapId = CleanUpId(mapID);
                    return new Play(
                        token["leaderboard"]["songHash"].ToString(),
                        cleanMapId,
                        Map.FromValue((int)token["leaderboard"]["difficulty"]),
                        token["leaderboard"]["modeName"].ToString(),
                        (float)token["score"]["pp"]
                    );
                },
                Throttle,
                (data, repData) =>
                {
                    if (repData is null || repData.Equals(string.Empty)) return (data, data.MapName);
                    data.MapName = repData;
                    return (data, data.MapName);
                },
                "metadata", "songName"
            ).ConfigureAwait(false);
        }
        public override async Task<float> GetProfilePP(string userId)
        {
            return (float)JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID, userId)).ConfigureAwait(false))?["pp"];
        }
        internal override async Task AddMap(Dictionary<string, Map> Data, string hash, CancellationToken ct = default)
        {
            try
            {
                if (ct.IsCancellationRequested) return;
                string dataStr = await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false);
                if (dataStr is null || dataStr.Length == 0) return;
                JToken dataToken = JObject.Parse(dataStr);
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
        public static string CleanUpId(string mapID) =>
            mapID.Contains('x') ? mapID.Substring(0, mapID.IndexOf('x')) : mapID.Substring(0, mapID.Length - 2);
    }
}
