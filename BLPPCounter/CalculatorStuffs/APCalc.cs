using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Enums;

namespace BLPPCounter.CalculatorStuffs
{
    internal class APCalc : Calculator
    {
        internal override List<(float, float)> PointList { get; } =
        [
            (0, 0),
            (0.9409324581850277f, 0.22864617746193472f),
            (0.9421364984358537f, 0.2344589535514457f),
            (0.943340858876901f, 0.24028320272237846f),
            (0.9445528451823f, 0.24615905401143912f),
            (0.9457521950057954f, 0.2519929536323373f),
            (0.9469652757511278f, 0.25791818908545416f),
            (0.9481613689691947f, 0.26378971348304325f),
            (0.9493682127874609f, 0.26974882033448855f),
            (0.9505744372202971f, 0.27574542941084734f),
            (0.9517783524884541f, 0.2817769019690621f),
            (0.9529892330175649f, 0.287896269816159f),
            (0.9541947185853665f, 0.2940478274623703f),
            (0.9554044516127758f, 0.30028781869718807f),
            (0.9566054381494079f, 0.30655637705110717f),
            (0.957807698698665f, 0.31291317977345784f),
            (0.9590221672423604f, 0.3194262585871035f),
            (0.9602231628864696f, 0.32596720585696376f),
            (0.961433998563471f, 0.3326729095043305f),
            (0.9626279572859802f, 0.339405506964074f),
            (0.963842342827163f, 0.3463882776436415f),
            (0.965050103040447f, 0.3534815900715788f),
            (0.96624960935703f, 0.36068799949007235f),
            (0.9674529869368587f, 0.3680959084197356f),
            (0.9686591348667645f, 0.3757183982279203f),
            (0.9698668993297f, 0.38356990598376894f),
            (0.9710750806787853f, 0.39166641973515987f),
            (0.9722824425660789f, 0.4000257103100814f),
            (0.9734877233230004f, 0.408667608162045f),
            (0.9746896497445529f, 0.41761433619185573f),
            (0.9759017934808459f, 0.42700834856603836f),
            (0.977108280868104f, 0.43677295486970896f),
            (0.9783078742661878f, 0.44694216753773164f),
            (0.9795145289674262f, 0.4576932612610576f),
            (0.9807121922419519f, 0.46894998821359746f),
            (0.981930401543003f, 0.48108071091217086f),
            (0.9831227248036967f, 0.4937134311802671f),
            (0.9843344315883069f, 0.5074369975183908f),
            (0.9855345565106794f, 0.5220472890164811f),
            (0.9867538435462135f, 0.5381018792987169f),
            (0.9879462160499057f, 0.5551918317559756f),
            (0.989158767583543f, 0.5742496799950565f),
            (0.9903616429313051f, 0.5951727896238259f),
            (0.9915723216173138f, 0.6187204908473445f),
            (0.9927779343719173f, 0.6452713618738384f),
            (0.9939826353978779f, 0.6757582832177143f),
            (0.9951928260723995f, 0.7116318568448161f),
            (0.99638391362715f, 0.7539893920553304f),
            (0.9975978174482817f, 0.8078649708462118f),
            (0.9988016676122579f, 0.8810362590039038f),
            (0.9997988680153226f, 1),
            (1, 1),
        ];
        public override Leaderboards Leaderboard => Leaderboards.Accsaber;
        public override int RatingCount => 1;
        public override bool UsesModifiers => false;
        public override string Label => "AP";
        public override string[] StarLabels { get; } = ["<color=#00FF00>Complexity</color>"];

        internal APCalc()
        {
            PointList.Reverse();
        }

        public override float[] GetPp(float acc, params float[] ratings)
        {
            return [GetCurve(acc) * (ratings[0] + 18) * 61];
        }

        public override float[] SelectRatings(float[] ratings)
        {
            return [.. ratings.Take(1)];
        }

        public override float GetAccDeflated(float deflatedPp, int precision = -1, params float[] ratings)
        {
            if (deflatedPp > GetSummedPp(1.0f, ratings))
                return precision < 0 ? 1.0f : 100.0f;
            float outp = InvertCurve(deflatedPp / ((ratings[0] + 18) * 61));
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
            if (diffData is not TaohCache cache)
                throw new ArgumentNullException(nameof(diffData));

            float complexity = cache.ComplexityAccSaber ?? 0f;

            return GetAccDeflated(deflatedPp, precision, complexity * modMult);
        }

        public override float Inflate(float deflatedPp)
        {
            return deflatedPp; //There is no inflation in accsaber
        }

        public override float Deflate(float inflatedPp)
        {
            return inflatedPp; //There is no deflation in accsaber
        }
    }
}
