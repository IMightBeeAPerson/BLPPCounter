using BeatLeader.Models;
using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace PleaseWork.Counters
{
    public class ClanCounter: IMyCounters
    {
        private static readonly HttpClient client = new HttpClient();
        private static int playerClanId = -1;
        private static readonly List<KeyValuePair<MapSelection, float[]>> mapCache = new List<KeyValuePair<MapSelection, float[]>>();
        private static PluginConfig pc;

        public string Name { get => "Clan"; }

        private TMP_Text display;
        private float accRating, passRating, techRating, nmAccRating, nmPassRating, nmTechRating;
        private float[] neededPPs, clanPPs;
        private int precision;
        private string addon;
        private bool mapCaptured, uncapturable, failed;
        #region Init
        public ClanCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            pc = PluginConfig.Instance;
            mapCaptured = uncapturable = failed = false;
            precision = pc.DecimalPrecision;
        }
        public ClanCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(map); }
        public void SetupData(MapSelection map)
        {
            JToken mapData = map.MapData.Item2;
            string songId = map.MapData.Item1;
            nmPassRating = HelpfulPaths.GetRating(mapData, PPType.Pass);
            nmAccRating = HelpfulPaths.GetRating(mapData, PPType.Acc);
            nmTechRating = HelpfulPaths.GetRating(mapData, PPType.Tech);
            neededPPs = new float[6];
            neededPPs[3] = GetCachedPP(map);
            if (neededPPs[3] <= 0)
            {
                float[] ppVals = LoadNeededPp(songId);
                if (mapCaptured) return;
                if (ppVals == null) { failed = true; return; }
                if (pc.MapCache > 0) mapCache.Add(new KeyValuePair<MapSelection, float[]>(map, ppVals));
            }
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
            while (mapCache.Count > pc.MapCache) mapCache.RemoveAt(0);
            uncapturable = pc.CeilEnabled && neededPPs[5] >= pc.ClanPercentCeil;
            switch (pc.CaptureType)
            {
                case "Percentage": addon = $"{neededPPs[5]}%</color>"; break;
                case "PP": addon = $"{Math.Round(neededPPs[3], precision)} pp</color>"; break;
                case "Both": addon = $"{neededPPs[5]}% ({Math.Round(neededPPs[3], precision)} pp)</color>"; break;
            }
        }
        public float[] LoadNeededPp(string mapId)
        {
            string id = Targeter.TargetID, check;
            neededPPs = new float[6];
            check = RequestData($"https://api.beatleader.xyz/player/{id}");
            if (playerClanId < 0 && check.Length > 0) playerClanId = ParseId(JToken.Parse(check));
            check = RequestData($"{HelpfulPaths.BLAPI_CLAN}{mapId}?page=1&count=1");
            if (check.Length <= 0) return null;
            JToken clanData = JToken.Parse(check)["clanRanking"].Children().First();
            int clanId = -1;
            if (clanData.Count() > 0) clanId = (int)clanData["clan"]["id"]; else return null;
            mapCaptured = clanId <= 0 || clanId == playerClanId;
            float pp = (float)clanData["pp"];
            JEnumerable<JToken> scores = JToken.Parse(RequestClanLeaderboard(id, mapId))["associatedScores"].Children();
            List<float> actualPpVals = new List<float>();
            float playerScore = 0.0f;
            foreach (JToken score in scores)
            {
                if (score["playerId"].ToString().Equals(id))
                    playerScore = (float)score["pp"];
                actualPpVals.Add((float)score["pp"]);
            }
            List<float> clone = new List<float>(actualPpVals);
            clone.Remove(playerScore);
            clanPPs = clone.ToArray();
            Array.Sort(clanPPs, (a, b) => (int)Math.Round(b - a));
            neededPPs[3] = mapCaptured ? 0.0f : BLCalc.GetNeededPlay(actualPpVals, pp, playerScore);
            return clanPPs.Prepend(neededPPs[3]).ToArray();
        }
        public void ReinitCounter(TMP_Text display) { this.display = display; }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating) { 
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = pc.DecimalPrecision;
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
        }
        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { this.display = display; passRating = map.PassRating; accRating = map.AccRating; techRating = map.TechRating; SetupData(map); }
        #endregion
        #region API Requests
        private string RequestClanLeaderboard(string id, string mapId)
        {
            try
            {
                int clanId = playerClanId > 0 ? playerClanId : ParseId(JToken.Parse(client.GetStringAsync($"https://api.beatleader.xyz/player/{id}").Result));
                return client.GetStringAsync($"https://api.beatleader.xyz/leaderboard/clanRankings/{mapId}/clan/{clanId}?count=100&page=1").Result;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed! This is very sad.\nError: {e.Message}\nMap ID: {mapId}\tPlayer ID: {id}");
                Plugin.Log.Debug(e);
                return "";
            }
        }
        private static string RequestData(string path)
        {
            try
            {
                return client.GetStringAsync(new Uri(path)).Result;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Beat Leader API request in ClanCounter failed!\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                return "";
            }
        }
        #endregion
        #region Helper Functions
        private static int ParseId(JToken playerData)
        {
            JEnumerable<JToken> clans = playerData["clans"].Children();
            if (clans.Count() <= 1) return clans.Count() == 1 ? (int)clans.First()["id"] : -1;
            string clan = playerData["clanOrder"].ToString().Split(',')[0];
            foreach (JToken token in clans)
                if (token["tag"].ToString().Equals(clan))
                    return (int)token["id"];
            return -1;
        }
        public static void ClearCache() => mapCache.Clear();
        private float GetCachedPP(MapSelection map)
        {
            foreach (KeyValuePair<MapSelection, float[]> pair in mapCache)
                if (pair.Key.Equals(map)) {
                    clanPPs = pair.Value.Skip(1).ToArray();
                    Plugin.Log.Info($"PP: {pair.Value[0]}");
                    return pair.Value[0];
                }
            return -1.0f;
        } 
        public static void AddToCache(MapSelection map, float[] vals) => mapCache.Add(new KeyValuePair<MapSelection, float[]>(map, vals));
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int badNotes, float fcPercent)
        {
            if (uncapturable || mapCaptured || failed)
            {
                UpdateWeightedCounter(acc, badNotes, fcPercent);
                return;
            }
            bool displayFc = pc.PPFC && badNotes > 0, showLbl = pc.ShowLbl, normal = pc.ClanWithNormal;
            float[] ppVals = new float[displayFc ? 16 : 8]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] - neededPPs[i];
            if (displayFc)
            {
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] - neededPPs[i - 8];
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };
            if (pc.SplitPPVals)
            {
                if (displayFc)
                {
                    string text = "";
                    for (int i = 0; i < 4; i++)
                    {
                        text += (ppVals[i + 4] > 0 ? "<color=\"green\">+" : ppVals[i + 4] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[i + 4]}</color> " + (normal ? $"({ppVals[i]}) / " : "/ ");
                        text += (ppVals[i + 12] > 0 ? "<color=\"green\">+" : ppVals[i + 12] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[i + 12]}</color>" + (normal ? $" ({ppVals[i + 8]})" : "") + (showLbl ? " " + labels[i] : "") + "\n";
                    }
                    display.text = text;
                }
                else
                {
                    string text = "";
                    for (int i = 0; i < 4; i++)
                        text += (ppVals[i + 4] > 0 ? "<color=\"green\">+" : ppVals[i + 4] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[i + 4]}</color>" + (normal ? $" ({ppVals[i]})" : "") + (showLbl ? " " + labels[i] : "") + "\n";
                    display.text = text;
                }
            }
            else
            {
                if (displayFc)
                    display.text = (ppVals[7] > 0 ? "<color=\"green\">+" : ppVals[7] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[7]}</color> " + (normal ? $"({ppVals[3]}) / " : "/ ") +
                        (ppVals[15] > 0 ? "<color=\"green\">+" : ppVals[15] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[15]}</color>" + (normal ? $" ({ppVals[11]})" : "") + (showLbl ? " " + labels[3] : "");
                else
                    display.text = (ppVals[7] > 0 ? "<color=\"green\">+" : ppVals[7] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[7]}</color>" + (normal ? $" ({ppVals[3]})" : "") + (showLbl ? " " + labels[3] : "");
                display.text += "\n";
            }
            display.text += "Aiming for " + (neededPPs[4] > acc ? "<color=\"red\">" : "<color=\"green\">") + addon;
        }
        private void UpdateWeightedCounter(float acc, int badNotes, float fcPercent)
        {
            bool displayFc = pc.PPFC && badNotes > 0, showLbl = pc.ShowLbl, normal = pc.ClanWithNormal;
            float[] ppVals = new float[displayFc ? 16 : 8]; //default pass, acc, tech, total pp for 0-3, modified for 4-7. Same thing but for fc with 8-15.
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            float weight = BLCalc.GetWeight(ppVals[3], clanPPs);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] * weight;
            if (displayFc)
            {
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcPercent, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] * weight;
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " Weighted PP" };
            if (pc.SplitPPVals)
            {
                if (displayFc)
                {
                    string text = "";
                    for (int i = 0; i < 4; i++)
                    {
                        text += $"{ppVals[i + 4]} " + (normal ? $"({ppVals[i]}) / " : "/ ");
                        text += $"{ppVals[i + 12]}" + (normal ? $" ({ppVals[i + 8]})" : "") + (showLbl ? " " + labels[i] : "") + "\n";
                    }
                    display.text = text;
                }
                else
                {
                    string text = "";
                    for (int i = 0; i < 4; i++)
                        text += $"{ppVals[i + 4]}" + (normal ? $" ({ppVals[i]})" : "") + (showLbl ? " " + labels[i] : "") + "\n";
                    display.text = text;
                }
            }
            else
            {
                if (displayFc)
                    display.text = $"{ppVals[7]} " + (normal ? $"({ppVals[3]}) / " : "/ ") + $"{ppVals[15]}" + (normal ? $" ({ppVals[11]})" : "") + (showLbl ? " " + labels[3] : "");
                else
                    display.text = $"{ppVals[7]}" + (normal ? $" ({ppVals[3]})" : "") + (showLbl ? " " + labels[3] : "");
                display.text += "\n";
            }
            if (!failed) display.text += mapCaptured ? "<color=\"green\">Map Was Captured!</color>" : "<color=\"red\">Map Uncapturable</color>"; 
            else display.text += "<color=\"red\">Loading Failed</color>";
        }
    }
    #endregion
}
