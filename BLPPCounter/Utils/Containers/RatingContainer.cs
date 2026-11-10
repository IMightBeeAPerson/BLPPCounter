using System;
using System.Diagnostics.CodeAnalysis;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.Enums;
using UnityEngine;
using Zenject;

namespace BLPPCounter.Utils.Containers
{
    public struct RatingContainer
    {
        private readonly ICalculatorProvider _calculatorProvider;

        public Leaderboards ValidLeaderboards { get; private set; }
        public float[]? SelectedRatings { get; private set; }

        private float StarRating,
            AccRating,
            PassRating,
            TechRating;
        private Leaderboards StarRatingLeader;

        internal RatingContainer(ICalculatorProvider calculatorProvider, Leaderboards leaderboard, float[] ratings)
        {
            _calculatorProvider = calculatorProvider;

            SetRatings(leaderboard, ratings);
            SetSelectedRatings(leaderboard);

            ValidLeaderboards = Leaderboards.None;
            ValidLeaderboards = GetValidLeaderboards();
        }

        public readonly float[] GetRatings(Leaderboards leaderboard)
        {
            if ((leaderboard & ValidLeaderboards) == Leaderboards.None)
                throw new ArgumentException("The given leaderboard is not valid for this container.");

            return _calculatorProvider.Get(leaderboard).SelectRatings(StarRating, AccRating, PassRating, TechRating);
        }

        public void SetSelectedRatings(Leaderboards leaderboard)
        {
            SelectedRatings = GetRatings(leaderboard);
        }

        public void MultiplyRatings(float multiplier)
        {
            StarRating *= multiplier;
            AccRating *= multiplier;
            PassRating *= multiplier;
            TechRating *= multiplier;
        }

        public readonly float[] GetAllRatings()
        {
            return [StarRating, AccRating, PassRating, TechRating];
        }

        /// <summary>
        /// Changes the ratings values of this container.
        /// </summary>
        /// <param name="leaderboard">The leaderboard the <paramref name="ratings"/> are from.</param>
        /// <param name="ratings">
        /// The ratings for a given <paramref name="leaderboard"/>. For each leaderboard there are specific conditions:<br/>
        /// <list type="bullet">
        ///     <item>
        ///         <term><see cref="Leaderboards.Beatleader"/></term>
        ///         <description>
        ///             Requires at least 3 ratings.
        ///             If 3 ratings are given, it should be in the order accRating, passRating, techRating.
        ///             If 4 or more ratings are given, then only the first 4 will be used. They should be in the order of starRating, accRating, passRating, techRating.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="Leaderboards.Scoresaber"/></term>
        ///         <description>
        ///             Requires either 1 rating or greater than or equal to 4 ratings.
        ///             If 1 rating is given, then it should be starRating.
        ///             If more than 1 rating is given, it expects at least 4 ratings in the order of starRating, accRating, passRating, techRating.
        ///         </description>
        ///     </item>
        ///     <item>
        ///     <term><see cref="Leaderboards.Accsaber"/></term>
        ///     <description>
        ///         Requires either 1 rating or greater than or equal to 4 ratings.
        ///             If 1 rating is given, then it should be starRating (which in this case should be the complexity of the map).
        ///             If more than 1 rating is given, it expects at least 4 ratings in the order of starRating, accRating, passRating, techRating.
        ///     </description>
        ///     </item>
        /// </list>
        /// If a bad leaderboard is given, then it will always throw an <see cref="ArgumentException"/>.
        /// </param>
        /// <exception cref="ArgumentException">Thrown if the ratings given do not match the requirements for a given <paramref name="leaderboard"/>.</exception>
        [MemberNotNull(nameof(StarRatingLeader))]
        public void SetRatings(Leaderboards leaderboard, params float[] ratings)
        {
            if (ratings is null)
                throw new ArgumentNullException("ratings");

            if (ratings.Length < 1)
                throw new ArgumentException(
                    "Invalid rating length given. There must be at least one rating given. If you wish to initialize an empty RatingContainer, use the default constructor."
                );

            switch (leaderboard)
            {
                case Leaderboards.Beatleader:
                    if (ratings.Length < 3)
                        throw new ArgumentException("Beatleader requires at least 3 ratings.");
                    if (ratings.Length == 3)
                    {
                        StarRatingLeader = Leaderboards.Beatleader;
                        AccRating = ratings[0];
                        PassRating = ratings[1];
                        TechRating = ratings[2];
                        return;
                    }
                    break;
                case Leaderboards.Scoresaber:
                case Leaderboards.Accsaber:
                    if (ratings.Length == 1)
                    {
                        StarRatingLeader = leaderboard;
                        StarRating = ratings[0];
                        ValidLeaderboards = GetValidLeaderboards();
                        return;
                    }
                    break;
                default:
                    throw new ArgumentException($"The leaderboard \"{leaderboard}\" is not valid for this function.");
            }
            if (ratings.Length < 4)
                throw new ArgumentException(
                    $"Invalid rating length given. Expected >= 4, instead given {ratings.Length}."
                );
            StarRatingLeader = leaderboard;
            StarRating = ratings[0];
            AccRating = ratings[1];
            PassRating = ratings[2];
            TechRating = ratings[3];
            ValidLeaderboards = GetValidLeaderboards();
        }

        private readonly Leaderboards GetValidLeaderboards()
        {
            Leaderboards outp = StarRatingLeader;
            if (
                !Mathf.Approximately(AccRating, default)
                && !Mathf.Approximately(PassRating, default)
                && !Mathf.Approximately(TechRating, default)
            )
                outp |= Leaderboards.Beatleader;
            return outp;
        }

        #region Overrides
        public override readonly bool Equals(object obj)
        {
            return obj is RatingContainer rc && Equals(rc);
        }

        public readonly bool Equals(RatingContainer other)
        {
            return Mathf.Approximately(StarRating, other.StarRating)
                && Mathf.Approximately(AccRating, other.AccRating)
                && Mathf.Approximately(PassRating, other.PassRating)
                && Mathf.Approximately(TechRating, other.TechRating);
        }

        public static bool operator ==(RatingContainer left, RatingContainer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RatingContainer left, RatingContainer right)
        {
            return !(left == right);
        }

        public override readonly string ToString()
        {
            return $"StarRating: {StarRating}\nAccRating: {AccRating}\nPassRating: {PassRating}\nTechRating {TechRating}";
        }

        public override readonly int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion

        internal sealed class Factory : PlaceholderFactory<Leaderboards, float[], RatingContainer>
        {
            private RatingContainer Create(
                Leaderboards starRatingLeader,
                float starRating,
                float accRating = default,
                float passRating = default,
                float techRating = default
            )
            {
                float[] ratings = [starRating, accRating, passRating, techRating];

                RatingContainer outp = Create(starRatingLeader, ratings);

                return outp;
            }

            /// <summary>
            /// Gets a container for the rating values.
            /// </summary>
            /// <param name="leaderboard">The leaderboard the <paramref name="ratings"/> are from.</param>
            /// <param name="ratings">
            /// The ratings for a given <paramref name="leaderboard"/>. For each leaderboard there are specific conditions:<br/>
            /// <list type="bullet">
            ///     <item>
            ///         <term><see cref="Leaderboards.Beatleader"/></term>
            ///         <description>
            ///             Requires at least 3 ratings.
            ///             If 3 ratings are given, it should be in the order accRating, passRating, techRating.
            ///             If 4 or more ratings are given, then only the first 4 will be used. They should be in the order of starRating, accRating, passRating, techRating.
            ///         </description>
            ///     </item>
            ///     <item>
            ///         <term><see cref="Leaderboards.Scoresaber"/></term>
            ///         <description>
            ///             Requires either 1 rating or greater than or equal to 4 ratings.
            ///             If 1 rating is given, then it should be starRating.
            ///             If more than 1 rating is given, it expects at least 4 ratings in the order of starRating, accRating, passRating, techRating.
            ///         </description>
            ///     </item>
            ///     <item>
            ///     <term><see cref="Leaderboards.Accsaber"/></term>
            ///     <description>
            ///         Requires either 1 rating or greater than or equal to 4 ratings.
            ///             If 1 rating is given, then it should be starRating (which in this case should be the complexity of the map).
            ///             If more than 1 rating is given, it expects at least 4 ratings in the order of starRating, accRating, passRating, techRating.
            ///     </description>
            ///     </item>
            /// </list>
            /// If a bad leaderboard is given, then it will only allow it if there are 4 ratings given. Otherwise, it will throw an <see cref="ArgumentException"/>.
            /// </param>
            /// <returns>A <see cref="RatingContainer"/> that stores the ratings.</returns>
            /// <exception cref="ArgumentException">Thrown if the ratings given do not match the requirements for a given <paramref name="leaderboard"/>.</exception>
            /// <exception cref="ArgumentNullException">Thrown if the ratings array given is null.</exception>
            public RatingContainer GetContainer(Leaderboards leaderboard, params float[] ratings)
            {
                if (ratings is null)
                    throw new ArgumentNullException("ratings");
                if (ratings.Length < 1)
                    throw new ArgumentException(
                        "Invalid rating length given. There must be at least one rating given. If you wish to initialize an empty RatingContainer, use the default constructor."
                    );
                switch (leaderboard)
                {
                    case Leaderboards.Beatleader:
                        if (ratings.Length < 3)
                            throw new ArgumentException("Beatleader requires at least 3 ratings.");
                        if (ratings.Length == 3)
                            return Create(Leaderboards.None, 0, ratings[0], ratings[1], ratings[2]);
                        break;
                    case Leaderboards.Scoresaber:
                    case Leaderboards.Accsaber:
                        if (ratings.Length == 1)
                            return Create(leaderboard, ratings[0], 0, 0, 0);
                        break;
                }
                if (ratings.Length < 4)
                    throw new ArgumentException(
                        $"Invalid rating length given. Expected >= 4, instead given {ratings.Length}."
                    );
                return Create(leaderboard, ratings[0], ratings[1], ratings[2], ratings[3]);
            }
        }
    }
}
