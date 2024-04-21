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
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter, INoteEventHandler
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private RelativeScoreAndImmediateRankCounter rsirc;
        private static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        private TMP_Text display;
        private bool dataLoaded = false;
        private Dictionary<string, string> data;
        private LeaderboardContexts context;
        private float passRating, accRating, techRating, stars;
        private float totalNotes, notes;

        public override void CounterDestroy() { }
        public override void CounterInit()
        {
            display = CanvasUtility.CreateTextFromSettings(Settings);
            display.text = "Loading...";
            display.fontSize = 3;
            notes = 0;
            if (!dataLoaded)
            {
                data = new Dictionary<string, string>();
                InitData();
            }
            SetupMapData();
        }

        public void OnNoteCut(NoteData data, NoteCutInfo info)
        {
            notes++;
            UpdateText(rsirc.relativeScore);
        }

        public void OnNoteMiss(NoteData data)
        {
            notes++;
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
                        this.data[new Regex(@"(?<=hash...)[A-z0-9]+").Match(m.Value).Value.ToUpper() + "_" + new Regex(@"(?<=difficultyName...)[A-z0-9]+").Match(m.Value).Value] = m.Value;
                    }
                    dataLoaded = true;
                } catch (Exception e)
                {
                    Plugin.Log.Error("Error loading bl cashe file: " + e.Message);
                }
            }
            
        }
        private void SetupMapData()
        {
            string data;
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper() + "_" + beatmap.difficulty.Name().Replace("+", "Plus");
            try
            {
                data = this.data[hash];
            } catch (KeyNotFoundException)
            {
                Plugin.Log.Info($"Data length: {this.data.Count}");
                Plugin.Log.Error("Level doesn't exist for some reason :(\nHash: " + hash);
                return;
            }
            Plugin.Log.Info("Map Hash: " + hash);
            //isRanked = int.Parse(new Regex("(?<=status..).").Match(data).Captures[0].Value) == 3;
            string context = new Regex("(?<=modeName...)[A-z]+").Match(data).Captures[0].Value;
            switch (context)
            {
                case "Standard": this.context = LeaderboardContexts.Standard; break;
                case "Golf": this.context = LeaderboardContexts.Golf; break;
                default: this.context = LeaderboardContexts.None; break;
            }
            totalNotes = NotesForMaxScore(int.Parse(new Regex(@"(?<=maxScore..)[0-9]+").Match(data).Value));
            string[] prefix = new string[] { "p", "a", "t", "s" };
            string mod = mods.songSpeedMul > 1.0 ? mods.songSpeedMul >= 1.5 ? "sf" : "fs" : mods.songSpeedMul != 1.0 ? "ss" : "";
            for (int i = 0; i < prefix.Length; i++)
                if (mod.Length > 0)
                    prefix[i] = mod + prefix[i].ToUpper();
            string pass = prefix[0] + "assRating", acc = prefix[1] + "ccRating", tech = prefix[2] + "echRating", star = prefix[3] + "tars";
            passRating = float.Parse(new Regex(@"(?<=" + pass + @"..)[0-9\.]+").Match(data).Value);
            accRating = float.Parse(new Regex(@"(?<=" + acc + @"..)[0-9\.]+").Match(data).Value);
            techRating = float.Parse(new Regex(@"(?<=" + tech + @"..)[0-9\.]+").Match(data).Value);
            stars = float.Parse(new Regex(@"(?<=" + star + @"..)[0-9\.]+").Match(data).Value);
            mod = mod.ToUpper();
            Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {stars}\n{mod} Pass Rating: {passRating}\n{mod} Acc Rating: {accRating}\n{mod} Tech Rating: {techRating}" : $"Stars {stars}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            UpdateText(1);
        }
        private void UpdateText(float acc)
        {
            float passVal, accVal, techVal;
            (passVal, accVal, techVal) = BLCalc.GetPp(context, acc, accRating, passRating, techRating);
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