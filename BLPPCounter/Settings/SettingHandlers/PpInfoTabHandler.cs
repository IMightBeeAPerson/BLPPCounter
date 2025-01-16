using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Newtonsoft.Json.Linq;
using BLPPCounter.Counters;
using UnityEngine;
using BLPPCounter.CalculatorStuffs;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class PpInfoTabHandler
    {
#pragma warning disable IDE0051, IDE0044
        #region Misc Static Variables & Injects
        internal StandardLevelDetailViewController Sldvc;
        internal GameplayModifiersPanelController Gmpc;
        public static PpInfoTabHandler Instance { get; }
        private static PluginConfig PC => PluginConfig.Instance;
        #endregion
        #region Variables
        private string CurrentMods = "", UnformattedCurrentMods = "";
        private float CurrentModMultiplier = 1f;
        private BeatmapKey CurrentMap;
        private JToken CurrentDiff;
        #region Relative Counter
        private static Func<string, float, string, string> RelativeFormatter;

        private float TargetPP = 0;
        private bool TargetHasScore = false;
        #endregion
        #region Clan Counter
        private float PPToCapture = 0;
        #endregion
        #endregion
        #region UI Variables & components
        [UIComponent(nameof(RelativeText))] private TextMeshProUGUI RelativeText;
        [UIComponent(nameof(ClanTable))] private TextMeshProUGUI ClanTable;

        [UIComponent(nameof(AccStarText))] private TextMeshProUGUI AccStarText;
        [UIComponent(nameof(TechStarText))] private TextMeshProUGUI TechStarText;
        [UIComponent(nameof(PassStarText))] private TextMeshProUGUI PassStarText;
        [UIComponent(nameof(StarText))] private TextMeshProUGUI StarText;
        [UIComponent(nameof(SpeedModText))] private TextMeshProUGUI SpeedModText;
        [UIComponent(nameof(ModMultText))] private TextMeshProUGUI ModMultText;
        
        [UIComponent(nameof(MapName))] private TextMeshProUGUI MapName;
        [UIComponent(nameof(MapID))] private TextMeshProUGUI MapID;
        [UIValue(nameof(BeatmapName))] private string BeatmapName
        {
            get => _BeatmapName;
            set { if (MapName is null) return; MapName.text = $"<color=#777777>Map Name: <color=#aaaaaa>{value}</color>"; _BeatmapName = value; }
        }
        private string _BeatmapName = "";
        [UIValue(nameof(BeatmapID))] private string BeatmapID
        {
            get => _BeatmapID;
            set { if (MapID is null) return; MapID.text = $"<color=#777777>Map ID: <color=#aaaaaa>{value}</color>"; _BeatmapID = value; }
        }
        private string _BeatmapID = "";
        #endregion
        #region UI Functions
        [UIAction(nameof(Refresh))]
        private void ForceRefresh() { if (Sldvc != null && Gmpc != null) Refresh(true); }
        [UIAction(nameof(RefreshMods))]
        private void RefreshMods() { if (Sldvc != null && Gmpc != null) { UpdateMods(); UpdateInfo(); } }
        public void Refresh(bool forceRefresh = false)
        {
            if (!forceRefresh && Sldvc.beatmapKey.Equals(CurrentMap)) return;
            UpdateMods();
            TargetPP = UpdateTargetPP();
            UpdateInfo();
        }
        #endregion
        #region Inits
        static PpInfoTabHandler()
        {
            InitRelativeFormatter();
            Instance = new PpInfoTabHandler();
        }
        internal void SldvcInit() { Sldvc.didChangeContentEvent += (a, b) => Refresh(); Sldvc.didChangeDifficultyBeatmapEvent += a => Refresh(); }
        //internal void GmpcInit() { Gmpc.didChangeGameplayModifiersEvent += UpdateMods; UpdateMods(); }
        #endregion
        #region Formatting
        #region Relative Counter
        private static void InitRelativeFormatter()
        {
            var simple = HelpfulFormatter.GetBasicTokenParser(PC.MessageSettings.RelativeCalcInfo,
                new Dictionary<string, char>()
                {
                    {"Target", 't' },
                    {"Accuracy", 'a' },
                    {"Mods", 'm' }
                }, "RelativeCalc", null, (tokens, tokensCopy, priority, vals) => {
                    if (vals['m'] is null || (vals['m'] is string str && str.Length == 0)) HelpfulFormatter.SetText(tokensCopy, 'm');
                }, out _, false).Invoke();
            RelativeFormatter = (target, acc, mods) =>
            {
                Dictionary<char, object> vals = new Dictionary<char, object>()
                {
                    {'t', target }, {'a', acc }, {'m', mods }
                };
                return simple.Invoke(vals);
            };
        }
        private float GetAccToBeatTarget() =>
            CurrentDiff == null ? 0.0f : BLCalc.GetAcc(TargetPP, CurrentDiff, Gmpc.gameplayModifiers.songSpeed, CurrentModMultiplier, PC.DecimalPrecision);
        private float UpdateTargetPP()
        {
            Plugin.Log.Info("Content Changed");
            CurrentMap = Sldvc.beatmapKey;
            CurrentDiff = null;
            if (!Sldvc.beatmapLevel.levelID.Substring(0, 6).Equals("custom")) return 0.0f; //means the level selected is not custom
            string apiOutput = RelativeCounter.RequestScore(
                Targeter.TargetID,
                Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(),
                Sldvc.beatmapKey.difficulty.ToString().Replace("+", "Plus"),
                Sldvc.beatmapKey.beatmapCharacteristic.serializedName,
                true
                );
            TargetHasScore = !(apiOutput is null || apiOutput.Length == 0);
            if (!TargetHasScore) { UpdateDiff(); return 0.0f; } //target doesn't have a score on this diff.
            JToken targetScore = JToken.Parse(apiOutput);
            BeatmapID = targetScore["leaderboardId"].ToString();
            BeatmapName = targetScore["song"]["name"].ToString();
            float outp = (float)targetScore["pp"];
            if (Mathf.Approximately(outp, 0.0f)) //If the score set doesn't have a pp value, calculate one manually
            {
                int maxScore = (int)targetScore["difficulty"]["maxScore"], playerScore = (int)targetScore["modifiedScore"];
                targetScore = targetScore["difficulty"];
                outp = BLCalc.Inflate(BLCalc.GetPpSum((float)playerScore / maxScore, (float)targetScore["accRating"], (float)targetScore["passRating"], (float)targetScore["techRating"]));
            } else targetScore = targetScore["difficulty"];
            CurrentDiff = targetScore;
            if (UnformattedCurrentMods.Length > 0) CurrentModMultiplier = HelpfulPaths.GetMultiAmounts(CurrentDiff, UnformattedCurrentMods.Split(' '));
            return outp;
        }
        #endregion
        #region Clan Table
        private void BuildTable()
        {
            string[][] arr = HelpfulMisc.RowToColumn(new string[] { "Slower", "Normal", "Faster", "Super Fast" }, 2);
            float[] ratings = HelpfulPaths.GetAllRatings(CurrentDiff); //ss-sf, [acc, pass, tech, star]
            for (int i = 0; i < arr.Length; i++) 
                arr[i][1] = BLCalc.GetAcc(ratings[i * 4], ratings[i * 4 + 1], ratings[i * 4 + 2], PPToCapture, PC.DecimalPrecision) + "%";
            HelpfulMisc.SetupTable(ClanTable, 50, arr, "Speed", "Acc to Cap");
        }
        #endregion
        private static string Grammarize(string mods) //this is very needed :)
        {
            if (mods.Count(c => c == ',') < 2) return mods;
            return mods.Substring(0, mods.LastIndexOf(',')) + " and" + mods.Substring(mods.LastIndexOf(','));
        }
        private void UpdateMods() //this is why you use bitmasks instead of a billion bools vars
        {
            string newMods = "";
            UnformattedCurrentMods = "";
            GameplayModifiers mods = Gmpc.gameplayModifiers;
            switch (mods.songSpeed)
            {//Speed mods are not added to UnformattedCurrentMods because they are handled in a different way.
                case GameplayModifiers.SongSpeed.Slower: newMods += "Slower Song, "; break;
                case GameplayModifiers.SongSpeed.Faster: newMods += "Faster Song, "; break;
                case GameplayModifiers.SongSpeed.SuperFast: newMods += "Super Fast Song, "; break;
            }
            if (mods.ghostNotes) { UnformattedCurrentMods += "gn "; newMods += "Ghost Notes, "; }
            if (mods.disappearingArrows) { UnformattedCurrentMods += "da "; newMods += "Disappearing Arrows, "; }
            if (mods.energyType == GameplayModifiers.EnergyType.Battery) newMods += "Four Lifes, ";
            if (mods.noArrows) { UnformattedCurrentMods += "na "; newMods += "No Arrows, "; }
            if (mods.noFailOn0Energy) { UnformattedCurrentMods += "nf "; newMods += "No Fail, "; }
            if (mods.zenMode) newMods += "Zen Mode (why are you using zen mode), ";
            if (mods.instaFail) newMods += "One Life, ";
            if (mods.noBombs) { UnformattedCurrentMods += "nb "; newMods += "No Bombs, "; }
            if (mods.proMode) { UnformattedCurrentMods += "pm "; newMods += "Pro Mode, "; }
            if (mods.smallCubes) { UnformattedCurrentMods += "sc "; newMods += "Small Cubes, "; }
            if (mods.strictAngles) { UnformattedCurrentMods += "sa "; newMods += "Strict Angles, "; }
            if (mods.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) { UnformattedCurrentMods += "no "; newMods += "No Walls, "; }
            CurrentMods = newMods.Length > 1 ? Grammarize(newMods.Substring(0, newMods.Length - 2)) : null;
            if (UnformattedCurrentMods.Length > 0) UnformattedCurrentMods = UnformattedCurrentMods.Trim();
            if (CurrentDiff != null) CurrentModMultiplier = HelpfulPaths.GetMultiAmounts(CurrentDiff, UnformattedCurrentMods.Split(' '));
        }
        #endregion
        #region Misc Functions
        public void UpdateInfo()
        {
            if (CurrentDiff != null)
            {
                const char Star = (char)9733;
                var (accRating, passRating, techRating, starRating) = HelpfulMisc.GetRatingsAndStar(CurrentDiff, Gmpc.gameplayModifiers.songSpeed, CurrentModMultiplier);
                AccStarText.text = Math.Round(accRating, PC.DecimalPrecision) + " " + Star;
                PassStarText.text = Math.Round(passRating, PC.DecimalPrecision) + " " + Star;
                TechStarText.text = Math.Round(techRating, PC.DecimalPrecision) + " " + Star;
                StarText.text = Math.Round(starRating, PC.DecimalPrecision) + " " + Star;
            }
            SpeedModText.text = "<color=green>" + HelpfulMisc.AddSpaces(Gmpc.gameplayModifiers.songSpeed.ToString());
            ModMultText.text = $"x{Math.Round(CurrentModMultiplier, 2):N2}";
            RelativeText.text = TargetHasScore ? RelativeFormatter.Invoke(Targeter.PlayerName, GetAccToBeatTarget(), CurrentMods) : $"<color=red>{Targeter.PlayerName}</color> doesn't have a score on this map.";
            PPToCapture = ClanCounter.LoadNeededPp(_BeatmapID, out _)?[0] ?? 0;
            BuildTable();
        }
        private void UpdateDiff()
        {
            if (!TheCounter.CallAPI("leaderboards/hash/" + Sldvc.beatmapLevel.levelID.Split('_')[2].ToUpper(), out string dataStr)) return;
            int val = Map.FromDiff(Sldvc.beatmapKey.difficulty);
            CurrentDiff = JToken.Parse(dataStr);
            BeatmapName = CurrentDiff["song"]["name"].ToString();
            CurrentDiff = CurrentDiff["leaderboards"].Children().First(t => ((int)t["difficulty"]["value"]) == val);
            BeatmapID = CurrentDiff["id"].ToString();
            CurrentDiff = CurrentDiff["difficulty"];
        }
        #endregion
    }
}
