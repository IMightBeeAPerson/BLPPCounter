using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        public (string, JToken) MapData => Map.Get(Mode, Difficulty);
        public bool IsUsable => HelpfulMisc.StatusIsUsable(this);
        public string Hash => Map.Hash;

        public MapSelection(Map map = default, BeatmapDifficulty diff = default, string mode = default, float starRating = default, float accRating = default, float passRating = default, float techRating = default)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            PassRating = passRating;
            AccRating = accRating;
            TechRating = techRating;
            StarRating = starRating;
        }
        public MapSelection(Map map = default, BeatmapDifficulty diff = default, string mode = default, params float[] ratings)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            StarRating = ratings.Length >= 1 ? ratings[0] : default;
            AccRating = ratings.Length >= 2 ? ratings[1] : default;
            PassRating = ratings.Length >= 3 ? ratings[2] : default;
            TechRating = ratings.Length >= 4 ? ratings[3] : default;
        }

        public void FixRates(float starRating = default, float accRating = default, float passRating = default, float techRating = default)
        {
            if (starRating != default) StarRating = starRating;
            if (accRating != default) AccRating = accRating;
            if (passRating != default) PassRating = passRating;
            if (techRating != default) TechRating = techRating;
        }
        public (bool, bool) GetDifference(MapSelection other)
        {
            bool ratingDiff = !Mathf.Approximately(PassRating, other.PassRating) || !Mathf.Approximately(AccRating, other.AccRating) || !Mathf.Approximately(TechRating, other.TechRating) || !Mathf.Approximately(StarRating, other.StarRating);
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
