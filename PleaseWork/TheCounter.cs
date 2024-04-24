using System;
using System.Text.RegularExpressions;
using System.IO;
using CountersPlus.Counters.Custom;
using CountersPlus.Counters.Interfaces;
using TMPro;
using Zenject;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
using System.Collections.Generic;
using System.Net.Http;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter, INoteEventHandler
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private RelativeScoreAndImmediateRankCounter rsirc;
        private static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        private static readonly string BLAPI_HASH = "http://api.beatleader.xyz/leaderboards/hash/";
        private static readonly HttpClient client = new HttpClient();
        private TMP_Text display;
        private bool dataLoaded = false, enabled;
        private Dictionary<string, Map> data;
        private float passRating, accRating, techRating, stars;
        private float totalNotes, notes;
        

        public override void CounterDestroy() { }
        public override void CounterInit()
        {
            notes = 0;
            enabled = false;
            if (!dataLoaded)
            {
                data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0,0,3);
                InitData();
            }
            try
            {
                enabled = dataLoaded ? SetupMapData() : SetupMapData(RequestData());
                if (enabled)
                {
                    display = CanvasUtility.CreateTextFromSettings(Settings);
                    display.text = "Loading...";
                    display.fontSize = 3;
                    UpdateText(1);
                } else
                {
                    Plugin.Log.Warn("Maps failed to load, most likely unranked.");
                }
            } catch (Exception ex)
            {
                Plugin.Log.Warn($"Map data failed to be parsed: {ex.Message}");
                enabled = false;
            }
        }

        public void OnNoteCut(NoteData data, NoteCutInfo info)
        {
            if (enabled) {
                notes++;
                UpdateText(rsirc.relativeScore);
            }
        }

        public void OnNoteMiss(NoteData data)
        {
            if (enabled)
                notes++;
        }
        private string RequestData()
        {
            try
            {
                string hash = beatmap.level.levelID.Split('_')[2].ToUpper();
                Plugin.Log.Info(BLAPI_HASH + hash);
                return client.GetStringAsync(new Uri(BLAPI_HASH + hash)).Result;
            } catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed!\nError: {e.Message}");
                return "";
            }
        }
        private void InitData()
        {
            dataLoaded = false;
            if (File.Exists(BL_CACHE_FILE))
            {
                try
                {
                    string data = File.ReadAllText(BL_CACHE_FILE);
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
                    //Plugin.Log.Error(e);
                }
            }
            
        }
        private bool SetupMapData()
        {
            string data = "";
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper();// + "_" + beatmap.difficulty.Name().Replace("+", "Plus");
            try
            {
                Dictionary<string, string> hold = this.data[hash].Get(beatmap.difficulty.Name().Replace("+", "Plus"));
                if (hold.Keys.Count == 1)
                    foreach (string s in hold.Keys)
                        data = hold[s]; //dumbest way to access a value
                else data = hold["Standard"];
            }
            catch (Exception)
            {
                Plugin.Log.Info($"Data length: {this.data.Count}");
                Plugin.Log.Warn("Level doesn't exist for some reason :(\nHash: " + hash);
                //Plugin.Log.Critical(e);
                return false;
            }
            Plugin.Log.Info("Map Hash: " + hash);
            //Plugin.Log.Info($"Data: {data}");
            /*if (int.Parse(new Regex("(?<=status..).").Match(data).Captures[0].Value) != 3)
                return false;*/
            return SetupMapData(data);
        }
        private bool SetupMapData(string data)
        {
            if (data.Length <= 0) return false;
            string mode = new Regex("(?<=modeName...)[A-z]+").Match(data).Value;
            totalNotes = NotesForMaxScore(int.Parse(new Regex(@"(?<=maxScore..)[0-9]+").Match(data).Value));
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
            float passVal, accVal, techVal;
            (passVal, accVal, techVal) = BLCalc.GetPp(acc, accRating, passRating, techRating);
            float pp = BLCalc.Inflate(passVal + techVal + accVal);
            if (PluginConfig.Instance.ProgressPP)
            {
                float mult = notes / totalNotes;
                mult = Math.Min(1, mult);
                passVal *= mult;
                accVal *= mult;
                techVal *= mult;
                pp *= mult;
            }
            pp = (float)Math.Round(pp, 2);
            if (PluginConfig.Instance.SplitPPVals)
            {
                passVal = (float)Math.Round(passVal, 2);
                accVal = (float)Math.Round(accVal, 2);
                techVal = (float)Math.Round(techVal, 2);
                display.text = $"{passVal} Pass PP\n{accVal} Acc PP\n{techVal} Tech PP\n{pp} PP";
            }
            else 
                display.text = $"{pp} PP";
        }
        private int MaxScoreForNotes(int notes)
        {
            if (notes <= 0) return 0;
            if (notes == 1) return 115;
            if (notes < 6) return 115 * (notes * 2 - 1);
            if (notes < 14) return 115 * ((notes - 5) * 4 + 9);
            return 115 * (notes - 14) * 8 + 5635;
        }
        private int NotesForMaxScore(int score)
        {
            if (score <= 0) return 0;
            if (score == 115) return 1;
            if (score < 1495) return (score / 115 + 1) / 2;
            if (score < 5635) return (score / 115 - 9) / 4 + 5;
            return (score - 5635) / 920 + 14;
        }
    }
}