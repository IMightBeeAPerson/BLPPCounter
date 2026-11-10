using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Profile_Utils;
using Newtonsoft.Json.Linq;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal sealed class SSAPI : APIHandler
    {
        internal static readonly Throttler Throttle = new(50, 10);

        private readonly HashSet<int> UnrankedIds = [];
        private readonly HashSet<string> UnrankedHashes = [];
        public override string API_HASH => HelpfulPaths.SSAPI_DIFFS;

        public override async Task<HttpContent?> CallAPI(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "scoresaber";
            if (!forceNoHeader && !path[..LinkHeader.Length].Equals(LinkHeader))
                path = HelpfulPaths.SSAPI + path;
            Throttler? t = null;
            if (path[LinkHeader.Length..(LinkHeader.Length + LeaderboardHeader.Length)].Equals(LeaderboardHeader))
                t = Throttle;
            return await CallAPI_Static(path, t, quiet, maxRetries, ct).ConfigureAwait(false);
        }

        public override float GetStarRating(
            ICacheModel diffData,
            SongSpeed speed = SongSpeed.Normal,
            float modMult = 1
        ) => diffData is TaohCache cache ? cache.StarScoreSaber ?? 0f : 0f;

        public override float[] GetRatings(
            ICacheModel diffData,
            SongSpeed speed = SongSpeed.Normal,
            float modMult = 1
        ) => [GetStarRating(diffData)];

        public override string GetSongName(ICacheModel diffData) => diffData is TaohCache cache ? cache.Name : "";

        public override string GetDiffName(ICacheModel diffData) => diffData.Difficulty.ToString();

        public override string GetLeaderboardId(ICacheModel diffData) => diffData.LeaderboardId;

        public override string GetHash(ICacheModel diffData) => diffData.Hash;

        public override bool MapIsUsable(ICacheModel diffData, bool useUnranked) =>
            diffData is TaohCache cache && (cache.StarScoreSaber ?? 0f) > 0f;

        public override bool AreRatingsNull(ICacheModel diffData) =>
            diffData is not TaohCache cache || cache.StarScoreSaber is null;

        public override int GetMaxScore(ICacheModel diffData)
        {
            // TaohCache does not store maxScore; fall back to a synchronous API call.
            // Prefer the async GetMaxScore(hash, diffNum, modeName) overload where possible.
            if (diffData is not TaohCache cache || cache.ScoreSaberID is null)
                return 0;
            string? response = CallAPI_String(
                    string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, cache.ScoreSaberID, "info")
                )
                .GetAwaiter()
                .GetResult();
            if (BetterString.IsNullOrEmpty(response))
                return 0;
            return (int)(JToken.Parse(response)["maxScore"] ?? 0);
        }

        public override int GetRank(JToken scoreData) => (int)(scoreData["rank"] ?? 0);

        public override string GetPlayerName(JToken scoreData) =>
            scoreData["leaderboardPlayerInfo"]?["name"]?.ToString() ?? "Unknown";

        public override float GetAcc(JToken scoreData) => (float)(scoreData["accuracy"] ?? 0);

        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName)
        {
            string? response = await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum))
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(response))
                return 0;
            return (int)(JToken.Parse(response)["maxScore"] ?? 0);
        }

        public override ICacheModel SelectSpecificDiff(ICacheModel diffData, int diffNum, string modeName) => diffData;

        public override async Task<string?> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum)).ConfigureAwait(false);

        public override async Task<JToken?> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        )
        {
            int diffNum = Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff));
            string? nameResponse = await CallAPI_String(
                    string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic"),
                    quiet,
                    maxRetries: 1,
                    ct: ct
                )
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(nameResponse))
                return null;
            JToken parsedNameResponse = JToken.Parse(nameResponse);
            if (parsedNameResponse["name"] is null)
                return null;
            string name = parsedNameResponse["name"]!.ToString();

            string? outp = await CallAPI_String(
                    string.Format(HelpfulPaths.SSAPI_HASH, hash, "scores", diffNum) + "&search=" + name,
                    quiet,
                    maxRetries: 1,
                    ct: ct
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrEmpty(outp))
                return null;

            if (
                JToken
                    .Parse(outp)["scores"]
                    ?.Children()
                    .FirstOrDefault(token => token["leaderboardPlayerInfo"]?["name"]?.ToString().Equals(name) ?? false)
                is not JObject tokenOutp
            )
                return null;

            string? mapInfoResponse = await CallAPI_String(
                    string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", diffNum),
                    quiet,
                    maxRetries: 1,
                    ct: ct
                )
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(mapInfoResponse))
                return null;

            JToken mapInfo = JToken.Parse(mapInfoResponse);
            tokenOutp.Property("id")?.AddAfterSelf(new JProperty("maxScore", (int)(mapInfo["maxScore"] ?? 0)));
            tokenOutp
                .Property("maxScore")
                ?.AddAfterSelf(
                    new JProperty(
                        "accuracy",
                        (float)(tokenOutp["modifiedScore"] ?? 0)
                            / (float)(tokenOutp["maxScore"] ?? float.PositiveInfinity)
                    )
                );

            if (mapInfo is JObject mapInfoObj && mapInfoObj.ContainsKey("id"))
                tokenOutp["id"] = (int)mapInfoObj["id"]!;

            return tokenOutp;
        }

        public override float GetPP(JToken scoreData) => (float)(scoreData["pp"] ?? 0);

        public override int GetScore(JToken scoreData) => (int)(scoreData["modifiedScore"] ?? 0);

        public override async Task<Play[]> GetScores(string userId, int count)
        {
            return await GetScores(
                    userId,
                    count,
                    HelpfulPaths.SSAPI_PLAYERSCORES,
                    "playerScores",
                    false,
                    token => new Play(
                        token["leaderboard"]?["songName"]?.ToString() ?? "Unknown",
                        token["leaderboard"]?["songHash"]?.ToString() ?? "",
                        Map.FromValue((int)(token["leaderboard"]?["difficulty"]?["difficulty"] ?? -1)),
                        token["leaderboard"]?["difficulty"]?["gameMode"]?.ToString().Replace("Solo", "") ?? "Standard",
                        (float)(token["score"]?["pp"] ?? 0f),
                        (uint)(token["score"]?["rank"] ?? 0)
                    ),
                    Throttle,
                    (data, repData) =>
                    {
                        if (repData is null || repData.Equals(string.Empty))
                            return (data, data.MapKey);
                        data.MapKey = repData;
                        return (data, data.MapKey);
                    },
                    "id"
                )
                .ConfigureAwait(false);
        }

        public override async Task<float> GetProfilePP(string userId)
        {
            string? response = await CallAPI_String(string.Format(HelpfulPaths.SSAPI_USERID, userId, "basic"))
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(response))
                return 0f;
            return (float)(JToken.Parse(response)["pp"] ?? 0);
        }

        public override async Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default)
        {
            List<ScoregraphInfo> pps = [];
            const float SS_PAGELENGTH = 12f;
            string path = string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "scores", Map.FromDiff(ms.Difficulty));
            int pages = (int)Math.Ceiling(pc.MinRank / SS_PAGELENGTH);

            string? infoResponse = await CallAPI_String(
                    string.Format(HelpfulPaths.SSAPI_HASH, ms.Hash, "info", Map.FromDiff(ms.Difficulty)),
                    ct: ct
                )
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(infoResponse))
                return [];
            int maxScore = (int)(JToken.Parse(infoResponse)["maxScore"] ?? 0);

            for (int i = 1; i < pages + 1; i++)
            {
                string? pageResponse = await CallAPI_String(path + "&page=" + i, ct: ct).ConfigureAwait(false);
                if (BetterString.IsNullOrEmpty(pageResponse))
                    continue;
                pps.AddRange(
                    JToken
                        .Parse(pageResponse)["scores"]
                        ?.Children()
                        .Select(token => new ScoregraphInfo(
                            (float)(token["modifiedScore"] ?? 0f) / maxScore,
                            (float)Math.Round((float)(token["pp"] ?? 0f), pc.DecimalPrecision),
                            SongSpeed.Normal,
                            1f,
                            token["leaderboardPlayerInfo"]?["name"]?.ToString().ClampString(pc.MaxNameLength) ?? ""
                        ))
                );
            }
            return [.. pps];
        }

        internal override async Task AddMap(Dictionary<string, Map> data, string hash, CancellationToken ct = default)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string normalizedHash = Map.NormalizeHash(hash);

            lock (UnrankedHashes)
            {
                if (UnrankedHashes.Contains(normalizedHash))
                {
                    return;
                }
            }

            try
            {
                ct.ThrowIfCancellationRequested();

                string? difficultyResponse = await CallAPI_String(
                        string.Format(HelpfulPaths.SSAPI_DIFFS, normalizedHash),
                        quiet: true,
                        maxRetries: 1,
                        ct: ct
                    )
                    .ConfigureAwait(false);

                if (BetterString.IsNullOrWhiteSpace(difficultyResponse))
                {
                    return;
                }

                Map result = new(normalizedHash);
                bool anyRanked = false;

                foreach (JToken difficulty in JToken.Parse(difficultyResponse).Children())
                {
                    ct.ThrowIfCancellationRequested();

                    int leaderboardId = difficulty.Value<int?>("leaderboardId") ?? 0;

                    if (leaderboardId <= 0)
                        continue;

                    lock (UnrankedIds)
                    {
                        if (UnrankedIds.Contains(leaderboardId))
                        {
                            continue;
                        }
                    }

                    string? mapResponse = await CallAPI_String(
                            string.Format(HelpfulPaths.SSAPI_LEADERBOARDID, leaderboardId, "info"),
                            quiet: true,
                            maxRetries: 1,
                            ct: ct
                        )
                        .ConfigureAwait(false);

                    if (BetterString.IsNullOrWhiteSpace(mapResponse))
                    {
                        continue;
                    }

                    JToken mapInfo = JToken.Parse(mapResponse);

                    if (mapInfo.Value<bool?>("ranked") != true)
                    {
                        lock (UnrankedIds)
                        {
                            UnrankedIds.Add(leaderboardId);
                        }

                        continue;
                    }

                    result.Combine(Map.ConvertSSToTaoh(normalizedHash, leaderboardId.ToString(), mapInfo));

                    anyRanked = true;
                }

                if (anyRanked)
                {
                    data[normalizedHash] = result;
                }
                else
                {
                    lock (UnrankedHashes)
                    {
                        UnrankedHashes.Add(normalizedHash);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Error loading a ScoreSaber map: " + exception.Message);

                Plugin.Log.Debug(exception);
            }
        }
    }
}
