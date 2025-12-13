using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Map_Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class SSAPI : APIHandler
    {
        internal static readonly Throttler Throttle = new(50, 10);
        internal static SSAPI Instance { get; private set; } = new SSAPI();
        private SSAPI() { }
        private readonly HashSet<int> UnrankedIds = [];
        private readonly HashSet<string> UnrankedHashes = [];
        public override string API_HASH => HelpfulPaths.SSAPI_DIFFS;
        public override async Task<(bool, HttpContent)> CallAPI(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3, CancellationToken ct = default)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "scoresaber";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.SSAPI + path;
            Throttler t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader)) 
                t = Throttle;
            return await CallAPI_Static(path, t, quiet, maxRetries, ct).ConfigureAwait(false);
        }
        public override float[] GetRatings(JObject diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1) => [(float)diffData["stars"]];
        public override string GetSongName(JObject diffData) => diffData["songName"].ToString();
        public override string GetDiffName(JObject diffData) => Map.FromValue(int.Parse(diffData["difficulty"].ToString())).ToString();
        public override string GetLeaderboardId(JObject diffData) => diffData["id"].ToString();
        public override bool MapIsUsable(JObject diffData) => diffData is not null && GetRatings(diffData)[0] > 0;
        public override bool AreRatingsNull(JObject diffData) => (diffData["stars"] ?? diffData["starScoreSaber"]) is null;
        public override int GetMaxScore(JObject diffData)
        {
            int score = (int)(diffData["maxScore"] ?? 0);
            if (score > 0) return score;
            return (int)JObject.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, diffData["scoreSaberID"], "info")).GetAwaiter().GetResult())["maxScore"];
        }
        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName) => GetMaxScore(JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)).ConfigureAwait(false)));
        public override float[] GetRatings(JObject diffData) =>
            [(float)(diffData["stars"] ?? diffData["starScoreSaber"])];
        public override JObject SelectSpecificDiff(JObject diffData, int diffNum, string modeName)
        {
            if (diffData["id"] is null)
            {
                string leaderboardId = diffData.Children().Where(token => (int)token["difficulty"] == diffNum && token["mode"].ToString().Substring(4).Equals(modeName)).First()["leaderboardId"].ToString();
                return JObject.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, leaderboardId, "Info")).GetAwaiter().GetResult());
            }
            return diffData;
        }
        public override Task<string> GetHashData(string hash, int diffNum) =>
            CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum));
        public override string GetHash(JObject diffData) => diffData["songHash"].ToString();
        public override async Task<JObject> GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false, CancellationToken ct = default)
        {
            diff = Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff)) + "";
            string name = await CallAPI_String(string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic"), quiet, maxRetries: 1, ct: ct).ConfigureAwait(false);
            if (name is null || JObject.Parse(name)["name"] is null) return null;
            name = (string)JObject.Parse(name)["name"];
            string outp = await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "scores", diff) + "&search=" + name, quiet, maxRetries: 1, ct: ct).ConfigureAwait(false);
            if (outp is null || outp.Length == 0) return null;
            if (JObject.Parse(outp)["scores"].Children().FirstOrDefault(token => token["leaderboardPlayerInfo"]["name"].ToString().Equals(name)) is not JObject tokenOutp) return null;
            JObject mapInfo = JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diff), quiet, maxRetries: 1, ct: ct).ConfigureAwait(false));
            tokenOutp.Property("id").AddAfterSelf(new JProperty("maxScore", (int)mapInfo["maxScore"]));
            tokenOutp.Property("maxScore").AddAfterSelf(new JProperty("accuracy", (float)tokenOutp["modifiedScore"] / (float)tokenOutp["maxScore"]));
            tokenOutp["id"] = (int)mapInfo["id"];
            return tokenOutp;
        }
        public override float GetPP(JObject scoreData) => (float)scoreData["pp"];
        public override int GetScore(JObject scoreData) => (int)scoreData["modifiedScore"];
        public override async Task<Play[]> GetScores(string userId, int count)
        {
            return await GetScores(
                userId,
                count,
                HelpfulPaths.SSAPI_PLAYERSCORES,
                "playerScores",
                false,
                token => new Play(
                    token["leaderboard"]["songName"].ToString(),
                    token["leaderboard"]["songHash"].ToString(),
                    Map.FromValue((int)token["leaderboard"]["difficulty"]["difficulty"]),
                    token["leaderboard"]["difficulty"]["gameMode"].ToString().Replace("Solo", ""),
                    (float)token["score"]["pp"]
                ),
                Throttle,
                (data, repData) =>
                {
                    if (repData is null || repData.Equals(string.Empty)) return (data, data.MapKey);
                    data.MapKey = repData;
                    return (data, data.MapKey);
                },
                "id"
            ).ConfigureAwait(false);
        }
        public override async Task<float> GetProfilePP(string userId) => 
            (float)JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic")).ConfigureAwait(false))?["pp"];
        public override async Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default)
        {
            List<ScoregraphInfo> pps = [];
            const float SS_PAGELENGTH = 12f;
            string path = string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "scores", Map.FromDiff(ms.Difficulty));
            int pages = (int)Math.Ceiling(PC.MinRank / SS_PAGELENGTH);
            int maxScore = (int)JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "info", Map.FromDiff(ms.Difficulty)), ct: ct).ConfigureAwait(false))["maxScore"];
            for (int i = 1; i < pages + 1; i++)
                pps.AddRange(JObject.Parse(await CallAPI_String(path + "&page=" + i, ct: ct).ConfigureAwait(false))["scores"].Children().Select(token => new ScoregraphInfo(
                (float)token["modifiedScore"] / maxScore,
                (float)Math.Round((float)token["pp"], PC.DecimalPrecision),
                SongSpeed.Normal, 1f, token["leaderboardPlayerInfo"]["name"].ToString().ClampString(PC.MaxNameLength)
                )));
            return [.. pps];
            /*if (ms.IsUsable)
            {
                for (int i = 1; i < pages; i++)
                    pps = pps.Union(JObject.Parse(await CallAPI_String(path + "&page=" + i).ConfigureAwait(false))["scores"].Children().Select(token => (float)token["pp"]));
            }
            else
            {
                JObject mapData = JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "hash", Map.FromDiff(ms.Difficulty))).ConfigureAwait(false));
                int maxScore = (int)mapData["maxScore"];
                for (int i = 1; i < pages; i++)
                    pps = pps.Union(JObject.Parse(await CallAPI_String(path + "&page=" + i).ConfigureAwait(false))["scores"].Children().Select(token => (float)token["modifiedScore"] / maxScore));
            }*/
        }
        internal override async Task AddMap(Dictionary<string, Map> Data, string hash, CancellationToken ct = default)
        {
            try
            {
                if (hash is null || UnrankedHashes.Contains(hash) || ct.IsCancellationRequested) return;
                IEnumerable<JObject> diffs = JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_DIFFS, hash), ct: ct).ConfigureAwait(false)).Children().Cast<JObject>();
                bool anyRanked = false;
                foreach (JObject diff in diffs)
                {
                    int songId = (int)diff["leaderboardId"];
                    if (UnrankedIds.Contains(songId)) continue;
                    if (ct.IsCancellationRequested) return;
                    JObject mapInfo = JObject.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, songId, "info"), ct: ct).ConfigureAwait(false));
                    if (!(bool)mapInfo["ranked"])
                    {
                        //Plugin.Log.Warn($"SS map \"{mapInfo["songName"]}\" (id {mapInfo["id"]}) cannot be added to cache as it is not ranked.");
                        UnrankedIds.Add(songId);
                        continue;
                    }
                    Map map = Map.ConvertSSToTaoh(hash, songId.ToString(), mapInfo);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
                    anyRanked = true;
                }
                if (!anyRanked)
                    UnrankedHashes.Add(hash);
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding SS map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
