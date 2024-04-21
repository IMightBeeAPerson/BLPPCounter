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
        private float passRating, accRating, techRating;

        public override void CounterDestroy() { }
        public override void CounterInit()
        {
            display = CanvasUtility.CreateTextFromSettings(Settings);
            display.text = "Loading...";
            display.fontSize = 3;
            if (!dataLoaded)
            {
                data = new Dictionary<string, string>();
                InitData();
            }
            SetupMapData();
        }

        public void OnNoteCut(NoteData data, NoteCutInfo info)
        {
            UpdateText(rsirc.relativeScore);
        }

        public void OnNoteMiss(NoteData data)
        {
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
            try
            {
                data = this.data[beatmap.level.levelID.Split('_')[2].ToUpper() + "_" + beatmap.difficulty.Name().Replace("+", "Plus")];
            } catch (KeyNotFoundException)
            {
                Plugin.Log.Info($"Data length: {this.data.Count}");
                Plugin.Log.Error("Level doesn't exist for some reason :(\nHash: " + beatmap.level.levelID.Split('_')[2].ToUpper() + "_" + beatmap.difficulty.Name().Replace("+","Plus"));
                return;
            }
            //isRanked = int.Parse(new Regex("(?<=status..).").Match(data).Captures[0].Value) == 3;
            string context = new Regex("(?<=modeName...)[A-z]+").Match(data).Captures[0].Value;
            switch (context)
            {
                case "Standard": this.context = LeaderboardContexts.Standard; break;
                case "Golf": this.context = LeaderboardContexts.Golf; break;
                default: this.context = LeaderboardContexts.None; break;
            }
            string[] prefix = new string[] { "p", "a", "t" };
            string mod = mods.songSpeedMul > 1.0 ? mods.songSpeedMul >= 1.5 ? "sf" : "fs" : mods.songSpeedMul != 1.0 ? "ss" : "";
            for (int i = 0; i < prefix.Length; i++)
                if (mod.Length > 0)
                    prefix[i] = mod + prefix[i].ToUpper();
            string pass = prefix[0] + "assRating", acc = prefix[1] + "ccRating", tech = prefix[2] + "echRating";
            passRating = float.Parse(new Regex(@"(?<=" + pass + @"..)[0-9\.]+").Match(data).Captures[0].Value);
            accRating = float.Parse(new Regex(@"(?<=" + acc + @"..)[0-9\.]+").Match(data).Captures[0].Value);
            techRating = float.Parse(new Regex(@"(?<=" + tech + @"..)[0-9\.]+").Match(data).Captures[0].Value);
            Plugin.Log.Info($"Pass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            UpdateText(1);
        }
        private void UpdateText(float acc)
        {
            float passVal, accVal, techVal;
            (passVal, accVal, techVal) = BLCalc.GetPp(context, acc, accRating, passRating, techRating);
            float pp = BLCalc.Inflate(passVal + techVal + accVal);
            pp = (float)Math.Round(pp,2);
            if (PluginConfig.Instance.splitPPVals)
            {
                passVal = (float)Math.Round(passVal, 2);
                accVal = (float)Math.Round(accVal, 2);
                techVal = (float)Math.Round(techVal, 2);
                display.text = $"{passVal} Pass PP\n{accVal} Acc PP\n{techVal} Tech PP\n{pp} PP";
            }
            else 
                display.text = $"{pp} PP";
        }
    }
}