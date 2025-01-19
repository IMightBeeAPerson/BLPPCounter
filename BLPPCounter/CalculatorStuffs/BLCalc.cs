using BeatmapSaveDataVersion4;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings;
using BLPPCounter.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;
using static AlphabetScrollInfo;

namespace BLPPCounter.CalculatorStuffs
{
    /* This is all ripped from the beatleader github and changed to work with my stuffs.*/
    public static class BLCalc
    {
        
        private static List<(double, double)> pointList2 = new List<(double, double)> {
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
        private static readonly float CLANWAR_WEIGHT_COEFFICIENT = 0.8f;
        #region PP Math
        public static float GetPassPp(float passRating) => 15.2f * Mathf.Exp(Mathf.Pow(passRating, 1 / 2.62f)) - 30f;
        public static float GetAccPp(float acc, float accRating) => Curve2(acc) * accRating * 34f;
        public static float GetTechPp(float acc, float techRating) => Mathf.Exp(1.9f * acc) * 1.08f * techRating;
        public static (float Pass, float Acc, float Tech) GetPp(float accuracy, float accRating, float passRating, float techRating)
        {
            float passPP = GetPassPp(passRating);
            if (float.IsNaN(accuracy)) accuracy = 0;
            else accuracy = Mathf.Max(0, Mathf.Min(1,accuracy));
            if (float.IsInfinity(passPP) || float.IsNaN(passPP) || passPP < 0) passPP = 0;
            //float accPP = context == LeaderboardContexts.Golf ? accuracy * accRating * 42f : Curve2(accuracy) * accRating * 34f;
            return (passPP, GetAccPp(accuracy, accRating), GetTechPp(accuracy, techRating));
        }
        public static (float Pass, float Acc, float Tech) GetPp(float accuracy, float accRating, float passRating, float techRating, int precision)
        {
            var (Pass, Acc, Tech) = GetPp(accuracy, accRating, passRating, techRating);
            return ((float)Math.Round(Pass, precision), (float)Math.Round(Acc, precision), (float)Math.Round(Tech, precision));
        }
        public static (float Pass, float Acc, float Tech, float Total) GetSummedPp(float accuracy, float accRating, float passRating, float techRating)
        {
            var (Pass, Acc, Tech) = GetPp(accuracy, accRating, passRating, techRating);
            return (Pass, Acc, Tech, Inflate(Pass + Acc + Tech));
        }
        public static (float Pass, float Acc, float Tech, float Total) GetSummedPp(float accuracy, float accRating, float passRating, float techRating, int precision)
        {
            var (Pass, Acc, Tech) = GetPp(accuracy, accRating, passRating, techRating, precision);
            return (Pass, Acc, Tech, (float)Math.Round(Inflate(Pass + Acc + Tech), precision));
        }
        public static float GetPpSum(float accuracy, float accRating, float passRating, float techRating)
        {
            float a, b, c;
            (a,b,c) = GetPp(accuracy, accRating, passRating, techRating);
            return a + b + c;
        }
        public static float GetPartPpSum(float accuracy, float accRating = -1, float passRating = -1, float techRating = -1)
        {
            float sum = 0;
            if (accRating > -1) sum += GetAccPp(accuracy, accRating);
            if (passRating > -1) sum += GetPassPp(passRating);
            if (techRating > -1) sum += GetTechPp(accuracy, techRating);
            return sum;
        }
        public static float GetPpSum(float accuracy, MapSelection map) => GetPpSum(accuracy, map.AccRating, map.PassRating, map.TechRating);
        public static float GetAcc(float accRating, float passRating, float techRating, float inflatedPp, int precision = -1) =>
            GetAccDeflated(accRating, passRating, techRating, Deflate(inflatedPp), precision);
        public static float GetAcc((float acc, float pass, float tech) ratings, float inflatedPp, int precision = -1) =>
            GetAccDeflated(ratings.acc, ratings.pass, ratings.tech, Deflate(inflatedPp), precision);
        public static float GetAcc(float inflatedPp, JToken diffData, GameplayModifiers.SongSpeed speed, float modMult = 1.0f, int precision = -1) =>
            GetAccDeflated(Deflate(inflatedPp), diffData, speed, modMult, precision);
        public static float GetAccDeflated(float accRating, float passRating, float techRating, float deflatedPp, int precision = -1)
        {
            if (deflatedPp > GetPpSum(1.0f, accRating, passRating, techRating)) return precision >= 0 ? 100.0f : 1.0f;
            deflatedPp -= GetPassPp(passRating);
            if (deflatedPp <= 0.0f) return 0.0f;
            float outp = CalculateX(deflatedPp, techRating, accRating);
            return precision >= 0 ? (float)Math.Round(outp * 100.0f, precision) : outp;
        }
        public static float GetAccDeflated((float acc, float pass, float tech) ratings, float deflatedPp, int precision = -1) =>
            GetAccDeflated(ratings.acc, ratings.pass, ratings.tech, deflatedPp, precision);
        public static float GetAccDeflated(float deflatedPp, JToken diffData, GameplayModifiers.SongSpeed speed, float modMult = 1.0f, int precision = -1)
        {
            float outp = GetAccDeflated(HelpfulMisc.GetRatings(diffData, speed, modMult), deflatedPp);
            return precision >= 0 ? (float)Math.Round(outp * 100.0f, precision) : outp;
        }
        public static float Inflate(float peepee)
        {
            return 650f * (float)Math.Pow(peepee, 1.3f) / (float)Math.Pow(650f, 1.3f);
        }
        public static float Deflate(float pp)
        {
            return (float)Math.Pow(pp * (float)Math.Pow(650f, 1.3f) / 650f, 1.0f / 1.3f);
        }
        public static float Curve2(float acc)
        {
            int i = 1;
            while (i < pointList2.Count && pointList2[i].Item1 > acc) i++;
            double middle_dis = (acc - pointList2[i - 1].Item1) / (pointList2[i].Item1 - pointList2[i - 1].Item1);
            return (float)(pointList2[i - 1].Item2 + middle_dis * (pointList2[i].Item2 - pointList2[i - 1].Item2));
        }
        public static float InvertCurve2(double curve2Output)
        {
            int i = 1;
            while (i < pointList2.Count && pointList2[i].Item2 > curve2Output) i++;
            double middle_dis = (curve2Output - pointList2[i - 1].Item2) / (pointList2[i].Item2 - pointList2[i - 1].Item2);
            return (float)(pointList2[i - 1].Item1 + middle_dis * (pointList2[i].Item1 - pointList2[i - 1].Item1));
        }
        public static float Curve2Derivative(double acc)
        {
            int i = 1;
            while (i < pointList2.Count && pointList2[i].Item1 > acc) i++;
            return (float)((pointList2[i].Item2 - pointList2[i - 1].Item2) / (pointList2[i].Item1 - pointList2[i - 1].Item1));
        }
        private static float CalculateX(float y, float t, float a, float initialGuess = 1.0f, float tolerance = 0.0001f, int maxIterations = 100)
        {
            float x = initialGuess;
            double error = 1.0;
            int iterations = 0;

            // Iterative method (Newton's Method or other root-finding techniques)
            while (error > tolerance && iterations < maxIterations)
            {
                // Compute the current value of the equation
                double currentValue = 1.08 * t * Math.Exp(1.9 * x) + 34 * a * Curve2(x);
                double difference = currentValue - y;

                // Adjust x using a basic Newton's method approach
                double derivative = 1.08 * t * Math.Exp(1.9 * x) + 34 * a * Curve2Derivative(x);
                x -= (float)(difference / derivative);

                // Calculate the error
                error = Math.Abs(difference);
                iterations++;
            }
            //Plugin.Log.Info("THE ACC: " + x * 100.0f);
            return x;
        }//Yes this is chatGPT code, modified to work properly with my code.
        #endregion
        #region Replay Math
        private const float MinBeforeCutScore = 0.0f;
        private const float MinAfterCutScore = 0.0f;
        private const float MaxBeforeCutScore = 70.0f;
        private const float MaxAfterCutScore = 30.0f;
        private const float MaxCenterDistanceCutScore = 15.0f;
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static int GetCutDistanceScore(float cutDistanceToCenter)
        {
            return RoundToInt(MaxCenterDistanceCutScore * (1f - Clamp01(cutDistanceToCenter / 0.3f)));
        }

        public static int GetBeforeCutScore(float beforeCutRating)
        {
            var rating = Clamp01(beforeCutRating);
            return RoundToInt(LerpUnclamped(MinBeforeCutScore, MaxBeforeCutScore, rating));
        }

        public static int GetAfterCutScore(float afterCutRating)
        {
            var rating = Clamp01(afterCutRating);
            return RoundToInt(LerpUnclamped(MinAfterCutScore, MaxAfterCutScore, rating));
        }
        public static int GetCutScore(BeatLeader.Models.Replay.NoteCutInfo info)
        {
            return GetBeforeCutScore(info.beforeCutRating) + GetCutDistanceScore(info.cutDistanceToCenter) + GetAfterCutScore(info.afterCutRating);
        }
        public static float Clamp01(float value)
        {
            if (value < 0F)
                return 0F;
            else if (value > 1F)
                return 1F;
            else
                return value;
        }
        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
        #endregion
        #region Clan Math
        private static float TotalPP(float coefficient, float[] ppVals, int startIndex)
        {
            if (ppVals.Length == 0) return 0.0f;
            float currentWeight = (float)Math.Pow(coefficient, startIndex);
            return ppVals.Aggregate(0.0f, (a, b) => {  var n = a + currentWeight * b; currentWeight *= coefficient; return n; });
        }
        private static float CalcFinalPP(float coefficient, float[] bottomPp, int index, float expected)
        {
            float oldPp = TotalPP(coefficient, bottomPp, index);
            float newPp = TotalPP(coefficient, bottomPp, index + 1);
            return (expected + oldPp - newPp) / (float)Math.Pow(coefficient, index);
        }
        public static float GetNeededPP(float[] clanPpVals, float ppDiff)
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
        public static float GetNeededPlay(List<float> clanPpVals, float otherClan, float playerPp)
        {
            float[] clone = clanPpVals.ToArray();
            clanPpVals.Remove(playerPp);
            float ourClan = TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clanPpVals.ToArray(), 0);
            if (otherClan - TotalPP(CLANWAR_WEIGHT_COEFFICIENT, clone, 0) <= 0) return 0.0f;
            float result = GetNeededPP(clanPpVals.ToArray(), otherClan - ourClan);
            return result;
        }
        public static float GetWeight(float pp, float[] sortedClanPps, out int rank)
        {
            rank = 0;
            if (sortedClanPps == null || sortedClanPps.Length == 0) return 1.0f;
            int count = 0;
            while (sortedClanPps[count++] > pp && count < sortedClanPps.Length);
            rank = count;
            return (float)Math.Pow(CLANWAR_WEIGHT_COEFFICIENT, count - 1);
        }
        public static float GetWeight(float pp, float[] sortedClanPps) => GetWeight(pp, sortedClanPps, out _);
        public static float GetWeightedPp(float pp, float[] clanPps)
        {
            return pp * GetWeight(pp, clanPps);
        }
        #endregion
    }
}
