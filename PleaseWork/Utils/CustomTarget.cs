using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace PleaseWork.Utils
{
    public struct CustomTarget
    {
        [JsonProperty(nameof(Name), Required = Required.DisallowNull)]
        public string Name;
        [JsonProperty(nameof(ID), Required = Required.DisallowNull)]
        public long ID;

        public CustomTarget(string name, long id)
        {
            Name = name;
            ID = id;
        }

        public static CustomTarget ConvertToId(string str)
        {
            if (long.TryParse(str, out long id))
            {
                if (TheCounter.CallAPI($"player/{id}", out string data)) return new CustomTarget(JObject.Parse(data)["name"].ToString(), id);
                else throw new ArgumentException("The ID provided does not exist.");
            }
            throw new ArgumentException("String given is not a number.");
        }

        public override string ToString() => $"{Name}, ID: {ID}";
    }
}
