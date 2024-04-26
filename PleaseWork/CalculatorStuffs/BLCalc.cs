using System;
using System.Collections.Generic;

namespace PleaseWork.CalculatorStuffs
{
    /* This is all ripped from the beatleader github and changed to work with my stuffs.*/
    public static class BLCalc
    {
        static List<(double, double)> pointList2 = new List<(double, double)> {
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
                (0.0, 0.000), };
        public static (float, float, float) GetPp(float accuracy, float accRating, float passRating, float techRating)
        {
            float passPP = 15.2f * (float)Math.Exp(Math.Pow(passRating, 1 / 2.62f)) - 30f;
            if (float.IsNaN(accuracy) || float.IsNegativeInfinity(accuracy))
                accuracy = 0;
            if (float.IsInfinity(accuracy))
                accuracy = 1;
            accuracy = (float)Math.Max(0,Math.Min(1,accuracy));
            if (float.IsInfinity(passPP) || float.IsNaN(passPP) || float.IsNegativeInfinity(passPP) || passPP < 0)
            {
                passPP = 0;
            }
            //float accPP = context == LeaderboardContexts.Golf ? accuracy * accRating * 42f : Curve2(accuracy) * accRating * 34f;
            float accPP = Curve2(accuracy) * accRating * 34f;
            float techPP = (float)Math.Exp(1.9f * accuracy) * 1.08f * techRating;

            return (passPP, accPP, techPP);
        }
        public static float GetStraightPP(float accuracy, float accRating, float passRating, float techRating)
        {
            float p, a, t;
            (p,a,t) = GetPp(accuracy, accRating, passRating, techRating);
            return Inflate(p + a + t);
        }
        public static float Inflate(float peepee)
        {
            return (650f * (float)Math.Pow(peepee, 1.3f)) / (float)Math.Pow(650f, 1.3f);
        }
        public static float Curve2(float acc)
        {
            int i = 0;
            for (; i < pointList2.Count; i++)
            {
                if (pointList2[i].Item1 <= acc)
                {
                    break;
                }
            }

            if (i == 0)
            {
                i = 1;
            }

            double middle_dis = (acc - pointList2[i - 1].Item1) / (pointList2[i].Item1 - pointList2[i - 1].Item1);
            return (float)(pointList2[i - 1].Item2 + middle_dis * (pointList2[i].Item2 - pointList2[i - 1].Item2));
        }
        #region ReplayMath
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
    }
}
