using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.Map_Utils
{
    public class Map
    {
        public string Hash { get; private set; }
        public static readonly string SS_MODE_NAME = "SS_Diff";
        public static readonly string AP_MODE_NAME = "AP_Diff";
        private static readonly string SS_TAOH_FORMAT = "\"name\":\"{0}\",\"scoreSaberID\":{1},\"hash\":\"{2}\",\"difficulty\":\"{3}\",\"characteristic\":\"{4}\",\"starScoreSaber\":{5}";
        private static readonly string AP_TAOH_FORMAT = "\"name\":\"{0}\",\"scoreSaberID\":{1},\"hash\":\"{2}\",\"difficulty\":\"{3}\",\"characteristic\":\"{4}\",\"complexityAccSaber\":{5}";
        private readonly Dictionary<string, Dictionary<BeatmapDifficulty, (string, JObject)>> data;
        public Map(string hash) {
            Hash = hash;
            data = [];
        }
        public Map(string hash, string songId, JObject data) {
            Hash = hash;
            this.data = [];
            Add(songId, data);
        }
        public Map(string hash, string mode, BeatmapDifficulty difficulty, string songId, JObject data)
        {
            Hash = hash;
            this.data = [];
            Add(mode, difficulty, songId, data);
        }
        public void Add(string songId, JObject data)
        {
            string mode = data["modeName"].ToString();
            BeatmapDifficulty difficulty = FromValue((int)data["value"]);
            Add(mode, difficulty, songId, data);
        }
        public void Add(string mode, BeatmapDifficulty difficulty, string songId, JObject data) {
            if (!this.data.ContainsKey(mode) || this.data[mode] == null)
                this.data.Add(mode, []);
            this.data[mode].Add(difficulty, (songId, data));
        }
        public (string MapId, JObject Data) Get(string mode, BeatmapDifficulty difficulty) => data[mode][difficulty];
        public static BeatmapDifficulty FromValue(int value) => (BeatmapDifficulty)((value + 1) / 2 - 1);
        public static int FromDiff(BeatmapDifficulty value) => ((int)value + 1) * 2 - 1;
        public Dictionary<string, (string, JObject)> Get(BeatmapDifficulty difficulty) =>
            data.Where(kvp => kvp.Value.ContainsKey(difficulty)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value[difficulty]);
        public bool TryGet(string mode, BeatmapDifficulty difficulty, out (string MapId, JObject Data) value)
        {
            if (data.TryGetValue(mode, out var hold) && hold.TryGetValue(difficulty, out value))
                return true;
            value = (default, default);
            return false;
        }
        public bool TryGet(BeatmapDifficulty difficulty, out Dictionary<string, (string, JObject)> value) 
        {
            value = Get(difficulty);
            return value != null;
        }
        public string[] GetModes() => [.. data.Keys];
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
        public static Map ConvertSSToTaoh(string hash, string songId, JObject SSInfo)
        {
            int diff = (int)SSInfo["difficulty"]["difficulty"];
            JObject newToken = JObject.Parse('{' + string.Format(SS_TAOH_FORMAT, SSInfo["songName"].ToString(), songId, hash, diff, SSInfo["difficulty"]["gameMode"].ToString().Replace("Solo", ""), (float)SSInfo["stars"]) + '}');
            return new Map(hash, SS_MODE_NAME, FromValue(diff), songId, newToken);
        }
        public static Map ConvertAPToTaoh(string hash, string songId, JObject APInfo)
        {
            //Plugin.Log.Info($"APInfo\n{APInfo}");
            string hold = APInfo["difficulty"].ToString().ToLower();
            int diff = FromDiff((BeatmapDifficulty)Enum.Parse(typeof(BeatmapDifficulty), char.ToUpper(hold[0]) + hold.Substring(1)));
            JObject newToken = JObject.Parse('{' + string.Format(AP_TAOH_FORMAT, APInfo["songName"].ToString(), songId, hash, diff, "Standard", (float)APInfo["complexity"]) + '}');
            return new Map(hash, AP_MODE_NAME, FromValue(diff), songId, newToken);
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
