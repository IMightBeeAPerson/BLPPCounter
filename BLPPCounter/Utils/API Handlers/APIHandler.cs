using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models.Base;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Profile_Utils;
using ModestTree;
using Newtonsoft.Json.Linq;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal abstract class APIHandler
    {
        private readonly Throttler _bsThrottler = new(50, 10);

        [Inject]
        protected readonly HttpClient client = null!;

        [Inject]
        protected readonly PluginConfig pc = null!;

        public abstract string API_HASH { get; }
        public abstract Task<HttpContent?> CallAPI(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        );

        public async Task<HttpContent?> CallAPI_Static(
            string path,
            Throttler? throttler = null,
            bool quiet = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            const int initialRetryDelayMs = 500;
            bool closeRequest = false;
            if (ct.IsCancellationRequested)
            {
                Plugin.Log.Warn("API call skipped due to CancellationToken.");
                return null;
            }
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (throttler is not null)
                        await throttler.Call(ct).ConfigureAwait(false);

                    Plugin.Log.Debug("API Call: " + path);

                    HttpResponseMessage response;
                    if (closeRequest)
                    {
                        HttpRequestMessage request = new(HttpMethod.Get, new Uri(path));
                        request.Headers.ConnectionClose = true;
                        response = await client.SendAsync(request, ct).ConfigureAwait(false);
                        closeRequest = false;
                    }
                    else
                        response = await client.GetAsync(new Uri(path), ct).ConfigureAwait(false);

                    int status = (int)response.StatusCode;
                    if (status is >= 400 and < 500)
                    {
                        if (!quiet)
                            Plugin.Log.Error(
                                "API request failed, skipping retries due to error code ("
                                    + status
                                    + ").\nPath: "
                                    + path
                            );
                        break;
                    }
                    response.EnsureSuccessStatusCode();

                    return response.Content;
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            Plugin.Log.Warn("API call has been canceled through cancel token.");
                            break;
                        }
                        if (!quiet)
                        {
                            Plugin.Log.Error(
                                $"API request failed with a TaskCanceledException, meaning the request almost certainly timed out. Clearing pool and retrying one more time."
                            );
                            Plugin.Log.Debug(e);
                        }

                        if (closeRequest)
                            continue;

                        attempt = Math.Max(maxRetries - 2, 0);
                        closeRequest = true;
                        continue;
                    }
                    if (!quiet)
                    {
                        Plugin.Log.Error(
                            $"API request failed (attempt {attempt}/{maxRetries})\nPath: {path}\nError: {e.Message}"
                        );
                        Plugin.Log.Debug(e);
                    }

                    if (attempt < maxRetries)
                    {
                        // Exponential backoff delay
                        int delay = initialRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                        if (!quiet)
                            Plugin.Log.Info($"Retrying in {delay} ms...");
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!quiet)
                            Plugin.Log.Error($"API request failed after {maxRetries} attempts. Returning failure.");
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Asks the BeatSaver API for data.
        /// </summary>
        /// <param name="hashes">The hashes to ask BeatSaver for data on.</param>
        /// <param name="path">The Json path(s) to grab the data of.</param>
        /// <returns>The data in string form, in the order that it was recieved.</returns>
        public async Task<string?[]> GetBSData(
            string[] hashes,
            int maxConcurrency = 5,
            int maxRetries = 3,
            params string[] path
        )
        {
            const int MaxCountForBSPage = 50;
            const int initialRetryDelayMs = 500;

            string?[] results = new string[hashes.Length];
            SemaphoreSlim semaphore = new(maxConcurrency);

            // Initial batching
            Task[] batchTasks =
            [
                .. Enumerable
                    .Range(0, (hashes.Length + MaxCountForBSPage - 1) / MaxCountForBSPage)
                    .Select(ProcessBatch),
            ];

            await Task.WhenAll(batchTasks);

            // Retry loop for missing hashes across all batches
            List<int> missingIndices = [.. Enumerable.Range(0, hashes.Length).Where(i => results[i] == null)];

            for (int attempt = 1; attempt <= maxRetries && missingIndices.Count > 0; attempt++)
            {
                List<List<int>> retryBatches =
                [
                    .. missingIndices
                        .Select((index, i) => new { index, batch = i / MaxCountForBSPage })
                        .GroupBy(x => x.batch)
                        .Select(g => g.Select(x => x.index).ToList()),
                ];

                foreach (List<int>? batch in retryBatches)
                {
                    await semaphore.WaitAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (_bsThrottler != null)
                                await _bsThrottler.Call().ConfigureAwait(false);

                            string hashString = string.Join(",", batch.Select(i => hashes[i]));
                            string apiPath = string.Format(HelpfulPaths.BSAPI_HASH, hashString);

                            HttpContent? content = await CallAPI_Static(apiPath, _bsThrottler);

                            if (content is null)
                                return;

                            JToken rawHashData = JToken.Parse(await content.ReadAsStringAsync().ConfigureAwait(false));
                            List<JToken> hashData = batch.Count > 1 ? [.. rawHashData.Children()] : [rawHashData];
                            HashSet<string> returnedHashes = [];

                            for (int i = 0; i < hashData.Count; i++)
                            {
                                JToken? token = hashData[i].First;

                                foreach (string p in path)
                                    token = token?[p];

                                string hash = hashes[batch[i]];

                                results[batch[i]] = token?.ToString();
                                returnedHashes.Add(hash);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                }

                // Wait before next retry if there are still missing
                await Task.Delay(initialRetryDelayMs * (int)Math.Pow(2, attempt - 1)).ConfigureAwait(false);
                missingIndices = [.. missingIndices.Where(i => results[i] == null)];
            }

            // Fill any remaining missing values with null
            foreach (int i in missingIndices)
                results[i] = null;

            return results;

            async Task ProcessBatch(int batchIndex)
            {
                await semaphore.WaitAsync();
                try
                {
                    int start = batchIndex * MaxCountForBSPage;
                    int count = Math.Min(MaxCountForBSPage, hashes.Length - start);
                    string hashString = string.Join(",", hashes.Skip(start).Take(count));
                    string apiPath = string.Format(HelpfulPaths.BSAPI_HASH, hashString);

                    HttpContent? content = await CallAPI_Static(apiPath, _bsThrottler);
                    if (content is null)
                    {
                        // leave these indices as null for retry
                        return;
                    }

                    List<JToken> hashData =
                    [
                        .. JToken.Parse(await content.ReadAsStringAsync().ConfigureAwait(false)).Children(),
                    ];
                    for (int i = 0; i < hashData.Count; i++)
                    {
                        JToken? token = hashData[i].First;

                        foreach (string p in path)
                            token = token?[p];

                        results[start + i] = token?.ToString();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        public async Task<string?> CallAPI_String(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            HttpContent? content = await CallAPI(path, quiet, forceNoHeader, maxRetries, ct).ConfigureAwait(false);

            if (content is null)
                return null;

            return await content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public async Task<byte[]?> CallAPI_Bytes(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            HttpContent? content = await CallAPI(path, quiet, forceNoHeader, maxRetries, ct).ConfigureAwait(false);

            if (content is null)
                return null;

            return await content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public async Task<IOrderedEnumerable<(int PageIndex, T? Results)>> CalledPagedAPI_Internal<T>(
            int totalCount,
            Func<int, int, string> pathParser,
            Throttler throttler,
            Func<string, Task<T>> tokenParser,
            bool zeroIndexedPages = false,
            int maxCountPerPage = 100,
            int maxConcurrency = 5
        )
        {
            int totalPages = (int)Math.Ceiling(totalCount / (double)maxCountPerPage);
            if (zeroIndexedPages)
                totalPages--;
            SemaphoreSlim semaphore = new(maxConcurrency);
            List<Task<(int PageIndex, T? Results)>> pageTasks = [with(totalPages)];

            for (int pageNum = zeroIndexedPages ? 0 : 1; pageNum <= totalPages; pageNum++)
            {
                int localPageNum = pageNum; //For async issues.
                int startIndex = (localPageNum - (zeroIndexedPages ? 0 : 1)) * maxCountPerPage;
                int pageCount = Math.Min(maxCountPerPage, totalCount - startIndex);

                pageTasks.Add(
                    Task.Run<(int, T?)>(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string apiPath = pathParser.Invoke(localPageNum, pageCount);
                            HttpContent? content = await CallAPI_Static(apiPath, throttler: throttler);

                            if (content is null)
                                return (localPageNum, default);

                            string? dataTokensStr = await content.ReadAsStringAsync().ConfigureAwait(false);

                            if (dataTokensStr is null)
                                return (localPageNum, default);
                            T outp = await tokenParser.Invoke(dataTokensStr);

                            return (localPageNum, outp);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    })
                );
            }

            return (await Task.WhenAll(pageTasks).ConfigureAwait(false)).OrderBy(r => r.PageIndex);
        }

        public async Task<IEnumerable<T?>> CalledPagedAPI<T>(
            int totalCount,
            Func<int, int, string> pathParser,
            Throttler throttler,
            Func<string, Task<T>> tokenParser,
            bool zeroIndexedPages = false,
            int maxCountPerPage = 100,
            int maxConcurrency = 5
        )
        {
            return (
                await CalledPagedAPI_Internal(
                    totalCount,
                    pathParser,
                    throttler,
                    tokenParser,
                    zeroIndexedPages,
                    maxCountPerPage,
                    maxConcurrency
                )
            ).Select(r => r.Results);
        }

        public async Task<IEnumerable<T>> CalledPagedAPIFlat<T>(
            int totalCount,
            Func<int, int, string> pathParser,
            Throttler throttler,
            Func<string, Task<IEnumerable<T>?>> tokenParser,
            bool zeroIndexedPages = false,
            int maxCountPerPage = 100,
            int maxConcurrency = 5
        )
        {
            return (
                await CalledPagedAPI_Internal(
                    totalCount,
                    pathParser,
                    throttler,
                    tokenParser,
                    zeroIndexedPages,
                    maxCountPerPage,
                    maxConcurrency
                )
            ).SelectMany(r => r.Results);
        }

        public abstract float GetStarRating(
            ICacheModel diffData,
            SongSpeed speed = SongSpeed.Normal,
            float modMult = 1
        );
        public abstract float[] GetRatings(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1);
        public abstract bool MapIsUsable(ICacheModel diffData, bool useUnranked);
        public abstract bool AreRatingsNull(ICacheModel diffData);
        public abstract string GetSongName(ICacheModel diffData);
        public abstract string GetDiffName(ICacheModel diffData);
        public abstract string GetLeaderboardId(ICacheModel diffData);
        public abstract int GetMaxScore(ICacheModel diffData);
        public abstract Task<int> GetMaxScore(string hash, int diffNum, string modeName);
        public abstract ICacheModel SelectSpecificDiff(ICacheModel diffData, int diffNum, string modeName);
        public abstract Task<string?> GetHashData(string hash, int diffNum);
        public abstract string GetHash(ICacheModel diffData);
        public abstract Task<JToken?> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        );
        public abstract float GetPP(JToken scoreData);
        public abstract int GetScore(JToken scoreData);
        public abstract int GetRank(JToken scoreData);
        public abstract string GetPlayerName(JToken scoreData);
        public abstract float GetAcc(JToken scoreData);
        public abstract Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default);
        public abstract Task<Play[]> GetScores(string userId, int count);

        protected async Task<Play[]> GetScores(
            string userId,
            int count,
            string apiPathFormat,
            string scoreArrayPath,
            bool isZeroIndexed,
            Func<JToken, Play> tokenSelector,
            Throttler throttler,
            Func<Play, string, (Play Data, string ExtraOutp)>? replaceSelector = null,
            params string[] jsonPath
        )
        {
            bool usesPages = apiPathFormat.Contains("{1}");
            async Task<IEnumerable<Play>?> DoStuff(string tokenData)
            {
                if (tokenData is null)
                    return null;

                IEnumerable<JToken>? dataTokens = (
                    scoreArrayPath is null ? JToken.Parse(tokenData) : JToken.Parse(tokenData)[scoreArrayPath]
                )?.Children();

                if (dataTokens is null || dataTokens.IsEmpty())
                    return null;

                if (!usesPages && dataTokens.Count() > count)
                    dataTokens = dataTokens.Take(count);

                Play[] current = [.. dataTokens.Select(tokenSelector)];

                if (replaceSelector is not null)
                {
                    string[] mapHashes = [.. current.Select(data => replaceSelector.Invoke(data, "").ExtraOutp)];
                    string?[] names = await GetBSData(mapHashes, path: jsonPath);

                    for (int i = 0; i < current.Length; i++)
                        current[i] = replaceSelector.Invoke(current[i], names[i] ?? "").Data;
                }

                return current;
            }
            return
            [
                .. usesPages
                    ? await CalledPagedAPIFlat(
                        count,
                        (page, currentCount) => string.Format(apiPathFormat, userId, page, currentCount),
                        throttler,
                        DoStuff,
                        isZeroIndexed
                    )
                    : await CalledPagedAPIFlat(
                        count,
                        (page, currentCount) => string.Format(apiPathFormat, userId),
                        throttler,
                        DoStuff,
                        isZeroIndexed,
                        count
                    ),
            ];
        }

        public abstract Task<float> GetProfilePP(string userId);
        internal abstract Task AddMap(Dictionary<string, Map> Data, string hash, CancellationToken ct = default);

        /*public static async Task<Leaderboards> GetRankedLeaderboards(
            string hash,
            BeatmapDifficulty diff,
            string trueMode
        )
        {
            (bool success, HttpContent data) = await CallAPI_Static(
                    string.Format(HelpfulPaths.BSAPI_HASH, hash),
                    _bsThrottler
                )
                .ConfigureAwait(false);
            if (!success)
                return Leaderboards.None;
            string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
            if (strData is null)
                return Leaderboards.None;
            return GetRankedLeaderboards(
                JToken.Parse(strData)["versions"].Children().First()["diffs"].Children(),
                hash,
                diff,
                trueMode
            );
        }

        public static async Task<(Leaderboards, string)> GetRankedLeaderboardsAndMapKey(
            string hash,
            BeatmapDifficulty diff,
            string trueMode
        )
        {
            (bool success, HttpContent data) = await CallAPI_Static(
                    string.Format(HelpfulPaths.BSAPI_HASH, hash),
                    _bsThrottler
                )
                .ConfigureAwait(false);
            if (!success)
                return (Leaderboards.None, "");
            string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
            if (strData is null)
                return (Leaderboards.None, "");
            JToken parsedData = JToken.Parse(strData);
            return (
                GetRankedLeaderboards(
                    parsedData["versions"].Children().First()["diffs"].Children(),
                    hash,
                    diff,
                    trueMode
                ),
                parsedData["id"].ToString()
            );
        }

        private static Leaderboards GetRankedLeaderboards(
            JEnumerable<JToken> BSDiffs,
            string hash,
            BeatmapDifficulty diff,
            string trueMode
        )
        {
            //Plugin.Log.Debug($"Diff: {diff} || trueMode: {trueMode}\nThe diffs\n{BSDiffs.Print()}");
            Leaderboards outp = Leaderboards.None;
            JToken BSData = BSDiffs.First(token =>
                token["difficulty"].ToString().Equals(diff.ToString())
                && token["characteristic"].ToString().Equals(trueMode)
            );
            if (BSData["stars"] != null)
                outp |= Leaderboards.Scoresaber;
            if (BSData["blStars"] != null)
                outp |= Leaderboards.Beatleader;
            if (TheCounter.Data[hash].GetModes().Contains(Map.AP_MODE_NAME))
                outp |= Leaderboards.Accsaber;
            //Plugin.Log.Info("Ranked Leaderboards: " + outp);
            return outp;
        }*/

        #region Internal Classes
        public struct ScoregraphInfo(float acc, float pp, SongSpeed speed, float modMult, string playerName)
        {
            public float Acc = acc;
            public float PP = pp;
            public SongSpeed Speed = speed;
            public float ModMult = modMult;
            public string PlayerName = playerName;

            public void ChangePP(float acc, float pp)
            {
                Acc = acc;
                PP = pp;
            }
        }
        #endregion
    }
}
