using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GameplayModifiers;
using static NoteData;
using static ScoreModel;

namespace BLPPCounter.CalculatorStuffs
{
    /* This is all ripped from the beatleader github and changed to work with my stuffs.*/
    public class BLCalc: Calculator
    {
        internal override List<(double, double)> PointList { get; } = new List<(double, double)> {
                (1.0, 7.424),
                (0.999, 6.241),
                (0.9975, 5.158),
                (0.995, 4.010),
                (0.9925, 3.241),
                (0.99, 2.700),
                (0.9875, 2.303),
                (0.985, 2.007),
                (0.9825, 1.786),
                (0.98, 1.618),
                (0.9775, 1.490),
                (0.975, 1.392),
                (0.9725, 1.315),
                (0.97, 1.256),
                (0.965, 1.167),
                (0.96, 1.094),
                (0.955, 1.039),
                (0.95, 1.000),
                (0.94, 0.931),
                (0.93, 0.867),
                (0.92, 0.813),
                (0.91, 0.768),
                (0.9, 0.729),
                (0.875, 0.650),
                (0.85, 0.581),
                (0.825, 0.522),
                (0.8, 0.473),
                (0.75, 0.404),
                (0.7, 0.345),
                (0.65, 0.296),
                (0.6, 0.256),
                (0.0, 0.000)
        };
        private readonly float CLANWAR_WEIGHT_COEFFICIENT = 0.8f;
        public static BLCalc Instance { get; private set; } = new BLCalc();
        private BLCalc() { }
        #region PP Math
        public override Leaderboards Leaderboard => Leaderboards.Beatleader;
        public override int RatingCount => 3;
        public override string Label => "BL";
        public override string[] StarLabels { get; } = new string[4] 
        { "<color=blue>Acc</color> Stars","<color=red>Pass</color> Stars","<color=green>Tech</color> Stars","<color=yellow>Total</color> Stars" };
        public override bool UsesModifiers => true;
        public float GetPassPp(float passRating) => 15.2f * Mathf.Exp(Mathf.Pow(passRating, 1 / 2.62f)) - 30f;
        public float GetAccPp(float acc, float accRating) => GetCurve(acc) * accRating * 34f;
        public float GetTechPp(float acc, float techRating) => Mathf.Exp(1.9f * acc) * 1.08f * techRating;
        public override float[] GetPp(float acc, params float[] ratings) //ratings order: acc pass tech
        {
            if (float.IsNaN(acc)) acc = 0;
            else acc = Mathf.Max(0, Mathf.Min(1, acc));
            var (accRating, passRating, techRating) = (ratings[0], ratings[1], ratings[2]);
            float passPP = GetPassPp(passRating);
            if (float.IsInfinity(passPP) || float.IsNaN(passPP) || passPP < 0) passPP = 0;
            return new float[] { GetAccPp(acc, accRating), passPP, GetTechPp(acc, techRating) };
            //Plugin.Log.Info($"acc = {acc}, ratings = {HelpfulMisc.Print(ratings)}, pp = {HelpfulMisc.Print(outp)}, total pp = {Inflate(outp[0] + outp[1] + outp[2])}");
            //return outp;
        }
        public override float[] SelectRatings(float[] ratings) => ratings.Skip(1).ToArray();
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
        public override float Inflate(float pp) => 650f * (float)Math.Pow(pp, 1.3f) / (float)Math.Pow(650f, 1.3f);
        public override float Deflate(float pp) => (float)Math.Pow(pp * (float)Math.Pow(650f, 1.3f) / 650f, 1.0f / 1.3f);
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
                float[] bottomData = clanPpVals.Skip(i).ToArray();
                float bottomPp = TotalPP(coefficient, bottomData, i);
                bottomData = bottomData.Prepend(clanPpVals[i]).ToArray();
                float modifiedBottomPp = TotalPP(coefficient, bottomData, i);
                float diff = modifiedBottomPp - bottomPp;
                if (diff > ppDiff) return CalcFinalPP(coefficient, clanPpVals.Skip(i + 1).ToArray(), i + 1, ppDiff);
            }
            return CalcFinalPP(coefficient, clanPpVals, 0, ppDiff);
        }
        public float GetNeededPlay(List<float> clanPpVals, float otherClan, float playerPp)
        {
            float[] clone = clanPpVals.ToArray();
            clanPpVals.Remove(playerPp);
            float ourClan = TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clanPpVals.ToArray(), 0);
            if (otherClan - TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clone, 0) <= 0) return 0.0f;
            float result = GetNeededPP(clanPpVals.ToArray(), otherClan - ourClan);
            return result;
        }
        public float GetWeight(float pp, float[] sortedClanPps, out int rank)
        {
            rank = 0;
            if (sortedClanPps == null || sortedClanPps.Length == 0) return 1.0f;
            int count = 0;
            while (sortedClanPps[count++] > pp && count < sortedClanPps.Length);
            rank = count;
            return (float)Math.Pow(CLANWAR_WEIGHT_COEFFICIENT, count - 1);
        }
        public float GetWeight(float pp, float[] sortedClanPps) => GetWeight(pp, sortedClanPps, out _);
        public float GetWeightedPp(float pp, float[] clanPps)
        {
            return pp * GetWeight(pp, clanPps);
        }
        #endregion
    }
}
