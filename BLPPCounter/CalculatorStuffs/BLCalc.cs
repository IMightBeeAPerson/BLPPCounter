using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GameplayModifiers;
using System.Threading.Tasks;

namespace BLPPCounter.CalculatorStuffs
{
    /* This is all ripped from the beatleader github and changed to work with my stuffs.*/
    public class BLCalc: Calculator
    {
        internal override List<(float, float)> PointList { get; } = [];
        private readonly float CLANWAR_WEIGHT_COEFFICIENT = 0.8f;
        public static BLCalc Instance { get; private set; } = new BLCalc();
        private BLCalc() { }
        #region PP Math
        public override Leaderboards Leaderboard => Leaderboards.Beatleader;
        public override int RatingCount => 3;
        public override string Label => "BL";
        public override string[] StarLabels { get; } = ["<color=blue>Acc</color> Stars","<color=red>Pass</color> Stars","<color=green>Tech</color> Stars","<color=yellow>Total</color> Stars"];
        public override bool UsesModifiers => true;
        public float GetPassPp(float passRating) => 0f;
        public float GetAccPp(float acc) => GetCurve(acc);
        public float GetTechPp(float acc, float techRating) => 0f;
        public override float[] GetPp(float acc, params float[] ratings) //ratings order: acc pass tech
        {
            if (float.IsNaN(acc)) acc = 0;
            else acc = Mathf.Max(0, Mathf.Min(1, acc));
            return [GetCurve(acc), 0, 0];
        }
        public override float[] SelectRatings(float[] ratings) => [.. ratings.Skip(1)];
        public override float GetAccDeflated(float deflatedPp, int precision = -1, params float[] ratings) //ratings order: acc pass tech
        {
            if (deflatedPp > GetSummedPp(1.0f, ratings) || ratings is null || ratings.Length < 3) return precision >= 0 ? 100.0f : 1.0f;
            var (accRating, passRating, techRating) = (ratings[0], ratings[1], ratings[2]);
            deflatedPp -= GetPassPp(passRating);
            if (deflatedPp <= 0.0f) return 0.0f;
            float outp = CalculateX(deflatedPp, techRating, accRating);
            return precision >= 0 ? (float)Math.Round(outp * 100.0f, precision) : outp;
        }
        public override float GetAccDeflated(float deflatedPp, JToken diffData, SongSpeed speed, float modMult = 1.0f, int precision = -1)
        {
            float outp = GetAccDeflated(deflatedPp, precision, BLAPI.Instance.GetRatings(diffData, speed, modMult));
            return precision >= 0 ? (float)Math.Round(outp * 100.0f, precision) : outp;
        }
        public float GetAccDeflatedUnsafe(float deflatedPp, int precision, float[] ratings, float initGuess = 1.0f, int maxIterations = 100)
        {
            var (accRating, passRating, techRating) = (ratings[0], ratings[1], ratings[2]);
            deflatedPp -= GetPassPp(passRating);
            return (float)Math.Round(CalculateX(deflatedPp, techRating, accRating, initGuess, maxIterations: maxIterations) * 100.0f, precision);
        }
        public override float Inflate(float pp) => pp;
        public override float Deflate(float pp) => pp;

        public async Task SetCurve(string leaderboardId, SongSpeed songSpeed)
        {
            string mod = songSpeed switch
            {
                SongSpeed.Slower => "SS",
                SongSpeed.Faster => "FS",
                SongSpeed.SuperFast => "SF",
                _ => ""
            };

            var (Success, Content) = await APIHandler.CallAPI_Static(string.Format(
                $"https://api.beatleader.com/pp/curve?leaderboardId={{0}}{(string.IsNullOrEmpty(mod) ? "" : "&modifiers=")}{{1}}", leaderboardId, mod));

            if (!Success)
                return;

            IEnumerable<(float, float)> newPoints = JObject.Parse(await Content.ReadAsStringAsync())["points"].Children().Select(arr => ((float)arr[0], (float)arr[1]));

            PointList.Clear();
            PointList.AddRange(newPoints);
            PointList.Reverse();
        }

        private float CalculateX(float y, float t, float a, float initialGuess = 1.0f, float tolerance = 0.0001f, int maxIterations = 100)
        {
            float x = initialGuess;
            double error = 1.0;
            int iterations = 0;

            // Iterative method (Newton's Method or other root-finding techniques)
            while (error > tolerance && iterations < maxIterations)
            {
                // Compute the current value of the equation
                double currentValue = 1.08 * t * Math.Exp(1.9 * x) + 34 * a * GetCurve(x);
                double difference = currentValue - y;

                // Adjust x using a basic Newton's method approach
                double derivative = 1.08 * t * Math.Exp(1.9 * x) + 34 * a * CurveDerivative(x);
                x -= (float)(difference / derivative);
                if (x < 0) x = Math.Abs(x); // Ensure x stays within valid bounds

                // Calculate the error
                error = Math.Abs(difference);
                iterations++;
            }
            return x;
        }//Yes this is chatGPT code, modified to work properly with my code.
        #endregion
        #region Clan Math
        private float TotalPP(float coefficient, float[] ppVals, int startIndex)
        {
            if (ppVals.Length == 0) return 0.0f;
            float currentWeight = (float)Math.Pow(coefficient, startIndex);
            return ppVals.Aggregate(0.0f, (a, b) => {  var n = a + currentWeight * b; currentWeight *= coefficient; return n; });
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
                if (diff > ppDiff) return CalcFinalPP(coefficient, [.. clanPpVals.Skip(i + 1)], i + 1, ppDiff);
            }
            return CalcFinalPP(coefficient, clanPpVals, 0, ppDiff);
        }
        public float GetNeededPlay(List<float> clanPpVals, float otherClan, float playerPp)
        {
            float[] clone = [.. clanPpVals];
            clanPpVals.Remove(playerPp);
            float ourClan = TotalPP(CLANWAR_WEIGHT_COEFFICIENT, [.. clanPpVals], 0);
            if (otherClan - TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clone, 0) <= 0) return 0.0f;
            float result = GetNeededPP([.. clanPpVals], otherClan - ourClan);
            return result;
        }
        public float GetWeight(float pp, float[] sortedClanPps, out int rank)
        {
            rank = 0;
            if (sortedClanPps is null || sortedClanPps.Length == 0) return 1.0f;
            int count = 0;
            float outp = 1.0f;
            while (sortedClanPps[count++] > pp && count < sortedClanPps.Length)
                outp *= CLANWAR_WEIGHT_COEFFICIENT;
            rank = count;
            return outp;
        }
        public float GetWeight(float pp, float[] sortedClanPps) => GetWeight(pp, sortedClanPps, out _);
        public float GetWeightedPp(float pp, float[] clanPps)
        {
            return pp * GetWeight(pp, clanPps);
        }
        #endregion
    }
}
