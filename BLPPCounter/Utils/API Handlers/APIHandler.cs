using static GameplayModifiers;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using BLPPCounter.Settings.Configs;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;
using BLPPCounter.Helpfuls;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.API_Handlers
{
    internal abstract class APIHandler
    {
        protected static readonly TimeSpan ClientTimeout = new TimeSpan(0, 0, 5);
        protected static readonly HttpClient client = new HttpClient
        {
            Timeout = ClientTimeout
        };
        private static Throttler BSThrottler = new Throttler(50, 10);

        public static bool UsingDefault = false;

        public abstract string API_HASH { get; }
        public abstract Task<(bool Success, HttpContent Content)> CallAPI(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3);
        public static async Task<(bool Success, HttpContent Content)> CallAPI_Static(string path, Throttler throttler = null, bool quiet = false, int maxRetries = 3)
        {
            const int initialRetryDelayMs = 500;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (throttler != null)
                        await throttler.Call();

                    Plugin.Log.Debug("API Call: " + path);

                    HttpResponseMessage response = await client.GetAsync(new Uri(path)).ConfigureAwait(false);
                    int status = (int)response.StatusCode;
                    if (status >= 400 && status < 500)
                    {
                        if (!quiet)
                            Plugin.Log.Error("API request failed, skipping retries due to error code (" + status + ").\nPath: " + path);
                        break;
                    }
                    response.EnsureSuccessStatusCode();

                    return (true, response.Content);
                }
                catch (Exception e)
                {
                    if (!quiet)
                    {
                        Plugin.Log.Error($"API request failed (attempt {attempt}/{maxRetries})\nPath: {path}\nError: {e.Message}");
                        Plugin.Log.Debug(e);
                    }

                    if (attempt < maxRetries)
                    {
                        // Exponential backoff delay
                        int delay = initialRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                        Plugin.Log.Info($"Retrying in {delay} ms...");
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!quiet)
                            Plugin.Log.Error($"API request failed after {maxRetries} attempts. Returning failure.");
                        return (false, null);
                    }
                }
            }

            return (false, null);
        }

        /// <summary>
        /// Asks the BeatSaver API for data.
        /// </summary>
        /// <param name="hashes">The hashes to ask BeatSaver for data on.</param>
        /// <param name="path">The Json path(s) to grab the data of.</param>
        /// <returns>The data in string form, in the order that it was recieved.</returns>
        public static async Task<string[]> GetBSData(string[] hashes, int maxConcurrency = 5, int maxRetries = 3, params string[] path)
        {
            const int MaxCountForBSPage = 50;
            const int initialRetryDelayMs = 500;

            var results = new string[hashes.Length];
            var semaphore = new SemaphoreSlim(maxConcurrency);

            // Initial batching
            var batchTasks = Enumerable.Range(0, (hashes.Length + MaxCountForBSPage - 1) / MaxCountForBSPage)
                .Select(batchIndex => ProcessBatch(batchIndex))
                .ToArray();

            await Task.WhenAll(batchTasks);

            // Retry loop for missing hashes across all batches
            var missingIndices = Enumerable.Range(0, hashes.Length).Where(i => results[i] == null).ToList();

            for (int attempt = 1; attempt <= maxRetries && missingIndices.Count > 0; attempt++)
            {
                var retryBatches = missingIndices
                    .Select((index, i) => new { index, batch = i / MaxCountForBSPage })
                    .GroupBy(x => x.batch)
                    .Select(g => g.Select(x => x.index).ToList())
                    .ToList();

                foreach (var batch in retryBatches)
                {
                    await semaphore.WaitAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (BSThrottler != null)
                                await BSThrottler.Call().ConfigureAwait(false);

                            string hashString = string.Join(",", batch.Select(i => hashes[i]));
                            string apiPath = string.Format(HelpfulPaths.BSAPI_HASH, hashString);

                            (bool succeeded, HttpContent content) = await CallAPI_Static(apiPath, BSThrottler);
                            if (!succeeded) return;

                            var hashData = JToken.Parse(await content.ReadAsStringAsync().ConfigureAwait(false)).Children().ToList();
                            var returnedHashes = new HashSet<string>();

                            for (int i = 0; i < hashData.Count; i++)
                            {
                                JToken token = hashData[i].First;
                                foreach (var p in path)
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
                missingIndices = missingIndices.Where(i => results[i] == null).ToList();
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

                    (bool succeeded, HttpContent content) = await CallAPI_Static(apiPath, BSThrottler);
                    if (!succeeded)
                    {
                        // leave these indices as null for retry
                        return;
                    }

                    var hashData = JToken.Parse(await content.ReadAsStringAsync().ConfigureAwait(false)).Children().ToList();
                    for (int i = 0; i < hashData.Count; i++)
                    {
                        JToken token = hashData[i].First;
                        foreach (var p in path)
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
        public async Task<string> CallAPI_String(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3)
        {
            var data = await CallAPI(path, quiet, forceNoHeader, maxRetries).ConfigureAwait(false);
            if (!data.Success) return null;
            return await data.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        public async Task<byte[]> CallAPI_Bytes(string path, bool quiet = false, bool forceNoHeader = false, int maxRetries = 3)
        {
            var data = await CallAPI(path, quiet, forceNoHeader, maxRetries).ConfigureAwait(false);
            if (!data.Success) return null;
            return await data.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
        public abstract float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1);
        public abstract bool MapIsUsable(JToken diffData);
        public abstract bool AreRatingsNull(JToken diffData);
        public abstract string GetSongName(JToken diffData);
        public abstract string GetDiffName(JToken diffData);
        public abstract string GetLeaderboardId(JToken diffData);
        public abstract int GetMaxScore(JToken diffData);
        public abstract Task<int> GetMaxScore(string hash, int diffNum, string modeName);
        public abstract float[] GetRatings(JToken diffData);
        public abstract JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName);
        public abstract Task<string> GetHashData(string hash, int diffNum);
        public abstract string GetHash(JToken diffData);
        public abstract Task<JToken> GetScoreData(string userId, string hash, string diff, string mode, bool quiet = false);
        public abstract float GetPP(JToken scoreData);
        public abstract int GetScore(JToken scoreData);
        public abstract Task<float[]> GetScoregraph(MapSelection ms);
        public abstract Task<(string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId)[]> GetScores(string userId, int count);
        protected async Task<(string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId)[]> GetScores(
        string userId, int count, string apiPathFormat, string scoreArrayPath,
        Func<JToken, (string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId)> tokenSelector,
        Func<(string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId), string, ((string MapName, BeatmapDifficulty Difficulty, float RawPP, string MapId) Data, string ExtraOutp)> replaceSelector,
        Throttler throttler = null, params string[] jsonPath)
        {
            const int MaxCountToPage = 100, maxConcurrency = 5;
            int totalPages = (int)Math.Ceiling(count / (double)MaxCountToPage);
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var pageTasks = new List<Task<(int PageIndex, (string, BeatmapDifficulty, float, string)[])>>();

            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                int localPageNum = pageNum; //For async issues.
                int startIndex = (localPageNum - 1) * MaxCountToPage;
                int pageCount = Math.Min(MaxCountToPage, count - startIndex);

                pageTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string apiPath = string.Format(apiPathFormat, userId, localPageNum, pageCount);
                        var response = await CallAPI_Static(apiPath, throttler: throttler);
                        if (!response.Success) return (localPageNum, Array.Empty<(string, BeatmapDifficulty, float, string)>());

                        List<JToken> dataTokens = JToken.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))?[scoreArrayPath]?.Children().ToList();
                        if (dataTokens == null || dataTokens.Count == 0)
                            return (localPageNum, Array.Empty<(string, BeatmapDifficulty, float, string)>());

                        var current = dataTokens.Select(tokenSelector).ToArray();

                        string[] mapHashes = current.Select(data => replaceSelector.Invoke(data, "").ExtraOutp).ToArray();
                        string[] names = await GetBSData(mapHashes, path: jsonPath);

                        for (int i = 0; i < current.Length; i++)
                            current[i] = replaceSelector.Invoke(current[i], names[i]).Data;

                        return (localPageNum, current);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            var results = await Task.WhenAll(pageTasks).ConfigureAwait(false);

            var orderedResults = results
                .OrderBy(r => r.PageIndex)
                .SelectMany(r => r.Item2)
                .ToArray();

            return orderedResults;
        }

        public abstract Task<float> GetProfilePP(string userId);
        internal abstract Task AddMap(Dictionary<string, Map> Data, string hash);
        public static APIHandler GetAPI(bool useDefault = false) => GetAPI(!useDefault ? PluginConfig.Instance.Leaderboard : PluginConfig.Instance.DefaultLeaderboard);
        public static APIHandler GetSelectedAPI() => GetAPI(UsingDefault);
        public static APIHandler GetAPI(Leaderboards leaderboard)
        {
            switch (leaderboard)
            {
                case Leaderboards.Beatleader:
                    return BLAPI.Instance;
                case Leaderboards.Scoresaber:
                    return SSAPI.Instance;
                case Leaderboards.Accsaber:
                    return APAPI.Instance;
                default:
                    return null;
            }
        }
        public static async Task<Leaderboards> GetRankedLeaderboards(string hash, BeatmapDifficulty diff, string trueMode)
        {
            (bool success, HttpContent data) = await CallAPI_Static(string.Format(HelpfulPaths.BSAPI_HASH, hash), BSThrottler).ConfigureAwait(false);
            if (!success) return Leaderboards.None;
            string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
            if (strData is null) return Leaderboards.None;
            return GetRankedLeaderboards(JToken.Parse(strData)["versions"].Children().First()["diffs"].Children(), hash, diff, trueMode);
        }
        public static async Task<(Leaderboards, string)> GetRankedLeaderboardsAndMapKey(string hash, BeatmapDifficulty diff, string trueMode)
        {
            (bool success, HttpContent data) = await CallAPI_Static(string.Format(HelpfulPaths.BSAPI_HASH, hash), BSThrottler).ConfigureAwait(false);
            if (!success) return (Leaderboards.None, "");
            string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
            if (strData is null) return (Leaderboards.None, "");
            JToken parsedData = JToken.Parse(strData);
            return (GetRankedLeaderboards(parsedData["versions"].Children().First()["diffs"].Children(), hash, diff, trueMode), parsedData["id"].ToString());
        }
        private static Leaderboards GetRankedLeaderboards(JEnumerable<JToken> BSDiffs, string hash, BeatmapDifficulty diff, string trueMode)
        {
            //Plugin.Log.Debug($"Diff: {diff} || trueMode: {trueMode}\nThe diffs\n{BSDiffs.Print()}");
            Leaderboards outp = Leaderboards.None;
            JToken BSData = BSDiffs.First(token => token["difficulty"].ToString().Equals(diff.ToString()) && token["characteristic"].ToString().Equals(trueMode));
            if (BSData["stars"] != null) outp |= Leaderboards.Scoresaber;
            if (BSData["blStars"] != null) outp |= Leaderboards.Beatleader;
            if (TheCounter.Data[hash].GetModes().Contains(Map.AP_MODE_NAME)) outp |= Leaderboards.Accsaber;
            return outp;
        }
    }
}
