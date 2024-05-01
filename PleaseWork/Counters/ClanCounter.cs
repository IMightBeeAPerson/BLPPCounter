using BeatLeader.Models;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using TMPro;

namespace PleaseWork.Counters
{
    public class ClanCounter: IMyCounters
    {
        private static readonly HttpClient client = new HttpClient();
        private static int playerClanId = -1;
        private static List<KeyValuePair<MapSelection, float>> mapCashe = new List<KeyValuePair<MapSelection, float>>();

        public string Name { get => "Clan"; }

        private TMP_Text display;
        private float accRating, passRating, techRating, nmAccRating, nmPassRating, nmTechRating;
        private float[] neededPPs;
        private int precision;
        private bool mapCaptured;
        private NormalCounter backup;
        #region Init
        public ClanCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            mapCaptured = false;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public ClanCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(TheCounter.UserID, map); }
        public void SetupData(string id, MapSelection map)
        {
            if (playerClanId < 0) playerClanId = ParseId(RequestData($"https://api.beatleader.xyz/player/{id}"));
            neededPPs = new float[6];
            string mapData = map.MapData;
            nmPassRating = float.Parse(new Regex(@"(?<=passRating..)[0-9\.]+").Match(mapData).Value);
            nmAccRating = float.Parse(new Regex(@"(?<=accRating..)[0-9\.]+").Match(mapData).Value);
            nmTechRating = float.Parse(new Regex(@"(?<=techRating..)[0-9\.]+").Match(mapData).Value);
            neededPPs[3] = GetCashedPP(map);
            if (neededPPs[3] <= 0)
            {
                string mapId = new Regex(@"(?<=LeaderboardId...)[A-z0-9]+").Match(mapData).Value;
                string clanData = RequestData($"{HelpfulPaths.BLAPI_CLAN}{mapId}?page=1&count=1");
                int clanId = -1;
                if (clanData.Length > 0) int.TryParse(new Regex(@"(?<=clan..{.id..)[0-9]+").Matches(clanData)[0].Value, out clanId);
                if (clanId > 0 && clanId != playerClanId)
                {
                    float pp = float.Parse(new Regex(@"(?<=:1,.pp..)[0-9.]+").Match(clanData).Value);
                    clanData = new Regex(@"(?<=associatedScores.:\[)(?:[^\[\]]+(?:\[[^\[\]]+.)?)+").Match(RequestClanLeaderboard(id, mapId)).Value;
                    MatchCollection ppVals = new Regex(@"(?<!(false|true)..pp..)(?<=pp..)[0-9.]+").Matches(clanData);
                    MatchCollection ids = new Regex(@"(?<=id...)[0-9]+(?=.,.name)").Matches(clanData);
                    List<float> actualPpVals = new List<float>();
                    float toRemove = 0.0f;
                    for (int i = 0; i < ppVals.Count; i++)
                        if (ids[i].Value == id)
                        {
                            toRemove = float.Parse(ppVals[i].Value);
                            actualPpVals.Add(toRemove);
                        }
                        else
                            actualPpVals.Add(float.Parse(ppVals[i].Value));

                    neededPPs[3] = BLCalc.GetNeededPlay(actualPpVals, pp, toRemove);
                }
                else neededPPs[3] = 0.0f;
                if (neededPPs[3] <= 0.0f)
                {
                    mapCaptured = true;
                    backup = new NormalCounter(display, accRating, passRating, techRating);
                    return;
                }
                mapCashe.Add(new KeyValuePair<MapSelection, float>(map, neededPPs[3]));
            }
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
            if (mapCashe.Count > PluginConfig.Instance.MapCashe) mapCashe.RemoveAt(0);
        }
        public void ReinitCounter(TMP_Text display) { this.display = display; if (mapCaptured) backup.ReinitCounter(display); }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating) { 
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = PluginConfig.Instance.DecimalPrecision;
            if (mapCaptured) {
                backup.ReinitCounter(display, passRating, accRating, techRating);
                return;
            }
            neededPPs[4] = BLCalc.GetAcc(accRating, passRating, techRating, neededPPs[3]);
            (neededPPs[0], neededPPs[1], neededPPs[2]) = BLCalc.GetPp(neededPPs[4], nmAccRating, nmPassRating, nmTechRating);
            neededPPs[5] = (float)Math.Round(neededPPs[4] * 100.0f, 2);
        }
        public void ReinitCounter(TMP_Text display, MapSelection map) 
        { this.display = display; SetupData(TheCounter.UserID, map);  }
        #endregion
        #region API Requests
        private string RequestClanLeaderboard(string id, string mapId)
        {
            try
            {
                int clanId = playerClanId > 0 ? playerClanId : ParseId(client.GetStringAsync($"https://api.beatleader.xyz/player/{id}").Result);
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
        private static int ParseId(string playerData)
        {
            string clan = new Regex(@"(?<=clanOrder...)[A-z]+").Match(playerData).Value.ToLower();
            MatchCollection clanIds = new Regex(@"{[^}]+}").Matches(new Regex(@"(?<=clans..\[)[^\]]+").Match(playerData).Value);
            Regex checker = new Regex($@"[0-9]+(?=..tag...{clan.ToUpper()})");
                foreach (Match clanId in clanIds)
                {
                    Match m = checker.Match(clanId.Value);
                    if (m.Success) return int.Parse(m.Value);
                }
            return -1;
        }
        private float GetCashedPP(MapSelection map)
        {
            foreach (KeyValuePair<MapSelection, float> pair in mapCashe)
                if (pair.Key.Equals(map)) return pair.Value;
            return -1.0f;
        } 
        #endregion
        #region Updates
        public void UpdateCounter(float acc, int notes, int badNotes, float fcPercent)
        {
            if (mapCaptured)
            {
                backup.UpdateCounter(acc, notes, badNotes, fcPercent);
                display.text += "\n<color=\"green\">Map Was Captured!</color>";
                return;
            }
            bool displayFc = PluginConfig.Instance.PPFC && badNotes > 0, showLbl = PluginConfig.Instance.ShowLbl, normal = PluginConfig.Instance.ClanWithNormal;
            float[] ppVals = new float[displayFc ? 16 : 8];
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
            if (PluginConfig.Instance.SplitPPVals)
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
            display.text += "Aiming for " + (neededPPs[4] > acc ? "<color=\"red\">" : "<color=\"green\">") + $"{neededPPs[5]}%</color>";
        }
    }
    #endregion
}
