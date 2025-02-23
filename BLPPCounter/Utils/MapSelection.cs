using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BLPPCounter.Utils
{
#pragma warning disable CS0659
    public struct MapSelection
    {
#pragma warning restore CS0659
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

        public MapSelection(Map map = default, BeatmapDifficulty diff = default, string mode = default, float passRating = default, float accRating = default, float techRating = default, float starRating = default)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            PassRating = passRating;
            AccRating = accRating;
            TechRating = techRating;
            StarRating = starRating;
        }

        public void FixRates(float passRating = default, float accRating = default, float techRating = default, float starRating = default)
        {
            if (passRating != default) PassRating = passRating;
            if (accRating != default) AccRating = accRating;
            if (techRating != default) TechRating = techRating;
            if (starRating != default) StarRating = starRating;
        }
        public (bool, bool) GetDifference(MapSelection other)
        {
            bool ratingDiff = !Mathf.Approximately(PassRating, other.PassRating) || !Mathf.Approximately(AccRating, other.AccRating) || !Mathf.Approximately(TechRating, other.TechRating) || !Mathf.Approximately(StarRating, other.StarRating);
            bool diffDiff = !Difficulty.Equals(other.Difficulty) || !Mode.Equals(other.Mode);
            return (ratingDiff, diffDiff);
        }
        public override string ToString()
        {
            string mapHash = Map == null ? "null" : Map.Hash;
            return $"Map: {mapHash}\nDifficulty: {Difficulty}\nMode: {Mode}\nStar Rating: {StarRating}\nPass Rating: {PassRating}\nAcc Rating: {AccRating}\nTech Rating: {TechRating}";
        }
        public bool Equals(MapSelection other) {
            if (other.Map == null || Map == null) return base.Equals(other);
            return other.Map.Hash.Equals(Map.Hash) && other.Difficulty.Equals(Difficulty) && other.Mode.Equals(Mode);
        }
        public override bool Equals(object obj) => obj is MapSelection selection && Equals(selection);
    }
}
