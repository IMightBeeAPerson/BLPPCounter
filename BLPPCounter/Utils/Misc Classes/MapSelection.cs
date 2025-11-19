using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
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
        public SongSpeed MapSpeed { get; private set; }
        public RatingContainer Ratings { get; private set; }
        public (string songId, JToken diffData) MapData => Map.Get(Mode, Difficulty);
        public bool IsUsable => HelpfulMisc.StatusIsUsable(this);
        public string Hash => Map.Hash;

        public MapSelection(Map map, BeatmapDifficulty diff, string mode, RatingContainer ratings, SongSpeed mapSpeed = SongSpeed.Slower)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            Ratings = ratings;
            MapSpeed = mapSpeed;

            if (mapSpeed == SongSpeed.Slower)
                GetSongSpeed(); //This is not a super common mod, plus even if it is used this should be a quick check.
        }
        public MapSelection(Map map, BeatmapDifficulty diff, string mode, SongSpeed mapSpeed = SongSpeed.Slower, Leaderboards currentLeaderboard = Leaderboards.None, params float[] ratings) :
        this(map, diff, mode, RatingContainer.GetContainer(currentLeaderboard == Leaderboards.None ? TheCounter.Leaderboard : currentLeaderboard, ratings), mapSpeed) { }

        public void FixRates(Leaderboards currentLeaderboard, params float[] ratings) => Ratings.SetRatings(currentLeaderboard, ratings);
        private void GetSongSpeed()
        {
            float[] arr = HelpfulPaths.GetAllRatings(MapData.diffData, Calculator.GetSelectedCalc()).SelectMany(rating => rating.Ratings).ToArray(), ratings = Ratings.Ratings;
            for (int i = 0; i < arr.Length; i += ratings.Length)
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
            bool ratingDiff = other.MapSpeed != MapSpeed || !Ratings.Equals(other.Ratings);
            bool diffDiff = !Difficulty.Equals(other.Difficulty) || !Mode.Equals(other.Mode);
            return (ratingDiff, diffDiff);
        }
        public override string ToString()
        {
            string mapHash = Map is null ? "null" : Map.Hash;
            return $"Map: {mapHash}\nDifficulty: {Difficulty}\nMode: {Mode}\n{Ratings}";
        }
        public bool Equals(MapSelection other) {
            if (other.Map is null || Map is null) return base.Equals(other);
            return other.Map.Hash.Equals(Map.Hash) && other.Difficulty.Equals(Difficulty) && other.Mode.Equals(Mode);
        }
        public override bool Equals(object obj) => obj is MapSelection selection && Equals(selection);
    }
}
