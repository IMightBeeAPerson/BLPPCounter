using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;
using static GameplayModifiers;

namespace BLPPCounter.Utils
{
#pragma warning disable CS0659
    public struct MapSelection
    {
        public Map Map { get; private set; }
        public BeatmapDifficulty Difficulty { get; private set; }
        public string Mode { get; private set; }
        public float PassRating { get; private set; }
        public float AccRating { get; private set; }
        public float TechRating { get; private set; }
        public float StarRating { get; private set; }
        public (string songId, JToken diffData) MapData => Map.Get(Mode, Difficulty);
        public bool IsUsable => HelpfulMisc.StatusIsUsable(this);
        public string Hash => Map.Hash;
        public SongSpeed MapSpeed { get; private set; }

        public MapSelection(Map map = default, BeatmapDifficulty diff = default, string mode = default, SongSpeed mapSpeed = SongSpeed.Slower, float starRating = default, float accRating = default, float passRating = default, float techRating = default) : this(map, diff, mode, mapSpeed)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            PassRating = passRating;
            AccRating = accRating;
            TechRating = techRating;
            StarRating = starRating;
        }
        public MapSelection(Map map = default, BeatmapDifficulty diff = default, string mode = default, SongSpeed mapSpeed = SongSpeed.Slower, params float[] ratings) : this(map, diff, mode, mapSpeed)
        {
            StarRating = ratings.Length >= 1 ? ratings[0] : default;
            AccRating = ratings.Length >= 2 ? ratings[1] : default;
            PassRating = ratings.Length >= 3 ? ratings[2] : default;
            TechRating = ratings.Length >= 4 ? ratings[3] : default;
        }
        private MapSelection(Map map, BeatmapDifficulty diff, string mode, SongSpeed mapSpeed = SongSpeed.Slower)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            MapSpeed = mapSpeed;
            PassRating = default;
            AccRating = default;
            TechRating = default;
            StarRating = default;

            if (mapSpeed == SongSpeed.Slower)
                GetSongSpeed(); //This is not a super common mod, plus even if it is used this should be a quick check.
        }

        public void FixRates(float starRating = default, float accRating = default, float passRating = default, float techRating = default)
        {
            if (starRating != default) StarRating = starRating;
            if (accRating != default) AccRating = accRating;
            if (passRating != default) PassRating = passRating;
            if (techRating != default) TechRating = techRating;
        }
        private void GetSongSpeed()
        {
            Calculator calc = Calculator.GetSelectedCalc();
            float[] arr = HelpfulPaths.GetAllRatings(MapData.diffData, calc), ratings = calc.SelectRatings(StarRating, AccRating, PassRating, TechRating);
            for (int i = 0; i < arr.Length; i += HelpfulMisc.OrderedSpeeds.Length)
            {
                bool success = true;
                for (int j = 0; j < ratings.Length; j++) 
                    if (!Mathf.Approximately(arr[i + j], ratings[j]))
                    {
                        success = false;
                        break;
                    }
                if (success)
                {
                    MapSpeed = HelpfulMisc.OrderedSpeeds[i / HelpfulMisc.OrderedSpeeds.Length];
                    return;
                }
            }
            throw new Exception("There is no ratings matching the ones in this mapSelection. Something is very wrong.");
        }
        public (bool, bool) GetDifference(MapSelection other)
        {
            bool ratingDiff = other.MapSpeed != MapSpeed || !Mathf.Approximately(PassRating, other.PassRating) || !Mathf.Approximately(AccRating, other.AccRating) || !Mathf.Approximately(TechRating, other.TechRating) || !Mathf.Approximately(StarRating, other.StarRating);
            bool diffDiff = !Difficulty.Equals(other.Difficulty) || !Mode.Equals(other.Mode);
            return (ratingDiff, diffDiff);
        }
        public override string ToString()
        {
            string mapHash = Map is null ? "null" : Map.Hash;
            return $"Map: {mapHash}\nDifficulty: {Difficulty}\nMode: {Mode}\nStar Rating: {StarRating}\nAcc Rating: {AccRating}\nPass Rating: {PassRating}\nTech Rating: {TechRating}";
        }
        public bool Equals(MapSelection other) {
            if (other.Map is null || Map is null) return base.Equals(other);
            return other.Map.Hash.Equals(Map.Hash) && other.Difficulty.Equals(Difficulty) && other.Mode.Equals(Mode);
        }
        public override bool Equals(object obj) => obj is MapSelection selection && Equals(selection);
    }
}
