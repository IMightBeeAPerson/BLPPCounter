using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils
{
    public class Map
    {
        public string Hash { get; private set; }
        public static readonly string SS_MODE_NAME = "SS_Diff";
        private static readonly string TAOH_FORMAT = "{\"name\":{0},\"scoreSaberID\":{1},\"hash\":{2},\"difficulty\":{3},\"characteristic\":{4},\"starScoreSaber\":{5}}";
        private readonly Dictionary<string, Dictionary<BeatmapDifficulty, (string, JToken)>> data;
        public Map(string hash) {
            Hash = hash;
            data = new Dictionary<string, Dictionary<BeatmapDifficulty, (string, JToken)>>();
        }
        public Map(string hash, string songId, JToken data) {
            Hash = hash;
            this.data = new Dictionary<string, Dictionary<BeatmapDifficulty, (string, JToken)>>();
            Add(songId, data);
        }
        public Map(string hash, string mode, BeatmapDifficulty difficulty, string songId, JToken data)
        {
            Hash = hash;
            this.data = new Dictionary<string, Dictionary<BeatmapDifficulty, (string, JToken)>>();
            Add(mode, difficulty, songId, data);
        }
        public void Add(string songId, JToken data)
        {
            string mode = data["modeName"].ToString();
            BeatmapDifficulty difficulty = FromValue((int)data["value"]);
            Add(mode, difficulty, songId, data);
        }
        public void Add(string mode, BeatmapDifficulty difficulty, string songId, JToken data) {
            if (!this.data.ContainsKey(mode) || this.data[mode] == null)
                this.data.Add(mode, new Dictionary<BeatmapDifficulty, (string, JToken)>());
            this.data[mode].Add(difficulty, (songId, data));
        }
        public (string, JToken) Get(string mode, BeatmapDifficulty difficulty) => data[mode][difficulty];
        public static BeatmapDifficulty FromValue(int value) => (BeatmapDifficulty)((value + 1) / 2 - 1);
        public static int FromDiff(BeatmapDifficulty value) => ((int)value + 1) * 2 - 1;
        public Dictionary<string, (string, JToken)> Get(BeatmapDifficulty difficulty) =>
            data.Where(kvp => kvp.Value.ContainsKey(difficulty)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value[difficulty]);
        public bool TryGet(string mode, BeatmapDifficulty difficulty, out (string, JToken) value)
        {
            if (data.TryGetValue(mode, out var hold) && hold.TryGetValue(difficulty, out value))
                return true;
            value = (default, default);
            return false;
        }
        public bool TryGet(BeatmapDifficulty difficulty, out Dictionary<string, (string, JToken)> value) 
        {
            value = Get(difficulty);
            return value != null;
        }
        public void Combine(Map other)
        {
            foreach(string s in other.data.Keys)
                if (data.ContainsKey(s))
                {
                    foreach (BeatmapDifficulty diff in other.data[s].Keys)
                        if (!data[s].ContainsKey(diff))
                            data[s].Add(diff, other.data[s][diff]);
                }
                else
                    data.Add(s, other.data[s]);
        }
        public static Map Combine(Map m1, Map m2)
        {
            m1.Combine(m2);
            return m1;
        }
        public static Map ConvertSSToTaoh(string hash, string songId, JToken SSInfo)
        {
            int diff = (int)SSInfo["difficulty"]["difficulty"];
            JToken newToken = JToken.Parse(string.Format(TAOH_FORMAT, SSInfo["songName"].ToString(), songId, hash, diff, SSInfo["difficulty"]["gameMode"].ToString().Replace("Solo", ""), (float)SSInfo["stars"]));
            return new Map(hash, SS_MODE_NAME, FromValue(diff), songId, newToken);
        }
        public override string ToString()
        {
            string outp = $"Hash: {Hash}\n";
            foreach (string mode in data.Keys)
            {
                outp += $"---- {mode}:\n";
                foreach (BeatmapDifficulty diff in data[mode].Keys)
                {
                    outp += $"-------- {diff}: {data[mode][diff].Item1}\n";
                }
            }
            return outp;
        }
        public override int GetHashCode()
        {
            return int.Parse(Hash, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
