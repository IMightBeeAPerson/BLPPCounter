using System;
using System.Diagnostics.CodeAnalysis;
using BLPPCounter.Models.MapModels;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Utils.Map_Utils
{
#pragma warning disable CS0659
    public readonly struct MapSelection : IEquatable<MapSelection>
    {
        internal MapSelection(
            Map map,
            BeatmapDifficulty difficulty,
            string mode,
            SongSpeed mapSpeed,
            RatingContainer ratings
        )
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));

            if (BetterString.IsNullOrWhiteSpace(mode))
            {
                throw new ArgumentException("A map mode is required.", nameof(mode));
            }

            if (!map.TryGet(mode, difficulty, out MapDiffModel? mapData))
            {
                throw new ArgumentException($"Map {map.Hash} does not contain " + $"{mode} {difficulty}.", nameof(map));
            }

            Map = map;
            Difficulty = difficulty;
            Mode = mode;
            Ratings = ratings;
            MapSpeed = mapSpeed;
            MapData = mapData;
        }

        public Map Map { get; }

        public BeatmapDifficulty Difficulty { get; }

        public string Mode { get; }

        public SongSpeed MapSpeed { get; }

        public RatingContainer Ratings { get; }

        public MapDiffModel MapData { get; }

        public string Hash => Map.Hash;

        [MemberNotNullWhen(true, nameof(Map), nameof(MapData))]
        public bool IsUsable =>
            Map is not null && MapData is not null && MapData.Data is not null && Ratings.SelectedRatings is not null;

        public void FixRates(Leaderboards leaderboard, params float[] ratings)
        {
            Ratings.SetRatings(leaderboard, ratings);
        }

        public (bool RatingDifference, bool DifficultyDifference) GetDifference(MapSelection other)
        {
            bool ratingDifference = other.MapSpeed != MapSpeed || !Ratings.Equals(other.Ratings);

            bool difficultyDifference =
                other.Difficulty != Difficulty || !string.Equals(other.Mode, Mode, StringComparison.Ordinal);

            return (ratingDifference, difficultyDifference);
        }

        public bool Equals(MapSelection other)
        {
            if (Map is null || other.Map is null)
            {
                return Map is null && other.Map is null;
            }

            return string.Equals(Map.Hash, other.Map.Hash, StringComparison.OrdinalIgnoreCase)
                && Difficulty == other.Difficulty
                && string.Equals(Mode, other.Mode, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is MapSelection other && Equals(other);
        }

        public override string ToString()
        {
            return Map is null
                ? "Map: null"
                : $"Map: {Map.Hash}\n" + $"Difficulty: {Difficulty}\n" + $"Mode: {Mode}\n" + Ratings;
        }

        internal sealed class Factory
            : PlaceholderFactory<Map, BeatmapDifficulty, string, SongSpeed, RatingContainer, MapSelection>
        {
            private readonly RatingContainer.Factory ratingFactory;

            public Factory(RatingContainer.Factory ratingFactory)
            {
                this.ratingFactory = ratingFactory;
            }

            public MapSelection Create(Map map, BeatmapDifficulty diff, string mode, RatingContainer ratingContainer)
            {
                return Create(map, diff, mode, SongSpeed.Normal, ratingContainer);
            }

            public MapSelection Create(
                Map map,
                BeatmapDifficulty diff,
                string mode,
                SongSpeed speed,
                Leaderboards leaderboard,
                params float[] ratings
            )
            {
                return Create(map, diff, mode, speed, ratingFactory.GetContainer(leaderboard, ratings));
            }

            public MapSelection Create(
                Map map,
                BeatmapDifficulty diff,
                string mode,
                Leaderboards leaderboard,
                params float[] ratings
            )
            {
                return Create(map, diff, mode, ratingFactory.GetContainer(leaderboard, ratings));
            }
        }
    }
}
