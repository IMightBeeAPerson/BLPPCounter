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
        private static PluginConfig PC => PluginConfig.Instance;
        //These store the ids, not the names.
        private static List<(string ID, int Rank)> _clanTargets, _followerTargets, _customTargets;
        public static IReadOnlyList<(string ID, int Rank)> ClanTargets => _clanTargets;
        public static IReadOnlyList<(string ID, int Rank)> FollowerTargets => _followerTargets;
        public static IReadOnlyList<(string ID, int Rank)> CustomTargets => _customTargets;
        public static Dictionary<string, string> IDtoNames;
        public static string PlayerID { get; private set; }
        public static string PlayerName { get; private set; }
        public static string TargetID => PC.Target.Equals(NO_TARGET) ? PlayerID : PC.Target;
        public static string TargetName => PC.Target.Equals(NO_TARGET) ? PlayerName : IDtoNames.ContainsKey(PC.Target) ? IDtoNames[PC.Target] : NO_TARGET;

        public static async Task GenerateTargets()
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
                _clanTargets = new List<(string ID, int Rank)>(0);
                goto _followerTargets;
            }
            JEnumerable<JToken> clanStuffs = JToken.Parse(clanInfo)["data"].Children();
            _clanTargets = new List<(string ID, int Rank)>(clanStuffs.Count());
            IDtoNames = new Dictionary<string, string>();
            foreach (JToken person in clanStuffs) {
                string playerName = person["name"].ToString();
                //ResolveDupes(IDtoNames, ref playerName);
                _clanTargets.Add((person["id"].ToString(), (int)person["rank"]));
                IDtoNames.Add(person["id"].ToString(), playerName);
            }

        _followerTargets:
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
                _followerTargets = new List<(string ID, int Rank)>(0);
                goto _customTargets;
            }
            _followerTargets = new List<(string ID, int Rank)>(data.Count());
            foreach (var (ID, Name) in data)
            {
                if (IDtoNames.TryAdd(ID, Name))
                    _followerTargets.Add((ID, -1));
            }

        _customTargets:
            List<CustomTarget> cts = PC.CustomTargets;
            _customTargets = new List<(string ID, int Rank)>(cts.Count);
            foreach (CustomTarget ct in cts)
            {
                string playerName = ct.Name;
                //ResolveDupes(IDtoNames, ref playerName);
                if (IDtoNames.TryAdd(ct.ID.ToString(), playerName))
                    _customTargets.Add((ct.ID.ToString(), -1));
            }
            Plugin.Log.Info("Targets loaded!");
        }
        public static void AddTarget(string name, string id, int rank = -1)
        {
            //ResolveDupes(IDtoNames, ref name);
            IDtoNames[id] = name;
            _customTargets.Insert(0, (name, rank));
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
                var pagedData = await APIHandler.CalledPagedAPI(playerID, 100, HelpfulPaths.BLAPI_FOLLOWERS, BLAPI.Throttle, PageHandler);
                IEnumerable<(string ID, string Name)> outp = new List<(string, string)>();
                foreach (var p in pagedData)
                    outp = outp.Union(p);
                return outp;
                //await BS_Utils.Gameplay.GetUserInfo.GetPlatformUserModel().GetUserFriendsUserIds(false).ConfigureAwait(false);
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
        }
    }
}
