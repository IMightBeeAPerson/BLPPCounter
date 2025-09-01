using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;

namespace BLPPCounter.Utils
{
    public static class Targeter
    {
        public static readonly string NO_TARGET = "None";
        private static PluginConfig pc => PluginConfig.Instance;
        public static List<object> theTargets;
        public static Dictionary<string, string> nameToId;
        public static string PlayerID { get; private set; }
        public static string PlayerName { get; private set; }
        public static string TargetID => pc.Target.Equals(NO_TARGET) ? PlayerID : nameToId[pc.Target];
        public static string TargetName => pc.Target.Equals(NO_TARGET) ? PlayerName : pc.Target;

        public static async void GenerateClanNames()
        {
            string clanInfo = "";
            try
            {
                clanInfo = await RequestClan((await BS_Utils.Gameplay.GetUserInfo.GetUserAsync()).platformUserId);
            } catch (Exception ex)
            {
                Plugin.Log.Error($"Error getting clan info: {ex}");
            }
            if (clanInfo.Length == 0)
            {
                theTargets = new List<object>();
                return;
            }
            JEnumerable<JToken> clanStuffs = JToken.Parse(clanInfo)["data"].Children();
            theTargets = new List<object>(clanStuffs.Count());
            nameToId = new Dictionary<string, string>();
            foreach (JToken person in clanStuffs) {
                string playerName = person["name"].ToString();
                if (nameToId.ContainsKey(playerName))
                {
                    playerName += " (2)";
                    int c = 3;
                    while (nameToId.ContainsKey(playerName))
                        playerName = playerName.Substring(0, playerName.Length - 4) + $" ({c++})";
                }
                theTargets.Add(playerName);

                nameToId[playerName] = person["id"].ToString();
            }
            var cts = pc.CustomTargets;
            List<object> otherTargets = new List<object>(cts.Count);
            foreach (CustomTarget ct in cts)
            {
                string playerName = ct.Name;
                if (nameToId.ContainsKey(playerName))
                {
                    playerName += " (2)";
                    int c = 3;
                    while (nameToId.ContainsKey(playerName))
                        playerName = playerName.Substring(0, playerName.Length - 4) + $" ({c++})";
                }
                otherTargets.Add(playerName);
                nameToId.TryAdd(playerName, $"{ct.ID}");
            }
            theTargets = otherTargets.Union(theTargets).ToList();
            SettingsHandler.Instance.TargetList.Values = SettingsHandler.Instance.ToTarget;
            SettingsHandler.Instance.TargetList.UpdateChoices();
        }
        public static void AddTarget(string name, string id)
        {
            if (nameToId.ContainsKey(name))
            {
                name += " (2)";
                int c = 3;
                while (nameToId.ContainsKey(name))
                    name = name.Substring(0, name.Length - 4) + $" ({c++})";
            }
            nameToId[name] = id;
            theTargets = theTargets.Prepend(name).ToList();
            //Plugin.Log.Info(string.Join(", ", theTargets));
        }
        public static async Task<string> RequestClan(string playerID)
        {
            try
            {
                PlayerID = playerID;
                (bool success, HttpContent data) = await APIHandler.CallAPI_Static($"https://api.beatleader.com/player/{playerID}", BLAPI.Throttle).ConfigureAwait(false);
                if (!success) return "";
                JToken playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
                PlayerName = playerData["name"].ToString();
                string clan = playerData["clanOrder"].ToString().Split(',')[0];
                (success, data) = await APIHandler.CallAPI_Static($"https://api.beatleader.com/clan/{clan}?count=100").ConfigureAwait(false);
                if (!success) return "";
                return await data.ReadAsStringAsync().ConfigureAwait(false);
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
