using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Map_Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static GameplayModifiers;
using System.Linq;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class APAPI: APIHandler
    {
        internal static readonly Throttler Throttle = new(400, 60);
        internal static APAPI Instance { get; private set; } = new APAPI();
        private static readonly HashSet<string> UnrankedHashes = [];
        private APAPI() { }
        public override string API_HASH => HelpfulPaths.SSAPI_DIFFS;
        public override Task<(bool, HttpContent)> CallAPI(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3, CancellationToken ct = default)
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.accsaberreloaded";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = HelpfulPaths.APAPI + path;
            Throttler t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return CallAPI_Static(path, t, quiet, maxRetries, ct);
        }
        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1) => [(float)(diffData["complexity"] ?? diffData["complexityAccSaber"])];
        public override float[] GetRatings(JToken diffData) => [(float)(diffData["complexity"] ?? diffData["complexityAccSaber"])];
        public override string GetSongName(JToken diffData) => diffData["songName"].ToString();
        public override string GetDiffName(JToken diffData) => diffData["difficulty"].ToString();
        public override string GetLeaderboardId(JToken diffData) => diffData["leaderboardId"].ToString();
        public override string GetHash(JToken diffData) => diffData["songHash"].ToString();
        public override bool MapIsUsable(JToken diffData) => diffData is not null && GetRatings(diffData)[0] > 0;
        public override bool AreRatingsNull(JToken diffData) => (diffData["complexity"] ?? diffData["complexityAccSaber"]) is null;
        public override int GetMaxScore(JToken diffData) => (int)JToken.Parse(CallAPI_String(string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, diffData["leaderboardId"] ?? diffData["scoreSaberID"], "info")).Result)["maxScore"];
        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName) => GetMaxScore(JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)).ConfigureAwait(false))["difficulty"]);
        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName) => diffData;
        public override async Task<string> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(string.Format(HelpfulPaths.APAPI_HASH_DIFF, hash, HelpfulPaths.DiffNumToReloadedDiff(diffNum)), true, maxRetries: 1).ConfigureAwait(false);
        public override async Task<JToken> GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false, CancellationToken ct = default)
        {
            string reloadedDiff = HelpfulPaths.DiffNumToReloadedDiff(Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff)));
            return JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_SCORE, userId, hash.ToLower(), reloadedDiff)).ConfigureAwait(false));
        }
        public override float GetPP(JToken scoreData)
        {
            float acc = (float)scoreData["accuracy"];
            float complexity = (float)scoreData["complexity"];
            return APCalc.Instance.GetPp(acc, complexity)[0];
        }
        public override int GetScore(JToken scoreData) => (int)scoreData["baseScore"];
        private Task<Play[]> GetScores(string userId, int count, string path)
        {
            return GetScores(
                userId,
                count,
                path,
                "content",
                true,
                token =>
                {
                    BeatmapDifficulty beatmapDiff = Map.FromValue(HelpfulPaths.ReloadedDiffToDiffNum(token["difficulty"].ToString()));
                    string coverUrl = token["coverUrl"].ToString();
                    coverUrl = coverUrl.Substring(coverUrl.IndexOf("m/") + 2);
                    return new Play(
                        token["songName"].ToString(),
                        coverUrl.Substring(0, coverUrl.Length - 4),
                        beatmapDiff,
                        Profile.DEFAULT_MODE,
                        (float)token["ap"],
                        (uint)token["rank"]
                        )
                        {
                            AccSaberCategory = (APCategory)Enum.Parse(typeof(APCategory), HelpfulPaths.ReloadedCategoryToCategoryId(token["categoryId"].ToString()))
                        };
                },
                Throttle,
                (data, repData) =>
                {
                    if (repData is null || repData.Equals(string.Empty)) return (data, data.MapKey);
                    data.MapKey = repData;
                    return (data, data.MapKey);
                },
                "id"
                );
        }
        public override Task<Play[]> GetScores(string userId, int count) => GetScores(userId, count, HelpfulPaths.APAPI_SCORES);
        public Task<Play[]> GetScores(string userId, int count, APCategory accSaberType) =>
            GetScores(userId, count, string.Format(HelpfulPaths.APAPI_CATEGORY_SCORES, "{0}", HelpfulPaths.CategoryIdToReloadedCategory(accSaberType.ToString()), 0, count));
        public override async Task<float> GetProfilePP(string userId)
        {
            return (float)JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYERID, userId)).ConfigureAwait(false))?["ap"];
        }
        public async Task<float> GetProfilePP(string userId, APCategory accSaberType)
        {
            return (float)JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYERID_CATEGORY, userId, accSaberType.ToString().ToLower() + "_acc")).ConfigureAwait(false))?["ap"];
        }
        public override Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default) => SSAPI.Instance.GetScoregraph(ms, ct);
        internal override async Task AddMap(Dictionary<string, Map> Data, string hash, CancellationToken ct = default)
        {
            try
            {
                if (UnrankedHashes.Contains(hash) || ct.IsCancellationRequested) return;
                JEnumerable<JToken> diffs = JToken.Parse(await CallAPI_String(string.Format(HelpfulPaths.APAPI_HASH, hash)))["difficulties"].Children();
                bool anyRanked = false;
                foreach (JToken diff in diffs)
                {
                    int songId = int.Parse(diff["beatsaverCode"].ToString(), System.Globalization.NumberStyles.HexNumber);
                    if (ct.IsCancellationRequested) return;
                    Map map = Map.ConvertAPToTaoh(hash, songId.ToString(), diff);
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
                Plugin.Log.Warn("Error adding AP map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
    }
}
