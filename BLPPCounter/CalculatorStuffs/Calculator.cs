using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Map_Utils;
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
    public abstract class Calculator
    {
        #region Static Variables
        private RatingContainer _ratings = default;
        /// <summary>
        /// Ratings container used for calculations when no specific ratings are given.
        /// </summary>
        public RatingContainer Ratings
        {
            get => _ratings;
            set {                 
                if (value.Equals(default) || value.SelectedRatings is null)
                    throw new ArgumentException("Given rating container is not valid.");
                _ratings = value;
            }
        }
        #endregion
        #region Variables
        /// <summary>
        /// The leaderboard a calculator is made for.
        /// </summary>
        public abstract Leaderboards Leaderboard { get; }
        /// <summary>
        /// How many ratings a leaderboard has. Beatleader would have 3 because of pass, tech, and acc ratings, while Scoresaber would only have 1.
        /// </summary>
        public abstract int RatingCount { get; }
        /// <summary>
        /// Whether or not this calculator uses modifiers in its calculations.
        /// </summary>
        public abstract bool UsesModifiers { get; }
        /// <summary>
        /// The label of the calculator/leaderboard.
        /// </summary>
        public abstract string Label { get; }
        public abstract string[] StarLabels { get; }
        /// <summary>
        /// This is how many ratings to display. If there is more than 1 rating type, the total pp also needs to be displayed, meaning plus 1.
        /// </summary>
        public int DisplayRatingCount => RatingCount > 1 ? RatingCount + 1 : RatingCount;
        /// <summary>
        /// The acc curve for a given leaderboard. Set to internal so that no outside programs modifies the curve.
        /// </summary>
        internal abstract List<(double, double)> PointList { get; }
        #endregion

        /// <summary>
        /// Calculates the pp for given ratings and accuracy.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values used to calculate the PP. This is specific to which leaderboard is being used.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public abstract float[] GetPp(float acc, params float[] ratings);
        /// <summary>Calculates the pp for given ratings and accuracy.</summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values used to calculate the PP. Global container is used of no rating is given.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public float[] GetPp(float acc, RatingContainer ratings) => GetPp(acc, ratings.GetRatings(Leaderboard));
        /// <summary>
        /// Calculates the pp for given accuracy using the global ratings container.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public float[] GetPp(float acc) => GetPp(acc, Ratings);
        /// <summary>Calculates the pp for given ratings and accuracy, then rounds them to the number of decimals given.</summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <param name="ratings">The rating values used to calculate the PP.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public float[] GetPp(float acc, int precision, RatingContainer ratings)
        {
            float[] outp = GetPp(acc, ratings);
            for (int i = 0; i < outp.Length; i++)
                outp[i] = (float)Math.Round(outp[i], precision);
            return outp;
        }
        /// <summary>Calculates the pp for given ratings and accuracy, then rounds them to the number of decimals given.</summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <param name="ratings">The rating values used to calculate the PP, these are <see cref="Calculator"/> dependent.</param>
        /// <returns>Returns all pp for each type of rating. Combining the numbers should result in the (deflated) pp value.</returns>
        public float[] GetPp(float acc, int precision, params float[] ratings)
        {
            float[] outp = GetPp(acc, ratings);
            for (int i = 0; i < outp.Length; i++)
                outp[i] = (float)Math.Round(outp[i], precision);
            return outp;
        }
        public float[] GetPp(float acc, int precision) => GetPp(acc, precision, Ratings);
        /// <summary>
        /// Sets the PP values in the specified array starting at the given offset.
        /// </summary>
        /// <param name="acc">The accuracy value used to calculate the performance points.</param>
        /// <param name="ppVals">The array where the calculated performance points will be stored. The array must have sufficient space to
        /// accommodate the values starting at the specified offset.</param>
        /// <param name="offset">The starting index in the <paramref name="ppVals"/> array where the performance points will be written. Must
        /// be within bounds of the array.</param>
        /// <param name="ratings">An optional <see cref="RatingContainer"/> object used to influence the performance point calculation.
        /// Defaults to <see cref="Ratings"/> if not provided.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if the <paramref name="offset"/> and <see cref="DisplayRatingCount"/> exceed the bounds of the
        /// <paramref name="ppVals"/> array.</exception>
        public void SetPp(float acc, float[] ppVals, int offset, RatingContainer ratings)
        {
            if (ppVals.Length < offset + DisplayRatingCount) throw new IndexOutOfRangeException("Given offset is too far out and goes out of bounds.");
            float[] pps = GetPpWithSummedPp(acc, ratings);
            for (int i = offset; i < offset + DisplayRatingCount; i++)
                ppVals[i] = pps[i - offset];
        }
        public void SetPp(float acc, float[] ppVals, int offset) => SetPp(acc, ppVals, offset, Ratings);
        public void SetPp(float acc, float[] ppVals, int offset, int precision, RatingContainer ratings)
        {
            if (ppVals.Length < offset + DisplayRatingCount) throw new IndexOutOfRangeException("Given offset is too far out and goes out of bounds.");
            float[] pps = GetPpWithSummedPp(acc, precision, ratings);
            for (int i = offset; i < offset + DisplayRatingCount; i++)
                ppVals[i] = pps[i - offset];
        }
        public void SetPp(float acc, float[] ppVals, int offset, int precision) => SetPp(acc, ppVals, offset, precision, Ratings);
        /// <summary>
        /// In the order of star, acc, pass, tech
        /// </summary>
        public abstract float[] SelectRatings(params float[] ratings);
        public float[] SelectRatings(MapSelection mapDiff) => 
            SelectRatings(mapDiff.Ratings.GetAllRatings());
        public float GetSummedPp(float acc, RatingContainer ratings) => GetPp(acc, ratings).Aggregate(0.0f, (total, current) => total + current);
        public float GetSummedPp(float acc, params float[] ratings) => GetPp(acc, ratings).Aggregate(0.0f, (total, current) => total + current);
        public float GetSummedPp(float acc) => GetSummedPp(acc, Ratings);
        /// <summary>
        /// Calculates the pp for given ratings and accuracy, then sums the number and rounds it to the number of decimals given.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <returns>Returns the summed pp for each type of rating. This should be the deflated pp value.</returns>
        public float GetSummedPp(float acc, int precision, RatingContainer ratings) => (float)Math.Round(GetSummedPp(acc, ratings), precision);
        public float GetSummedPp(float acc, int precision, params float[] ratings) => (float)Math.Round(GetSummedPp(acc, ratings), precision);
        public float GetSummedPp(float acc, int precision) => GetSummedPp(acc, precision, Ratings);
        /// <summary>
        /// Calculates the pp for given ratings and accuracy.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <returns>Returns all pp for each type of rating. There is also the summed and inflated pp as the last element in <paramref name="ratings"/>.</returns>
        public float[] GetPpWithSummedPp(float acc, RatingContainer ratings) =>
            RatingCount == 1 ? GetPp(acc, ratings) : [.. GetPp(acc, ratings), Inflate(GetSummedPp(acc, ratings))];
        public float[] GetPpWithSummedPp(float acc, params float[] ratings) =>
            RatingCount == 1 ? GetPp(acc, ratings) : [.. GetPp(acc, ratings), Inflate(GetSummedPp(acc, ratings))];
        public float[] GetPpWithSummedPp(float acc) => GetPpWithSummedPp(acc, Ratings);
        /// <summary>
        /// Calculates the pp for given ratings and accuracy, then rounds them to the number of decimals given.
        /// </summary>
        /// <param name="acc">The accuracy, which should be between 0 and 1, inclusive.</param>
        /// <param name="ratings">The rating values. This should match with the leaderboards index (for BL it should be 3, for SS it should 1, etc).</param>
        /// <param name="precision">This the number of decimals to round the numbers to.</param>
        /// <returns>Returns all pp for each type of rating. There is also the summed and inflated pp as the last element in the array.</returns>
        public float[] GetPpWithSummedPp(float acc, int precision, RatingContainer ratings) =>
            RatingCount == 1 ? GetPp(acc, precision, ratings) : [.. GetPp(acc, precision, ratings), (float)Math.Round(Inflate(GetSummedPp(acc, ratings)), precision)];
        public float[] GetPpWithSummedPp(float acc, int precision, params float[] ratings) =>
            RatingCount == 1 ? GetPp(acc, precision, ratings) : [.. GetPp(acc, precision, ratings), (float)Math.Round(Inflate(GetSummedPp(acc, ratings)), precision)];
        public float[] GetPpWithSummedPp(float acc, int precision) => GetPpWithSummedPp(acc, precision, Ratings);
        public abstract float GetAccDeflated(float deflatedPp, int precision = -1, params float[] ratings);
        public float GetAccDeflated(float deflatedPp, RatingContainer ratings, int precision = -1) => GetAccDeflated(deflatedPp, precision, ratings.SelectedRatings);
        public float GetAccDeflated(float deflatedPp, int precision = -1) => GetAccDeflated(deflatedPp, Ratings, precision);
        public abstract float GetAccDeflated(float deflatedPp, JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1.0f, int precision = -1);
        public float GetAcc(float inflatedPp, RatingContainer ratings, int precision = -1) =>
            GetAccDeflated(Deflate(inflatedPp), ratings, precision);
        public float GetAcc(float inflatedPp, int precision = -1, params float[] ratings) =>
            GetAccDeflated(Deflate(inflatedPp), precision, ratings);
        public float GetAcc(float inflatedPp, int precision = -1) => GetAcc(Deflate(inflatedPp), Ratings, precision);
        public float GetAcc(float inflatedPp, JToken diffData, SongSpeed speed = SongSpeed.Normal, float modMult = 1.0f, int precision = -1) =>
            GetAccDeflated(Deflate(inflatedPp), diffData, speed, modMult, precision);
        public abstract float Inflate(float deflatedPp);
        public abstract float Deflate(float inflatedPp);
        public float GetCurve(float acc) => GetCurve(acc, PointList);
        public float InvertCurve(double curveOutput) => GetInvertCurve(curveOutput, PointList);
        public float CurveDerivative(float acc) => GetCurveDerivative(acc, PointList);
        #region Static Methods
        public static Calculator GetSelectedCalc() => GetCalc(TheCounter.Leaderboard);
        public static Calculator GetCalc(Leaderboards leaderboard) => leaderboard switch
        {
            Leaderboards.Beatleader => BLCalc.Instance,
            Leaderboards.Scoresaber => SSCalc.Instance,
            Leaderboards.Accsaber => APCalc.Instance,
            _ => null,
        };
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
        #region Replay Math
        private static readonly int ScoringTypeMax =
            ((ScoringType[])Enum.GetValues(typeof(ScoringType))).Aggregate(-1, (total, current) => Math.Max(total, (int)current));
        private static readonly int ExtendedScoringTypeMax =
            ((ExtendedScoringType[])Enum.GetValues(typeof(ExtendedScoringType))).Aggregate(-1, (total, current) => Math.Max(total, (int)current));
        private static readonly Dictionary<ExtendedScoringType, NoteScoreDefinition> ExtendedNoteScoreDefinition = new()
        {
#if !NEWER_VERSION
            {ExtendedScoringType.ArcHeadArcTail, new NoteScoreDefinition(15, 70, 70, 30, 30, 0) },
            {ExtendedScoringType.ChainHeadArcTail, new NoteScoreDefinition(15, 70, 70, 0, 0, 0) },
            {ExtendedScoringType.ChainLinkArcHead, new NoteScoreDefinition(0, 0, 0, 0, 0, 20) },
#endif
            {ExtendedScoringType.ChainHeadArcHead, new NoteScoreDefinition(15, 0, 70, 30, 30, 0) },
            {ExtendedScoringType.ChainHeadArcHeadArcTail, new NoteScoreDefinition(15, 70, 70, 30, 30, 0) }
        };
        internal static (int Before, int After, int Acc) CutScoresForNote(BeatLeader.Models.Replay.NoteEvent note) =>
            CutScoresForNote(note.noteCutInfo, GetScoringType(note.noteID));
        internal static (int Before, int After, int Acc) CutScoresForNote(BeatLeader.Models.Replay.NoteCutInfo cut, ScoringType scoringType)
        {
            NoteScoreDefinition noteVals = GetNoteScoreDefinition(scoringType);

#if NEWER_VERSION
            if (scoringType == ScoringType.ChainLink || scoringType == ScoringType.ChainLinkArcHead)
#elif NEW_VERSION
            if (scoringType == (ScoringType)5 || (int)scoringType == (int)ExtendedScoringType.ChainLinkArcHead)
#else
            if (scoringType == ScoringType.BurstSliderElement || (int)scoringType == (int)ExtendedScoringType.ChainLinkArcHead)
#endif
                return (noteVals.minBeforeCutScore, noteVals.minAfterCutScore, noteVals.fixedCutScore);

            int beforeCutRawScore, afterCutRawScore, cutDistanceRawScore;
            beforeCutRawScore = noteVals.minBeforeCutScore == noteVals.maxBeforeCutScore ? noteVals.maxBeforeCutScore :
                (int)Mathf.Clamp(Mathf.Round(noteVals.maxBeforeCutScore * cut.beforeCutRating), noteVals.minBeforeCutScore, noteVals.maxBeforeCutScore);
            afterCutRawScore = noteVals.minAfterCutScore == noteVals.maxAfterCutScore ? noteVals.maxAfterCutScore :
                (int)Mathf.Clamp(Mathf.Round(noteVals.maxAfterCutScore * cut.afterCutRating), noteVals.minAfterCutScore, noteVals.maxAfterCutScore);
            cutDistanceRawScore = noteVals.maxCenterDistanceCutScore > 0 ? (int)Mathf.Round(noteVals.maxCenterDistanceCutScore * (1 - Mathf.Clamp01(cut.cutDistanceToCenter / 0.3f))) : 0;

            return (beforeCutRawScore, afterCutRawScore, cutDistanceRawScore);
        }
        internal static int GetMaxCutScore(BeatLeader.Models.Replay.NoteEvent note) =>
            GetNoteScoreDefinition(GetScoringType(note.noteID)).maxCutScore;
        internal static int GetCutScore(BeatLeader.Models.Replay.NoteEvent note) =>
            GetCutScore(note.noteCutInfo, GetScoringType(note.noteID));
        internal static int GetCutScore(BeatLeader.Models.Replay.NoteCutInfo cut, ScoringType scoringType)
        {
            var (Before, After, Acc) = CutScoresForNote(cut, scoringType);
            return Before + After + Acc;
        }
        //Link: https://github.com/BeatLeader/beatleader-mod/blob/master/Source/7_Utils/ReplayStatisticUtils.cs#L15
        internal static ScoringType GetScoringType(int noteId)
        {
            ScoringType outp;
            if (noteId < 100_000)
                outp = (ScoringType)(noteId / 10_000 - 2);
            else outp = (ScoringType)(noteId / 10_000_000 - 2);
            return outp > (ScoringType)ExtendedScoringTypeMax ? ScoringType.Normal : outp;
        }
        internal static NoteScoreDefinition GetNoteScoreDefinition(ScoringType scoringType)
        {
            try
            {
                if ((int)scoringType > ScoringTypeMax)
                    return (int)scoringType > ExtendedScoringTypeMax ? ScoreModel.GetNoteScoreDefinition(ScoringType.Normal) : ExtendedNoteScoreDefinition[(ExtendedScoringType)scoringType];
                return ScoreModel.GetNoteScoreDefinition(scoringType);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("There was an error trying to get the ScoringType \"" + scoringType + "\". Defaulting to ScoringType.Normal.");
                Plugin.Log.Error(e);
                return ScoreModel.GetNoteScoreDefinition(ScoringType.Normal);
            }
        }
        internal enum ExtendedScoringType
        {
#if !NEWER_VERSION
            ArcHeadArcTail = 6, ChainHeadArcTail = 7, ChainLinkArcHead = 8,
#endif
            ChainHeadArcHead = 9, ChainHeadArcHeadArcTail = 10 //for now, 1.40.9+ stuff will always be used. Once modding there becomes normal, I'll change it to only work on pre 1.40.9
        }
        #endregion
    }
}
