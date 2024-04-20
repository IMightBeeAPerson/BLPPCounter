using System;
using System.Text.RegularExpressions;
using System.IO;
using CountersPlus.Counters.Custom;
using CountersPlus.Counters.Interfaces;
using TMPro;
using Zenject;
using PleaseWork.CalculatorStuffs;
using PleaseWork.Settings;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter, INoteEventHandler
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private RelativeScoreAndImmediateRankCounter rsirc;
        private static readonly string BL_CACHE_FILE = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatLeader", "LeaderboardsCache");
        //https://github.com/PulseLane/PPCounter/blob/master/PPCounter/Data/BeatLeaderData.cs#L67
        private TMP_Text display;
        private bool isRanked;
        private LeaderboardContexts context;
        private float passRating, accRating, techRating;

        public override void CounterDestroy() { }
        public override void CounterInit()
        {
            display = CanvasUtility.CreateTextFromSettings(Settings);
            display.text = "Loading...";
            display.fontSize = 3;
            InitData();
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
            string id = beatmap.level.levelID.Split('_')[2].ToLower();
            string data = "";
            if (File.Exists(BL_CACHE_FILE))
            {
                try
                {
                    data = File.ReadAllText(BL_CACHE_FILE);
                    data = new Regex("(?={.LeaderboardId[^}]+"+id+")({[^{}]+}*[^{}]+)+}").Match(data).Captures[0].Value;
                    
                } catch (Exception e)
                {
                    Plugin.Log.Error("error loading bl cashe file" + e);
                    return;
                }
            }
            isRanked = int.Parse(new Regex("(?<=status..).").Match(data).Captures[0].Value) == 3;
            string context = new Regex("(?<=modeName...)[A-z]+").Match(data).Captures[0].Value;
            switch (context)
            {
                case "Standard": this.context = LeaderboardContexts.Standard; break;
                case "Golf": this.context = LeaderboardContexts.Golf; break;
                default: this.context = LeaderboardContexts.None; break;
            }
            string[] prefix = new string[] { "p", "a", "t" };
            string mod = mods.songSpeedMul > 1.0 ? mods.songSpeedMul > 1.2 ? "sf" : "fs" : mods.songSpeedMul != 1.0 ? "ss" : "";
            for (int i = 0; i < prefix.Length; i++)
                if (mod.Length > 0)
                    prefix[i] = mod + prefix[i].ToUpper();
            string pass = prefix[0] + "assRating", acc = prefix[1] + "ccRating", tech = prefix[2] + "echRating";
            passRating = float.Parse(new Regex(@"(?<=" + pass + @"..)[0-9\.]+").Match(data).Captures[0].Value);
            accRating = float.Parse(new Regex(@"(?<=" + acc + @"..)[0-9\.]+").Match(data).Captures[0].Value);
            techRating = float.Parse(new Regex(@"(?<=" + tech + @"..)[0-9\.]+").Match(data).Captures[0].Value);
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