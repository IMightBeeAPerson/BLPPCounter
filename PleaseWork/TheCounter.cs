using System;
using System.Text.RegularExpressions;
using System.IO;
using CountersPlus.Counters.Custom;
using CountersPlus.Counters.Interfaces;
using TMPro;
using Zenject;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Policy;
using System.Linq;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private PlayerDataModel pdm;
        [Inject] private ScoreController sc;
        [Inject] private RelativeScoreAndImmediateRankCounter rsirc;
        private static readonly HttpClient client = new HttpClient();
        private TMP_Text display;
        private bool dataLoaded = false, enabled;
        private Dictionary<string, Map> data;
        private float passRating, accRating, techRating, stars;
        private int totalNotes, notes, badNotes;
        private int precision, highScore, fcScore, totalHitscore;



        public override void CounterDestroy() {
            if (enabled)
            {
                sc.scoreDidChangeEvent -= OnScoreChange;
                if (PluginConfig.Instance.PPFC)
                    sc.scoringForNoteFinishedEvent -= OnNoteScored;
            }
        }
        public override void CounterInit()
        {
            highScore = pdm.playerData.GetPlayerLevelStatsData(beatmap).highScore;
            notes = fcScore = badNotes = totalHitscore = 0;
            enabled = false;
            precision = PluginConfig.Instance.DecimalPrecision;
            if (!dataLoaded)
            {
                data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0,0,3);
                InitData();
            }
            try
            {
                enabled = dataLoaded ? SetupMapData() : SetupMapData(RequestHashData());
                if (enabled)
                {
                    display = CanvasUtility.CreateTextFromSettings(Settings);
                    display.text = "Loading...";
                    display.fontSize = (float)PluginConfig.Instance.FontSize;
                    if (PluginConfig.Instance.PPFC)
                        sc.scoringForNoteFinishedEvent += OnNoteScored;
                    UpdateText(1);
                    sc.scoreDidChangeEvent += OnScoreChange;
                } else
                {
                    Plugin.Log.Warn("Maps failed to load, most likely unranked.");
                }
            } catch (Exception e)
            {
                Plugin.Log.Warn($"Map data failed to be parsed: {e.Message}");
                Plugin.Log.Debug(e);
                enabled = false;
                if (display != null)
                    display.text = "";
            }
        }

        private void OnNoteScored(ScoringElement scoringElement)
        {
            notes++;
            if (scoringElement is GoodCutScoringElement goodCut && goodCut.noteData.scoringType == NoteData.ScoringType.Normal)
            {
                fcScore += goodCut.cutScore * HelpfulMath.MultiplierForNote(notes);
                totalHitscore += goodCut.cutScore;
            }
            else
            {
                badNotes++;
                fcScore += (int)Math.Round(totalHitscore / (double)(notes - badNotes)) * HelpfulMath.MultiplierForNote(notes);
            }
            
        }
        private void OnScoreChange(int score, int modifiedScore)
        {
            UpdateText(rsirc.relativeScore);
        }
        private string RequestHashData()
        {
            string path = HelpfulPaths.BLAPI_HASH + beatmap.level.levelID.Split('_')[2].ToUpper();
            Plugin.Log.Info(path);
            return RequestData(path);
        }
        private string RequestData(string path)
        {
            try
            {
                return client.GetStringAsync(new Uri(path)).Result;
            } catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed!\nPath: {path}\nError: {e.Message}");
                return "";
            }
        }
        private void InitData()
        {
            dataLoaded = false;
            if (File.Exists(HelpfulPaths.BL_CACHE_FILE))
            {
                try
                {
                    string data = File.ReadAllText(HelpfulPaths.BL_CACHE_FILE);
                    MatchCollection matches = new Regex(@"(?={.LeaderboardId[^}]+)({[^{}]+}*[^{}]+)+}").Matches(data);
                    foreach (Match m in matches)
                    {
                        Map map = new Map(new Regex(@"(?<=hash...)[A-z0-9]+").Match(m.Value).Value.ToUpper(), m.Value);
                        if (this.data.ContainsKey(map.hash))
                            this.data[map.hash].Combine(map);
                        else this.data[map.hash] = map;
                    }
                    dataLoaded = true;
                }
                catch (Exception e)
                {
                    Plugin.Log.Warn("Error loading bl cashe file: " + e.Message);
                    Plugin.Log.Debug(e);
                }
            }
            
        }
        private bool SetupMapData()
        {
            string data = "";
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper();
            try
            {
                Dictionary<string, string> hold = this.data[hash].Get(beatmap.difficulty.Name().Replace("+", "Plus"));
                if (hold.Keys.Count == 1)
                    data = hold[hold.Keys.First()];
                else data = hold["Standard"];
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Data length: {this.data.Count}");
                Plugin.Log.Warn("Level doesn't exist for some reason :(\nHash: " + hash);
                Plugin.Log.Debug(e);
                return false;
            }
            Plugin.Log.Info("Map Hash: " + hash);
            return SetupMapData(data);
        }
        private bool SetupMapData(string data)
        {
            if (data.Length <= 0) return false;
            string mode = new Regex("(?<=modeName...)[A-z]+").Match(data).Value;
            totalNotes = HelpfulMath.NotesForMaxScore(int.Parse(new Regex(@"(?<=maxScore..)[0-9]+").Match(data).Value));
            string[] prefix = new string[] { "p", "a", "t", "s" };
            string mod = mods.songSpeedMul > 1.0 ? mods.songSpeedMul >= 1.5 ? "sf" : "fs" : mods.songSpeedMul != 1.0 ? "ss" : "";
            if (mod.Length > 0)
                for (int i = 0; i < prefix.Length; i++)
                    prefix[i] = mod + prefix[i].ToUpper();
            string pass = prefix[0] + "assRating", acc = prefix[1] + "ccRating", tech = prefix[2] + "echRating", star = prefix[3] + "tars";
            passRating = float.Parse(new Regex($@"(?<={pass}..)[0-9\.]+").Match(data).Value);
            accRating = float.Parse(new Regex($@"(?<={acc}..)[0-9\.]+").Match(data).Value);
            techRating = float.Parse(new Regex($@"(?<={tech}..)[0-9\.]+").Match(data).Value);
            stars = float.Parse(new Regex($@"(?<={star}..)[0-9\.]+").Match(data).Value);
            mod = mod.ToUpper();
            Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {stars}\n{mod} Pass Rating: {passRating}\n{mod} Acc Rating: {accRating}\n{mod} Tech Rating: {techRating}" : $"Stars: {stars}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            return stars > 0;
        }
        private void UpdateText(float acc)
        {
            bool[] settings = { PluginConfig.Instance.ProgressPP, PluginConfig.Instance.SplitPPVals, PluginConfig.Instance.ShowLbl, PluginConfig.Instance.PPFC && badNotes > 0 };
            float[] ppVals = new float[settings[3] ? 8 : 4];
            (ppVals[0], ppVals[1], ppVals[2]) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            ppVals[3] = BLCalc.Inflate(ppVals[0] + ppVals[1] + ppVals[2]);
            if (settings[3])
            {
                float fcAcc = fcScore / (float)HelpfulMath.MaxScoreForNotes(notes);
                if (float.IsNaN(fcAcc)) fcAcc = 1;
                (ppVals[4], ppVals[5], ppVals[6]) = BLCalc.GetPp(fcAcc, accRating, passRating, techRating);
                ppVals[7] = BLCalc.Inflate(ppVals[4] + ppVals[5] + ppVals[6]);
            }
            if (settings[0])
            {
                float mult = notes / (float)totalNotes;
                mult = Math.Min(1, mult);
                for (int i = 0; i < ppVals.Length; i++)
                    ppVals[i] = (float)Math.Round(ppVals[i] * mult, precision);
            } else
                for (int i = 0; i < ppVals.Length; i++)
                    ppVals[i] = (float)Math.Round(ppVals[i], precision);
            if (settings[1])
            {
                display.text = settings[3] ?
                    settings[2] ? $"{ppVals[0]}/{ppVals[4]} Pass PP\n{ppVals[1]}/{ppVals[5]} Acc PP\n{ppVals[2]}/{ppVals[6]} Tech PP\n{ppVals[3]}/{ppVals[7]} PP" : 
                    $"{ppVals[0]}/{ppVals[4]}\n{ppVals[1]}/{ppVals[5]}\n{ppVals[2]}/{ppVals[6]}\n{ppVals[3]}/{ppVals[7]}" :
                    settings[2] ? $"{ppVals[0]} Pass PP\n{ppVals[1]} Acc PP\n{ppVals[2]} Tech PP\n{ppVals[3]} PP" : 
                    $"{ppVals[0]}\n{ppVals[1]}\n{ppVals[2]}\n{ppVals[3]}";
            }
            else 
                display.text = settings[3] ?
                    settings[2] ? $"{ppVals[3]}/{ppVals[7]} PP" : $"{ppVals[3]}/{ppVals[7]}" :
                    settings[2] ? $"{ppVals[3]} PP" : $"{ppVals[3]}";
        }
        
    }
}