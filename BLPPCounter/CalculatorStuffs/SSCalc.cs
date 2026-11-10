using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.CalculatorStuffs
{
    public class SSCalc : Calculator
    {
        private static readonly float SecretMultiplier = 42.117208413f;
        internal override List<(float, float)> PointList { get; } =
        [
            (0, 0),
            (0.6f, 0.182232335f),
            (0.65f, 0.586601f),
            (0.7f, 0.6125566f),
            (0.75f, 0.6451808f),
            (0.8f, 0.6872269f),
            (0.825f, 0.7150466f),
            (0.85f, 0.746229053f),
            (0.875f, 0.781693459f),
            (0.9f, 0.825756133f),
            (0.91f, 0.8488376f),
            (0.92f, 0.872871041f),
            (0.93f, 0.9039994f),
            (0.94f, 0.9417363f),
            (0.95f, 1),
            (0.955f, 1.0388633f),
            (0.96f, 1.08718836f),
            (0.965f, 1.155212f),
            (0.97f, 1.24858081f),
            (0.9725f, 1.30903327f),
            (0.975f, 1.38071024f),
            (0.9775f, 1.46647263f),
            (0.98f, 1.570241f),
            (0.9825f, 1.69753623f),
            (0.985f, 1.85638881f),
            (0.9875f, 2.058947f),
            (0.99f, 2.32450628f),
            (0.99125f, 2.49029064f),
            (0.9925f, 2.68566775f),
            (0.99375f, 2.91901565f),
            (0.995f, 3.20220184f),
            (0.99625f, 3.55261445f),
            (0.9975f, 3.99679351f),
            (0.99825f, 4.32502747f),
            (0.999f, 4.715471f),
            (0.9995f, 5.01954365f),
            (1, 5.36739445f),
        ];
        public override Leaderboards Leaderboard => Leaderboards.Scoresaber;
        public override int RatingCount => 1;
        public override string Label => "SS";
        public override string[] StarLabels { get; } = ["<color=yellow>Stars</color>"];
        public override bool UsesModifiers => false;

        internal SSCalc()
        {
            PointList.Reverse();
        }

        public override float[] GetPp(float acc, params float[] ratings)
        {
            return [SecretMultiplier * GetCurve(acc) * ratings[0]];
        }

        public override float[] SelectRatings(float[] ratings)
        {
            return [.. ratings.Take(1)];
        }

        public override float GetAccDeflated(float deflatedPp, int precision = -1, params float[] ratings)
        {
            if (deflatedPp > GetSummedPp(1.0f, ratings))
                return precision < 0 ? 1.0f : 100.0f;
            float outp = InvertCurve(deflatedPp / (SecretMultiplier * ratings[0]));
            return precision < 0 ? outp : (float)Math.Round(outp * 100.0f, precision);
        }

        public override float GetAccDeflated(
            float deflatedPp,
            ICacheModel diffData,
            GameplayModifiers.SongSpeed speed = GameplayModifiers.SongSpeed.Normal,
            float modMult = 1,
            int precision = -1
        )
        {
            if (diffData is not TaohCache taohCache)
                throw new ArgumentNullException(nameof(diffData));

            float starRating = taohCache.StarScoreSaber ?? 0f;

            return GetAccDeflated(deflatedPp, precision, starRating * modMult);
        }

        public override float Inflate(float deflatedPp)
        {
            return deflatedPp; //There is no inflation in SS
        }

        public override float Deflate(float inflatedPp)
        {
            return inflatedPp; //There is no deflation in SS
        }
    }
}
