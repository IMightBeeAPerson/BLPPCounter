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
        private static List<(string ID, int Rank)> _clanTargets = null, _followerTargets = null, _customTargets = null;
        internal static readonly HashSet<long> UsedIDs = new HashSet<long>();
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
            bool showWarnings = PluginConfig.Instance.TargeterStartupWarnings;

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
                long id = (long)person["id"];
                if (UsedIDs.Contains(id))
                {
                    if (showWarnings) Plugin.Log.Warn($"There is a duplicate id \"{id}\" inside of your home clan.");
                    continue;
                }
                UsedIDs.Add(id);
                string playerName = person["name"].ToString();
                //ResolveDupes(IDtoNames, ref playerName);
                _clanTargets.Add((id.ToString(), (int)person["rank"]));
                IDtoNames.Add(id.ToString(), playerName);
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
                long id = long.Parse(ID);
                if (UsedIDs.Contains(id))
                {
                    if (showWarnings) Plugin.Log.Warn($"There is a duplicate id \"{id}\" inside your follower list.");
                    continue;
                }
                UsedIDs.Add(id);
                if (IDtoNames.TryAdd(ID, Name))
                    _followerTargets.Add((ID, -1));
            }

        _customTargets:
            List<CustomTarget> cts = PC.CustomTargets;
            _customTargets = new List<(string ID, int Rank)>(cts.Count);
            List<(int RepIndex, CustomTarget NewCustomTarget)> toReplace = new List<(int RepIndex, CustomTarget NewCustomTarget)>();
            List<int> toRemove = new List<int>();
            for (int i = 0; i < cts.Count; i++)
            {
                CustomTarget ct = cts[i];
                string playerName = ct.Name;
                if (UsedIDs.Contains(ct.ID))
                {
                    if (showWarnings) Plugin.Log.Warn($"There is a duplicate id \"{ct.ID}\" inside your custom targets list.");
                    toRemove.Add(i);
                    continue;
                }
                UsedIDs.Add(ct.ID);
                if (IDtoNames.TryAdd(ct.ID.ToString(), playerName))
                {
                    if (ct.Rank > 0 || ct.Rank == -2)
                        _customTargets.Add((ct.ID.ToString(), ct.Rank));
                    else
                    {
                        (bool success, HttpContent content) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_USERID, ct.ID), BLAPI.Throttle);
                        if (success)
                        {
                            JToken profileData = JToken.Parse(await content.ReadAsStringAsync().ConfigureAwait(false));
                            ct = new CustomTarget(ct.Name, ct.ID, (int)profileData["rank"]);
                            _customTargets.Add((ct.ID.ToString(), ct.Rank));
                            toReplace.Add((i, ct));
                        }
                        else _customTargets.Add((ct.ID.ToString(), -2));
                    }
                }
            }
            _customTargets.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            foreach (var (RepIndex, NewCustomTarget) in toReplace)
                PC.CustomTargets[RepIndex] = NewCustomTarget;
            for (int i = toRemove.Count - 1; i >= 0; i--)
                PC.CustomTargets.RemoveRange(toRemove[i], 1);
            PC.CustomTargets.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }
        public static void AddTarget(string name, string id, int rank)
        {
            //ResolveDupes(IDtoNames, ref name);
            IDtoNames[id] = name;
            _customTargets.AddSorted((id, rank), (first, second) => first.Item2.CompareTo(second.Item2));
            //Plugin.Log.Info(string.Join(", ", theTargets));
        }
        public static void AddTarget(CustomTarget target) => AddTarget(target.Name, target.ID.ToString(), target.Rank);
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
                return await APIHandler.CalledPagedAPIFlat(100, (page, count) => string.Format(HelpfulPaths.BLAPI_FOLLOWERS, playerID, page, count), BLAPI.Throttle, PageHandler);
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
