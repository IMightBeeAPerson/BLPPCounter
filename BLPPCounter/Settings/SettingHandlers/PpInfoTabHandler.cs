﻿using BeatSaberMarkupLanguage.Attributes;
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
using BLPPCounter.Patches;
using UnityEngine;
using System.Collections;
using System.Net.Http;
using static GameplayModifiers;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Components.Settings;
using System.Windows.Forms;
using BS_Utils.Utilities;

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
        public static readonly string TabName = "PP Calculator";
        #endregion
        #region Variables
        private string CurrentMods = "", UnformattedCurrentMods = "", CurrentTab = "Info";
        private float CurrentModMultiplier = 1f;
#if NEW_VERSION
        private BeatmapKey CurrentMap; // 1.37.0 and above
#else
        internal IDifficultyBeatmap CurrentMap; // 1.34.2 and below
#endif
        private JToken CurrentDiff;
        private object RefreshLock = new object();
        private bool SelectButtonsOn = false;
#if NEW_VERSION
        private readonly Dictionary<string, BeatmapKey> TabMapInfo = new Dictionary<string, BeatmapKey>() // 1.37.0 and above
#else
        private readonly Dictionary<string, IDifficultyBeatmap> TabMapInfo = new Dictionary<string, IDifficultyBeatmap>() // 1.34.2 and below
#endif
        {
            { "Info", default },
            { "Capture", default },
            { "Relative", default },
            { "Custom", default }
        };
        private readonly Dictionary<string, Action<PpInfoTabHandler>> Updates = new Dictionary<string, Action<PpInfoTabHandler>>()
        {
            {"Info", new Action<PpInfoTabHandler>(pith => pith.UpdateInfo()) },
            {"Capture", new Action<PpInfoTabHandler>(pith => pith.UpdateCaptureValues()) },
            {"Relative", new Action<PpInfoTabHandler>(pith => pith.UpdateRelativeValues()) },
            {"Custom", new Action<PpInfoTabHandler>(pith => pith.UpdateCustomAccuracy()) },
        };
        private static readonly string[] SelectionButtonTags = new string[4] { "SSButton", "NMButton", "FSButton", "SFButton" };
        private Table ClanTableTable, RelativeTableTable, PPTableTable, PercentTableTable; 
        #region Relative Counter
        private static Func<string> GetTarget, GetNoScoreTarget;
        private float TargetPP = 0;
        private bool TargetHasScore = false;
        #endregion
        #region Clan Counter
        private float PPToCapture = 0;
        #endregion
        #region Custom Accuracy
        private bool IsPPMode = true;
        #endregion
        #endregion
        #region UI Variables & components
        [UIParams] private BSMLParserParams Parser;

        [UIValue(nameof(PercentSliderMin))] private float PercentSliderMin
        {
            get => _PercentSliderMin;
            set
            {
                _PercentSliderMin = value;
                CA_PercentSliderSlider.ChangeMinValue(value);
            }
        }
        [UIValue(nameof(PercentSliderMax))] private float PercentSliderMax
        {
            get => _PercentSliderMax;
            set
            {
                _PercentSliderMax = value;
                CA_PercentSliderSlider.ChangeMaxValue(value);
            }
        }
        [UIValue(nameof(PPSliderMin))] private int PPSliderMin
        {
            get => _PPSliderMin;
            set
            {
                _PPSliderMin = value;
                CA_PPSliderSlider.ChangeMinValue(value);
            }
        }
        [UIValue(nameof(PPSliderMax))] private int PPSliderMax
        {
            get => _PPSliderMax;
            set
            {
                _PPSliderMax = value;
                CA_PPSliderSlider.ChangeMaxValue(value);
            }
        }
        [UIValue(nameof(SliderIncrementNum))]
        private int SliderIncrementNum
        {
            get => _SliderIncrementNum;
            set
            {
                _SliderIncrementNum = value;
                UpdateTabSliders(value);
            }
        }
        private float _PercentSliderMin = PC.PercentSliderMin;
        private float _PercentSliderMax = PC.PercentSliderMax;
        private int _PPSliderMin = PC.PPSliderMin;
        private int _PPSliderMax = PC.PPSliderMax;
        private int _SliderIncrementNum = PC.SliderIncrementNum;
        [UIComponent(nameof(MinAccSlider))] private SliderSetting MinAccSlider;
        [UIComponent(nameof(MaxAccSlider))] private SliderSetting MaxAccSlider;
        [UIComponent(nameof(MinPPSlider))] private SliderSetting MinPPSlider;
        [UIComponent(nameof(MaxPPSlider))] private SliderSetting MaxPPSlider;

        [UIValue(nameof(TestAcc))] private float TestAcc 
        {
            get => PC.TestAccAmount;
            set => PC.TestAccAmount = value;
        }
        [UIValue(nameof(TestPp))] private int TestPp
        {
            get => PC.TestPPAmount;
            set => PC.TestPPAmount = value;
        }
        [UIValue(nameof(TabPos))]
#if NEW_VERSION
        private float TabPos = -5.5f;
#else
        private float TabPos = 0f;
#endif
        [UIComponent(nameof(PercentTable))] private TextMeshProUGUI PercentTable;
        [UIComponent(nameof(PPTable))] private TextMeshProUGUI PPTable;
        [UIComponent("ModeButton")] private TextMeshProUGUI ModeButtonText;
        [UIObject(nameof(CA_PercentSlider))] private GameObject CA_PercentSlider;
        [UIComponent(nameof(CA_PercentSlider))] private SliderSetting CA_PercentSliderSlider;
        [UIObject(nameof(CA_PPSlider))] private GameObject CA_PPSlider;
        [UIComponent(nameof(CA_PPSlider))] private SliderSetting CA_PPSliderSlider;
        [UIObject(nameof(PercentTable_BG))] private GameObject PercentTable_BG;
        [UIObject(nameof(PPTable_BG))] private GameObject PPTable_BG;

        [UIComponent(nameof(RelativeText))] private TextMeshProUGUI RelativeText;
        [UIComponent(nameof(RelativeTarget))] private TextMeshProUGUI RelativeTarget;
        [UIComponent(nameof(RelativeTable))] private TextMeshProUGUI RelativeTable;

        [UIComponent(nameof(ClanTable))] private TextMeshProUGUI ClanTable;
        [UIComponent(nameof(OwningClan))] private TextMeshProUGUI OwningClan;
        [UIComponent(nameof(ClanTarget))] private TextMeshProUGUI ClanTarget;
        [UIComponent(nameof(PPTarget))] private TextMeshProUGUI PPTarget;

        [UIComponent(nameof(AccStarText))] private TextMeshProUGUI AccStarText;
        [UIComponent(nameof(TechStarText))] private TextMeshProUGUI TechStarText;
        [UIComponent(nameof(PassStarText))] private TextMeshProUGUI PassStarText;
        [UIComponent(nameof(StarText))] private TextMeshProUGUI StarText;
        [UIComponent(nameof(SpeedModText))] private TextMeshProUGUI SpeedModText;
        [UIComponent(nameof(ModMultText))] private TextMeshProUGUI ModMultText;
        
        [UIComponent(nameof(MapName))] private TextMeshProUGUI MapName;
        [UIComponent(nameof(MapID))] private TextMeshProUGUI MapID;
        [UIComponent(nameof(MapMode))] private TextMeshProUGUI MapMode;
        [UIComponent(nameof(MapDiff))] private TextMeshProUGUI MapDiff;
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
        [UIValue(nameof(MapModeText))] private string MapModeText
        {
            get => _MapModeText;
            set { if (MapID is null) return; MapMode.text = $"<color=#777777>Map Type: <color=#aaaaaa>{value}</color>"; _MapModeText = value; }
        }
        private string _MapModeText = "";
        [UIValue(nameof(MapDiffText))] private string MapDiffText
        {
            get => _MapDiffText;
            set { if (MapID is null) return; MapDiff.text = $"<color=#777777>Map Difficulty: <color=#aaaaaa>{value}</color>"; _MapDiffText = value; }
        }
        private string _MapDiffText = "";
#endregion
        #region UI Functions
        [UIAction(nameof(Refresh))]
        private void ForceRefresh() { if (Sldvc != null && Gmpc != null) Refresh(true); }
        [UIAction(nameof(RefreshMods))]
        private void RefreshMods() { if (Sldvc != null && Gmpc != null) { UpdateMods(); UpdateTabDisplay(true); } }
        [UIAction(nameof(ChangeTab))]
        private void ChangeTab(SegmentedControl sc, int index)
        {
            if (sc.cells[index] is TextSegmentedControlCell tscc)
            {
                if (CurrentTab.Equals("Settings")) SaveSettings();
                CurrentTab = tscc.text;
                if (Sldvc != null && Gmpc != null)
                    UpdateTabDisplay();
            }
        }
        [UIAction(nameof(PercentFormat))]
        private string PercentFormat(float toFormat) => $"{toFormat:N2}%";
        [UIAction(nameof(PPFormat))]
        private string PPFormat(int toFormat) => $"{toFormat} pp";
        [UIAction(nameof(ToggleCAMode))]
        private void ToggleCAMode()
        {
            IsPPMode = !IsPPMode;
            CA_PPSlider.SetActive(IsPPMode);
            CA_PercentSlider.SetActive(!IsPPMode);
            PPTable_BG.SetActive(!IsPPMode);
            PercentTable_BG.SetActive(IsPPMode);
            ModeButtonText.text = IsPPMode ? "<color=#A020F0>Input PP" : "<color=#FFD700>Input Percentage";
            if (Sldvc != null) 
            {
                UpdateCustomAccuracy();
                BuildTable(); 
            }
        }
        [UIAction(nameof(SelectSS))] private void SelectSS() => SelectMod(SongSpeed.Slower);
        [UIAction(nameof(SelectNM))] private void SelectNM() => SelectMod(SongSpeed.Normal);
        [UIAction(nameof(SelectFS))] private void SelectFS() => SelectMod(SongSpeed.Faster);
        [UIAction(nameof(SelectSF))] private void SelectSF() => SelectMod(SongSpeed.SuperFast);
        private void SelectMod(SongSpeed speed)
        {
            Gmpc.ChangeSongSpeed(speed);
            UpdateMods();
            UpdateCurrentTable();
        }
        [UIAction(nameof(SaveSettings))] private void SaveSettings()
        {
            PC.PercentSliderMin = _PercentSliderMin;
            PC.PercentSliderMax = _PercentSliderMax;
            PC.PPSliderMin = _PPSliderMin;
            PC.PPSliderMax = _PPSliderMax;
            PC.SliderIncrementNum = _SliderIncrementNum;
        }
        [UIAction("#UpdateCurrentTable")] private void UpdateCurrentTable() => BuildTable();
        [UIAction("#UpdateCurrentTab")] private void UpdateCurrentTab() => UpdateTabDisplay(true);
        [UIAction("#post-parse")] private void DoStuff()
        {
            HelpfulMisc.CoupleMinMaxSliders(Instance.MinAccSlider, Instance.MaxAccSlider);
            HelpfulMisc.CoupleMinMaxSliders(Instance.MinPPSlider, Instance.MaxPPSlider);
            foreach (string s in SelectionButtonTags)
                foreach (GameObject go in Parser.GetObjectsWithTag(s))
                    go.SetActive(false);
        }
        #endregion
        #region Inits
        static PpInfoTabHandler()
        {
            BSEvents.levelCleared += (transition, results) =>
            {
                Instance.ClearMapTabs();
            };
            InitFormatters();
            Instance = new PpInfoTabHandler();
            TabSelectionPatch.AddToTabSelectedAction(TabName, () =>
            {
                if (Instance.Sldvc != null && Instance.Gmpc != null)
                    Instance.Refresh();
            });
            TabSelectionPatch.ModTabSelected += tabName =>
            {
                if (tabName.Equals("Mods") && Instance.Sldvc != null && Instance.Gmpc != null)
                    Instance.Refresh(true);
            };
            SettingsHandler.Instance.PropertyChanged += (parent, args) => {
#if NEW_VERSION
                if (!args.PropertyName.Equals(nameof(SettingsHandler.Target)) || Instance.CurrentMap == default) return; // 1.37.0 and above
#else
                if (!args.PropertyName.Equals(nameof(SettingsHandler.Target)) || Instance.CurrentMap is null) return; // 1.34.2 and below
#endif
                Instance.ClearMapTabs();
            };
        }
        internal void SldvcInit() 
        { 
            Sldvc.didChangeContentEvent += (a, b) => Refresh();
#if NEW_VERSION
            Sldvc.didChangeDifficultyBeatmapEvent += a => Refresh(); // 1.37.0 and above
#else
            Sldvc.didChangeDifficultyBeatmapEvent += (a,b) => Refresh(); // 1.34.2 and below
#endif
        }
        //internal void GmpcInit() { Gmpc.didChangeGameplayModifiersEvent += UpdateMods; UpdateMods(); }
        #endregion
        #region Formatting
        #region Relative Counter
        private static void InitFormatters()
        {
            var simple = HelpfulFormatter.GetBasicTokenParser(PC.MessageSettings.TargetHasNoScoreMessage,
                new Dictionary<string, char>() { { "Target", 't' } }, "TargetNoScoreMessage", null, (tokens, tokensCopy, priority, vals) =>
                {
                    foreach (char key in vals.Keys) if (vals[key] is null || vals[key].ToString().Length == 0) HelpfulFormatter.SetText(tokensCopy, key);
                }, out _, false).Invoke();
            GetNoScoreTarget = () => simple.Invoke(new Dictionary<char, object>() { { 't', Targeter.TargetName } });
            GetTarget = () => TheCounter.TargetFormatter?.Invoke(Targeter.TargetName, "") ?? "Target formatter is null";
        }
        private float GetAccToBeatTarget() =>
            CurrentDiff == null ? 0.0f : BLCalc.GetAcc(TargetPP, CurrentDiff, Gmpc.gameplayModifiers.songSpeed, CurrentModMultiplier, PC.DecimalPrecision);
        private float UpdateTargetPP()
        {
#if NEW_VERSION
            CurrentMap = Sldvc.beatmapKey; // 1.37.0 and above
#else
            CurrentMap = Sldvc.selectedDifficultyBeatmap; // 1.34.2 and below
#endif
            CurrentDiff = null;
            if (!Sldvc.beatmapLevel.levelID.Substring(0, 6).Equals("custom")) return 0.0f; //means the level selected is not custom
            string apiOutput = APIHandler.CallAPI_String(string.Format(HelpfulPaths.BLAPI_SCORE,
                Targeter.TargetID,
                Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(),
#if NEW_VERSION
                Sldvc.beatmapKey.difficulty.ToString().Replace("+", "Plus"),
                Sldvc.beatmapKey.beatmapCharacteristic.serializedName // 1.37.0 and above
#else
                Sldvc.selectedDifficultyBeatmap.difficulty.ToString().Replace("+", "Plus"),
                Sldvc.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName // 1.34.2 and below
#endif
                ), true);
            TargetHasScore = !(apiOutput is null || apiOutput.Length == 0);
            if (!TargetHasScore) { UpdateDiff(); return 0.0f; } //target doesn't have a score on this diff.
            JToken targetScore = JToken.Parse(apiOutput);
            BeatmapID = targetScore["leaderboardId"].ToString();
            BeatmapName = targetScore["song"]["name"].ToString();
            MapDiffText = HelpfulMisc.AddSpaces(targetScore["difficulty"]["difficultyName"].ToString());
            MapModeText = HelpfulMisc.AddSpaces(targetScore["difficulty"]["modeName"].ToString());
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
        private void BuildTable(Func<float, float, float, string> valueCalc, TextMeshProUGUI table, ref Table tableTable,
            string suffix = "%",
            string speedLbl = "<color=blue>Speed</color>",
            string accLbl = "<color=#0D0>Acc</color> to Cap",
            string gnLbl = "With <color=green>Selected Mods</color>")
        {
            string[][] arr = new string[] { "<color=red>Slower</color>", "<color=#aaa>Normal</color>", "<color=#0F0>Faster</color>", "<color=#FFD700>Super Fast</color>" }.RowToColumn(3);
            float[] ratings = HelpfulPaths.GetAllRatings(CurrentDiff); //ss-sf, [acc, pass, tech, star]
            for (int i = 0; i < arr.Length; i++)
                arr[i][1] = "<color=#0c0>" + valueCalc(ratings[i * 4], ratings[i * 4 + 1], ratings[i * 4 + 2]) + "</color>" + suffix;
            if (!Mathf.Approximately(CurrentModMultiplier, 1.0f)) for (int i = 0; i < arr.Length; i++)
                    arr[i][2] = "<color=green>" + valueCalc(ratings[i * 4] * CurrentModMultiplier, ratings[i * 4 + 1] * CurrentModMultiplier, ratings[i * 4 + 2] * CurrentModMultiplier) + "</color>" + suffix;
            else for (int i = 0; i < arr.Length; i++) arr[i][2] = "N/A";
            if (tableTable is null)
                tableTable = new Table(table, arr, speedLbl, accLbl, gnLbl) { HasEndColumn = true };
            else
                tableTable.SetValues(arr);
            Table tableTableTable = tableTable; //this is done because ref vars are not allowed to be passed into functions (probably something to do with security being compromised).
            IEnumerator DelayRoutine()
            {
                yield return new WaitForEndOfFrame();
                tableTableTable.UpdateTable();
                TurnOnSelectButtons();
            };
            Task.Run(() => Sldvc.StartCoroutine(DelayRoutine()));
            //This is done so that the game object is shown before the table is built. Otherwise, the game object gives wrong measurements, which messes up the table.
        }
        private void BuildTable()
        {
            switch (CurrentTab)
            {
                case "Capture":
                    BuildTable((acc, pass, tech) => BLCalc.GetAcc(acc, pass, tech, PPToCapture, PC.DecimalPrecision) + "", ClanTable, ref ClanTableTable);
                    ClanTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    break;
                case "Relative":
                    BuildTable((acc, pass, tech) => BLCalc.GetAcc(acc, pass, tech, TargetPP, PC.DecimalPrecision) + "", RelativeTable, ref RelativeTableTable, accLbl: "<color=#0D0>Acc</color> to Beat");
                    RelativeTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    break;
                case "Custom":
                    if (IsPPMode)
                    {
                        BuildTable((acc, pass, tech) => BLCalc.GetAcc(acc, pass, tech, TestPp, PC.DecimalPrecision) + "", PercentTable, ref PercentTableTable, accLbl: "<color=#0D0>Acc</color>");
                        PercentTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    }
                    else
                    {
                        BuildTable((acc, pass, tech) => BLCalc.GetSummedPp(TestAcc / 100.0f, acc, pass, tech, PC.DecimalPrecision).Total + "", PPTable, ref PPTableTable, " pp", accLbl: "<color=#0D0>PP</color>");
                        PPTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    }
                    break;
                default: return;
            }
        }
        private static string Grammarize(string mods) //this is very needed :)
        {
            int commaCount = mods.Count(c => c == ',');
            if (commaCount == 0) return mods;
            if (commaCount == 1) return mods.Replace(", ", " and ");
            return mods.Substring(0, mods.LastIndexOf(',')) + " and" + mods.Substring(mods.LastIndexOf(','));
        }
        private void UpdateMods() //this is why you use bitmasks instead of a billion bools vars
        {
            string newMods = "";
            UnformattedCurrentMods = "";
            GameplayModifiers mods = Gmpc.gameplayModifiers;
            switch (mods.songSpeed)
            {//Speed mods are not added to UnformattedCurrentMods because they are handled in a different way.
                case SongSpeed.Slower: newMods += "Slower Song, "; break;
                case SongSpeed.Faster: newMods += "Faster Song, "; break;
                case SongSpeed.SuperFast: newMods += "Super Fast Song, "; break;
            }
            if (mods.ghostNotes) { UnformattedCurrentMods += "gn "; newMods += "Ghost Notes, "; }
            if (mods.disappearingArrows) { UnformattedCurrentMods += "da "; newMods += "Disappearing Arrows, "; }
            if (mods.energyType == EnergyType.Battery) newMods += "Four Lifes, ";
            if (mods.noArrows) { UnformattedCurrentMods += "na "; newMods += "No Arrows, "; }
            if (mods.noFailOn0Energy) newMods += "No Fail, ";
            if (mods.zenMode) newMods += "Zen Mode (why are you using zen mode), ";
            if (mods.instaFail) newMods += "One Life, ";
            if (mods.noBombs) { UnformattedCurrentMods += "nb "; newMods += "No Bombs, "; }
            if (mods.proMode) { UnformattedCurrentMods += "pm "; newMods += "Pro Mode, "; }
            if (mods.smallCubes) { UnformattedCurrentMods += "sc "; newMods += "Small Cubes, "; }
            if (mods.strictAngles) { UnformattedCurrentMods += "sa "; newMods += "Strict Angles, "; }
            if (mods.enabledObstacleType == EnabledObstacleType.NoObstacles) { UnformattedCurrentMods += "no "; newMods += "No Walls, "; }
            CurrentMods = newMods.Length > 1 ? Grammarize(newMods.Substring(0, newMods.Length - 2)) : null;
            if (UnformattedCurrentMods.Length > 0) UnformattedCurrentMods = UnformattedCurrentMods.Trim();
            float modMult = CurrentModMultiplier;
            if (CurrentDiff != null) CurrentModMultiplier = HelpfulPaths.GetMultiAmounts(CurrentDiff, UnformattedCurrentMods.Split(' '));
            if (!Mathf.Approximately(modMult, CurrentModMultiplier))
            {
                TabMapInfo["Capture"] = default;
                TabMapInfo["Relative"] = default;
            }
        }
        private void TurnOnSelectButtons()
        {
            if (SelectButtonsOn) return;
            foreach (string s in SelectionButtonTags)
                foreach (GameObject go in Parser.GetObjectsWithTag(s))
                    go.SetActive(true);
            SelectButtonsOn = true;
        }
#endregion
        #region Misc Functions
        public void UpdateTabDisplay(bool forceUpdate = false, bool runAsync = true) 
        {
            if (CurrentTab.Equals("Settings") || Sldvc is null || CurrentTab.Length == 0 || (!forceUpdate && TabMapInfo[CurrentTab] == CurrentMap)) return;
            void Update()
            {
                TabMapInfo[CurrentTab] = CurrentMap;
                Updates[CurrentTab].Invoke(this);
                BuildTable();
            }
            if (runAsync) Task.Run(Update);
            else Update();
        }
        private void UpdateInfo()
        {
            if (CurrentDiff != null && HelpfulMisc.StatusIsUsable((int)CurrentDiff["status"]))
            {
                const char Star = (char)9733;
                var (accRating, passRating, techRating, starRating) = HelpfulMisc.GetRatingsAndStar(CurrentDiff, Gmpc.gameplayModifiers.songSpeed, CurrentModMultiplier);
                AccStarText.SetText(Math.Round(accRating, PC.DecimalPrecision) + " " + Star);
                PassStarText.SetText(Math.Round(passRating, PC.DecimalPrecision) + " " + Star);
                TechStarText.SetText(Math.Round(techRating, PC.DecimalPrecision) + " " + Star);
                StarText.SetText(Math.Round(starRating, PC.DecimalPrecision) + " " + Star);
            } else
            {
                const string failText = "Not Found!";
                AccStarText.SetText(failText);
                PassStarText.SetText(failText);
                TechStarText.SetText(failText);
                StarText.SetText(failText);
            }
            SpeedModText.SetText("<color=green>" + HelpfulMisc.AddSpaces(Gmpc.gameplayModifiers.songSpeed.ToString()));
            ModMultText.SetText($"x{Math.Round(CurrentModMultiplier, 2):N2}");
        }
        private void UpdateCaptureValues() 
        {
            if (CurrentDiff is null || !HelpfulMisc.StatusIsUsable((int)CurrentDiff["status"])) return;
            ClanTarget.SetText(TargetHasScore ? GetTarget() : GetNoScoreTarget());
            PPToCapture = ClanCounter.LoadNeededPp(BeatmapID, out _, out string owningClan)[0];
            OwningClan.SetText($"<color=red>{owningClan}</color> owns this map.");
            PPTarget.SetText($"<color=#0F0>{Math.Round(PPToCapture, PC.DecimalPrecision)}</color> pp");
        }
        private void UpdateRelativeValues()
        {
            RelativeText.SetText(TargetHasScore ? GetTarget() : GetNoScoreTarget());
            RelativeTarget.SetText($"<color=#0F0>{Math.Round(TargetPP, PC.DecimalPrecision)}</color> pp");
        }
        private void UpdateCustomAccuracy()
        {
            if (CurrentDiff is null) return;
            if (IsPPMode) PC.TestAccAmount = TestAcc;
            else PC.TestPPAmount = TestPp;
        }
        private void UpdateDiff()
        {
            if (!APIHandler.CallAPI("leaderboards/hash/" + Sldvc.beatmapLevel.levelID.Split('_')[2].ToUpper(), out HttpContent dataStr, forceBLCall: true)) return;
#if NEW_VERSION
            int val = Map.FromDiff(Sldvc.beatmapKey.difficulty); // 1.37.0 and below
            string modeName = Sldvc.beatmapKey.beatmapCharacteristic.serializedName;
#else
            int val = Map.FromDiff(Sldvc.selectedDifficultyBeatmap.difficulty); // 1.34.2 and above
            string modeName = Sldvc.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
#endif
            JToken tokens = JToken.Parse(dataStr.ReadAsStringAsync().Result);
            BeatmapName = tokens["song"]["name"].ToString();
            tokens = tokens["leaderboards"].Children().First(t => ((int)t["difficulty"]["value"]) == val && ((string)t["difficulty"]["modeName"]).Equals(modeName));
            BeatmapID = tokens["id"].ToString();
            CurrentDiff = tokens["difficulty"];
            MapDiffText = HelpfulMisc.AddSpaces(CurrentDiff["difficultyName"].ToString());
            MapModeText = HelpfulMisc.AddSpaces(CurrentDiff["modeName"].ToString());
            if (CurrentDiff["modifiersRating"] is null || CurrentDiff["modifiersRating"].ToString().Length == 0)
                CurrentDiff = null;
        }
        public void Refresh(bool forceRefresh = false) => Task.Run(() => DoRefresh(forceRefresh));
        private void DoRefresh(bool forceRefresh)
        {
#if NEW_VERSION
            if (!TabSelectionPatch.GetIfTabIsSelected(TabName) || (!forceRefresh && Sldvc.beatmapKey.Equals(CurrentMap))) return; // 1.37.0 and above

#else
            if (!TabSelectionPatch.GetIfTabIsSelected(TabName) || (!forceRefresh && Sldvc.selectedDifficultyBeatmap.Equals(CurrentMap))) return; // 1.34.2 and below

#endif
            if (Monitor.TryEnter(RefreshLock))
            {
                try
                {
                    UpdateMods();
                    TargetPP = UpdateTargetPP();
                    UpdateTabDisplay(forceRefresh, false);
                }
                catch (Exception e) { Plugin.Log.Error("There was an error!\n" + e); }
                finally 
                { 
                    Monitor.Exit(RefreshLock);
                }
            }
        }
        public void ClearMapTabs()
        {
            TabMapInfo["Capture"] = default;
            TabMapInfo["Relative Values"] = default;
            ClanTableTable?.ClearTable();
            RelativeTableTable?.ClearTable();
            CurrentMap = default;
        }
        private void UpdateTabSliders(int amount = -1)
        {
            if (amount <= 0) amount = PC.SliderIncrementNum;
            HelpfulMisc.SetIncrements(amount, MinPPSlider, MaxPPSlider, MinAccSlider, MaxAccSlider, CA_PPSliderSlider, CA_PercentSliderSlider);
        }
        #endregion
    }
}
