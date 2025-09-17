using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static AlphabetScrollInfo;
using static UnityEngine.GraphicsBuffer;

namespace BLPPCounter.Utils
{
    public static class Targeter
    {
        public static readonly string NO_TARGET = "None";
        public static readonly int MAX_LIST_LENGTH = 100;
        private static PluginConfig PC => PluginConfig.Instance;
        private static readonly object CustomRefreshLock = new object();
        //These store the ids, not the names.
        private static List<(string ID, int Rank)> _clanTargets = null, _followerTargets = null, _customTargets = null;
        internal static readonly HashSet<long> UsedIDs = new HashSet<long>();
        public static IReadOnlyList<(string ID, int Rank)> ClanTargets => _clanTargets;
        public static IReadOnlyList<(string ID, int Rank)> FollowerTargets => _followerTargets;
        public static IReadOnlyList<(string ID, int Rank)> CustomTargets => _customTargets;
        public static Dictionary<string, string> IDtoNames;
        public static string PlayerID { get; private set; }
        public static string PlayerName { get; private set; }
        public static string TargetID => PC.TargetID < 0 ? PlayerID : PC.TargetID.ToString();
        public static string TargetName => PC.Target.Equals(NO_TARGET) ? PlayerName : PC.Target;

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
            try
            {
                if (!File.Exists(HelpfulPaths.TARGET_DATA))
                {
                    await LoadFollowers();
                    goto _customTargets;
                }
                JsonSerializer serializer = new JsonSerializer();
                IEnumerable<CustomTarget> followerData;
                using (StreamReader reader = File.OpenText(HelpfulPaths.TARGET_DATA))
                    followerData = serializer.Deserialize(reader, typeof(IEnumerable<CustomTarget>)) as IEnumerable<CustomTarget>;
                _followerTargets = new List<(string ID, int Rank)>(followerData.Count());
                foreach (CustomTarget target in followerData)
                {
                    _followerTargets.Add((target.ID.ToString(), target.Rank));
                    IDtoNames.Add(target.ID.ToString(), target.Name);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Followers failed to load!");
                if (e is JsonSerializationException)
                {
                    Plugin.Log.Error($"The {HelpfulPaths.TARGET_DATA} file has bad json data in it. Deleting it.");
                    File.Delete(HelpfulPaths.TARGET_DATA);
                }
                Plugin.Log.Error(e);
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

            if (!IDtoNames.TryGetValue(PC.TargetID.ToString(), out string val) || !val.Equals(PC.Target))
            {
                if (PC.TargetID > -1)
                    AddTarget(await CustomTarget.ConvertToId(PC.TargetID));
                else
                    PC.Target = NO_TARGET;
            }
            SettingsHandler.Instance.SetSelectedTargetRelation();
        }
        internal static void ReloadCustomPlayers()
        {
            List<CustomTarget> temp = PC.CustomTargets;
            ReloadTargetList(ref temp, ref _customTargets);
            PC.CustomTargets = temp;
        }
        internal static void ReloadFollowers()
        {
            foreach (var p in _followerTargets)
            {
                UsedIDs.Remove(long.Parse(p.ID));
                IDtoNames.Remove(p.ID);
            }
            LoadFollowers().GetAwaiter().GetResult();
            List<CustomTarget> targets = _followerTargets.Select(token => new CustomTarget(IDtoNames[token.ID], long.Parse(token.ID), token.Rank)).ToList();
            ReloadTargetList(ref targets, ref _followerTargets);
        }
        internal static void ReloadTargetList(ref List<CustomTarget> listVar, ref List<(string ID, int Rank)> displayListVar)
        {
            if (Monitor.TryEnter(CustomRefreshLock))
            {
                try
                {
                    (listVar, displayListVar) = ReloadTargetListInternal(listVar, displayListVar).GetAwaiter().GetResult();
                    IEnumerator WaitThenUpdate()
                    {
                        yield return new WaitForEndOfFrame();
                        SettingsHandler.Instance.UpdateTargetLists();
                    }
                    WaitThenUpdate().AsTask(CoroutineHost.Instance).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error("Error reloading custom players\n" + ex);
                }
                finally
                {
                    Monitor.Exit(CustomRefreshLock);
                }
            }
        } 
        private static async Task<(List<CustomTarget>, List<(string ID, int Rank)>)> ReloadTargetListInternal(List<CustomTarget> list, List<(string ID, int Rank)> displayList)
        {
            List<CustomTarget> outpList = new List<CustomTarget>(list.Count);
            List<(string ID, int Rank)> outpDisplayList;
            foreach (CustomTarget ct in list)
            {
                var (Success, Content) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_USERID, ct.ID), BLAPI.Throttle);
                if (!Success) continue;
                string data = await Content.ReadAsStringAsync().ConfigureAwait(false);
                if (data is null || data.Length == 0) continue;
                JToken playerData = JToken.Parse(data);
                outpList.Add(new CustomTarget(playerData["name"].ToString(), ct.ID, (int)playerData["rank"]));
            }
            outpList.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            outpDisplayList = outpList.Select(token => (token.ID.ToString(), token.Rank)).ToList();
            return (outpList, outpDisplayList);
        }
        public static void AddTarget(string name, string id, int rank)
        {
            IDtoNames[id] = name;
            _customTargets.AddSorted((id, rank), (first, second) => first.Item2.CompareTo(second.Item2));
        }
        public static void AddTarget(CustomTarget target) => AddTarget(target.Name, target.ID.ToString(), target.Rank);
        public static void SetTarget(string name, long id)
        {
            PC.Target = name;
            PC.TargetID = id;
        }
        public static void SetTarget(CustomTarget target) => SetTarget(target.Name, target.ID);
        public static async Task<string> RequestClan(string playerID)
        {
            try
            {
                (bool success, HttpContent data) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_USERID, playerID), BLAPI.Throttle);
                if (!success) return "";
                JToken playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
                PlayerName = playerData["name"].ToString();
                string clan = playerData["clanOrder"].ToString().Split(',')[0];
                (success, data) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_CLAN_PLAYERS, clan, MAX_LIST_LENGTH), BLAPI.Throttle);
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
        public static async Task<IEnumerable<CustomTarget>> RequestFollowers(string playerID)
        {
            try
            {
                async Task<IEnumerable<CustomTarget>> PageHandler(string token) => 
                    await Task.Run(() => 
                JToken.Parse(token).Children().Select(t => new CustomTarget(t["name"].ToString(), (long)t["id"], -1)));
                IEnumerable<CustomTarget> outp;
                outp = await APIHandler.CalledPagedAPIFlat(MAX_LIST_LENGTH, (page, count) => string.Format(HelpfulPaths.BLAPI_FOLLOWERS, playerID, page, count), BLAPI.Throttle, PageHandler);
                //await BS_Utils.Gameplay.GetUserInfo.GetPlatformUserModel().GetUserFriendsUserIds(false).ConfigureAwait(false);
                return outp;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn("There was an error when doing the API requests for follower data: " + e.Message);
                Plugin.Log.Debug(e);
            }
            return null;
        }
        internal static void SaveAll()
        {
            IEnumerable<JToken> toSave = FollowerTargets.Where(token => token.Rank >= 0)
                .Select(token => JToken.FromObject(new CustomTarget(IDtoNames[token.ID], long.Parse(token.ID), token.Rank)));
            using (StreamWriter sw = new StreamWriter(HelpfulPaths.TARGET_DATA))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(sw, toSave, typeof(CustomTarget));
            }
        }
        private static async Task LoadFollowers(bool ignoreDupes = false)
        {
            IEnumerable<CustomTarget> data = null;
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
                return;
            }
            _followerTargets = new List<(string ID, int Rank)>(data.Count());
            foreach (CustomTarget target in data)
            {
                if (UsedIDs.Contains(target.ID))
                {
                    if (PC.TargeterStartupWarnings) Plugin.Log.Warn($"There is a duplicate id \"{target.ID}\" inside your follower list.");
                    continue;
                }
                else UsedIDs.Add(target.ID);
                if (IDtoNames.TryAdd(target.ID.ToString(), target.Name))
                    _followerTargets.Add((target.ID.ToString(), target.Rank));
            }
        }
        /*private static void ResolveDupes<T>(Dictionary<string, T> dict, ref string name) 
        {
            if (dict.ContainsKey(name))
            {
                name += " (2)";
                int c = 3;
                while (dict.ContainsKey(name))
                    name = name.Substring(0, name.Length - 4 - (int)Math.Floor(Math.Log(c, 10))) + $" ({c++})";
            }
        }*/
    }
}
