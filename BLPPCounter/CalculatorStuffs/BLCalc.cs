using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Misc_Classes;
using ModestTree;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static GameplayModifiers;

namespace BLPPCounter.CalculatorStuffs
{
    /* This is all ripped from the beatleader github and changed to work with my stuffs.*/
    public class BLCalc : Calculator
    {
        protected internal override List<(float accuracy, float points)> PointList { get; protected set; } = [];
        private readonly ObjectCacher<
            (string leaderboardId, SongSpeed speed),
            List<(float accuracy, float points)>
        > PointListCache = new(TimeSpan.FromMinutes(30));
        private string CurrentLeaderboardId = "";
        private SongSpeed? CurrentSongSpeed;
        private const float CLANWAR_WEIGHT_COEFFICIENT = 0.8f;

        internal BLCalc() { }

        #region PP Math
        public override Leaderboards Leaderboard { get; } = Leaderboards.Beatleader;
        public override int RatingCount { get; } = 3;
        public override string Label { get; } = "BL";
        public override string[] StarLabels { get; } =
        [
            "<color=blue>Acc</color> Stars",
            "<color=red>Pass</color> Stars",
            "<color=green>Tech</color> Stars",
            "<color=yellow>Total</color> Stars",
        ];
        public override bool UsesModifiers { get; } = true;

        public override float[] GetPp(float acc, params float[] ratings)
        {
            if (PointList.Count == 0)
                return [-1f, 0f, 0f];

            if (float.IsNaN(acc))
                acc = 0;
            else
                acc = Mathf.Clamp01(acc);

            return [GetCurve(acc), 0, 0];
        }

        public override float[] SelectRatings(float[] ratings)
        {
            return [.. ratings.Skip(1)];
        }

        public override float GetAccDeflated(
            float deflatedPp,
            ICacheModel diffData,
            SongSpeed speed = SongSpeed.Normal,
            float modMult = 1.0f,
            int precision = -1
        )
        {
            return GetAccDeflated(deflatedPp, precision);
        }

        protected override float GetAccDeflatedInternal(float deflatedPp, float[] ratings)
        {
            return GetInvertCurve(deflatedPp, PointList);
        }

        internal async Task<bool> SetCurve(
            string leaderboardId,
            SongSpeed songSpeed,
            ILeaderboardApiProvider apiProvider
        )
        {
            return await SetCurve(leaderboardId, songSpeed, apiProvider.BeatLeader);
        }

        internal async Task<bool> SetCurve(string leaderboardId, SongSpeed songSpeed, APIHandler apiHandler)
        {
            bool sameLeaderboard = CurrentLeaderboardId.Equals(leaderboardId);
            bool sameSpeed = songSpeed.Equals(CurrentSongSpeed);

            if (sameLeaderboard && sameSpeed)
                return true;

            if (
                PointListCache.TryGetCachedItem(
                    (leaderboardId, songSpeed),
                    out List<(float accuracy, float points)>? pointList
                )
            )
            {
                PointList = pointList;

                CurrentLeaderboardId = leaderboardId;
                CurrentSongSpeed = songSpeed;

                return true;
            }

            string mod = songSpeed switch
            {
                SongSpeed.Slower => "SS",
                SongSpeed.Faster => "FS",
                SongSpeed.SuperFast => "SF",
                _ => "",
            };

            System.Net.Http.HttpContent? content = await apiHandler
                .CallAPI_Static(
                    string.Format(
                        $"https://api.beatleader.com/pp/curve?leaderboardId={{0}}{(BetterString.IsNullOrEmpty(mod) ? "" : "&modifiers=")}{{1}}",
                        leaderboardId,
                        mod
                    )
                )
                .ConfigureAwait(false);

            if (content is null)
                return false;

            string contentStr = await content.ReadAsStringAsync();

            IEnumerable<(float, float)>? newPoints = JObject
                .Parse(contentStr)["points"]
                ?.Children()
                .Select(arr => ((float)(arr[0] ?? 0f), (float)(arr[1] ?? 0f)));

            if (newPoints is null || newPoints.IsEmpty())
                return false;

            CurrentLeaderboardId = leaderboardId;
            CurrentSongSpeed = songSpeed;

            List<(float, float)> points = [with(newPoints)];
            points.Reverse();

            Plugin.Log.Info($"points = {points.Count}");

            PointListCache.CacheItem((leaderboardId, songSpeed), points);
            PointList = points;

            return true;
        }
        #endregion
        #region Clan Math
        private float TotalPP(float coefficient, float[] ppVals, int startIndex)
        {
            if (ppVals.Length == 0)
                return 0.0f;
            float currentWeight = (float)Math.Pow(coefficient, startIndex);
            return ppVals.Aggregate(
                0.0f,
                (a, b) =>
                {
                    float n = a + (currentWeight * b);
                    currentWeight *= coefficient;
                    return n;
                }
            );
        }

        private float CalcFinalPP(float coefficient, float[] bottomPp, int index, float expected)
        {
            float oldPp = TotalPP(coefficient, bottomPp, index);
            float newPp = TotalPP(coefficient, bottomPp, index + 1);
            return (expected + oldPp - newPp) / (float)Math.Pow(coefficient, index);
        }

        public float GetNeededPP(float[] clanPpVals, float ppDiff)
        {
            float coefficient = CLANWAR_WEIGHT_COEFFICIENT;
            for (int i = clanPpVals.Length - 1; i >= 0; i--)
            {
                float[] bottomData = [.. clanPpVals.Skip(i)];
                float bottomPp = TotalPP(coefficient, bottomData, i);
                bottomData = [.. bottomData.Prepend(clanPpVals[i])];
                float modifiedBottomPp = TotalPP(coefficient, bottomData, i);
                float diff = modifiedBottomPp - bottomPp;
                if (diff > ppDiff)
                    return CalcFinalPP(coefficient, [.. clanPpVals.Skip(i + 1)], i + 1, ppDiff);
            }
            return CalcFinalPP(coefficient, clanPpVals, 0, ppDiff);
        }

        public float GetNeededPlay(List<float> clanPpVals, float otherClan, float playerPp)
        {
            float[] clone = [.. clanPpVals];
            clanPpVals.Remove(playerPp);
            float ourClan = TotalPP(CLANWAR_WEIGHT_COEFFICIENT, [.. clanPpVals], 0);
            if (otherClan - TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clone, 0) <= 0)
                return 0.0f;
            float result = GetNeededPP([.. clanPpVals], otherClan - ourClan);
            return result;
        }

        public float GetWeight(float pp, float[] sortedClanPps, out int rank)
        {
            rank = 0;
            if (sortedClanPps is null || sortedClanPps.Length == 0)
                return 1.0f;
            int count = 0;
            float outp = 1.0f;
            while (sortedClanPps[count++] > pp && count < sortedClanPps.Length)
                outp *= CLANWAR_WEIGHT_COEFFICIENT;
            rank = count;
            return outp;
        }

        public float GetWeight(float pp, float[] sortedClanPps)
        {
            return GetWeight(pp, sortedClanPps, out _);
        }

        public float GetWeightedPp(float pp, float[] clanPps)
        {
            return pp * GetWeight(pp, clanPps);
        }
        #endregion
    }
}
