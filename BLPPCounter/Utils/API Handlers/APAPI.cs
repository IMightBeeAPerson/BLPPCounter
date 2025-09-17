using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class APAPI: APIHandler
    {
        private static readonly Throttler Throttle = new Throttler(400, 60);
        internal static APAPI Instance { get; private set; } = new APAPI();
        private static readonly HashSet<int> UnrankedIds = new HashSet<int>();
        private static readonly HashSet<string> UnrankedHashes = new HashSet<string>();
        private APAPI() { }
        public override string API_HASH => HelpfulPaths.SSAPI_DIFFS;
        public override Task<(bool, HttpContent)> CallAPI(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "accsaber";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.APAPI + path;
            Throttler t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return CallAPI_Static(path, t, quiet, maxRetries);
        }
        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1) => new float[1] { (float)(diffData["complexity"] ?? diffData["complexityAccSaber"]) };
        public override float[] GetRatings(JToken diffData) => new float[1] { (float)(diffData["complexity"] ?? diffData["complexityAccSaber"]) };
        public override string GetSongName(JToken diffData) => diffData["songName"].ToString();
        public override string GetDiffName(JToken diffData) => diffData["difficulty"].ToString();
        public override string GetLeaderboardId(JToken diffData) => diffData["leaderboardId"].ToString();
        public override string GetHash(JToken diffData) => diffData["songHash"].ToString();
        public override bool MapIsUsable(JToken diffData) => !(diffData is null) && GetRatings(diffData)[0] > 0;
        public override bool AreRatingsNull(JToken diffData) => (diffData["complexity"] ?? diffData["complexityAccSaber"]) is null;
        public override int GetMaxScore(JToken diffData) => (int)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, diffData["leaderboardId"] ?? diffData["scoreSaberID"], "info")).Result)["maxScore"];
        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName) => GetMaxScore(JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)).ConfigureAwait(false))["difficulty"]);
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName) => diffData;
        public override async Task<string> GetHashData(string hash, int diffNum)
        {
            string id = JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)).ConfigureAwait(false))["id"].ToString();
            return await CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, id), true, maxRetries: 1).ConfigureAwait(false);
        }
        public override Task<JToken> GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false) => 
            SSAPI.Instance.GetScoreData(userId, hash, diff, mode, quiet);
        public override float GetPP(JToken scoreData)
        {
            float acc = (float)scoreData["accuracy"];
            float complexity = (float)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, scoreData["id"].ToString()), true, maxRetries: 1).Result)["complexity"];
            return APCalc.Instance.GetPp(acc, complexity)[0];
        }
        public override int GetScore(JToken scoreData) => (int)scoreData["baseScore"];
        private Task<Play[]> GetScores(string userId, int count, string path)
        {
            return GetScores(
                userId,
                count,
                path,
                null,
                true,
                token =>
                {
                    string beatmapDiff = token["difficulty"].ToString().Replace("plus", "Plus");
                    beatmapDiff = char.ToUpper(beatmapDiff[0]) + beatmapDiff.Substring(1);
                    return new Play(
                    token["songName"].ToString(),
                    token["beatsaverKey"].ToString(),
                    (BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), beatmapDiff),
                    Profile.DEFAULT_MODE,
                    (float)token["ap"]
                    )
                    {
                        AccSaberCategory = (APCategory)Enum.Parse(typeof(APCategory), token["categoryDisplayName"].ToString().Split(' ')[0])
                    };
                },
                Throttle
                );
        }
        public override Task<Play[]> GetScores(string userId, int count) => GetScores(userId, count, HelpfulPaths.APAPI_SCORES);
        public Task<Play[]> GetScores(string userId, int count, APCategory accSaberType) =>
            GetScores(userId, count, HelpfulPaths.APAPI_CATEGORY_SCORES.Replace("{1}", accSaberType.ToString().ToLower()));
        public override async Task<float> GetProfilePP(string userId)
        {
            return (float)JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYERID, userId)).ConfigureAwait(false))?["ap"];
        }
        public async Task<float> GetProfilePP(string userId, APCategory accSaberType)
        {
            return (float)JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYERID, userId) + "/" + accSaberType.ToString().ToLower()).ConfigureAwait(false))?["ap"];
        }
        public override Task<float[]> GetScoregraph(MapSelection ms) => SSAPI.Instance.GetScoregraph(ms);
        internal override async Task AddMap(Dictionary<string, Map> Data, string hash, CancellationToken ct = default)
        {
            try
            {
                if (UnrankedHashes.Contains(hash) || ct.IsCancellationRequested) return;
                JEnumerable<JToken> diffs = JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_DIFFS, hash)).ConfigureAwait(false)).Children();
                bool anyRanked = false;
                List<int> unrankedIdsToAdd = new List<int>();
                foreach (JToken diff in diffs)
                {
                    int songId = (int)diff["leaderboardId"];
                    if (UnrankedIds.Contains(songId)) continue;
                    if (ct.IsCancellationRequested) return;
                    string mapInfoStr = await CallAPI_String(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, songId)).ConfigureAwait(false);
                    if (mapInfoStr is null)
                    {
                        //Plugin.Log.Warn($"AP map \"{mapInfo["songName"]}\" (id {mapInfo["id"]}) cannot be added to cache as it is not ranked.");
                        unrankedIdsToAdd.Add(songId);
                        continue;
                    }
                    JToken mapInfo = JToken.Parse(mapInfoStr);
                    Map map = Map.ConvertAPToTaoh(hash, songId.ToString(), mapInfo);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
                    anyRanked = true;
                }
                if (!anyRanked)
                    UnrankedHashes.Add(hash);
                else if (unrankedIdsToAdd.Count > 0)
                    foreach (int id in unrankedIdsToAdd)
                        UnrankedIds.Add(id);
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding AP map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
