using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Models.ScoreSaber;
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

        public override float GetStarRating(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            return diffData is TaohCache cache ? cache.StarScoreSaber ?? 0f : 0f;
        }

        public override float[] GetRatings(ICacheModel diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            return [GetStarRating(diffData)];
        }

        public override string GetSongName(ICacheModel diffData)
        {
            return diffData is TaohCache cache ? cache.Name : "";
        }

        public override string GetDiffName(ICacheModel diffData)
        {
            return diffData.Difficulty.ToString();
        }

        public override string GetLeaderboardId(ICacheModel diffData)
        {
            return diffData.LeaderboardId;
        }

        public override string GetHash(ICacheModel diffData)
        {
            return diffData.Hash;
        }

        public override bool MapIsUsable(ICacheModel diffData, bool useUnranked)
        {
            return diffData is TaohCache cache && (cache.StarScoreSaber ?? 0f) > 0f;
        }

        public override bool AreRatingsNull(ICacheModel diffData)
        {
            return diffData is not TaohCache cache || cache.StarScoreSaber is null;
        }

        public override int GetMaxScore(ICacheModel diffData)
        {
            // TaohCache does not store maxScore; fall back to a synchronous API call.
            // Prefer the async GetMaxScore(hash, diffNum, modeName) overload where possible.
            if (diffData is not TaohCache cache || cache.ScoreSaberID is null)
                return 0;

            ScoreSaberLeaderboard? response = CallAPI_Json<ScoreSaberLeaderboard>(
                    string.Format(HelpfulPaths.SSAPI_LEADERBOARD_INFO, cache.ScoreSaberID)
                )
                .GetAwaiter()
                .GetResult();

            return response?.MaxScore ?? 0;
        }

        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName)
        {
            ScoreSaberLeaderboard? response = await CallAPI_Json<ScoreSaberLeaderboard>(
                string.Format(HelpfulPaths.SSAPI_HASH_INFO, hash, "SoloStandard", diffNum)
            );

            return response?.MaxScore ?? 0;
        }

        public override ICacheModel SelectSpecificDiff(ICacheModel diffData, int diffNum, string modeName)
        {
            return diffData;
        }

        public override async Task<string?> GetHashData(string hash, int diffNum)
        {
            return await CallAPI_String(string.Format(HelpfulPaths.SSAPI_HASH_INFO, hash, "SoloStandard", diffNum))
                .ConfigureAwait(false);
        }

        public override async Task<IScoreModel?> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        )
        {
            int diffNum = Map.FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), diff));

            ScoreSaberBasicPlayer? nameResponse = await CallAPI_Json<ScoreSaberBasicPlayer>(
                string.Format(HelpfulPaths.SSAPI_PLAYER_PROFILE_BASIC, userId),
                quiet,
                maxRetries: 1,
                ct: ct
            );

            if (nameResponse is null)
                return null;

            string name = nameResponse.Name;

            ScoreSaberPagedScores? scores = await CallAPI_Json<ScoreSaberPagedScores>(
                string.Format(HelpfulPaths.SSAPI_HASH_SCORES, hash, "SoloStandard", diffNum) + "&search=" + name,
                quiet,
                maxRetries: 1,
                ct: ct
            );

            if (scores is null)
                return null;

            ScoreSaberScore score = scores.Data.FirstOrDefault(s => s.PlayerName.Equals(name));

            ScoreSaberLeaderboard? mapInfo = await CallAPI_Json<ScoreSaberLeaderboard>(
                string.Format(HelpfulPaths.SSAPI_HASH_INFO, hash, "SoloStandard", diffNum),
                quiet,
                maxRetries: 1,
                ct: ct
            );

            if (
                mapInfo is not null
                && mapInfo.AdditionalData is not null
                && mapInfo.AdditionalData.TryGetValue("bsid", out JToken bsid)
            )
                score.MapId = bsid.ToString();

            return score;
        }

        public override async Task<Play[]> GetScores(string userId, int count)
        {
            return await GetScores(
                    userId,
                    count,
                    HelpfulPaths.SSAPI_PLAYER_SCORES,
                    "Data",
                    false,
                    token =>
                    {
                        ScoreSaberPlayerScore? score = token.ToObject<ScoreSaberPlayerScore>();

                        if (score is null)
                            return default;

                        return new(
                            score.Leaderboard.Map.SongName,
                            score.Leaderboard.Map.Hash,
                            score.Leaderboard.Difficulty?.Difficulty ?? BeatmapDifficulty.Easy,
                            score.Leaderboard.Difficulty?.GameMode.Replace("Solo", "") ?? "Standard",
                            score.Score.Pp,
                            (uint)score.Score.Rank
                        );
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
                )
                .ConfigureAwait(false);
        }

        public override async Task<float> GetProfilePP(string userId)
        {
            ScoreSaberBasicPlayer? response = await CallAPI_Json<ScoreSaberBasicPlayer>(
                string.Format(HelpfulPaths.SSAPI_PLAYER_PROFILE_BASIC, userId)
            );

            return response?.Stats.TotalPp ?? 0f;
        }

        public override async Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default)
        {
            List<ScoregraphInfo> pps = [];
            const int SS_MAX_PAGELENGTH = 100;

            int pageLen = Math.Min(SS_MAX_PAGELENGTH, pc.MinRank);

            string path = string.Format(
                HelpfulPaths.SSAPI_HASH_SCORES_PAGED,
                ms.Hash,
                "SoloStandard",
                Map.FromDiff(ms.Difficulty),
                "{0}",
                pageLen
            );

            int pages = (int)Math.Ceiling(pc.MinRank / (float)pageLen);

            ScoreSaberLeaderboard? infoResponse = await CallAPI_Json<ScoreSaberLeaderboard>(
                string.Format(HelpfulPaths.SSAPI_HASH_INFO, ms.Hash, "SoloStandard", Map.FromDiff(ms.Difficulty)),
                ct: ct
            );

            if (infoResponse is null)
                return [];

            int maxScore = infoResponse.MaxScore;

            for (int i = 1; i < pages + 1; i++)
            {
                ScoreSaberPagedScores? pageResponse = await CallAPI_Json<ScoreSaberPagedScores>(
                    path + "&page=" + i,
                    ct: ct
                );

                if (pageResponse is null)
                    continue;

                IEnumerable<ScoregraphInfo> scores = pageResponse.Data.Select(score => new ScoregraphInfo(
                    score.Accuracy,
                    score.Pp,
                    SongSpeed.Normal,
                    1f,
                    score.PlayerName.ClampString(pc.MaxNameLength)
                ));

                pps.AddRange(scores);
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

                    ScoreSaberLeaderboard? mapInfo = await CallAPI_Json<ScoreSaberLeaderboard>(
                        string.Format(HelpfulPaths.SSAPI_LEADERBOARD_INFO, leaderboardId),
                        quiet: true,
                        maxRetries: 1,
                        ct: ct
                    );

                    if (mapInfo is null)
                    {
                        continue;
                    }

                    if (mapInfo.Realm.RankedAt is null)
                    {
                        lock (UnrankedIds)
                        {
                            UnrankedIds.Add(leaderboardId);
                        }

                        continue;
                    }
                    //result.Combine(Map.ConvertSSToTaoh(normalizedHash, leaderboardId.ToString(), mapInfo));
                    result.Combine(
                        new Map(
                            normalizedHash,
                            "Standard",
                            mapInfo.Difficulty?.Difficulty ?? BeatmapDifficulty.Easy,
                            mapInfo.Map.BsId ?? mapInfo.Id.ToString(),
                            mapInfo
                        )
                    );

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
