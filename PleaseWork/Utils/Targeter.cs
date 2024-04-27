using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PleaseWork.Utils
{
    public static class Targeter
    {
        private static readonly HttpClient client = new HttpClient();
        public static List<object> clanNames;
        public static Dictionary<string, string> nameToId;

        public static string GetTargetId()
        {
            return nameToId[Settings.PluginConfig.Instance.Target];
        }
        public static async void GenerateClanNames()
        {
            string clanStuff = RequestClan((await BS_Utils.Gameplay.GetUserInfo.GetUserAsync()).platformUserId);
            clanNames = new List<object>() { "None" };
            MatchCollection mc = new Regex(@"(?<=name...)[^,]+(?=...platform)").Matches(clanStuff);
            MatchCollection ids = new Regex(@"(?<=id...)[^,]+(?=...name)").Matches(clanStuff);
            nameToId = new Dictionary<string, string>();
            for (int i = 0; i < mc.Count; i++)
            {
                clanNames.Add(mc[i].Value);
                nameToId[mc[i].Value] = ids[i].Value;
            }
        }
        public static string RequestClan(string playerID)
        {
            try
            {
                string playerData = client.GetStringAsync($"https://api.beatleader.xyz/player/{playerID}").Result;
                string clan = new Regex(@"(?<=clanOrder...)[A-z]+").Match(playerData).Value.ToLower();
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
