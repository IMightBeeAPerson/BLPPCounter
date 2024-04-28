using System;
using System.Text.RegularExpressions;
using System.IO;
using CountersPlus.Counters.Custom;
using TMPro;
using Zenject;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using PleaseWork.Utils;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using BeatLeader.Models.Replay;
using UnityEngine.UIElements;
using System.Runtime.CompilerServices;
using PleaseWork.Counters;
using System.Threading.Tasks;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private ScoreController sc;
        private static readonly HttpClient client = new HttpClient();
        private static Dictionary<string, Map> data;
        private static bool dataLoaded = false;
        private TMP_Text display;
        private bool enabled;
        private float passRating, accRating, techRating, stars;
        private int totalNotes, notes, badNotes;
        private int fcScore, totalHitscore;
        private string userID, mode, ppMode;
        private string tempData;
        private IMyCounters theCounter;

        #region Overrides & Event Calls

        public override void CounterDestroy() {
            if (enabled)
            {
                theCounter = null;
                sc.scoreDidChangeEvent -= OnScoreChange;
                if (PluginConfig.Instance.PPFC)
                    sc.scoringForNoteFinishedEvent -= OnNoteScored;
            }
            PluginConfig.Instance.PPType = ppMode;
        }
        public override void CounterInit()
        {
            //highScore = pdm.playerData.GetPlayerLevelStatsData(beatmap).highScore;
            ppMode = PluginConfig.Instance.PPType;
            notes = fcScore = badNotes = totalHitscore = 0;
            enabled = false;
            if (!dataLoaded)
            {
                data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0, 0, 3);
                userID = PluginConfig.Instance.Target.Equals("None") ? Targeter.playerID : Targeter.GetTargetId();
                InitData();
            }
            try
            {
                enabled = dataLoaded ? SetupMapData() : SetupMapData(RequestHashData());
                if (enabled)
                {
                    display = CanvasUtility.CreateTextFromSettings(Settings);
                    display.fontSize = (float)PluginConfig.Instance.FontSize;
                    display.text = "";
                    if (PluginConfig.Instance.PPFC)
                        sc.scoringForNoteFinishedEvent += OnNoteScored;
                    sc.scoreDidChangeEvent += OnScoreChange;
                    if (!InitCounter()) throw new Exception("Counter somehow failed to init. Weedoo weedoo weedoo weedoo.");
                    if (userID == null || userID.Length <= 0) userID = PluginConfig.Instance.Target.Equals("None") ? Targeter.playerID : Targeter.GetTargetId();
                    Plugin.Log.Debug(userID);
                    theCounter.SetupData(userID, beatmap.level.levelID.Split('_')[2], beatmap.difficulty.Name().Replace("+", "Plus"), mode, tempData);
                    tempData = "";
                    theCounter.UpdateCounter(1, 0, 0, 0);
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
            theCounter.UpdateCounter(score / (float)HelpfulMath.MaxScoreForNotes(notes), notes, badNotes, fcScore);

        }
        #endregion
        #region API Calls
        
        
        private string RequestHashData()
        {
            string path = HelpfulPaths.BLAPI_HASH + beatmap.level.levelID.Split('_')[2].ToUpper();
            Plugin.Log.Info(path);
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
        #region Init
        private bool InitCounter()
        {
            switch (PluginConfig.Instance.PPType)
            {
                case "Relative":
                case "Relative w/ normal":
                    theCounter = new RelativeCounter(display, accRating, passRating, techRating); break;
                case "Progressive":
                    theCounter = new ProgressCounter(display, accRating, passRating, techRating, totalNotes); break;
                case "Normal":
                    theCounter = new NormalCounter(display, accRating, passRating, techRating); break;
                case "Clan PP":
                case "Clan w/ normal":
                    theCounter = new ClanCounter(display, accRating, passRating, techRating); break;
                default: return false;
            }
            return true;
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
                        if (TheCounter.data.ContainsKey(map.hash))
                            TheCounter.data[map.hash].Combine(map);
                        else TheCounter.data[map.hash] = map;
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
            string data;
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper();
            try
            {
                Dictionary<string, string> hold = TheCounter.data[hash].Get(beatmap.difficulty.Name().Replace("+", "Plus"));
                mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                data = hold[mode];
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Data length: {TheCounter.data.Count}");
                Plugin.Log.Warn("Level doesn't exist for some reason :(\nHash: " + hash);
                Plugin.Log.Debug(e);
                return false;
            }
            //if (PluginConfig.Instance.Relative || PluginConfig.Instance.RelativeWithNormal)
                
            Plugin.Log.Info("Map Hash: " + hash);
            return SetupMapData(data);
        }
        private bool SetupMapData(string data)
        {
            if (data.Length <= 0) return false;
            tempData = data;
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
        #endregion
        #region Updates
        
        public static void UpdateText(bool displayFc, TMP_Text display, float[] ppVals)
        {
            bool showLbl = PluginConfig.Instance.ShowLbl;
            if (PluginConfig.Instance.SplitPPVals)
            {
                display.text = displayFc ?
                    showLbl ? $"{ppVals[0]}/{ppVals[4]} Pass PP\n{ppVals[1]}/{ppVals[5]} Acc PP\n{ppVals[2]}/{ppVals[6]} Tech PP\n{ppVals[3]}/{ppVals[7]} PP" :
                    $"{ppVals[0]}/{ppVals[4]}\n{ppVals[1]}/{ppVals[5]}\n{ppVals[2]}/{ppVals[6]}\n{ppVals[3]}/{ppVals[7]}" :
                    showLbl ? $"{ppVals[0]} Pass PP\n{ppVals[1]} Acc PP\n{ppVals[2]} Tech PP\n{ppVals[3]} PP" :
                    $"{ppVals[0]}\n{ppVals[1]}\n{ppVals[2]}\n{ppVals[3]}";
            }
            else
                display.text = displayFc ?
                    showLbl ? $"{ppVals[3]}/{ppVals[7]} PP" : $"{ppVals[3]}/{ppVals[7]}" :
                    showLbl ? $"{ppVals[3]} PP" : $"{ppVals[3]}";
            string target = PluginConfig.Instance.Target;
            if (!target.Equals("None"))
                display.text += $"\nTargeting <color=\"red\">{target}</color>";
        }
        #endregion
    }
}