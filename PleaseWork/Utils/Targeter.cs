using IPA.Config.Data;
using PleaseWork.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace PleaseWork.Utils
{
    public static class Targeter
    {
        private static readonly HttpClient client = new HttpClient();
        private static PluginConfig pc => PluginConfig.Instance;
        public static List<object> theTargets;
        public static Dictionary<string, string> nameToId;
        public static string playerID;
        public static string TargetID => pc.Target.Equals("None") ? playerID : nameToId[pc.Target];

        public static async void GenerateClanNames()
        {
            string clanStuff = RequestClan((await BS_Utils.Gameplay.GetUserInfo.GetUserAsync()).platformUserId);
            theTargets = new List<object>();
            MatchCollection mc = new Regex("(?<=name...)[^,]+(?=...platform)").Matches(clanStuff);
            MatchCollection ids = new Regex("(?<=id...)[^,]+(?=...name)").Matches(clanStuff);
            nameToId = new Dictionary<string, string>();
            for (int i = 0; i < mc.Count; i++)
            {
                theTargets.Add(mc[i].Value);
                nameToId[mc[i].Value] = ids[i].Value;
            }
            var cts = pc.CustomTargets;
            if (cts.All(a => a.ID != pc.CustomTarget))
                try
                {
                    var converted = CustomTarget.ConvertToId($"{pc.CustomTarget}");
                    pc.CustomTarget = converted.ID;
                    pc.CustomTargets.Add(converted);
                    nameToId.Add(converted.Name, $"{converted.ID}");
                }
                catch (ArgumentException e)
                {
                    Plugin.Log.Warn(e.Message);
                }
            List<object> otherTargets = new List<object>() { "None" };
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
            theTargets[0] = name;
            theTargets.Prepend("None");
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
