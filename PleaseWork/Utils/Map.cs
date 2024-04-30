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
        public string hash { get; private set; }
        private Dictionary<string, Dictionary<string, string>> data;
        public Map(string hash) {
            this.hash = hash;
            data = new Dictionary<string, Dictionary<string, string>>();
        }
        public Map(string hash, string data) {
            this.hash = hash;
            this.data = new Dictionary<string, Dictionary<string, string>>();
            Add(data);
        }
        public void Add(string data)
        {
            string mode = new Regex("(?<=modeName...)[A-z]+").Match(data).Value;
            string difficulty = new Regex(@"(?<=difficultyName...)[A-z0-9]+").Match(data).Value;
            Add(mode, difficulty, data);
        }
        public void Add(string mode, string difficulty, string data) {
            if (!this.data.ContainsKey(mode) || this.data[mode] == null)
                this.data.Add(mode, new Dictionary<string, string>());
            this.data[mode].Add(difficulty, data);
        }
        public string Get(string mode, string difficulty)
        {
            return data[mode][difficulty];
        }
        public Dictionary<string, string> Get(string difficulty)
        {
            Dictionary<string, string> outp = new Dictionary<string, string>();
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

        public override int GetHashCode()
        {
            return int.Parse(hash, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
