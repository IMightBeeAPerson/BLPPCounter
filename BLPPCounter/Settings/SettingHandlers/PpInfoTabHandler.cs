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
using BLPPCounter.CalculatorStuffs;
using System.Threading.Tasks;
using System.Threading;
using HMUI;
using BeatSaberMarkupLanguage.Tags;
using BeatSaberMarkupLanguage.Components;

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
        private string CurrentMods = "", UnformattedCurrentMods = "", CurrentTab = "Info";
        private float CurrentModMultiplier = 1f;
        private BeatmapKey CurrentMap;
        private JToken CurrentDiff;
        private readonly Dictionary<string, (BeatmapKey, Action<PpInfoTabHandler>)> Updates = new Dictionary<string, (BeatmapKey, Action<PpInfoTabHandler>)>()
        {
            {"Info", (default, new Action<PpInfoTabHandler>(pith => pith.UpdateInfo())) },
            {"Capture Values", (default, new Action<PpInfoTabHandler>(pith => pith.UpdateCaptureValues())) },
            {"Misc Values", (default, new Action<PpInfoTabHandler>(pith => pith.UpdateMiscValues())) }
        };
        #region Relative Counter
        private static Func<string, float, string, string> RelativeFormatter;

        private float TargetPP = 0;
        private bool TargetHasScore = false;
        private object RefreshLock = new object();
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
        private void RefreshMods() { if (Sldvc != null && Gmpc != null) { UpdateMods(); UpdateTabDisplay(); } }
        [UIAction(nameof(RefreshTable))]
        private void RefreshTable() => BuildTable();
        [UIAction(nameof(ChangeTab))]
        private void ChangeTab(SegmentedControl sc, int index)
        {
            if (Sldvc != null && Gmpc != null && sc.cells[index] is TextSegmentedControlCell tscc)
            {
                CurrentTab = tscc.text;
                UpdateTabDisplay();
            }
        }
        #endregion
        #region Inits
        static PpInfoTabHandler()
        {
            InitRelativeFormatter();
            Instance = new PpInfoTabHandler();
        }
        internal void SldvcInit() { Sldvc.didChangeContentEvent += (a, b) => RefreshAsync(); Sldvc.didChangeDifficultyBeatmapEvent += a => RefreshAsync(); }
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
            //Plugin.Log.Info("Content Changed");
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
            if (outp == 0.0f) //If the score set doesn't have a pp value, calculate one manually
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
            string[][] arr = new string[] { "<color=red>Slower</color>", "<color=#aaa>Normal</color>", "<color=#0F0>Faster</color>", "<color=#FFD700>Super Fast</color>" }.RowToColumn(3);
            float[] ratings = HelpfulPaths.GetAllRatings(CurrentDiff); //ss-sf, [acc, pass, tech, star]
            float gnMult = (float)CurrentDiff["modifierValues"]["gn"] + 1.0f;
            for (int i = 0; i < arr.Length; i++) 
                arr[i][1] = "<color=#0c0>" + BLCalc.GetAcc(ratings[i * 4], ratings[i * 4 + 1], ratings[i * 4 + 2], PPToCapture, PC.DecimalPrecision) + "</color>%";
            if (gnMult > 1.0f) for (int i = 0; i < arr.Length; i++)
                    arr[i][2] = "<color=#77cc77cc>" + BLCalc.GetAcc(ratings[i * 4] * gnMult, ratings[i * 4 + 1] * gnMult, ratings[i * 4 + 2] * gnMult, PPToCapture, PC.DecimalPrecision) + "</color>%";
            else for (int i = 0; i < arr.Length; i++) arr[i][2] = "0." + new string('0', PC.DecimalPrecision) + "%";
            HelpfulMisc.SetupTable(ClanTable, 0, arr, true, true, "<color=blue>Speed</color>", "<color=#0D0>Acc</color> to Cap", "With <color=#666>GN</color>");
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
        public void UpdateTabDisplay() { if (CurrentTab.Length > 0) Updates[CurrentTab].Item2.Invoke(this); }
        private void UpdateInfo()
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
        }
        private void UpdateCaptureValues() => BuildTable();
        private void UpdateMiscValues()
        {
            RelativeText.text = TargetHasScore ? RelativeFormatter.Invoke(Targeter.PlayerName, GetAccToBeatTarget(), CurrentMods) : $"<color=red>{Targeter.PlayerName}</color> doesn't have a score on this map.";
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
        private void RefreshAsync() => Task.Run(() => Refresh());
        public void Refresh(bool forceRefresh = false)
        {
            if (!forceRefresh && Sldvc.beatmapKey.Equals(CurrentMap)) return;
            if (Monitor.TryEnter(RefreshLock))
            {
                try
                {
                    UpdateMods();
                    TargetPP = UpdateTargetPP();
                    UpdateTabDisplay();
                }
                finally { Monitor.Exit(RefreshLock); }
            }
        }
        /*private void ChangeTextDisplay(bool show)
        {
            RelativeText.gameObject.SetActive(show);
            ClanTable.gameObject.SetActive(show);

            AccStarText.gameObject.SetActive(show);
            TechStarText.gameObject.SetActive(show);
            PassStarText.gameObject.SetActive(show);
            StarText.gameObject.SetActive(show);
            SpeedModText.gameObject.SetActive(show);
            ModMultText.gameObject.SetActive(show);

            MapName.gameObject.SetActive(show);
            MapID.gameObject.SetActive(show);
        }*/
        #endregion
    }
}
