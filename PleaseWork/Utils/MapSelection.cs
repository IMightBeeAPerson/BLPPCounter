using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PleaseWork.Utils
{
    public struct MapSelection
    {
        public Map map;
        public string diff;
        public string mode;
        public float passRating, accRating, techRating;

        public MapSelection(Map map = default, string diff = default, string mode = default, float passRating = default, float accRating = default, float techRating = default)
        {
            this.map = map;
            this.diff = diff;
            this.mode = mode;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
        }

        public (bool, bool) GetDifference(MapSelection other)
        {
            bool ratingDiff = !Mathf.Approximately(passRating, other.passRating) || !Mathf.Approximately(accRating, other.accRating) || !Mathf.Approximately(techRating, other.techRating);
            bool diffDiff = !diff.Equals(other.diff) || !mode.Equals(other.mode);
            return (ratingDiff, diffDiff);
        }
        public override string ToString()
        {
            string mapHash = map == null ? "null" : map.hash;
            return $"Map: {mapHash}\nDifficulty: {diff}\nMode: {mode}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}";
        }
    }
}
