using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using BLPPCounter.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace BLPPCounter.Utils
{
    public static class Targeter
    {
        public static readonly string NO_TARGET = "None";
        private static readonly HttpClient client = new HttpClient();
        private static PluginConfig pc => PluginConfig.Instance;
        public static List<object> theTargets;
        public static Dictionary<string, string> nameToId;
        public static string playerID;
        public static string TargetID => pc.Target.Equals(NO_TARGET) ? playerID : nameToId[pc.Target];

        public static async void GenerateClanNames()
        {
            JEnumerable<JToken> clanStuffs = JToken.Parse(RequestClan((await BS_Utils.Gameplay.GetUserInfo.GetUserAsync()).platformUserId))["data"].Children();
            theTargets = new List<object>(clanStuffs.Count());
            nameToId = new Dictionary<string, string>();
            foreach (JToken person in clanStuffs) {
                theTargets.Add(person["name"].ToString());
                nameToId[person["name"].ToString()] = person["id"].ToString();
            }
            var cts = pc.CustomTargets;
            List<object> otherTargets = new List<object>(cts.Count);
            foreach (CustomTarget ct in cts)
            {
                otherTargets.Add(ct.Name);
                nameToId.Add(ct.Name, $"{ct.ID}");
            }
            theTargets = otherTargets.Union(theTargets).ToList();
        }
        public static void AddTarget(string name, string id)
        {
            if (nameToId.ContainsKey(name)) return;
            nameToId[name] = id;
            theTargets = theTargets.Prepend(name).ToList();
            //Plugin.Log.Info(string.Join(", ", theTargets));
        }
        public static string RequestClan(string playerID)
        {
            try
            {
                Targeter.playerID = playerID;
                string playerData = client.GetStringAsync($"https://api.beatleader.xyz/player/{playerID}").Result;
                string clan = new Regex("(?<=clanOrder...)[A-z]+").Match(playerData).Value.ToLower();
                return client.GetStringAsync($"https://api.beatleader.xyz/clan/{clan}?count=100").Result;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn("There was an error when doing the API requests for clan data: " + e.Message);
                Plugin.Log.Debug(e);
            }
            return "";
        }
    }
}
