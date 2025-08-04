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
using BLPPCounter.Patches;
using UnityEngine;
using System.Collections;
using static GameplayModifiers;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Components.Settings;
using BS_Utils.Utilities;
using BLPPCounter.Utils.API_Handlers;
using BeatSaberMarkupLanguage.Components;
using IPA.Utilities;

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
        private IDifficultyBeatmap CurrentMap; // 1.34.2 and below
#endif
        private (JToken Diffdata, JToken Scoredata) CurrentDiff;
        private object RefreshLock = new object();
        private bool SelectButtonsOn = false;
        private Leaderboards CurrentLeaderboard = PC.Leaderboard;
        private Calculator CurrentCalculator => Calculator.GetCalc(CurrentLeaderboard);
        private APIHandler CurrentAPI => APIHandler.GetAPI(CurrentLeaderboard);
#if NEW_VERSION
        private readonly Dictionary<string, BeatmapKey> TabMapInfo = new Dictionary<string, BeatmapKey>() // 1.37.0 and above
#else
        private readonly Dictionary<string, IDifficultyBeatmap> TabMapInfo = new Dictionary<string, IDifficultyBeatmap>() // 1.34.2 and below
#endif
        {
            { "Info", default },
            { "Capture", default },
            { "Relative", default },
            { "Custom", default },
            { "Profile", default }
        };
        private readonly Dictionary<string, Action<PpInfoTabHandler>> Updates = new Dictionary<string, Action<PpInfoTabHandler>>()
        {
            {"Info", new Action<PpInfoTabHandler>(pith => pith.UpdateInfo()) },
            {"Capture", new Action<PpInfoTabHandler>(pith => pith.UpdateCaptureValues()) },
            {"Relative", new Action<PpInfoTabHandler>(pith => pith.UpdateRelativeValues()) },
            {"Custom", new Action<PpInfoTabHandler>(pith => pith.UpdateCustomAccuracy()) },
            {"Profile", new Action<PpInfoTabHandler>(pith => pith.UpdateProfile()) }
        };
        private static readonly string[] SelectionButtonTags = new string[4] { "SSButton", "NMButton", "FSButton", "SFButton" };
        private Table ClanTableTable, RelativeTableTable, PPTableTable, PercentTableTable;
        public bool ChangeTabSettings = false;
        #region Relative Counter
        private static Func<string> GetTarget, GetNoScoreTarget;
        private float TargetPP = 0;
        private bool TargetHasScore = false;
        #endregion
        #region Clan Counter
        private float PPToCapture = 0;
        #endregion
        #region Custom Accuracy & Profile
        private bool IsPPMode = true;

        private Profile CurrentProfile;
        #endregion
        #endregion
        #region UI Variables & components
        [UIParams] private BSMLParserParams Parser;
        private bool UsesMods => CurrentCalculator.UsesModifiers;
        private bool IsBL => CurrentLeaderboard == Leaderboards.Beatleader;

        [UIValue(nameof(ShowTrueID))] private bool ShowTrueID
        {
            get => _ShowTrueID;
            set
            {
                _ShowTrueID = value;
                MapID.gameObject.SetActive(!value);
                TrueMapID.gameObject.SetActive(value);
            }
        }
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
        private bool _ShowTrueID = PC.ShowTrueID;
        private float _PercentSliderMin = PC.PercentSliderMin;
        private float _PercentSliderMax = PC.PercentSliderMax;
        private int _PPSliderMin = PC.PPSliderMin;
        private int _PPSliderMax = PC.PPSliderMax;
        private int _SliderIncrementNum = PC.SliderIncrementNum;
        [UIComponent(nameof(MinAccSlider))] private SliderSetting MinAccSlider;
        [UIComponent(nameof(MaxAccSlider))] private SliderSetting MaxAccSlider;
        [UIComponent(nameof(MinPPSlider))] private SliderSetting MinPPSlider;
        [UIComponent(nameof(MaxPPSlider))] private SliderSetting MaxPPSlider;

        [UIValue(nameof(ProfileTestPp))] private int ProfileTestPp
        {
            get => PC.ProfileTestPP;
            set => PC.ProfileTestPP = value;
        }

        [UIComponent(nameof(PlusOneText))] private TextMeshProUGUI PlusOneText;

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

        [UIComponent(nameof(CaptureTab))] private Tab CaptureTab;
        [UIComponent(nameof(ClanTable))] private TextMeshProUGUI ClanTable;
        [UIComponent(nameof(OwningClan))] private TextMeshProUGUI OwningClan;
        [UIComponent(nameof(ClanTarget))] private TextMeshProUGUI ClanTarget;
        [UIComponent(nameof(PPTarget))] private TextMeshProUGUI PPTarget;

        [UIComponent(nameof(SpeedModText))] private TextMeshProUGUI SpeedModText;
        [UIComponent(nameof(ModMultText))] private TextMeshProUGUI ModMultText;
        [UIComponent(nameof(PrefixLabels))] private TextMeshProUGUI PrefixLabels;
        [UIComponent(nameof(StarRatings))] private TextMeshProUGUI StarRatings;
        [UIComponent(nameof(MapName))] private TextMeshProUGUI MapName;
        [UIComponent(nameof(MapID))] private TextMeshProUGUI MapID;
        [UIComponent(nameof(MapMode))] private TextMeshProUGUI MapMode;
        [UIComponent(nameof(MapDiff))] private TextMeshProUGUI MapDiff;
        [UIComponent(nameof(TrueMapID))] private TextMeshProUGUI TrueMapID;
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
        [UIValue(nameof(TrueBeatmapID))] private string TrueBeatmapID
        {
            get => _TrueBeatmapID;
            set { if (MapID is null) return; TrueMapID.text = $"<color=#777777>True Map ID: <color=#aaaaaa>{value}</color>"; _TrueBeatmapID = value; }
        }
        private string _TrueBeatmapID = "";
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
        private string PPFormat(int toFormat) => $"{toFormat} " + GetPPLabel();
        [UIAction(nameof(ToggleCAMode))]
        private void ToggleCAMode()
        {
            IsPPMode = !IsPPMode;
            CA_PPSlider.SetActive(IsPPMode);
            CA_PercentSlider.SetActive(!IsPPMode);
            PPTable_BG.SetActive(!IsPPMode);
            PercentTable_BG.SetActive(IsPPMode);
            ModeButtonText.text = IsPPMode ? "<color=#A020F0>Input " + GetPPLabel().ToUpper() : "<color=#FFD700>Input Percentage";
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
            PC.ShowTrueID = _ShowTrueID;
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
            CaptureTab.IsVisible = IsBL;
            MapID.gameObject.SetActive(!ShowTrueID);
            TrueMapID.gameObject.SetActive(ShowTrueID);
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
            SettingsHandler.Instance.PropertyChanged += (parent, args) => {
#if NEW_VERSION
                if (Instance.CurrentMap == default) return; // 1.37.0 and above
#else
                if (Instance.CurrentMap is null) return; // 1.34.2 and below
#endif
                if (args.PropertyName.Equals(nameof(SettingsHandler.Target)))
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
        private float GetAccToBeatTarget()
        {
            if (CurrentDiff.Diffdata is null || CurrentAPI.AreRatingsNull(CurrentDiff.Diffdata)) return 0f;
            return CurrentCalculator.GetAcc(CurrentAPI.GetPP(CurrentDiff.Scoredata), PC.DecimalPrecision, CurrentAPI.GetRatings(CurrentDiff.Diffdata));
        }

        private float UpdateTargetPP()
        {
#if NEW_VERSION
            CurrentMap = Sldvc.beatmapKey; // 1.37.0 and above
#else
            CurrentMap = Sldvc.selectedDifficultyBeatmap; // 1.34.2 and below
#endif
            CurrentDiff = (null, null);
            if (!Sldvc.beatmapLevel.levelID.Substring(0, 6).Equals("custom")) return 0.0f; //means the level selected is not custom
#if NEW_VERSION
            string mode = CurrentMap.beatmapCharacteristic.serializedName; // 1.37.0 and above
#else
            string mode = CurrentMap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName; // 1.34.2 and below
#endif
        APIHandler api = CurrentAPI;
            JToken scoreData = api.GetScoreData(
                Targeter.TargetID,
                Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(),
                CurrentMap.difficulty.ToString().Replace("+", "Plus"),
                mode,
                true //Debug option, just prevents prints when the API was called.
                );
            TargetHasScore = !(scoreData is null);
            if (!TargetHasScore) { UpdateDiff(); return 0.0f; } //target doesn't have a score on this diff.
            JToken diffData = null;
            try
            {
                JToken rawDiffData = JToken.Parse(api.GetHashData(Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(), Map.FromDiff(CurrentMap.difficulty)));
                diffData = api.SelectSpecificDiff(rawDiffData, Map.FromDiff(CurrentMap.difficulty), mode);
                BeatmapID = api.GetLeaderboardId(diffData);
                TrueBeatmapID = IsBL ? BeatmapID.Replace('x', (char)0).Substring(0, BeatmapID.Length - 2) : JToken.Parse(BLAPI.Instance.GetHashData(Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(), Map.FromDiff(CurrentMap.difficulty)))["song"]["id"].ToString().Replace('x', (char)0);
                BeatmapName = api.GetSongName(rawDiffData);
            } catch (Exception)
            { //This exception should only happen when a map isn't on the radar of the selected leaderboard (aka unranked), and thus doesn't need to be broadcasted.
                UpdateDiff(); 
                return 0.0f;
            }
            MapDiffText = HelpfulMisc.AddSpaces(CurrentMap.difficulty.ToString().Replace("+", "Plus"));
            MapModeText = HelpfulMisc.AddSpaces(mode);
            float outp = api.GetPP(scoreData);
            if (outp == 0.0f && !api.AreRatingsNull(diffData)) //If the score set doesn't have a pp value, calculate one manually. Make sure there are ratings to do calculation, otherwise skip.
                outp = CurrentCalculator.Inflate(CurrentCalculator.GetSummedPp(api.GetScore(scoreData) / (float)api.GetMaxScore(diffData), api.GetRatings(diffData)));
            CurrentDiff = (diffData, scoreData);
            if (UnformattedCurrentMods.Length > 0) CurrentModMultiplier = HelpfulPaths.GetMultiAmounts(diffData, UnformattedCurrentMods.Split(' '));
            return outp;
        }
#endregion
        private void BuildTable(Func<float[], string> valueCalc, TextMeshProUGUI table, ref Table tableTable,
            string suffix = "%",
            string speedLbl = "<color=blue>Speed</color>",
            string accLbl = "<color=#0D0>Acc</color> to Cap",
            string gnLbl = "With <color=green>Selected Mods</color>")
        {
            Calculator calc = CurrentCalculator;
            string[][] arr = new string[] { "<color=red>Slower</color>", "<color=#aaa>Normal</color>", "<color=#0F0>Faster</color>", "<color=#FFD700>Super Fast</color>" }.RowToColumn(3);
            float[] ratings = HelpfulPaths.GetAllRatings(IsBL ? CurrentDiff.Diffdata["difficulty"] : CurrentDiff.Diffdata, calc); //ss-sf, [star, acc, pass, tech] (selects by leaderboard)
            if (!UsesMods)
            {
                float[] newArr = new float[ratings.Length * 4];
                for (int i = 0; i < ratings.Length; i++)
                {
                    newArr[i] = ratings[i];
                    newArr[i + ratings.Length] = ratings[i];
                    newArr[i + ratings.Length * 2] = ratings[i];
                    newArr[i + ratings.Length * 3] = ratings[i];
                }
                ratings = newArr;
            }
            int len = ratings.Length / 4; //divide by 4 because 3 speed mods + 1 no mod
            Plugin.Log.Info($"rating len: {ratings.Length}, len: {len}");
            for (int i = 0; i < arr.Length; i++)
                arr[i][1] = "<color=#0c0>" + valueCalc.Invoke(ratings.Skip(i * len).Take(len).ToArray()) + "</color>" + suffix;
            if (!Mathf.Approximately(CurrentModMultiplier, 1.0f)) for (int i = 0; i < arr.Length; i++)
                    arr[i][2] = "<color=green>" + valueCalc(ratings.Skip(i * len).Take(len).Select(current => current * CurrentModMultiplier).ToArray()) + "</color>" + suffix;
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
                    if (!IsBL) break;
                    BuildTable(ratings => CurrentCalculator.GetAcc(PPToCapture, PC.DecimalPrecision, ratings) + "", ClanTable, ref ClanTableTable);
                    ClanTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    break;
                case "Relative":
                    BuildTable(ratings => CurrentCalculator.GetAcc(TargetPP, PC.DecimalPrecision, ratings) + "", RelativeTable, ref RelativeTableTable, accLbl: "<color=#0D0>Acc</color> to Beat");
                    RelativeTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    break;
                case "Custom":
                    if (IsPPMode)
                    {
                        BuildTable(ratings => CurrentCalculator.GetAcc(TestPp, PC.DecimalPrecision, ratings) + "", PercentTable, ref PercentTableTable, accLbl: "<color=#0D0>Acc</color>");
                        PercentTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    }
                    else
                    {
                        BuildTable(ratings => CurrentCalculator.GetSummedPp(TestAcc / 100.0f, PC.DecimalPrecision, ratings) + "", PPTable, ref PPTableTable, " pp", accLbl: "<color=#0D0>PP</color>");
                        PPTableTable.HighlightCell(HelpfulMisc.OrderSongSpeedCorrectly(Gmpc.gameplayModifiers.songSpeed) + 1, 0);
                    }
                    break;
                default: 
                    return;
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
            if (!UsesMods) return;
            float modMult = CurrentModMultiplier;
            if (CurrentDiff.Diffdata != null) CurrentModMultiplier = HelpfulPaths.GetMultiAmounts(CurrentDiff.Diffdata, UnformattedCurrentMods.Split(' '));
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
        public string GetPPLabel() => CurrentLeaderboard == Leaderboards.Accsaber ? "ap" : "pp";
        public void UpdateTabDisplay(bool forceUpdate = false, bool runAsync = true) 
        {
            if (CurrentTab.Equals("Settings") || Sldvc is null || CurrentTab.Length == 0 || (!forceUpdate && TabMapInfo[CurrentTab] == CurrentMap)) return;
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                CaptureTab.IsVisible = IsBL;
            }
            void Update()
            {
                bool mapUsable = CurrentAPI.MapIsUsable(CurrentDiff.Diffdata);
                TabMapInfo[CurrentTab] = CurrentMap;
                Updates[CurrentTab].Invoke(this);
                BuildTable();
                if (ChangeTabSettings)
                {
                    ChangeTabSettings = false;
                    Sldvc?.StartCoroutine(WaitThenUpdate());
                }
            }
            if (runAsync) Task.Run(Update);
            else Update();
        }
        private void UpdateInfo()
        {
            const string Delimiter = "\n";
            const string LineHeight = "80";
            PrefixLabels.SetText($"<line-height={LineHeight}%>" + CurrentCalculator.StarLabels.Aggregate((total, current) => total + Delimiter + current));
            if (CurrentDiff.Diffdata != null && CurrentAPI.MapIsUsable(CurrentDiff.Diffdata))
            {
                const char Star = (char)9733;
                switch (CurrentLeaderboard) 
                {
                    case Leaderboards.Beatleader:
                        (float accRating, float passRating, float techRating, float starRating) = 
                            HelpfulMisc.GetRatingsAndStar(CurrentDiff.Diffdata, Gmpc.gameplayModifiers.songSpeed, CurrentModMultiplier);
                        StarRatings.SetText($"<line-height={LineHeight}%>" +
                            Math.Round(accRating, PC.DecimalPrecision) + ' ' + Star + Delimiter +
                            Math.Round(passRating, PC.DecimalPrecision) + ' ' + Star + Delimiter +
                            Math.Round(techRating, PC.DecimalPrecision) + ' ' + Star + Delimiter +
                            Math.Round(starRating, PC.DecimalPrecision) + ' ' + Star);
                        break;
                    case Leaderboards.Scoresaber:
                        StarRatings.SetText($"<line-height={LineHeight}%>{Math.Round((float)CurrentDiff.Diffdata["stars"], PC.DecimalPrecision)} {Star}");
                        break;
                    case Leaderboards.Accsaber:
                        StarRatings.SetText($"<line-height={LineHeight}%>{Math.Round((float)CurrentDiff.Diffdata["complexity"], PC.DecimalPrecision)} {Star}");
                        break;
                }
            } else
            {
                const string failText = "Not Found!";
                string failString = "";
                for (int i = 0; i < CurrentCalculator.DisplayRatingCount; i++)
                    failString += Delimiter + failText;
                StarRatings.SetText($"<line-height={LineHeight}%>" + failString.Substring(Delimiter.Length));
            }
            SpeedModText.SetText("<color=green>" + HelpfulMisc.AddSpaces(Gmpc.gameplayModifiers.songSpeed.ToString()));
            ModMultText.SetText($"x{Math.Round(CurrentModMultiplier, 2):N2}");
        }
        private void UpdateCaptureValues() 
        {
            if (!IsBL || CurrentDiff.Diffdata is null || !BLAPI.Instance.MapIsUsable(CurrentDiff.Diffdata)) return;
            ClanTarget.SetText(TargetHasScore ? GetTarget() : GetNoScoreTarget());
            PPToCapture = ClanCounter.LoadNeededPp(BeatmapID, out _, out string owningClan)[0];
            OwningClan.SetText($"<color=red>{owningClan}</color> owns this map.");
            PPTarget.SetText($"<color=#0F0>{Math.Round(PPToCapture, PC.DecimalPrecision)}</color> " + GetPPLabel());
        }
        private void UpdateRelativeValues()
        {
            RelativeText.SetText(TargetHasScore ? GetTarget() : GetNoScoreTarget());
            RelativeTarget.SetText($"<color=#0F0>{Math.Round(TargetPP, PC.DecimalPrecision)}</color> " + GetPPLabel());
        }
        private void UpdateCustomAccuracy()
        {
            if (CurrentDiff.Diffdata is null) return;
            if (IsPPMode) PC.TestAccAmount = TestAcc;
            else PC.TestPPAmount = TestPp;
        }
        private void UpdateDiff()
        {
            APIHandler api = CurrentAPI;
#if NEW_VERSION
            int val = Map.FromDiff(Sldvc.beatmapKey.difficulty); // 1.37.0 and below
            string modeName = Sldvc.beatmapKey.beatmapCharacteristic.serializedName;
#else
            int val = Map.FromDiff(Sldvc.selectedDifficultyBeatmap.difficulty); // 1.34.2 and above
            string modeName = Sldvc.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
#endif
            string jsonData = api.GetHashData(Sldvc.beatmapLevel.levelID.Split('_')[2], val);
            if (jsonData is null) return;
            JToken tokens = JToken.Parse(jsonData);
            BeatmapName = api.GetSongName(tokens);
            tokens = api.SelectSpecificDiff(tokens, val, modeName);
            BeatmapID = api.GetLeaderboardId(tokens);
            TrueBeatmapID = IsBL ? BeatmapID.Replace('x', (char)0).Substring(0, BeatmapID.Length - 2) : JToken.Parse(BLAPI.Instance.GetHashData(Sldvc.beatmapLevel.levelID.Split('_')[2].ToLower(), Map.FromDiff(CurrentMap.difficulty)))["song"]["id"].ToString().Replace('x', (char)0);
            CurrentDiff = (tokens, CurrentDiff.Scoredata);
            MapDiffText = HelpfulMisc.AddSpaces(api.GetDiffName(CurrentDiff.Diffdata));
            MapModeText = HelpfulMisc.AddSpaces(modeName);
            if (api.AreRatingsNull(CurrentDiff.Diffdata))
                CurrentDiff = (null, null);
        }
        private void UpdateProfile()
        {
            CurrentProfile = new Profile(CurrentLeaderboard, Targeter.PlayerID);
            PlusOneText.SetText($"+1{GetPPLabel()}: <color=#00FF00>{CurrentProfile.PlusOne}</color>{GetPPLabel()}");
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
                    CurrentLeaderboard = PC.Leaderboard;
                    UpdateMods();
                    TargetPP = UpdateTargetPP();
                    UpdateTabDisplay(forceRefresh, false);
                }
                catch (Exception e) 
                { 
                    Plugin.Log.Error("There was an error!\n" + e); 
                }
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
        public void ResetTabs()
        {
            string[] tabNames = TabMapInfo.Keys.ToArray();//.Clone();
            foreach (string s in tabNames)
                TabMapInfo[s] = default;
            PPTableTable?.ClearTable();
            PercentTableTable?.ClearTable();
            ClanTableTable?.ClearTable();
            RelativeTableTable?.ClearTable();
            CurrentMap = default;
            CurrentDiff = default;
        }
        private void UpdateTabSliders(int amount = -1)
        {
            if (amount <= 0) amount = PC.SliderIncrementNum;
            HelpfulMisc.SetIncrements(amount, MinPPSlider, MaxPPSlider, MinAccSlider, MaxAccSlider, CA_PPSliderSlider, CA_PercentSliderSlider);
        }
        #endregion
    }
}
