using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Profile_Utils;
using Newtonsoft.Json.Linq;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class APAPI : APIHandler
    {
        internal static readonly Throttler Throttle = new(400, 60);
        private static readonly HashSet<string> UnrankedHashes = [];

        private APAPI() { }

        public override string API_HASH => HelpfulPaths.SSAPI_DIFFS;

        public override Task<(bool, HttpContent?)> CallAPI(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.accsaberreloaded";
            if (!forceNoHeader && !path[..LinkHeader.Length].Equals(LinkHeader))
                path = HelpfulPaths.APAPI + path;
            Throttler? t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return CallAPI_Static(path, t, quiet, maxRetries, ct);
        }

        public override float GetStarRating(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            if (diffData is TaohCache taohCache)
                return taohCache.ComplexityAccSaber ?? 0f;
            return 0f;
        }

        public override float[] GetRatings(
            ICacheModel diffData,
            SongSpeed speed = SongSpeed.Normal,
            float modMult = 1
        ) => [GetStarRating(diffData)];

        public override string GetSongName(ICacheModel diffData) =>
            diffData is TaohCache taohCache ? taohCache.Name
            : diffData is BlCacheEntry cacheEntry ? cacheEntry.SongInfo.SongName ?? ""
            : "";

        public override string GetDiffName(ICacheModel diffData) => diffData.Difficulty.Name();

        public override string GetLeaderboardId(ICacheModel diffData) => diffData.LeaderboardId;

        public override string GetHash(ICacheModel diffData) => diffData.Hash;

        public override bool MapIsUsable(ICacheModel diffData) => diffData is not null && GetRatings(diffData)[0] > 0;

        public override bool AreRatingsNull(ICacheModel diffData) =>
            diffData is not TaohCache taohCache || taohCache.ComplexityAccSaber is null;

        public override int GetMaxScore(ICacheModel diffData) => GetAPI(Leaderboards.Scoresaber).GetMaxScore(diffData);

        public override int GetRank(JToken scoreData) => (int)(scoreData["rank"] ?? -1);

        public override string GetPlayerName(JToken scoreData) => scoreData["userName"]?.ToString() ?? "";

        public override float GetAcc(JToken scoreData) => (float)(scoreData["accuracy"] ?? 0f);

        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName)
        {
            string? result = await CallAPI_String(
                string.Format(HelpfulPaths.APAPI_HASH_DIFF, hash, PathUtils.DiffNumToReloadedDiff(diffNum))
            );

            if (BetterString.IsNullOrEmpty(result))
                return -1;

            JToken? outp = JToken.Parse(result)["difficulties"];

            if (outp is null)
                return -1;

            return (int)(outp.Children().First()["maxScore"] ?? -1);
        }

        public override JToken SelectSpecificDiff(ICacheModel diffData, int diffNum, string modeName) =>
            (JToken)diffData;

        public override async Task<string?> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(
                    string.Format(HelpfulPaths.APAPI_HASH_DIFF, hash, PathUtils.DiffNumToReloadedDiff(diffNum)),
                    true,
                    maxRetries: 1
                )
                .ConfigureAwait(false);

        public override async Task<JToken?> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        )
        {
            string reloadedDiff = PathUtils.DiffNumToReloadedDiff(
                Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff))
            );
            string? dataStr = await CallAPI_String(
                    string.Format(HelpfulPaths.APAPI_SCORE, userId, hash.ToLower(), reloadedDiff),
                    ct: ct
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrEmpty(dataStr))
                return null;

            return JToken.Parse(dataStr);
        }

        public override float GetPP(JToken scoreData) => (float)(scoreData["ap"] ?? 0);

        public override int GetScore(JToken scoreData) => (int)(scoreData["baseScore"] ?? -1);

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
                    BeatmapDifficulty beatmapDiff = Map.FromValue(
                        PathUtils.ReloadedDiffToDiffNum(token["difficulty"]?.ToString())
                    );
                    return new Play(
                        token["songName"]?.ToString() ?? "Unknown",
                        token["songHash"]?.ToString() ?? "",
                        beatmapDiff,
                        Profile.DEFAULT_MODE,
                        (float)(token["ap"] ?? -1f),
                        (uint)(token["rank"] ?? 0)
                    )
                    {
                        AccSaberCategory = (APCategory)
                            Enum.Parse(
                                typeof(APCategory),
                                PathUtils.ReloadedCategoryToCategoryId(token["categoryId"]?.ToString())
                            ),
                    };
                },
                Throttle,
                (data, repData) =>
                {
                    if (repData is null || repData.Equals(string.Empty))
                        return (data, data.MapKey);
                    data.MapKey = repData;
                    return (data, data.MapKey);
                },
                "id"
            );
        }

        public override Task<Play[]> GetScores(string userId, int count) =>
            GetScores(userId, count, HelpfulPaths.APAPI_SCORES);

        public Task<Play[]> GetScores(string userId, int count, APCategory accSaberType) =>
            GetScores(
                userId,
                count,
                string.Format(
                    HelpfulPaths.APAPI_CATEGORY_SCORES,
                    "{0}",
                    PathUtils.CategoryIdToReloadedCategory(accSaberType.ToString()),
                    0,
                    count
                )
            );

        public override async Task<float> GetProfilePP(string userId)
        {
            string? result = await CallAPI_String(string.Format(HelpfulPaths.APAPI_PLAYERID, userId))
                .ConfigureAwait(false);

            if (BetterString.IsNullOrEmpty(result))
                return 0f;

            JToken token = JToken.Parse(result);

            return (float)(token["ap"] ?? 0f);
        }

        public async Task<float> GetProfilePP(string userId, APCategory accSaberType)
        {
            string? result = await CallAPI_String(
                    string.Format(
                        HelpfulPaths.APAPI_PLAYERID_CATEGORY,
                        userId,
                        accSaberType.ToString().ToLower() + "_acc"
                    )
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrEmpty(result))
                return 0f;

            JToken token = JToken.Parse(result);

            return (float)(token["ap"] ?? 0f);
        }

        public override Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default) =>
            GetAPI(Leaderboards.Scoresaber).GetScoregraph(ms, ct);

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

                string? response = await CallAPI_String(
                        string.Format(HelpfulPaths.APAPI_HASH, normalizedHash),
                        quiet: true,
                        maxRetries: 1,
                        ct: ct
                    )
                    .ConfigureAwait(false);

                if (BetterString.IsNullOrWhiteSpace(response))
                {
                    lock (UnrankedHashes)
                    {
                        UnrankedHashes.Add(normalizedHash);
                    }

                    return;
                }

                Map result = new(normalizedHash);

                foreach (JToken difficulty in JToken.Parse(response)["difficulties"]?.Children() ?? [])
                {
                    ct.ThrowIfCancellationRequested();

                    string? beatSaverCode = difficulty.Value<string>("beatsaverCode");

                    if (BetterString.IsNullOrWhiteSpace(beatSaverCode))
                    {
                        continue;
                    }

                    int songId = int.Parse(beatSaverCode, System.Globalization.NumberStyles.HexNumber);

                    result.Combine(Map.ConvertAPToTaoh(normalizedHash, songId.ToString(), difficulty));
                }

                if (result.GetModes().Length == 0)
                {
                    lock (UnrankedHashes)
                    {
                        UnrankedHashes.Add(normalizedHash);
                    }

                    return;
                }

                data[normalizedHash] = result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Error loading an AccSaber map: " + exception.Message);

                Plugin.Log.Debug(exception);
            }
        }
    }
}
