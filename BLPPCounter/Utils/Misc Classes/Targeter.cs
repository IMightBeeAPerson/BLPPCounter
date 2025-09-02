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
using BLPPCounter.Helpfuls;

namespace BLPPCounter.Utils
{
    public static class Targeter
    {
        public static readonly string NO_TARGET = "None";
        private static PluginConfig pc => PluginConfig.Instance;
        public static List<object> ClanTargets, FollowerTargets, CustomTargets;
        public static Dictionary<string, string> nameToId;
        public static string PlayerID { get; private set; }
        public static string PlayerName { get; private set; }
        public static string TargetID => pc.Target.Equals(NO_TARGET) ? PlayerID : nameToId[pc.Target];
        public static string TargetName => pc.Target.Equals(NO_TARGET) ? PlayerName : pc.Target;

        public static async void GenerateTargets()
        {
            string clanInfo = "";
            PlayerID = (await BS_Utils.Gameplay.GetUserInfo.GetUserAsync()).platformUserId;

            try
            {
                clanInfo = await RequestClan(PlayerID);
            } catch (Exception ex)
            {
                Plugin.Log.Error($"Error getting clan info: {ex}");
            }
            if (clanInfo.Length == 0)
            {
                ClanTargets = new List<object>(0);
                goto FollowerTargets;
            }
            JEnumerable<JToken> clanStuffs = JToken.Parse(clanInfo)["data"].Children();
            ClanTargets = new List<object>(clanStuffs.Count());
            nameToId = new Dictionary<string, string>();
            foreach (JToken person in clanStuffs) {
                string playerName = person["name"].ToString();
                ResolveDupes(nameToId, ref playerName);
                ClanTargets.Add(playerName);
                nameToId[playerName] = person["id"].ToString();
            }

        FollowerTargets:
            IEnumerable<(string ID, string Name)> data = null;
            try
            {
                data = await RequestFollowers(PlayerID);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error getting follower info: {ex}");
            }
            if (data is null)
            {
                FollowerTargets = new List<object>(0);
                goto CustomTargets;
            }
            FollowerTargets = new List<object>(data.Count());
            foreach (var (ID, Name) in data)
            {
                FollowerTargets.Add(Name);
                nameToId.Add(Name, ID);
            }

        CustomTargets:
            List<CustomTarget> cts = pc.CustomTargets;
            CustomTargets = new List<object>(cts.Count);
            foreach (CustomTarget ct in cts)
            {
                string playerName = ct.Name;
                ResolveDupes(nameToId, ref playerName);
                CustomTargets.Add(playerName);
                nameToId.TryAdd(playerName, $"{ct.ID}");
            }
            theTargets = otherTargets.Union(theTargets).ToList();
#if NEW_VERSION
            SettingsHandler.Instance.TargetList.Values = SettingsHandler.Instance.ToTarget;
#else
            SettingsHandler.Instance.TargetList.values = SettingsHandler.Instance.ToTarget;
#endif
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
            CustomTargets = CustomTargets.Prepend(name).ToList();
            //Plugin.Log.Info(string.Join(", ", theTargets));
        }
        public static async Task<string> RequestClan(string playerID)
        {
            try
            {
                (bool success, HttpContent data) = await APIHandler.CallAPI_Static($"https://api.beatleader.com/player/{playerID}", BLAPI.Throttle).ConfigureAwait(false);
                if (!success) return "";
                JToken playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
                PlayerName = playerData["name"].ToString();
                string clan = playerData["clanOrder"].ToString().Split(',')[0];
                (success, data) = await APIHandler.CallAPI_Static($"https://api.beatleader.com/clan/{clan}?count=100", BLAPI.Throttle).ConfigureAwait(false);
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
        public static async Task<IEnumerable<(string ID, string Name)>> RequestFollowers(string playerID)
        {
            try
            {
                async Task<IEnumerable<(string ID, string Name)>> PageHandler(string token) => await Task.Run(() => 
                JToken.Parse(token).Children().Select(t => (t["id"].ToString(), t["name"].ToString())));
                return (await APIHandler.CalledPagedAPI(playerID, 100, HelpfulPaths.BLAPI_FOLLOWERS, BLAPI.Throttle, PageHandler))
                    .Aggregate(new List<string>() as IEnumerable<(string ID, string Name)>, (total, current) => total.Union(current));
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn("There was an error when doing the API requests for follower data: " + e.Message);
                Plugin.Log.Debug(e);
            }
            return null;
        }
        private static void ResolveDupes<T>(Dictionary<string, T> dict, ref string name) 
        {
            if (dict.ContainsKey(name))
            {
                name += " (2)";
                int c = 3;
                while (dict.ContainsKey(name))
                    name = name.Substring(0, name.Length - 4 - (int)Math.Floor(Math.Log(c, 10))) + $" ({c++})";
            }
            return name;
        }
    }
}
