using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GameplayModifiers;

namespace BLPPCounter.CalculatorStuffs
{
    public abstract class Calculator
    {
        public static bool UsingDefault = false;
        /// <summary>
        /// How many ratings a leaderboard has. Beatleader would have 3 because of pass, tech, and acc ratings, while Scoresaber would only have 1.
        /// </summary>
        public abstract int RatingCount { get; }
        public abstract bool UsesModifiers { get; }
        public abstract string Label { get; }
        /// <summary>
        /// This is how many ratings to display. If there is more than 1 rating type, the total pp also needs to be displayed, meaning plus 1.
        /// </summary>
        public int DisplayRatingCount => RatingCount > 1 ? RatingCount + 1 : RatingCount;
        /// <summary>
        /// The acc curve for a given leaderboard. Set to internal so that no outside programs modifies the curve.
        /// </summary>
        internal abstract List<(double, double)> PointList { get; }
        /// <summary>
        /// Calculates the pp for given ratings and accuracy.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public abstract float[] GetPp(float acc, params float[] ratings);
        /// <summary>
        /// Calculates the pp for given ratings and accuracy, then rounds them to the number of decimals given.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public float[] GetPp(float acc, int precision, params float[] ratings)
        {
            float[] outp = new float[ratings.Length], pps = GetPp(acc, ratings);
            for (int i = 0; i < ratings.Length; i++)
                outp[i] = (float)Math.Round(pps[i], precision);
            return outp;
        }
        public void SetPp(float acc, float[] ppVals, int offset, params float[] ratings)
        {
            if (ppVals.Length < offset + DisplayRatingCount) throw new IndexOutOfRangeException("Given offset is too far out and goes out of bounds.");
            float[] pps = GetPpWithSummedPp(acc, ratings);
            for (int i = offset; i < offset + DisplayRatingCount; i++)
                ppVals[i] = pps[i - offset];
        }
        public void SetPp(float acc, float[] ppVals, int offset, int precision, params float[] ratings)
        {
            if (ppVals.Length < offset + DisplayRatingCount) throw new IndexOutOfRangeException("Given offset is too far out and goes out of bounds.");
            float[] pps = GetPpWithSummedPp(acc, precision, ratings);
            for (int i = offset; i < offset + DisplayRatingCount; i++)
                ppVals[i] = pps[i - offset];
        }
        /// <summary>
        /// In the order of star, acc, pass, tech
        /// </summary>
        /// <param name="ratings"></param>
        /// <returns></returns>
        public abstract float[] SelectRatings(params float[] ratings); //This should be given in the order of: star, acc, pass, tech
        public float GetSummedPp(float acc, params float[] ratings) => GetPp(acc, ratings).Aggregate(0.0f, (total, current) => total + current);
        /// <summary>
        /// Calculates the pp for given ratings and accuracy, then sums the number and rounds it to the number of decimals given.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <returns>Returns the summed pp for each type of rating. This should be the deflated pp value.</returns>
        public float GetSummedPp(float acc, int precision, params float[] ratings) => (float)Math.Round(GetSummedPp(acc, ratings), precision);
        public float[] GetPpWithSummedPp(float acc, params float[] ratings) =>
            RatingCount == 1 ? GetPp(acc, ratings) : GetPp(acc, ratings).Append(Inflate(GetSummedPp(acc, ratings))).ToArray();
        /// <summary>
        /// Calculates the pp for given ratings and accuracy, then rounds them to the number of decimals given.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <returns>Returns all pp for each type of rating. There is also the summed and inflated pp as the last element in the array.</returns>
        public float[] GetPpWithSummedPp(float acc, int precision, params float[] ratings) =>
            RatingCount == 1 ? GetPp(acc, precision, ratings) : GetPp(acc, precision, ratings).Append((float)Math.Round(Inflate(GetSummedPp(acc, ratings)), precision)).ToArray();
        public abstract float GetAccDeflated(float deflatedPp, int precision = -1, params float[] ratings);
        public abstract float GetAccDeflated(float deflatedPp, JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1.0f, int precision = -1);
        public float GetAcc(float inflatedPp, int precision = -1, params float[] ratings) =>
            GetAccDeflated(Deflate(inflatedPp), precision, ratings);
        public float GetAcc(float inflatedPp, JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1.0f, int precision = -1) =>
            GetAccDeflated(Deflate(inflatedPp), diffData, speed, modMult, precision);
        public abstract float Inflate(float deflatedPp);
        public abstract float Deflate(float inflatedPp);
        public float GetCurve(float acc) => GetCurve(acc, PointList);
        public float InvertCurve(double curveOutput) => GetInvertCurve(curveOutput, PointList);
        public float CurveDerivative(float acc) => GetCurveDerivative(acc, PointList);
        #region Static Methods
        public static Calculator GetCalc(bool useDefault = false) => GetCalc(!useDefault ? PluginConfig.Instance.Leaderboard : PluginConfig.Instance.DefaultLeaderboard);
        public static Calculator GetSelectedCalc() => GetCalc(UsingDefault);
        private static Calculator GetCalc(Leaderboards leaderboard)
        {
            switch(leaderboard)
            {
                case Leaderboards.Beatleader:
                    return BLCalc.Instance;
                case Leaderboards.Scoresaber:
                    return SSCalc.Instance;
                case Leaderboards.Accsaber:
                    return APCalc.Instance;
                default:
                    return null;
            }
        }
        public static float GetCurve(float acc, List<(double, double)> curve)
        {
            int i = 1;
            while (i < curve.Count && curve[i].Item1 > acc) i++;
            double middle_dis = (acc - curve[i - 1].Item1) / (curve[i].Item1 - curve[i - 1].Item1);
            return (float)(curve[i - 1].Item2 + middle_dis * (curve[i].Item2 - curve[i - 1].Item2));
        }
        public static float GetInvertCurve(double curveOutput, List<(double, double)> curve)
        {
            int i = 1;
            while (i < curve.Count && curve[i].Item2 > curveOutput) i++;
            double middle_dis = (curveOutput - curve[i - 1].Item2) / (curve[i].Item2 - curve[i - 1].Item2);
            return (float)(curve[i - 1].Item1 + middle_dis * (curve[i].Item1 - curve[i - 1].Item1));
        }
        public static float GetCurveDerivative(double acc, List<(double, double)> curve)
        {
            int i = 1;
            while (i < curve.Count && curve[i].Item1 > acc) i++;
            return (float)((curve[i].Item2 - curve[i - 1].Item2) / (curve[i].Item1 - curve[i - 1].Item1));
        }
        #endregion
    }
}
