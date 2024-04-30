using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using BeatLeader.Models.Replay;
using System.Text.RegularExpressions;
using System.Net.Http;
using PleaseWork.Settings;
using System.Security.Policy;
using BeatLeader.Models;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Utils;

namespace PleaseWork.Counters
{
    public class RelativeCounter: IMyCounters
    {
        private static readonly HttpClient client = new HttpClient();
        public string Name { get => "Relative"; }

        private TMP_Text display;
        private float accRating, passRating, techRating;
        private float[] best; //pass, acc, tech, total, replay pass rating, replay acc rating, replay tech rating, current score, current combo
        private Replay bestReplay;
        private NoteEvent[] noteArray;
        private int precision;
        #region Init
        public RelativeCounter(TMP_Text display, float accRating, float passRating, float techRating)
        {
            this.accRating = accRating;
            this.passRating = passRating;
            this.techRating = techRating;
            this.display = display;
            precision = PluginConfig.Instance.DecimalPrecision;
        }
        public RelativeCounter(TMP_Text display, MapSelection map) : this(display, map.AccRating, map.PassRating, map.TechRating) { SetupData(TheCounter.userID, map); }
        private void SetupReplayData(string data)
        {
            Plugin.Log.Debug(data);
            string replay = new Regex(@"(?<=replay.:.)[^,]+(?=.,)").Match(data).Value;
            ReplayDecoder.TryDecodeReplay(RequestByteData(replay), out bestReplay);
            noteArray = bestReplay.notes.ToArray();
            string hold = bestReplay.info.modifiers.ToLower();
            string mod = hold.Contains("fs") ? "fs" : hold.Contains("sf") ? "sf" : hold.Contains("ss") ? "ss" : "";
            string[] prefix = new string[] { "p", "a", "t" };
            if (mod.Length > 0)
                for (int i = 0; i < prefix.Length; i++)
                    prefix[i] = mod + prefix[i].ToUpper();
            string pass = prefix[0] + "assRating", acc = prefix[1] + "ccRating", tech = prefix[2] + "echRating";
            best[4] = float.Parse(new Regex($@"(?<={pass}..)[0-9\.]+").Match(data).Value);
            best[5] = float.Parse(new Regex($@"(?<={acc}..)[0-9\.]+").Match(data).Value);
            best[6] = float.Parse(new Regex($@"(?<={tech}..)[0-9\.]+").Match(data).Value);
        }
        #endregion
        #region Overrides
        public void SetupData(string id, MapSelection map)
        {
            try
            {
                string playerData = RequestScore(id, map.Map.hash, map.Difficulty, map.Mode);
                if (playerData != null && playerData.Length > 0)
                {
                    best = new float[9];
                    SetupReplayData(playerData);
                    playerData = new Regex(@"(?<=contextExtensions..\[)[^\[\]]+").Match(playerData).Value;
                    playerData = new Regex(@"{.+?(?=..scoreImprovement)").Matches(playerData)[0].Value;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("There was an error loading the replay of the player, most likely they have never played the map before.");
                Plugin.Log.Debug(e);
                PluginConfig.Instance.PPType = "Normal";
            }
            if (best != null && best.Length >= 9)
                best[7] = best[8] = 0;
        }
        public void ReinitCounter(TMP_Text display)
        {
            this.display = display;
            if (best != null && best.Length >= 9)
                best[7] = best[8] = 0;
        }
        public void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating)
        {
            this.display = display;
            this.passRating = passRating;
            this.accRating = accRating;
            this.techRating = techRating;
            precision = PluginConfig.Instance.DecimalPrecision;
            if (best != null && best.Length >= 9)
                best[7] = best[8] = 0;
        }
        public void ReinitCounter(TMP_Text display, MapSelection map)
        { this.display = display; SetupData(TheCounter.userID, map); }
        #endregion

        #region API Calls
        private byte[] RequestByteData(string path)
        {
            try
            {
                HttpResponseMessage hrm = client.GetAsync(new Uri(path)).Result;
                hrm.EnsureSuccessStatusCode();
                return hrm.Content.ReadAsByteArrayAsync().Result;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed!\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                return null;
            }
        }
        private string RequestScore(string id, string hash, string diff, string mode)
        {
            return RequestData($"https://api.beatleader.xyz/score/8/{id}/{hash}/{diff}/{mode}");
        }
        private string RequestData(string path)
        {
            try
            {
                return client.GetStringAsync(new Uri(path)).Result;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed!\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                return "";
            }
        }
        #endregion
        #region Updates
        private void UpdateBest(int notes)
        {
            if (notes < 1) return;
            NoteEvent note = noteArray[notes-1];
            if (note.eventType == NoteEventType.good)
            {
                best[8]++;
                best[7] += BLCalc.GetCutScore(note.noteCutInfo) * HelpfulMath.MultiplierForNote((int)Math.Round(best[8]));
            }
            else
            {
                best[8] = HelpfulMath.DecreaseMultiplier((int)Math.Round(best[8]));
            }

            (best[0], best[1], best[2]) = BLCalc.GetPp(best[7] / HelpfulMath.MaxScoreForNotes(notes), best[5], best[4], best[6]);
            best[3] = BLCalc.Inflate(best[0] + best[1] + best[2]);
        }
        public void UpdateCounter(float acc, int notes, int badNotes, int fcScore)
        {
            bool displayFc = PluginConfig.Instance.PPFC && badNotes > 0, showLbl = PluginConfig.Instance.ShowLbl, normal = PluginConfig.Instance.RelativeWithNormal;
            UpdateBest(notes);
            float[] ppVals = new float[displayFc ? 16 : 8];
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            for (int i = 0; i < 4; i++)
                ppVals[i + 4] = ppVals[i] - best[i];
            if (displayFc)
            {
                float fcAcc = fcScore / (float)HelpfulMath.MaxScoreForNotes(notes);
                if (float.IsNaN(fcAcc)) fcAcc = 1;
                (ppVals[8], ppVals[9], ppVals[10]) = BLCalc.GetPp(fcAcc, accRating, passRating, techRating);
                ppVals[11] = BLCalc.Inflate(ppVals[8] + ppVals[9] + ppVals[10]);
                for (int i = 8; i < 12; i++)
                    ppVals[i + 4] = ppVals[i] - best[i - 8];
            }
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = (float)Math.Round(ppVals[i], precision);
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };
            string target = PluginConfig.Instance.ShowEnemy ? PluginConfig.Instance.Target : "None";
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
                if (!target.Equals("None"))
                    display.text += $"Targeting <color=\"red\">{target}</color>";
            }
            else
            {
                if (displayFc)
                    display.text = (ppVals[7] > 0 ? "<color=\"green\">+" : ppVals[7] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[7]}</color> " + (normal ? $"({ppVals[3]}) / " : "/ ") +
                        (ppVals[15] > 0 ? "<color=\"green\">+" : ppVals[15] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[15]}</color>" + (normal ? $" ({ppVals[11]})" : "") + (showLbl ? " " + labels[3] : "");
                else
                    display.text = (ppVals[7] > 0 ? "<color=\"green\">+" : ppVals[7] == 0 ? "<color=\"yellow\">" : "<color=\"red\">") + $"{ppVals[7]}</color>" + (normal ? $" ({ppVals[3]})" : "") + (showLbl ? " " + labels[3] : "");
                if (!target.Equals("None"))
                    display.text += $"\nTargeting <color=\"red\">{target}</color>";
            }

        }
        #endregion
    }
}
