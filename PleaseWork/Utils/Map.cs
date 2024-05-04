using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PleaseWork.Utils
{
    public class Map
    {
        public string Hash { get; private set; }
        public string SongID { get; private set; }
        private Dictionary<string, Dictionary<string, JToken>> data;
        public Map(string hash, string songId) {
            Hash = hash;
            SongID = songId;
            data = new Dictionary<string, Dictionary<string, JToken>>();
        }
        public Map(string hash, string songId, JToken data) {
            Hash = hash;
            SongID = songId;
            this.data = new Dictionary<string, Dictionary<string, JToken>>();
            Add(data);
        }
        public void Add(JToken data)
        {
            string mode = data["modeName"].ToString();
            string difficulty = data["difficultyName"].ToString();
            Add(mode, difficulty, data);
        }
        public void Add(string mode, string difficulty, JToken data) {
            if (!this.data.ContainsKey(mode) || this.data[mode] == null)
                this.data.Add(mode, new Dictionary<string, JToken>());
            this.data[mode].Add(difficulty, data);
        }
        public JToken Get(string mode, string difficulty)
        {
            return data[mode][difficulty];
        }
        public Dictionary<string, JToken> Get(string difficulty)
        {
            Dictionary<string, JToken> outp = new Dictionary<string, JToken>();
            foreach(string key in data.Keys)
            {
                if (data[key].ContainsKey(difficulty))
                    outp[key] = data[key][difficulty];
            }
            return outp;
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
                    outp += $"--------{diff}: [Insert Data Here]\n";
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
