using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BLPPCounter.Utils
{
#pragma warning disable CS0659
    public struct MapSelection
    {
#pragma warning restore CS0659
        public Map Map { get; private set; }
        public string Difficulty { get; private set; }
        public string Mode { get; private set; }
        public float PassRating { get; private set; }
        public float AccRating { get; private set; }
        public float TechRating { get; private set; }
        public (string, JToken) MapData { get => Map.Get(Mode, Difficulty); }
        public string Hash { get => Map.Hash; }

        public MapSelection(Map map = default, string diff = default, string mode = default, float passRating = default, float accRating = default, float techRating = default)
        {
            Map = map;
            Difficulty = diff;
            Mode = mode;
            PassRating = passRating;
            AccRating = accRating;
            TechRating = techRating;
        }

        public void FixRates(float passRating = default, float accRating = default, float techRating = default)
        {
            if (passRating != default) PassRating = passRating;
            if (accRating != default) AccRating = accRating;
            if (techRating != default) TechRating = techRating;
        }
        public (bool, bool) GetDifference(MapSelection other)
        {
            bool ratingDiff = !Mathf.Approximately(PassRating, other.PassRating) || !Mathf.Approximately(AccRating, other.AccRating) || !Mathf.Approximately(TechRating, other.TechRating);
            bool diffDiff = !Difficulty.Equals(other.Difficulty) || !Mode.Equals(other.Mode);
            return (ratingDiff, diffDiff);
        }
        public override string ToString()
        {
            string mapHash = Map == null ? "null" : Map.Hash;
            return $"Map: {mapHash}\nDifficulty: {Difficulty}\nMode: {Mode}\nPass Rating: {PassRating}\nAcc Rating: {AccRating}\nTech Rating: {TechRating}";
        }
        public bool Equals(MapSelection other) {
            if (other.Map == null || Map == null) return base.Equals(other);
            return other.Map.Hash.Equals(Map.Hash) && other.Difficulty.Equals(Difficulty) && other.Mode.Equals(Mode);
        }
        public override bool Equals(object obj) => obj is MapSelection selection && Equals(selection);
    }
}
