﻿using IPA.Config.Data;
using Newtonsoft.Json.Linq;
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
            theTargets = new List<object>();
            nameToId = new Dictionary<string, string>();
            foreach (JToken person in clanStuffs) {
                theTargets.Add(person["name"].ToString());
                nameToId[person["name"].ToString()] = person["id"].ToString();
            }
            var cts = pc.CustomTargets;
            List<object> otherTargets = new List<object>() { NO_TARGET };
            foreach (CustomTarget ct in cts)
            {
                otherTargets.Add(ct.Name);
                nameToId.Add(ct.Name, $"{ct.ID}");
            }
            theTargets = otherTargets.Union(theTargets).ToList();
            if (!((string)theTargets[0]).Equals(NO_TARGET))
                theTargets.Insert(0, NO_TARGET); //I don't really understand why this is even needed but sometimes the no target option gets deleted ;-;
        }
        public static void AddTarget(string name, string id)
        {
            if (nameToId.ContainsKey(name)) return;
            nameToId[name] = id;
            theTargets[0] = name;
            theTargets = theTargets.Prepend(NO_TARGET).ToList();
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
