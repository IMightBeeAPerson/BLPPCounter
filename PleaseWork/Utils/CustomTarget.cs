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
            return ConvertToIdAlias(str);
        }
        private static CustomTarget ConvertToIdAlias(string str)
        {
            if (TheCounter.CallAPI($"player/{str.ToLower()}", out string data))
            {
                JObject parsedData = JObject.Parse(data);
                if (long.TryParse(parsedData["id"].ToString(), out long id))
                    return new CustomTarget(parsedData["name"].ToString(), id);
                throw new ArgumentException("Id in api is incorrect. This is very sad :(");
            }
            throw new ArgumentException("String given is not an ID or alias.");
        }
        public override string ToString() => $"{Name}, ID: {ID}";
    }
}
