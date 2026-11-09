using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Profile_Utils;
using Newtonsoft.Json.Linq;
using static GameplayModifiers;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class BLAPI : APIHandler
    {
        internal static readonly Throttler Throttle = new(100, 15);

        private BLAPI() { }

        public override string API_HASH => HelpfulPaths.BLAPI_HASH;

        public override async Task<(bool, HttpContent)> CallAPI(
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

        public override float GetStarRating(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            diffData = diffData.TryEnter("difficulty");
            if (speed != SongSpeed.Normal)
                diffData = diffData["modifiersRating"];
            return (float)diffData[HelpfulMisc.AddModifier("stars", speed)] * modMult;
        }

        public override float[] GetRatings(JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1)
        {
            diffData = diffData.TryEnter("difficulty");
            if (speed != SongSpeed.Normal)
                diffData = diffData["modifiersRating"];
            return
            [
                (float)diffData[HelpfulMisc.AddModifier("accRating", speed)] * modMult,
                (float)diffData[HelpfulMisc.AddModifier("passRating", speed)] * modMult,
                (float)diffData[HelpfulMisc.AddModifier("techRating", speed)] * modMult,
            ];
        }

        public override string GetSongName(JToken diffData) => diffData["song"]["name"].ToString();

        public override string GetDiffName(JToken diffData) =>
            (diffData["difficulty"]?["difficultyName"] ?? diffData["difficultyName"]).ToString();

        public override string GetLeaderboardId(JToken diffData) => diffData["id"].ToString();

        public override string GetHash(JToken diffData) => diffData["song"]["hash"].ToString();

        public override int GetMaxScore(JToken diffData) =>
            (int)(diffData["difficulty"]?["maxScore"] ?? diffData["maxScore"]);

        public override async Task<int> GetMaxScore(string hash, int diffNum, string modeName) =>
            GetMaxScore(
                SelectSpecificDiff(
                    JToken.Parse(
                        await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false)
                    ),
                    diffNum,
                    modeName
                )
            );

        public override int GetRank(JToken scoreData) => (int)scoreData["rank"];

        public override string GetPlayerName(JToken scoreData)
        {
            JToken outp = scoreData["player"];
            return outp is JValue ? outp.ToString() : outp["name"].ToString();
        }

        public override float GetAcc(JToken scoreData) => (float)scoreData["accuracy"];

        public override JToken SelectSpecificDiff(JToken diffData, int diffNum, string modeName)
        {
            //Plugin.Log.Info(diffData.ToString());
            //Plugin.Log.Info(diffData["leaderboards"].ToString());
            return diffData["leaderboards"]
                .Children()
                .First(t =>
                    ((int)t["difficulty"]["value"]) == diffNum && ((string)t["difficulty"]["modeName"]).Equals(modeName)
                );
        }

        public override bool MapIsUsable(JToken diffData)
        {
            if (diffData is null)
                return false;
            if (diffData["difficulty"] is not null)
                diffData = diffData["difficulty"];
            return HelpfulMisc.StatusIsUsable((int)diffData["status"]);
        }

        public override bool AreRatingsNull(JToken diffData)
        {
            if (diffData["difficulty"] is not null)
                diffData = diffData["difficulty"];
            return diffData["modifiersRating"] is null || diffData["modifiersRating"].ToString().Length == 0;
        }

        public override async Task<string> GetHashData(string hash, int diffNum) =>
            await CallAPI_String(string.Format(HelpfulPaths.BLAPI_HASH, hash)).ConfigureAwait(false);

        public override async Task<JToken> GetScoreData(
            string userId,
            string hash,
            string diff,
            string mode,
            bool quiet = false,
            CancellationToken ct = default
        )
        {
            string outp = await CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_SCORE, userId, hash, diff, mode),
                    quiet,
                    maxRetries: 1,
                    ct: ct
                )
                .ConfigureAwait(false);
            if (outp is null || outp.Length == 0)
                return null;
            return JToken.Parse(outp);
        }

        public override float GetPP(JToken scoreData) => (float)scoreData["pp"];

        public override int GetScore(JToken scoreData) => (int)scoreData["modifiedScore"];

        public override async Task<ScoregraphInfo[]> GetScoregraph(MapSelection ms, CancellationToken ct = default)
        {
            if (ms.IsUsable)
            {
                ScoregraphInfo Parse1(JToken token)
                {
                    (SongSpeed speed, float modMult) = HelpfulMisc.ParseModifiers(
                        token["modifiers"].ToString(),
                        ms.MapData.diffData
                    );
                    return new ScoregraphInfo(
                        (float)token["accuracy"] / 100f,
                        (float)Math.Round((float)token["pp"], PC.DecimalPrecision),
                        speed,
                        modMult,
                        token["playerName"].ToString().ClampString(PC.MaxNameLength)
                    );
                }

                string data = await CallAPI_String($"leaderboard/{ms.MapData.songId}/scoregraph", ct: ct)
                    .ConfigureAwait(false);
                return [.. JToken.Parse(data).Children().Select(Parse1)];
            }
            else
            {
                string data = await CallAPI_String($"leaderboard/scores/{ms.MapData.songId}?count={PC.MinRank}", ct: ct)
                    .ConfigureAwait(false);
                JToken mapData = ms.MapData.diffData;
                float maxScore = (int)mapData["maxScore"];
                float acc = (float)mapData["accRating"],
                    pass = (float)mapData["passRating"],
                    tech = (float)mapData["techRating"];

                ScoregraphInfo Parse2(JToken token)
                {
                    (SongSpeed speed, float modMult) = HelpfulMisc.ParseModifiers(
                        token["modifiers"].ToString(),
                        ms.MapData.diffData
                    );
                    return new ScoregraphInfo(
                        (float)token["modifiedScore"] / (float)mapData["maxScore"],
                        (float)
                            Math.Round(
                                BLCalc.Instance.Inflate(
                                    BLCalc.Instance.GetSummedPp((int)token["modifiedScore"] / maxScore, acc, pass, tech)
                                ),
                                PC.DecimalPrecision
                            ),
                        speed,
                        modMult,
                        token["playerName"].ToString().ClampString(PC.MaxNameLength)
                    );
                }

                return [.. JToken.Parse(data)["scores"].Children().Select(Parse2)];
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
                            (float)token["score"]["pp"],
                            (uint)token["score"]["rank"]
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

        public override async Task<float> GetProfilePP(string userId) =>
            (float)(
                JToken
                    .Parse(await CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID, userId)).ConfigureAwait(false))
                    ?["pp"]
                ?? 0
            );

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
