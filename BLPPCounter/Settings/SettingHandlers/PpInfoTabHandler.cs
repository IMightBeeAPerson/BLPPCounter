using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Patches;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using BS_Utils.Utilities;
using HMUI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameplayModifiers;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class PpInfoTabHandler
    {
#pragma warning disable IDE0051, IDE0044, CS0649
        #region Misc Static Variables & Controllers
        internal StandardLevelDetailViewController Sldvc { private get; set; }
        internal GameplayModifiersPanelController Gmpc { private get; set; }
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
        private AsyncLock RefreshLock = new AsyncLock(), ProfileLock = new AsyncLock();
        private bool SelectButtonsOn = false;
        internal Leaderboards CurrentLeaderboard { get; private set; } = PC.LeaderboardsInUse.First();
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
            { "Profile", default },
            { "Settings", default } //This one is only here to provide a list of names
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

        private Profile CurrentProfile = null;
        private bool IsPlayTableOpen = false, UpdatePlayTableOnOpen = false;
        #endregion
        #endregion
        #region UI Variables & components
        [UIParams] private BSMLParserParams Parser;
        private bool UsesMods => CurrentCalculator.UsesModifiers;
        private bool IsBL => CurrentLeaderboard == Leaderboards.Beatleader;
        private bool IsAP => CurrentLeaderboard == Leaderboards.Accsaber;
        private bool ShowProfileTab => true;// CurrentLeaderboard != Leaderboards.Accsaber;

        [UIComponent(nameof(MainTabSelector))] internal TabSelector MainTabSelector;

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
                ProfilePPSlider.ChangeMinValue(value);
            }
        }
        [UIValue(nameof(PPSliderMax))] private int PPSliderMax
        {
            get => _PPSliderMax;
            set
            {
                _PPSliderMax = value;
                CA_PPSliderSlider.ChangeMaxValue(value);
                ProfilePPSlider.ChangeMaxValue(value);
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
        [UIValue(nameof(PercentSliderIncrementNum))]
        private float PercentSliderIncrementNum => _SliderIncrementNum / 100f;

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
        [UIValue(nameof(APSetting))] private string APSetting
        {
            get => _APSetting.ToString();
            set
            {
                _APSetting = (APCategory)Enum.Parse(typeof(APCategory), value);
                Task.Run(() => UpdateProfile());
            }
        }
        [UIValue(nameof(APCategorySettings))] private List<object> APCategorySettings = Enum.GetNames(typeof(APCategory)).Skip(1).Cast<object>().ToList();
        private APCategory _APSetting = APCategory.All;

        [UIComponent(nameof(ProfileTab))] private Tab ProfileTab;

        [UIComponent(nameof(PlayTable))] private TextMeshProUGUI PlayTable;
        [UIComponent(nameof(PlayTableButtons))] private HorizontalLayoutGroup PlayTableButtons;
        [UIObject(nameof(PlayTableModal))] private GameObject PlayTableModal;

        [UIObject(nameof(SessionWindow))] private GameObject SessionWindow;
        [UIComponent(nameof(SessionWindow_PlaysSet))] private TextMeshProUGUI SessionWindow_PlaysSet;
        [UIComponent(nameof(SessionWindow_PpGained))] private TextMeshProUGUI SessionWindow_PpGained;
        [UIComponent(nameof(SessionTable))] private CustomCellListTableData SessionTable;
        [UIValue(nameof(SessionTable_Infos))] private List<object> SessionTable_Infos => CurrentProfile?.CurrentSession.Info ?? new List<object>(0);

        [UIComponent(nameof(PlusOneLabel))] private TextMeshProUGUI PlusOneLabel;
        [UIComponent(nameof(PlusOneText))] private TextMeshProUGUI PlusOneText;
        [UIComponent(nameof(LevelText))] private TextMeshProUGUI LevelText;
        [UIComponent(nameof(ExperienceText))] private TextMeshProUGUI ExperienceText;
        [UIComponent(nameof(ReloadDataButton))] private Button ReloadDataButton;
        [UIComponent(nameof(AccSaberSetting))] private DropDownListSetting AccSaberSetting;
        [UIComponent(nameof(ProfilePPSlider))] private SliderSetting ProfilePPSlider;
        [UIComponent(nameof(WeightedText))] private TextMeshProUGUI WeightedText;
        [UIComponent(nameof(WeightedTextValue))] private TextMeshProUGUI WeightedTextValue;
        [UIComponent(nameof(ProfilePPText))] private TextMeshProUGUI ProfilePPText;
        [UIComponent(nameof(ProfilePPTextValue))] private TextMeshProUGUI ProfilePPTextValue;

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
#pragma warning disable CS0414
        [UIValue(nameof(TabPos))]
#if NEW_VERSION
        private float TabPos = -5.5f;
#else
        private float TabPos = 0f;
#endif
#pragma warning restore CS0414
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
            set { if (MapName is null) return; MapName.text = $"<color=#777>Map Name: <color=#AAA>{value.ClampString(25)}</color>"; _BeatmapName = value; }
        }
        private string _BeatmapName = "";
        [UIValue(nameof(BeatmapID))] private string BeatmapID
        {
            get => _BeatmapID;
            set { if (MapID is null) return; MapID.text = $"<color=#777>Map ID: <color=#AAA>{value}</color>"; _BeatmapID = value; }
        }
        private string _BeatmapID = "";
        [UIValue(nameof(MapModeText))] private string MapModeText
        {
            get => _MapModeText;
            set { if (MapID is null) return; MapMode.text = $"<color=#777>Map Type: <color=#AAA>{value}</color>"; _MapModeText = value; }
        }
        private string _MapModeText = "";
        [UIValue(nameof(MapDiffText))] private string MapDiffText
        {
            get => _MapDiffText;
            set { if (MapID is null) return; MapDiff.text = $"<color=#777>Map Difficulty: <color=#AAA>{value}</color>"; _MapDiffText = value; }
        }
        private string _MapDiffText = "";
        [UIValue(nameof(TrueBeatmapID))] private string TrueBeatmapID
        {
            get => _TrueBeatmapID;
            set { if (MapID is null) return; TrueMapID.text = $"<color=#777>True Map ID: <color=#AAA>{value}</color>"; _TrueBeatmapID = value; }
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
        [UIAction(nameof(UpdateProfilePP))] private void UpdateProfilePP()
        {
            float ppAmount = Mathf.Round(ProfilePPSlider.Value);
            WeightedText.SetText($"<color=green>Weighted</color> {GetPPLabel()}");
            string weightedPp = $"{CurrentProfile.GetWeightedPP(ppAmount)}";
            if (weightedPp[0] == '-') weightedPp = "Unknown";
            WeightedTextValue.SetText("<color=#A72>" + weightedPp + "</color> " + GetPPLabel());
            ProfilePPText.SetText($"<color=yellow>Profile</color> {GetPPLabel()}");
            string profilePp = $"{CurrentProfile.GetProfilePPRaw(ppAmount)}";
            if (profilePp[0] == '-') profilePp = "Unknown";
            ProfilePPTextValue.SetText("<color=purple>" + profilePp + "</color> " + GetPPLabel());
        }
        /*[UIAction(nameof(DoTestThing))]
        private void DoTestThing()
        {
            //CurrentProfile.AddPlay(ProfilePPSlider.Value);
            //UpdateProfilePP();
            //UpdateProfile();
            CompletedMap(0.995f, Sldvc.selectedDifficultyBeatmap);
        }*/
        [UIAction(nameof(RefreshProfilePP))] private void RefreshProfilePP()
        {
            Task.Run(async () => await RefreshProfileScores());
        }
        private async Task RefreshProfileScores()
        {
            AsyncLock.Releaser? theLock = await ProfileLock.TryLockAsync();
            if (theLock is null) return;
            using (theLock.Value)
            {
                ReloadDataButton.interactable = false;
                if (CurrentProfile is null)
                    UpdateProfile();
                CurrentProfile.ReloadScores();
                await Refresh(true);
                ReloadDataButton.interactable = true;
            }
        }
        private IEnumerator DelayUpdatePlayTable(Table theTable)
        {
            yield return new WaitForEndOfFrame();
            theTable.UpdateTable();
            const int ButtonWidths = 60; //correct value: 60
            PlayTableButtons.spacing = (theTable.TableWidth - ButtonWidths) / 2f;
            (PlayTableModal.transform as RectTransform).sizeDelta = new Vector2(theTable.TableWidth, theTable.TableHeight * 1.5f);
            //theTable.SpawnButtonsForColumn(5, str => Plugin.Log.Info("Button Pressed! Id: " + str), PlayTableButton, "Buttons");
        }
        [UIAction(nameof(PlayTable_PageUp))] private void PlayTable_PageUp()
        {
            if (CurrentProfile is null || CurrentProfile.PlayTable is null) return;
            CurrentProfile.PageUp();
            CoroutineHost.Start(DelayUpdatePlayTable(CurrentProfile.PlayTable));
        }
        [UIAction(nameof(PlayTable_PageTop))] private void PlayTable_PageTop()
        {
            if (CurrentProfile is null || CurrentProfile.PlayTable is null) return;
            CurrentProfile.PageTop();
            CoroutineHost.Start(DelayUpdatePlayTable(CurrentProfile.PlayTable));
        }
        [UIAction(nameof(PlayTable_PageDown))] private void PlayTable_PageDown()
        {
            if (CurrentProfile is null || CurrentProfile.PlayTable is null) return;
            CurrentProfile.PageDown();
            CoroutineHost.Start(DelayUpdatePlayTable(CurrentProfile.PlayTable));
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
        [UIAction("#ShowPlayTable")] private void ShowPlayTable()
        {
            IsPlayTableOpen = true;
            if (CurrentProfile is null)
                UpdateProfile();
            Table theTable = CurrentProfile.PlayTable;
            if (theTable is null)
            {
                Profile.TextContainer = PlayTable;
                theTable = CurrentProfile.PlayTable;
            }

            if (!theTable.ContainerUpdated || UpdatePlayTableOnOpen)
            {
                UpdatePlayTableOnOpen = false;
                CoroutineHost.Start(DelayUpdatePlayTable(theTable));
            }
        }
        [UIAction("#HidePlayTable")] private void HidePlayTable() => IsPlayTableOpen = false;
        [UIAction("#ShowSessionTable")] private void ShowSessionTable()
        {
            int hold = CurrentProfile?.CurrentSession.PlaysSet ?? 0;
            SessionWindow_PlaysSet.SetText($"<color=#0F0>{hold}</color> Top {Profile.GetPlusOneCount(CurrentLeaderboard)} Score{(hold == 1 ? "" : "s")}");
            SessionWindow_PpGained.SetText($"+<color=purple>{CurrentProfile?.CurrentSession.GainedProfilePp ?? 0}</color> Profile {GetPPLabel().ToUpper()}");

            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
#if NEW_VERSION
                SessionTable.Data = SessionTable_Infos;
                SessionTable.TableView.ReloadData();
#else
                SessionTable.data = SessionTable_Infos;
                SessionTable.tableView.ReloadData();
#endif
                (SessionWindow.transform as RectTransform).sizeDelta = new Vector2(120, 50);
            }
            CoroutineHost.Start(WaitThenUpdate());
        }
        [UIAction("#UpdateCurrentTable")] private void UpdateCurrentTable() => BuildTable();
        [UIAction("#UpdateCurrentTab")] private void UpdateCurrentTab() => UpdateTabDisplay(true);
        [UIAction("#post-parse")] private void DoStuff()
        {
            HelpfulMisc.CoupleMinMaxSliders(MinAccSlider, MaxAccSlider);
            HelpfulMisc.CoupleMinMaxSliders(MinPPSlider, MaxPPSlider);
            foreach (string s in SelectionButtonTags)
                foreach (GameObject go in Parser.GetObjectsWithTag(s))
                    go.SetActive(false);
            CaptureTab.IsVisible = IsBL;
            ProfileTab.IsVisible = ShowProfileTab;
            AccSaberSetting.gameObject.SetActive(IsAP);
            MapID.gameObject.SetActive(!ShowTrueID);
            TrueMapID.gameObject.SetActive(ShowTrueID);
        }
#endregion
        #region Inits
        static PpInfoTabHandler()
        {
            InitFormatters();
            Instance = new PpInfoTabHandler();
            Instance.ChangeMapListeners(true);
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
            Sldvc.didChangeContentEvent += async (a, b) => await Refresh().ConfigureAwait(false);
#if NEW_VERSION
            Sldvc.didChangeDifficultyBeatmapEvent += async a => await Refresh().ConfigureAwait(false); // 1.37.0 and above
#else
            Sldvc.didChangeDifficultyBeatmapEvent += async (a, b) => await Refresh().ConfigureAwait(false); // 1.34.2 and below
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

        private async Task<float> UpdateTargetPP()
        {
            if (Sldvc is null) return 0f;
#if NEW_VERSION
            CurrentMap = Sldvc.beatmapKey; // 1.37.0 and above
            if (CurrentMap == default) return 0f;
#else
            CurrentMap = Sldvc.selectedDifficultyBeatmap; // 1.34.2 and below
            if (CurrentMap is null) return 0f;
#endif
            
            CurrentDiff = (null, null);
            if (!Sldvc.beatmapLevel.levelID.Substring(0, 6).Equals("custom")) return 0.0f; //means the level selected is not custom
#if NEW_VERSION
            string mode = CurrentMap.beatmapCharacteristic.serializedName; // 1.37.0 and above
#else
            string mode = CurrentMap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName; // 1.34.2 and below
#endif
            string hash = Sldvc.beatmapLevel.levelID.Split('_')[2];
            JToken scoreData = await CurrentAPI.GetScoreData(
                Targeter.TargetID,
                hash,
                CurrentMap.difficulty.ToString().Replace("+", "Plus"),
                mode,
                true //Debug option, just prevents prints when the API was called.
                ).ConfigureAwait(false);
            TargetHasScore = !(scoreData is null);
            if (!TargetHasScore) { await UpdateDiff().ConfigureAwait(false); return 0.0f; } //target doesn't have a score on this diff.
            JToken diffData = null;
            try
            {
                string actualMode = TheCounter.SelectMode(mode, CurrentLeaderboard);
                Map map = await TheCounter.GetMap(hash, actualMode, CurrentLeaderboard, true);
                //Plugin.Log.Info($"SelectedMap: {map}");
                if (!map.TryGet(actualMode, CurrentMap.difficulty, out var val))
                    throw new Exception();
                BeatmapID = val.MapId;
                diffData = val.Data;
                TrueBeatmapID = BLAPI.CleanUpId(IsBL ? BeatmapID : map.Get(mode ?? "Standard", CurrentMap.difficulty).MapId);
#if NEW_VERSION
                BeatmapName = Sldvc.beatmapLevel.songName;
#else
                BeatmapName = CurrentMap.level.songName;
#endif
            }
            catch (Exception)
            { //This exception should only happen when a map isn't on the radar of the selected leaderboard (aka unranked), and thus doesn't need to be broadcasted.
                await UpdateDiff(true).ConfigureAwait(false);
                return 0.0f;
            }
            MapDiffText = HelpfulMisc.AddSpaces(CurrentMap.difficulty.ToString().Replace("+", "Plus"));
            MapModeText = HelpfulMisc.AddSpaces(mode);
            float outp = CurrentAPI.GetPP(scoreData);
            if (outp == 0.0f && !CurrentAPI.AreRatingsNull(diffData)) //If the score set doesn't have a pp value, calculate one manually. Make sure there are ratings to do calculation, otherwise skip.
                outp = CurrentCalculator.Inflate(CurrentCalculator.GetSummedPp(CurrentAPI.GetScore(scoreData) / (float)CurrentAPI.GetMaxScore(diffData), CurrentAPI.GetRatings(diffData)));
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
            float[] ratings = HelpfulPaths.GetAllRatings(IsBL && !(CurrentDiff.Diffdata["difficulty"] is null) ? CurrentDiff.Diffdata["difficulty"] : CurrentDiff.Diffdata, calc); //ss-sf, [star, acc, pass, tech] (selects by leaderboard)
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
            //Plugin.Log.Info($"rating len: {ratings.Length}, len: {len}");
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
            Task.Run(() => CoroutineHost.Start(DelayRoutine()));
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
        private void ChangeMapListeners(bool toEnable)
        {
            if (toEnable)
            {
                BSEvents.levelCleared += SucceededMap;
                /*BSEvents.levelFailed += FailedMap;
                BSEvents.levelQuit += FailedMap;
                BSEvents.levelRestarted += FailedMap;*/
            } else
            {
                BSEvents.levelCleared -= SucceededMap;
                /*BSEvents.levelFailed -= FailedMap;
                BSEvents.levelQuit -= FailedMap;
                BSEvents.levelRestarted -= FailedMap;*/
            }
        }
        private void SucceededMap(StandardLevelScenesTransitionSetupDataSO transition, LevelCompletionResults results)
        {
            ClearMapTabs();
            TheCounter.SettingChanged = true;
            Task.Run(async () => {
#if NEW_VERSION
                await CompletedMap(HelpfulMisc.GetAcc(transition, results), transition.beatmapKey, transition.beatmapLevel);
#else
                await CompletedMap(HelpfulMisc.GetAcc(transition, results), transition.difficultyBeatmap);
#endif
            });
        }
        /*private void FailedMap(StandardLevelScenesTransitionSetupDataSO transition, LevelCompletionResults results)
        {
            FinalMapData = default;
        }*/
#if NEW_VERSION
        private Task CompletedMap(float finalAcc, BeatmapKey diffData, BeatmapLevel levelData)
        {
            return CompletedMap(
                finalAcc,
                levelData.levelID.Split('_')[2],
                levelData.songName,
                diffData.difficulty,
                diffData.beatmapCharacteristic.serializedName
                );
        }
#else
        private Task CompletedMap(float finalAcc, IDifficultyBeatmap data)
        {
            return CompletedMap(
                finalAcc,
                data.level.levelID.Split('_')[2],
                data.level.songName,
                data.difficulty,
                data.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName
                );
        }
#endif
        private Task CompletedMap(float finalAcc, string hash, string mapName, BeatmapDifficulty diff, string mode)
        {
            //finalAcc = 1.0f;
            Plugin.Log.Debug("Map completion info: " + HelpfulMisc.Print(new object[5] { finalAcc, hash, mapName, diff, mode }));
            if (float.IsNaN(finalAcc)) 
            {
                TheCounter.LastMap = default;
                return Task.CompletedTask; 
            }
            Task outp = Task.Run(async () =>
            {
                if (await Profile.AddPlay(Targeter.PlayerID, hash, finalAcc, mapName, diff, mode).ConfigureAwait(false))
                {
                    await Refresh(true).ConfigureAwait(false);
                }
            });
            TheCounter.LastMap = default;
            return outp ?? Task.CompletedTask;
        }
        private void UpdateDisplayVisibility(bool waitForEndOfFrame = true)
        {
            void Update()
            {
                AccSaberSetting.gameObject.SetActive(IsAP);
                if ((CaptureTab.IsVisible == IsBL) && (ProfileTab.IsVisible == ShowProfileTab))
                    return;
#if NEW_VERSION
                int currentTabNum = MainTabSelector.TextSegmentedControl.selectedCellNumber;
#else
                int currentTabNum = MainTabSelector.textSegmentedControl.selectedCellNumber;
#endif

                bool change = CaptureTab.IsVisible != IsBL;
                bool keepPos = !change || !CurrentTab.Equals(CaptureTab.TabName);
                if (change)
                {
                    CaptureTab.IsVisible = IsBL;
                    if (currentTabNum >= TabMapInfo.Keys.ToList().IndexOf(CaptureTab.TabName))
                        currentTabNum += IsBL ? 1 : -1;
                }

                change = ProfileTab.IsVisible != ShowProfileTab;
                keepPos &= !change || !CurrentTab.Equals(ProfileTab.TabName);
                if (change)
                {
                    ProfileTab.IsVisible = ShowProfileTab;
                    if (currentTabNum >= TabMapInfo.Keys.ToList().IndexOf(ProfileTab.TabName))
                        currentTabNum += ShowProfileTab ? 1 : -1;
                }

                MainTabSelector.ForceSelectAndNotify(keepPos ? currentTabNum : 0);
                if (!keepPos) CurrentTab = TabMapInfo.Keys.First();
            }
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                Update();
            }
            if (waitForEndOfFrame) CoroutineHost.Start(WaitThenUpdate());
            else Update();
        }
        public string GetPPLabel() => IsAP ? "ap" : "pp";
        public Task UpdateTabDisplay(bool forceUpdate = false, bool runAsync = true) 
        {
            if (CurrentTab.Length == 0 || (!forceUpdate && TabMapInfo[CurrentTab] == CurrentMap)) return Task.CompletedTask;
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                UpdateDisplayVisibility(false);
            }
            async Task Update()
            {
                if (ChangeTabSettings)
                {
                    ChangeTabSettings = false;
                    await WaitThenUpdate().AsTask(CoroutineHost.Instance).ConfigureAwait(false);
                }
                //Plugin.Log.Info("DiffData:\n" + CurrentDiff.Diffdata);
                if (CurrentTab.Equals("Settings")) return;
                TabMapInfo[CurrentTab] = CurrentMap;
                Updates[CurrentTab].Invoke(this);
                BuildTable();
            }
            if (runAsync) 
                return Task.Run(Update);
            return Update();
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
                        StarRatings.SetText($"<line-height={LineHeight}%>{Math.Round(CurrentAPI.GetRatings(CurrentDiff.Diffdata)[0], PC.DecimalPrecision)} {Star}");
                        break;
                    case Leaderboards.Accsaber:
                        StarRatings.SetText($"<line-height={LineHeight}%>{Math.Round(CurrentAPI.GetRatings(CurrentDiff.Diffdata)[0], PC.DecimalPrecision)} {Star}");
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
            PPToCapture = ClanCounter.LoadNeededPp(BeatmapID, out _, out string owningClan)?.First() ?? -1f;
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
        private async Task UpdateDiff(bool mapFailed = false)
        {
#if NEW_VERSION
            BeatmapDifficulty diff = Sldvc.beatmapKey.difficulty; // 1.37.0 and below
            string modeName = Sldvc.beatmapKey.beatmapCharacteristic.serializedName;
            string hash = Sldvc.beatmapLevel.levelID.Split('_')[2];
#else
            BeatmapDifficulty diff = Sldvc.selectedDifficultyBeatmap.difficulty; // 1.34.2 and above
            string modeName = Sldvc.selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            string hash = Sldvc.selectedDifficultyBeatmap.level.levelID.Split('_')[2];
#endif
            string actualModeName = TheCounter.SelectMode(modeName, CurrentLeaderboard);
            Map map = mapFailed ? null : await TheCounter.GetMap(hash, actualModeName, CurrentLeaderboard, true);
            (string MapId, JToken Data) val = default;
            bool failed = !(map?.TryGet(actualModeName, diff, out val) ?? false);
            if (failed)
            {
                //Plugin.Log.Warn("Map failed to load. Most likely unranked.");
                map = await TheCounter.GetMap(hash, modeName, Leaderboards.Beatleader, true);
                if (!map.TryGet(modeName, diff, out val))
                {
                    Plugin.Log.Error("Completely failed to load any map whatsoever. Either you are disconnected from the internet or beatleader is down.");
                    return;
                }
            }
            BeatmapID = val.MapId;
            JToken tokens = val.Data;
            TrueBeatmapID =  BLAPI.CleanUpId(IsBL || failed ? BeatmapID : (await TheCounter.GetMap(hash, modeName, Leaderboards.Beatleader, true)).Get(modeName ?? "Standard", CurrentMap.difficulty).MapId);
            //Plugin.Log.Info("CurrentMap\n" + map);
            CurrentDiff = (tokens, CurrentDiff.Scoredata);
            MapDiffText = HelpfulMisc.AddSpaces(CurrentMap.difficulty.ToString().Replace("+", "Plus"));
            MapModeText = HelpfulMisc.AddSpaces(modeName);
#if NEW_VERSION
            BeatmapName = Sldvc.beatmapLevel.songName;
#else
            BeatmapName = CurrentMap.level.songName;
#endif
            if (failed || CurrentAPI.AreRatingsNull(CurrentDiff.Diffdata))
                CurrentDiff = (null, null);
        }
        private void UpdateProfile()
        {
            CurrentProfile = IsAP ? Profile.GetProfile(CurrentLeaderboard, Targeter.PlayerID, _APSetting) : Profile.GetProfile(CurrentLeaderboard, Targeter.PlayerID);
            CurrentProfile.ReloadTableValues();
            PlusOneLabel.SetText("+1 " + GetPPLabel());
            PlusOneText.SetText($"<color=#0F0>{CurrentProfile.PlusOne}</color> {GetPPLabel()}");
            LevelText.SetText($"Level: <color=#0F0>{CurrentProfile.Level}</color>");
            ExperienceText.SetText($"Experience: <color=#0F0>{CurrentProfile.Experience}</color>");
            UpdateProfilePP();
        }
        public Task Refresh(bool forceRefresh = false) => DoRefresh(forceRefresh);
        private async Task DoRefresh(bool forceRefresh)
        {
            //Plugin.Log.Info($"ActualMap: {Sldvc?.selectedDifficultyBeatmap.level.songName ?? "null"} || Currently Saved Map: {CurrentMap?.level.songName ?? "null"}");
            //Plugin.Log.Info("TabSelectionPatch: " + TabSelectionPatch.GetIfTabIsSelected(TabName));
            if (!TabSelectionPatch.GetIfTabIsSelected(TabName) || !TabSelectionPatch.IsOnModsTab) return;
#if NEW_VERSION
            if (!forceRefresh && (Sldvc?.beatmapKey.Equals(CurrentMap) ?? false)) return; // 1.37.0 and above

#else
            if (!forceRefresh && (Sldvc?.selectedDifficultyBeatmap?.Equals(CurrentMap) ?? false)) return; // 1.34.2 and below

#endif
            AsyncLock.Releaser? theLock = await RefreshLock.TryLockAsync();
            if (theLock is null) return;
            using (theLock.Value)
            {
                try
                {
                    CurrentLeaderboard = PC.LeaderboardsInUse.First();
                    UpdateMods();
                    TargetPP = await UpdateTargetPP();
                    await UpdateTabDisplay(forceRefresh, false);
                    await UpdatePlayTable();
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error("There was an issue refreshing the " + TabName + " display!");
                    Plugin.Log.Error(ex);
                }
            }
        }
        private async Task UpdatePlayTable()
        {
            if (CurrentProfile is null)
                return;
            if (IsPlayTableOpen)
                await DelayUpdatePlayTable(CurrentProfile.PlayTable).AsTask(Sldvc).ConfigureAwait(false);
            else
                UpdatePlayTableOnOpen = true;
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
            string[] tabNames = TabMapInfo.Keys.ToArray();
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
            HelpfulMisc.SetIncrements(amount, MinPPSlider, MaxPPSlider, CA_PPSliderSlider, ProfilePPSlider);
            HelpfulMisc.SetIncrements(amount, 1f / 100f, MinAccSlider, MaxAccSlider, CA_PercentSliderSlider);
        }
#endregion
    }
}
