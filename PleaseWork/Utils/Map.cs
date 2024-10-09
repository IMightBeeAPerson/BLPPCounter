using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace PleaseWork.Utils
{
    public class Map
    {
        public string Hash { get; private set; }
        private readonly Dictionary<string, Dictionary<string, (string, JToken)>> data;
        public Map(string hash) {
            Hash = hash;
            data = new Dictionary<string, Dictionary<string, (string, JToken)>>();
        }
        public Map(string hash, string songId, JToken data) {
            Hash = hash;
            this.data = new Dictionary<string, Dictionary<string, (string, JToken)>>();
            Add(songId, data);
        }
        public void Add(string songId, JToken data)
        {
            string mode = data["modeName"].ToString();
            string difficulty = data["difficultyName"].ToString();
            Add(mode, difficulty, songId, data);
        }
        public void Add(string mode, string difficulty, string songId, JToken data) {
            if (!this.data.ContainsKey(mode) || this.data[mode] == null)
                this.data.Add(mode, new Dictionary<string, (string, JToken)>());
            this.data[mode].Add(difficulty, (songId, data));
        }
        public (string, JToken) Get(string mode, string difficulty) => data[mode][difficulty];
        public Dictionary<string, (string, JToken)> Get(string difficulty)
        {
            Dictionary<string, (string, JToken)> outp = new Dictionary<string, (string, JToken)>();
            foreach(string key in data.Keys)
            {
                if (data[key].ContainsKey(difficulty))
                    outp[key] = data[key][difficulty];
            }
            return outp;
        }
        public bool TryGet(string mode, string difficulty, out (string, JToken) value)
        {
            if (data.TryGetValue(mode, out var hold) && hold.TryGetValue(difficulty, out value))
                return true;
            value = (default, default);
            return false;
        }
        public bool TryGet(string difficulty, out Dictionary<string, (string, JToken)> value) 
        {
            value = Get(difficulty);
            return value != null;
        }
        public void Combine(Map other)
        {
            foreach(string s in other.data.Keys)
                if (data.ContainsKey(s))
                {
                    foreach (string s2 in other.data[s].Keys)
                        if (!data[s].ContainsKey(s2))
                            data[s].Add(s2, other.data[s][s2]);
                }
                else
                    data.Add(s, other.data[s]);
        }
        public static Map Combine(Map m1, Map m2)
        {
            m1.Combine(m2);
            return m1;
        }
        public override string ToString()
        {
            string outp = $"Hash: {Hash}\n";
            foreach (string mode in data.Keys)
            {
                outp += $"----{mode}:\n";
                foreach (string diff in data[mode].Keys)
                {
                    outp += $"--------{diff}: {data[mode][diff].Item1}\n";
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
