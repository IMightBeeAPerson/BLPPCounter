using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Profile_Utils;
using Newtonsoft.Json.Linq;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class BLAPI : APIHandler
    {
        internal static readonly Throttler Throttle = new(100, 15);

        [Inject]
        private readonly BLCalc calc = null!;

        public override string API_HASH => HelpfulPaths.BLAPI_HASH;

        public override async Task<HttpContent?> CallAPI(
            string path,
            bool quiet = false,
            bool forceNoHeader = false,
            int maxRetries = 3,
            CancellationToken ct = default
        )
        {
            const string LinkHeader = "https://";
            const string LeaderboardHeader = "api.beatleader";
            if (!forceNoHeader && !path[..LinkHeader.Length].Equals(LinkHeader))
                path = HelpfulPaths.BLAPI + path;
            Throttler? t = null;
            if (path.Substring(LinkHeader.Length, LeaderboardHeader.Length).Equals(LeaderboardHeader))
                t = Throttle;
            return await CallAPI_Static(path, t, quiet, maxRetries, ct).ConfigureAwait(false);
        }

        public override float GetStarRating(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            if (diffData is not BlCacheEntry entry)
                return 0f;
            return entry.DifficultyInfo.GetRating(PPType.Star, speed) * modMult;
        }

        public override float[] GetRatings(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            if (diffData is not BlCacheEntry entry)
                return [0f, 0f, 0f];
            BlCacheDifficultyInfo di = entry.DifficultyInfo;
            return
            [
                di.GetRating(PPType.Acc, speed) * modMult,
                di.GetRating(PPType.Pass, speed) * modMult,
                di.GetRating(PPType.Tech, speed) * modMult,
            ];
        }

        public override string GetSongName(ICacheModel diffData) =>
            diffData is BlCacheEntry entry ? entry.SongInfo.SongName ?? "" : "";

        public override string GetDiffName(ICacheModel diffData) =>
            diffData is BlCacheEntry entry ? entry.DifficultyInfo.DifficultyName : diffData.Difficulty.ToString();

        public override string GetLeaderboardId(ICacheModel diffData) => diffData.LeaderboardId;

        public override string GetHash(ICacheModel diffData) => diffData.Hash;

        public override int GetMaxScore(ICacheModel diffData) =>
            diffData is BlCacheEntry entry ? entry.DifficultyInfo.MaxScore : 0;

        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName)
        {
            string? response = await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(response))
                return 0;
            JToken? diff = JToken
                .Parse(response)["leaderboards"]
                ?.Children()
                .FirstOrDefault(t =>
                    t["difficulty"]?.Value<int?>("value") == diffNum
                    && t["difficulty"]?.Value<string>("modeName") == modeName
                );
            return (int)(diff?["difficulty"]?["maxScore"] ?? diff?["maxScore"] ?? 0);
        }

        public override int GetRank(JToken scoreData) => (int)(scoreData["rank"] ?? 0);

        public override string GetPlayerName(JToken scoreData)
        {
            JToken? outp = scoreData["player"];
            return outp is JValue ? outp.ToString() : outp?["name"]?.ToString() ?? "";
        }

        public override float GetAcc(JToken scoreData) => (float)(scoreData["accuracy"] ?? 0f);

        public override ICacheModel SelectSpecificDiff(ICacheModel diffData, int diffNum, string modeName) => diffData;

        public override bool MapIsUsable(ICacheModel diffData, bool useUnranked) =>
            diffData is BlCacheEntry entry && HelpfulMisc.StatusIsUsable(entry.DifficultyInfo.Status, useUnranked);

        public override bool AreRatingsNull(ICacheModel diffData) =>
            diffData is not BlCacheEntry entry || entry.DifficultyInfo.ModifierRatings is null;

        public override async Task<string?> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false);

        public override async Task<JToken?> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        )
        {
            string? outp = await CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_SCORE, userId, hash, diff, mode),
                    quiet,
                    maxRetries: 1,
                    ct: ct
                )
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(outp))
                return null;
            return JToken.Parse(outp);
        }

        public override float GetPP(JToken scoreData) => (float)(scoreData["pp"] ?? 0f);

        public override int GetScore(JToken scoreData) => (int)(scoreData["modifiedScore"] ?? 0);

        public override async Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default)
        {
            if (ms.IsUsable)
            {
                ScoregraphInfo Parse1(JToken token)
                {
                    string modStr = token["modifiers"]?.ToString() ?? "";
                    (SongSpeed speed, float modMult) = HelpfulMisc.ParseModifiers(modStr, ms.MapData.Data);
                    return new ScoregraphInfo(
                        (float)(token["accuracy"] ?? 0f) / 100f,
                        (float)Math.Round((float)(token["pp"] ?? 0f), pc.DecimalPrecision),
                        speed,
                        modMult,
                        token["playerName"]?.ToString().ClampString(pc.MaxNameLength) ?? ""
                    );
                }

                string? data = await CallAPI_String($"leaderboard/{ms.MapData.SongId}/scoregraph", ct: ct)
                    .ConfigureAwait(false);
                if (BetterString.IsNullOrEmpty(data))
                    return [];
                return [.. JToken.Parse(data).Children().Select(Parse1)];
            }
            else
            {
                if (ms.MapData.Data is not BlCacheEntry blEntry)
                    return [];
                BlCacheDifficultyInfo di = blEntry.DifficultyInfo;
                float maxScore = di.MaxScore;
                float acc = di.AccRating,
                    pass = di.PassRating,
                    tech = di.TechRating;

                string? data = await CallAPI_String(
                        $"leaderboard/scores/{ms.MapData.SongId}?count={pc.MinRank}",
                        ct: ct
                    )
                    .ConfigureAwait(false);
                if (BetterString.IsNullOrEmpty(data))
                    return [];

                ScoregraphInfo Parse2(JToken token)
                {
                    string modStr = token["modifiers"]?.ToString() ?? "";
                    (SongSpeed speed, float modMult) = HelpfulMisc.ParseModifiers(modStr, ms.MapData.Data);
                    return new ScoregraphInfo(
                        (float)(token["modifiedScore"] ?? 0f) / maxScore,
                        (float)
                            Math.Round(
                                calc.Inflate(
                                    calc.GetSummedPp((int)(token["modifiedScore"] ?? 0f) / maxScore, acc, pass, tech)
                                ),
                                pc.DecimalPrecision
                            ),
                        speed,
                        modMult,
                        token["playerName"]?.ToString().ClampString(pc.MaxNameLength) ?? ""
                    );
                }

                return [.. JToken.Parse(data)["scores"]?.Children().Select(Parse2) ?? []];
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
                        string mapID = token["leaderboard"]?["id"]?.ToString() ?? "";
                        string cleanMapId = CleanUpId(mapID);
                        return new Play(
                            token["leaderboard"]?["songHash"]?.ToString() ?? "",
                            cleanMapId,
                            Map.FromValue((int)(token["leaderboard"]?["difficulty"] ?? -1)),
                            token["leaderboard"]?["modeName"]?.ToString() ?? "Standard",
                            (float)(token["score"]?["pp"] ?? 0f),
                            (uint)(token["score"]?["rank"] ?? 0)
                        );
                    },
                    Throttle,
                    (data, repData) =>
                    {
                        if (repData is null || repData.Equals(string.Empty))
                            return (data, data.MapName);
                        data.MapName = repData;
                        return (data, data.MapName);
                    },
                    "metadata",
                    "songName"
                )
                .ConfigureAwait(false);
        }

        public override async Task<float> GetProfilePP(string userId)
        {
            string? response = await CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID, userId))
                .ConfigureAwait(false);
            if (BetterString.IsNullOrEmpty(response))
                return 0f;
            return (float)(JToken.Parse(response)["pp"] ?? 0);
        }

        internal override async Task AddMap(Dictionary<string, Map> data, string hash, CancellationToken ct = default)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string normalizedHash = Map.NormalizeHash(hash);

            try
            {
                ct.ThrowIfCancellationRequested();

                string? response = await CallAPI_String(
                        string.Format(HelpfulPaths.BLAPI_HASH, normalizedHash),
                        quiet: true,
                        maxRetries: 1,
                        ct: ct
                    )
                    .ConfigureAwait(false);

                if (BetterString.IsNullOrWhiteSpace(response))
                    return;

                JObject root = JObject.Parse(response!);

                JToken? song = root["song"];

                if (song is null)
                    return;

                string songId = song.Value<string>("id") ?? "";

                string songHash = song.Value<string>("hash") ?? normalizedHash;

                Map result = new(normalizedHash);

                foreach (JToken difficultyToken in song["difficulties"]?.Children() ?? [])
                {
                    ct.ThrowIfCancellationRequested();

                    BlCacheDifficultyInfo? difficultyInfo = difficultyToken.ToObject<BlCacheDifficultyInfo>();

                    if (difficultyInfo is null)
                        continue;

                    string leaderboardId =
                        difficultyToken.Value<string>("id")
                        ?? songId + difficultyInfo.DifficultyValue + difficultyInfo.Characteristic;

                    BlCacheEntry entry = new()
                    {
                        LeaderboardId = leaderboardId,
                        SongInfo = new BlCacheSongInfo { Id = songId, Hash = songHash },
                        DifficultyInfo = difficultyInfo,
                    };

                    result.Add(leaderboardId, entry);
                }

                if (result.GetModes().Length > 0)
                    data[normalizedHash] = result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Error loading a BeatLeader map: " + exception.Message);

                Plugin.Log.Debug(exception);
            }
        }

        public static string CleanUpId(string mapID) => mapID.Contains('x') ? mapID[..mapID.IndexOf('x')] : mapID[..^2];
    }
}
