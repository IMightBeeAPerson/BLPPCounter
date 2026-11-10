using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Models.Base;
using BLPPCounter.Models.MapModels;
using BLPPCounter.Patches;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;
using BS_Utils.Utilities;
using HMUI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static GameplayModifiers;

namespace BLPPCounter.Settings.SettingHandlers
{
    internal sealed class PpInfoTabHandler : SafeNotifyPropertyChanged, IInitializable, IDisposable
    {
#pragma warning disable IDE0051, IDE0044, CS0649

        public const string TabName = "PP Calculator";

        private static readonly string[] SelectionButtonTags = ["SSButton", "NMButton", "FSButton", "SFButton"];

        private readonly StandardLevelDetailViewController _levelDetailView;
        private readonly GameplayModifiersPanelController _modifiersPanel;
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly SettingsHandler _settingsHandler;
        private readonly PluginConfig _config;
        private readonly LeaderboardSelector _leaderboardSelector;
        private readonly PathUtils _pathUtils;
        private readonly LeaderboardSettingsHandler _leaderboardSettings;
        private readonly IProfileRepository _profileRepository;
        private readonly ICompletedPlayService _completedPlayService;
        private readonly ITargeter _targeter;
        private readonly IBackgroundTaskRunner _taskRunner;
        private readonly ProfilePanelPresenter _profilePanelPresenter;
        private readonly IMapRepository _mapRepository;
        private readonly IClanService _clanService;
        private readonly IMainCounterFormattingService _mainFormatting;
        private readonly Formatter.Factory _formatterFactory;
        private readonly TokenParser _tokenParser;
        private readonly Table.Factory _tableFactory;

        private readonly CancellationTokenSource _lifetimeSource = new();
        private readonly AsyncLock _refreshLock = new();
        private readonly AsyncLock _profileLock = new();

        private readonly Dictionary<string,
#if NEW_VERSION
            BeatmapKey
#else
            IDifficultyBeatmap
#endif
        > _tabMapInfo = new()
        {
            { "Info", default },
            { "Capture", default },
            { "Relative", default },
            { "Custom", default },
            { "Profile", default },
            { "Settings", default },
        };

        private string _currentMods = "";
        private string _unformattedCurrentMods = "";
        private string _currentTab = "Info";

        private float _currentModMultiplier = 1f;
        private float _targetPp;
        private float _ppToCapture;

#if NEW_VERSION
        private BeatmapKey _currentMap;
#else
        private IDifficultyBeatmap? _currentMap;
#endif

        private (ICacheModel? DiffData, JToken? ScoreData) _currentDiff;

        private Table? _clanTable;
        private Table? _relativeTable;
        private Table? _ppTable;
        private Table? _percentTable;

        private FormatWrapper? _noScoreTargetWrapper;
        private Func<string>? _getTarget;
        private Func<string>? _getNoScoreTarget;

        private Profile? _currentProfile;

        private bool _selectButtonsOn;
        private bool _targetHasScore;
        private bool _isPpMode = true;
        private bool _isPlayTableOpen;
        private bool _postParsed;
        private bool _disposed;
        private bool _sldvcListenersAttached;

        private APCategory _accSaberCategory = APCategory.All;

        private bool _showTrueId;
        private float _percentSliderMin;
        private float _percentSliderMax;
        private int _ppSliderMin;
        private int _ppSliderMax;
        private int _sliderIncrementNumber;

        private string _beatmapName = "";
        private string _beatmapId = "";
        private string _mapModeText = "";
        private string _mapDifficultyText = "";
        private string _trueBeatmapId = "";

        public PpInfoTabHandler(
            StandardLevelDetailViewController levelDetailView,
            IMainThreadDispatcher dispatcher,
            SettingsHandler settingsHandler,
            PluginConfig config,
            LeaderboardSelector leaderboardSelector,
            PathUtils pathUtils,
            LeaderboardSettingsHandler leaderboardSettings,
            IProfileRepository profileRepository,
            ICompletedPlayService completedPlayService,
            ITargeter targeter,
            IBackgroundTaskRunner taskRunner,
            ProfilePanelPresenter profilePanelPresenter,
            IMapRepository mapRepository,
            IClanService clanService,
            IMainCounterFormattingService mainFormatting,
            Formatter.Factory formatterFactory,
            TokenParser tokenParser,
            Table.Factory tableFactory
        )
        {
            _levelDetailView = levelDetailView ?? throw new ArgumentNullException(nameof(levelDetailView));

            _modifiersPanel =
                Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().FirstOrDefault()
                ?? throw new ArgumentNullException(nameof(_modifiersPanel));

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _settingsHandler = settingsHandler ?? throw new ArgumentNullException(nameof(settingsHandler));

            _config = config ?? throw new ArgumentNullException(nameof(config));

            _leaderboardSelector = leaderboardSelector ?? throw new ArgumentNullException(nameof(leaderboardSelector));

            _pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));

            _leaderboardSettings = leaderboardSettings ?? throw new ArgumentNullException(nameof(leaderboardSettings));

            _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));

            _completedPlayService =
                completedPlayService ?? throw new ArgumentNullException(nameof(completedPlayService));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));

            _profilePanelPresenter =
                profilePanelPresenter ?? throw new ArgumentNullException(nameof(profilePanelPresenter));

            _mapRepository = mapRepository ?? throw new ArgumentNullException(nameof(mapRepository));

            _clanService = clanService ?? throw new ArgumentNullException(nameof(clanService));

            _mainFormatting = mainFormatting ?? throw new ArgumentNullException(nameof(mainFormatting));

            _formatterFactory = formatterFactory ?? throw new ArgumentNullException(nameof(formatterFactory));

            _tokenParser = tokenParser ?? throw new ArgumentNullException(nameof(tokenParser));

            _tableFactory = tableFactory ?? throw new ArgumentNullException(nameof(tableFactory));
        }

        internal Leaderboards CurrentLeaderboard { get; private set; }

        public bool ChangeTabSettings { get; set; }

        private Calculator CurrentCalculator => _leaderboardSelector.GetCalc(CurrentLeaderboard);

        private APIHandler CurrentApi => _leaderboardSelector.GetAPI(CurrentLeaderboard);

        private bool UsesMods => CurrentCalculator.UsesModifiers;

        private bool IsBeatLeader => CurrentLeaderboard == Leaderboards.Beatleader;

        private bool IsAccSaber => CurrentLeaderboard == Leaderboards.Accsaber;

        private const bool ShowProfileTab = true;

        #region BSML fields

        [UIParams]
        private BSMLParserParams Parser = null!;

        [UIComponent(nameof(MainTabSelector))]
        internal TabSelector MainTabSelector = null!;

        [UIComponent(nameof(SaveLeaderboardButton))]
        private Button SaveLeaderboardButton = null!;

        [UIComponent(nameof(CalcSelector))]
        internal ListSetting CalcSelector = null!;

        [UIComponent(nameof(MinAccSlider))]
        private SliderSetting MinAccSlider = null!;

        [UIComponent(nameof(MaxAccSlider))]
        private SliderSetting MaxAccSlider = null!;

        [UIComponent(nameof(MinPPSlider))]
        private SliderSetting MinPPSlider = null!;

        [UIComponent(nameof(MaxPPSlider))]
        private SliderSetting MaxPPSlider = null!;

        [UIComponent(nameof(ProfileTab))]
        private Tab ProfileTab = null!;

        [UIComponent(nameof(PlayTable))]
        private TextMeshProUGUI PlayTable = null!;

        [UIComponent(nameof(PlayTableButtons))]
        private HorizontalLayoutGroup PlayTableButtons = null!;

        [UIObject(nameof(PlayTableModal))]
        private GameObject PlayTableModal = null!;

        [UIObject(nameof(SessionWindow))]
        private GameObject SessionWindow = null!;

        [UIComponent(nameof(SessionWindow_PlaysSet))]
        private TextMeshProUGUI SessionWindow_PlaysSet = null!;

        [UIComponent(nameof(SessionWindow_PpGained))]
        private TextMeshProUGUI SessionWindow_PpGained = null!;

        [UIComponent(nameof(SessionTable))]
        private CustomCellListTableData SessionTable = null!;

        [UIComponent(nameof(PlusOneLabel))]
        private TextMeshProUGUI PlusOneLabel = null!;

        [UIComponent(nameof(PlusOneText))]
        private TextMeshProUGUI PlusOneText = null!;

        [UIComponent(nameof(ReloadDataButton))]
        private Button ReloadDataButton = null!;

        [UIComponent(nameof(AccSaberSetting))]
        private DropDownListSetting AccSaberSetting = null!;

        [UIComponent(nameof(ProfilePPSlider))]
        private SliderSetting ProfilePPSlider = null!;

        [UIComponent(nameof(WeightedText))]
        private TextMeshProUGUI WeightedText = null!;

        [UIComponent(nameof(WeightedTextValue))]
        private TextMeshProUGUI WeightedTextValue = null!;

        [UIComponent(nameof(ProfilePPText))]
        private TextMeshProUGUI ProfilePPText = null!;

        [UIComponent(nameof(ProfilePPTextValue))]
        private TextMeshProUGUI ProfilePPTextValue = null!;

        [UIComponent(nameof(PercentTable))]
        private TextMeshProUGUI PercentTable = null!;

        [UIComponent(nameof(PPTable))]
        private TextMeshProUGUI PPTable = null!;

        [UIComponent("ModeButton")]
        private TextMeshProUGUI ModeButtonText = null!;

        [UIObject(nameof(CA_PercentSlider))]
        private GameObject CA_PercentSlider = null!;

        [UIComponent(nameof(CA_PercentSlider))]
        private SliderSetting CA_PercentSliderSlider = null!;

        [UIObject(nameof(CA_PPSlider))]
        private GameObject CA_PPSlider = null!;

        [UIComponent(nameof(CA_PPSlider))]
        private SliderSetting CA_PPSliderSlider = null!;

        [UIObject(nameof(PercentTable_BG))]
        private GameObject PercentTable_BG = null!;

        [UIObject(nameof(PPTable_BG))]
        private GameObject PPTable_BG = null!;

        [UIComponent(nameof(RelativeText))]
        private TextMeshProUGUI RelativeText = null!;

        [UIComponent(nameof(RelativeTarget))]
        private TextMeshProUGUI RelativeTarget = null!;

        [UIComponent(nameof(RelativeTable))]
        private TextMeshProUGUI RelativeTable = null!;

        [UIComponent(nameof(CaptureTab))]
        private Tab CaptureTab = null!;

        [UIComponent(nameof(ClanTable))]
        private TextMeshProUGUI ClanTable = null!;

        [UIComponent(nameof(OwningClan))]
        private TextMeshProUGUI OwningClan = null!;

        [UIComponent(nameof(ClanTarget))]
        private TextMeshProUGUI ClanTarget = null!;

        [UIComponent(nameof(PPTarget))]
        private TextMeshProUGUI PPTarget = null!;

        [UIComponent(nameof(SpeedModText))]
        private TextMeshProUGUI SpeedModText = null!;

        [UIComponent(nameof(ModMultText))]
        private TextMeshProUGUI ModMultText = null!;

        [UIComponent(nameof(PrefixLabels))]
        private TextMeshProUGUI PrefixLabels = null!;

        [UIComponent(nameof(StarRatings))]
        private TextMeshProUGUI StarRatings = null!;

        [UIComponent(nameof(MapName))]
        private TextMeshProUGUI MapName = null!;

        [UIComponent(nameof(MapID))]
        private TextMeshProUGUI MapID = null!;

        [UIComponent(nameof(MapMode))]
        private TextMeshProUGUI MapMode = null!;

        [UIComponent(nameof(MapDiff))]
        private TextMeshProUGUI MapDiff = null!;

        [UIComponent(nameof(TrueMapID))]
        private TextMeshProUGUI TrueMapID = null!;

        #endregion

        #region BSML values

#if DEBUG
        [UIValue(nameof(Testing))]
        private bool Testing => true;
#else
        [UIValue(nameof(Testing))]
        private bool Testing => false;
#endif

        [UIValue(nameof(ShowTrueID))]
        private bool ShowTrueID
        {
            get => _showTrueId;
            set
            {
                _showTrueId = value;

                MapID?.gameObject.SetActive(!value);

                TrueMapID?.gameObject.SetActive(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(CalcLeaderboard))]
        private string CalcLeaderboard
        {
            get => _config.CalcLeaderboard.ToString();
            set => _config.CalcLeaderboard = Enum.Parse<Leaderboards>(value);
        }

        [UIValue(nameof(CalcLeaderboards))]
        private List<object> CalcLeaderboards =>
            [.. _leaderboardSettings.UsableLeaderboards.Select(leaderboard => (object)leaderboard.ToString())];

        [UIValue(nameof(PercentSliderMin))]
        private float PercentSliderMin
        {
            get => _percentSliderMin;
            set
            {
                _percentSliderMin = value;

                CA_PercentSliderSlider?.ChangeMinValue(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PercentSliderMax))]
        private float PercentSliderMax
        {
            get => _percentSliderMax;
            set
            {
                _percentSliderMax = value;

                CA_PercentSliderSlider?.ChangeMaxValue(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PPSliderMin))]
        private int PPSliderMin
        {
            get => _ppSliderMin;
            set
            {
                _ppSliderMin = value;

                CA_PPSliderSlider?.ChangeMinValue(value);

                ProfilePPSlider?.ChangeMinValue(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PPSliderMax))]
        private int PPSliderMax
        {
            get => _ppSliderMax;
            set
            {
                _ppSliderMax = value;

                CA_PPSliderSlider?.ChangeMaxValue(value);

                ProfilePPSlider?.ChangeMaxValue(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(SliderIncrementNum))]
        private int SliderIncrementNum
        {
            get => _sliderIncrementNumber;
            set
            {
                _sliderIncrementNumber = value;

                if (_postParsed)
                    UpdateTabSliders(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PercentSliderIncrementNum))]
        private float PercentSliderIncrementNum => SliderIncrementNum / 100f;

        [UIValue(nameof(ProfileTestPp))]
        private int ProfileTestPp
        {
            get => _config.ProfileTestPP;
            set => _config.ProfileTestPP = value;
        }

        [UIValue(nameof(APSetting))]
        private string APSetting
        {
            get => _accSaberCategory.ToString();
            set
            {
                _accSaberCategory = Enum.Parse<APCategory>(value);

                _taskRunner.Run(LoadCurrentProfileAsync, "Change AccSaber profile category", _lifetimeSource.Token);
            }
        }

        [UIValue(nameof(APCategorySettings))]
        private List<object> APCategorySettings => [.. Enum.GetNames(typeof(APCategory)).Skip(1).Cast<object>()];

        [UIValue(nameof(SessionTable_Infos))]
        private List<object> SessionTable_Infos => [.. _profilePanelPresenter.SessionRows];

        [UIValue(nameof(TestAcc))]
        private float TestAcc
        {
            get => _config.TestAccAmount;
            set => _config.TestAccAmount = value;
        }

        [UIValue(nameof(TestPp))]
        private int TestPp
        {
            get => _config.TestPPAmount;
            set => _config.TestPPAmount = value;
        }

#pragma warning disable CS0414
        [UIValue(nameof(TabPos))]
#if NEW_VERSION
        private float TabPos = -5.5f;
#else
        private float TabPos;
#endif
#pragma warning restore CS0414

        [UIValue(nameof(BeatmapName))]
        private string BeatmapName
        {
            get => $"<color=#777>Map Name: " + $"<color=#AAA>{_beatmapName.ClampString(25)}</color>";
            set
            {
                _beatmapName = value;

                MapName?.SetText($"<color=#777>Map Name: " + $"<color=#AAA>{value.ClampString(25)}</color>");
            }
        }

        [UIValue(nameof(BeatmapID))]
        private string BeatmapID
        {
            get => $"<color=#777>Map ID: " + $"<color=#AAA>{_beatmapId}</color>";
            set
            {
                _beatmapId = value;

                MapID?.SetText($"<color=#777>Map ID: " + $"<color=#AAA>{value}</color>");
            }
        }

        [UIValue(nameof(MapModeText))]
        private string MapModeText
        {
            get => $"<color=#777>Map Type: " + $"<color=#AAA>{_mapModeText}</color>";
            set
            {
                _mapModeText = value;

                MapMode?.SetText($"<color=#777>Map Type: " + $"<color=#AAA>{value}</color>");
            }
        }

        [UIValue(nameof(MapDiffText))]
        private string MapDiffText
        {
            get => $"<color=#777>Map Difficulty: " + $"<color=#AAA>{_mapDifficultyText}</color>";
            set
            {
                _mapDifficultyText = value;

                MapDiff?.SetText($"<color=#777>Map Difficulty: " + $"<color=#AAA>{value}</color>");
            }
        }

        [UIValue(nameof(TrueBeatmapID))]
        private string TrueBeatmapID
        {
            get => $"<color=#777>True Map ID: " + $"<color=#AAA>{_trueBeatmapId}</color>";
            set
            {
                _trueBeatmapId = value;

                TrueMapID?.SetText($"<color=#777>True Map ID: " + $"<color=#AAA>{value}</color>");
            }
        }

        #endregion

        #region Lifecycle

        public void Initialize()
        {
            ThrowIfDisposed();

            CurrentLeaderboard = _config.CalcLeaderboard;

            _showTrueId = _config.ShowTrueID;

            _percentSliderMin = _config.PercentSliderMin;

            _percentSliderMax = _config.PercentSliderMax;

            _ppSliderMin = _config.PPSliderMin;

            _ppSliderMax = _config.PPSliderMax;

            _sliderIncrementNumber = _config.SliderIncrementNum;

            SldvcInit();

            ChangeMapListeners(true);
            InitFormatters();

            TabSelectionPatch.AddToTabSelectedAction(TabName, HandleCalculatorTabSelected);

            _settingsHandler.PropertyChanged += HandleSettingsChanged;

            _taskRunner.Run(LoadCurrentProfileAsync, "Initialize PP calculator profile", _lifetimeSource.Token);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            ChangeMapListeners(false);

            TabSelectionPatch.RemoveFromTabSelectedAction(TabName, HandleCalculatorTabSelected);

            TabSelectionPatch.SetIgnoredSelector(null);

            _settingsHandler.PropertyChanged -= HandleSettingsChanged;

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();

            _profilePanelPresenter.Dispose();

            _refreshLock.Dispose();
            _profileLock.Dispose();
        }

        private void HandleCalculatorTabSelected()
        {
            _taskRunner.Run(
                cancellationToken => RefreshAsync(false, cancellationToken),
                "Refresh selected PP calculator tab",
                _lifetimeSource.Token
            );
        }

        private void HandleSettingsChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SettingsHandler.Target))
            {
                ClearMapTabs();
            }
        }

        internal void SldvcInit()
        {
            if (_sldvcListenersAttached)
                return;

            _sldvcListenersAttached = true;

            _levelDetailView.didChangeContentEvent += (_, _) =>
                _taskRunner.Run(
                    cancellationToken => RefreshAsync(false, cancellationToken),
                    "Refresh PP calculator content",
                    _lifetimeSource.Token
                );

#if NEW_VERSION
            _levelDetailView.didChangeDifficultyBeatmapEvent += _ =>
                _taskRunner.Run(
                    cancellationToken => RefreshAsync(false, cancellationToken),
                    "Refresh PP calculator difficulty",
                    _lifetimeSource.Token
                );
#else
            _levelDetailView.didChangeDifficultyBeatmapEvent += (_, _) =>
            {
                _taskRunner.Run(
                    cancellationToken => RefreshAsync(false, cancellationToken),
                    "Refresh PP calculator difficulty",
                    _lifetimeSource.Token
                );
            };
#endif
        }

        #endregion

        #region BSML actions

        [UIAction(nameof(Refresh))]
        private void ForceRefresh()
        {
            _taskRunner.Run(
                cancellationToken => RefreshAsync(true, cancellationToken),
                "Force refresh PP calculator",
                _lifetimeSource.Token
            );
        }

        [UIAction(nameof(RefreshMods))]
        private void RefreshMods()
        {
            MainThreadDispatcher.AssertOnMainThread();

            UpdateMods();

            _taskRunner.Run(
                cancellationToken => UpdateTabDisplayAsync(true, cancellationToken),
                "Refresh PP calculator modifiers",
                _lifetimeSource.Token
            );
        }

        [UIAction(nameof(ChangeTab))]
        private void ChangeTab(SegmentedControl segmentedControl, int index)
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (segmentedControl.cells[index] is not TextSegmentedControlCell cell)
            {
                return;
            }

            if (_currentTab == "Settings")
                SaveSettings();

            _currentTab = cell.text;

            _taskRunner.Run(
                cancellationToken => UpdateTabDisplayAsync(false, cancellationToken),
                "Change PP calculator tab",
                _lifetimeSource.Token
            );
        }

        [UIAction(nameof(PercentFormat))]
        private string PercentFormat(float value)
        {
            return $"{value:N2}%";
        }

        [UIAction(nameof(PPFormat))]
        private string PPFormat(int value)
        {
            return $"{value} {GetPPLabel()}";
        }

        [UIAction(nameof(SaveLeaderboard))]
        private void SaveLeaderboard()
        {
            MainThreadDispatcher.AssertOnMainThread();

            SaveLeaderboardButton.interactable = false;

            ChangeTabSettings = true;

            _settingsHandler.CalcSelector.ReceiveValue();

            ResetTabs();

            _taskRunner.Run(SaveLeaderboardAsync, "Change PP calculator leaderboard", _lifetimeSource.Token);
        }

        [UIAction(nameof(ToggleCAMode))]
        private void ToggleCAMode()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _isPpMode = !_isPpMode;

            CA_PPSlider.SetActive(_isPpMode);
            CA_PercentSlider.SetActive(!_isPpMode);
            PPTable_BG.SetActive(!_isPpMode);
            PercentTable_BG.SetActive(_isPpMode);

            ModeButtonText.SetText(
                _isPpMode
                    ? "<color=#A020F0>Input " + GetPPLabel().ToUpperInvariant()
                    : "<color=#FFD700>Input Percentage"
            );

            UpdateCustomAccuracy();
            BuildTable();
        }

        [UIAction(nameof(SelectSS))]
        private void SelectSS()
        {
            SelectMod(SongSpeed.Slower);
        }

        [UIAction(nameof(SelectNM))]
        private void SelectNM()
        {
            SelectMod(SongSpeed.Normal);
        }

        [UIAction(nameof(SelectFS))]
        private void SelectFS()
        {
            SelectMod(SongSpeed.Faster);
        }

        [UIAction(nameof(SelectSF))]
        private void SelectSF()
        {
            SelectMod(SongSpeed.SuperFast);
        }

        [UIAction(nameof(UpdateProfilePP))]
        private void UpdateProfilePP()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _profilePanelPresenter.UpdateProfilePpPreview(
                Mathf.Round(ProfilePPSlider.Value),
                WeightedText,
                WeightedTextValue,
                ProfilePPText,
                ProfilePPTextValue
            );
        }

        [UIAction(nameof(DoTestThing))]
        private void DoTestThing()
        {
            MainThreadDispatcher.AssertOnMainThread();

            UpdateProfilePP();

#if NEW_VERSION
            string hash = _levelDetailView.beatmapLevel.levelID.Split('_')[2];

            string mapName = _levelDetailView.beatmapLevel.songName;

            BeatmapDifficulty difficulty = _levelDetailView.beatmapKey.difficulty;

            string mode = _levelDetailView.beatmapKey.beatmapCharacteristic.serializedName;
#else
            IDifficultyBeatmap beatmap = _levelDetailView.selectedDifficultyBeatmap;

            string hash = beatmap.level.levelID.Split('_')[2];

            string mapName = beatmap.level.songName;

            BeatmapDifficulty difficulty = beatmap.difficulty;

            string mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
#endif

            _taskRunner.Run(
                cancellationToken => CompletedMapAsync(0.999f, hash, mapName, difficulty, mode, cancellationToken),
                "Add PP calculator test play",
                _lifetimeSource.Token
            );
        }

        [UIAction(nameof(RefreshProfilePP))]
        private void RefreshProfilePP()
        {
            _taskRunner.Run(RefreshProfileScoresAsync, "Reload PP calculator profile", _lifetimeSource.Token);
        }

        [UIAction(nameof(PlayTable_PageUp))]
        private void PlayTable_PageUp()
        {
            _profilePanelPresenter.PageUp();
        }

        [UIAction(nameof(PlayTable_PageTop))]
        private void PlayTable_PageTop()
        {
            _profilePanelPresenter.PageTop();
        }

        [UIAction(nameof(PlayTable_PageDown))]
        private void PlayTable_PageDown()
        {
            _profilePanelPresenter.PageDown();
        }

        [UIAction(nameof(SaveSettings))]
        private void SaveSettings()
        {
            _config.PercentSliderMin = PercentSliderMin;

            _config.PercentSliderMax = PercentSliderMax;

            _config.PPSliderMin = PPSliderMin;

            _config.PPSliderMax = PPSliderMax;

            _config.SliderIncrementNum = SliderIncrementNum;

            _config.ShowTrueID = ShowTrueID;
        }

        [UIAction("#ShowPlayTable")]
        private void ShowPlayTable()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _isPlayTableOpen = true;

            if (_currentProfile is null)
            {
                _taskRunner.Run(
                    async cancellationToken =>
                    {
                        await LoadCurrentProfileAsync(cancellationToken).ConfigureAwait(false);

                        await _dispatcher
                            .InvokeAsync(_profilePanelPresenter.ShowPlayTable, cancellationToken)
                            .ConfigureAwait(false);
                    },
                    "Open profile play table",
                    _lifetimeSource.Token
                );

                return;
            }

            _profilePanelPresenter.ShowPlayTable();
        }

        [UIAction("#HidePlayTable")]
        private void HidePlayTable()
        {
            _isPlayTableOpen = false;
            _profilePanelPresenter.HidePlayTable();
        }

        [UIAction("#ShowSessionTable")]
        private void ShowSessionTable()
        {
            _profilePanelPresenter.ShowSessionTable();
        }

        [UIAction("#HideSessionTable")]
        private void HideSessionTable()
        {
            _profilePanelPresenter.HideSessionTable();
        }

        [UIAction("#UpdateCurrentTable")]
        private void UpdateCurrentTable()
        {
            BuildTable();
        }

        [UIAction("#UpdateCurrentTab")]
        private void UpdateCurrentTab()
        {
            _taskRunner.Run(
                cancellationToken => UpdateTabDisplayAsync(true, cancellationToken),
                "Update current PP calculator tab",
                _lifetimeSource.Token
            );
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _postParsed = true;

#if NEW_VERSION
            TabSelectionPatch.SetIgnoredSelector(MainTabSelector.TextSegmentedControl);
#else
            TabSelectionPatch.SetIgnoredSelector(MainTabSelector.textSegmentedControl);
#endif

            HelpfulMisc.CoupleMinMaxSliders(MinAccSlider, MaxAccSlider);

            HelpfulMisc.CoupleMinMaxSliders(MinPPSlider, MaxPPSlider);

            foreach (string tag in SelectionButtonTags)
            {
                foreach (GameObject gameObject in Parser.GetObjectsWithTag(tag))
                {
                    gameObject.SetActive(false);
                }
            }

            CaptureTab.IsVisible = IsBeatLeader;
            ProfileTab.IsVisible = ShowProfileTab;

            AccSaberSetting.gameObject.SetActive(IsAccSaber);

            MapID.gameObject.SetActive(!ShowTrueID);

            TrueMapID.gameObject.SetActive(ShowTrueID);

            PercentSliderMin = _percentSliderMin;
            PercentSliderMax = _percentSliderMax;
            PPSliderMin = _ppSliderMin;
            PPSliderMax = _ppSliderMax;

            UpdateTabSliders(_sliderIncrementNumber);

            _profilePanelPresenter.Initialize(
                PlayTable,
                PlayTableButtons,
                PlayTableModal,
                SessionWindow,
                SessionWindow_PlaysSet,
                SessionWindow_PpGained,
                SessionTable,
                PlusOneLabel,
                PlusOneText
            );

            if (_currentProfile is not null)
            {
                _profilePanelPresenter.Bind(_currentProfile);
            }
        }

        #endregion

        #region Profile operations

        private async Task SaveLeaderboardAsync(CancellationToken cancellationToken)
        {
            try
            {
                await LoadCurrentProfileAsync(cancellationToken).ConfigureAwait(false);

                await RefreshAsync(true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await _dispatcher
                        .InvokeAsync(() => SaveLeaderboardButton.interactable = true)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task LoadCurrentProfileAsync(CancellationToken cancellationToken)
        {
            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            Leaderboards leaderboard = await _dispatcher
                .InvokeAsync(() => _config.CalcLeaderboard, cancellationToken)
                .ConfigureAwait(false);

            APCategory category = leaderboard == Leaderboards.Accsaber ? _accSaberCategory : APCategory.None;

            Profile profile = await _profileRepository
                .GetAsync(leaderboard, _targeter.Target.PlayerID, category, cancellationToken)
                .ConfigureAwait(false);

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        CurrentLeaderboard = leaderboard;

                        SetCurrentProfile(profile);

                        UpdateProfilePP();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private void SetCurrentProfile(Profile profile)
        {
            MainThreadDispatcher.AssertOnMainThread();

            _currentProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            _profilePanelPresenter.Bind(profile);
        }

        private void UpdateProfile()
        {
            if (_currentProfile is not null)
            {
                _profilePanelPresenter.Refresh();
                UpdateProfilePP();
                return;
            }

            _taskRunner.Run(LoadCurrentProfileAsync, "Load PP calculator profile", _lifetimeSource.Token);
        }

        private async Task RefreshProfileScoresAsync(CancellationToken cancellationToken)
        {
            AsyncLock.Releaser? releaser = await _profileLock.TryLockAsync(cancellationToken).ConfigureAwait(false);

            if (releaser is null)
                return;

            using (releaser.Value)
            {
                await _dispatcher
                    .InvokeAsync(() => ReloadDataButton.interactable = false, cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    if (_currentProfile is null)
                    {
                        await LoadCurrentProfileAsync(cancellationToken).ConfigureAwait(false);
                    }

                    Profile profile =
                        _currentProfile ?? throw new InvalidOperationException("The current profile was not loaded.");

                    await _profileRepository.ReloadAsync(profile, cancellationToken).ConfigureAwait(false);

                    await _dispatcher
                        .InvokeAsync(
                            () =>
                            {
                                SetCurrentProfile(profile);

                                UpdateProfilePP();
                            },
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    await RefreshAsync(true, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await _dispatcher.InvokeAsync(() => ReloadDataButton.interactable = true).ConfigureAwait(false);
                    }
                }
            }
        }

        private Task UpdatePlayTableAsync(CancellationToken cancellationToken)
        {
            if (_currentProfile is null)
                return Task.CompletedTask;

            if (_isPlayTableOpen)
            {
                return _dispatcher.InvokeAsync(_profilePanelPresenter.Refresh, cancellationToken);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Completed plays

        private void SucceededMap(StandardLevelScenesTransitionSetupDataSO transition, LevelCompletionResults results)
        {
            ClearMapTabs();
            TheCounter.ClearCounter();

            float accuracy = HelpfulMisc.GetAcc(transition, results);

#if NEW_VERSION
            string hash = transition.beatmapLevel.levelID.Split('_')[2];

            string mapName = transition.beatmapLevel.songName;

            BeatmapDifficulty difficulty = transition.beatmapKey.difficulty;

            string mode = transition.beatmapKey.beatmapCharacteristic.serializedName;
#else
            IDifficultyBeatmap beatmap = transition.difficultyBeatmap;

            string hash = beatmap.level.levelID.Split('_')[2];

            string mapName = beatmap.level.songName;

            BeatmapDifficulty difficulty = beatmap.difficulty;

            string mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
#endif

            _taskRunner.Run(
                cancellationToken => CompletedMapAsync(accuracy, hash, mapName, difficulty, mode, cancellationToken),
                "Record completed map",
                _lifetimeSource.Token
            );
        }

        private async Task CompletedMapAsync(
            float finalAccuracy,
            string hash,
            string mapName,
            BeatmapDifficulty difficulty,
            string mode,
            CancellationToken cancellationToken
        )
        {
            Plugin.Log.Debug(
                "Map completion info: "
                    + HelpfulMisc.Print(new object[] { finalAccuracy, hash, mapName, difficulty, mode })
            );

            TheCounter.LastMap = default;

            if (float.IsNaN(finalAccuracy) || float.IsInfinity(finalAccuracy) || finalAccuracy <= 0f)
            {
                return;
            }

            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            bool added = await _completedPlayService
                .AddCompletedPlayAsync(
                    _targeter.Target.PlayerID,
                    hash,
                    finalAccuracy,
                    mapName,
                    difficulty,
                    mode,
                    TheCounter.LastMods,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!added)
                return;

            await LoadCurrentProfileAsync(cancellationToken).ConfigureAwait(false);

            await RefreshAsync(true, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Refresh and map loading

        public Task Refresh(bool forceRefresh = false)
        {
            return RefreshAsync(forceRefresh, _lifetimeSource.Token);
        }

        private async Task RefreshAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            bool shouldRefresh = await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        if (!TabSelectionPatch.GetIfTabIsSelected(TabName) || !TabSelectionPatch.IsOnModsTab)
                        {
                            return false;
                        }

#if NEW_VERSION
                        if (
                            !forceRefresh
                            && _levelDetailView.beatmapKey.levelId?.Equals(_currentMap.levelId) == true
                            && _levelDetailView.beatmapKey.difficulty == _currentMap.difficulty
                        )
                        {
                            return false;
                        }
#else
                        if (!forceRefresh && Equals(_levelDetailView.selectedDifficultyBeatmap, _currentMap))
                        {
                            return false;
                        }
#endif

                        return true;
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!shouldRefresh)
                return;

            AsyncLock.Releaser? releaser = await _refreshLock.TryLockAsync(cancellationToken).ConfigureAwait(false);

            if (releaser is null)
                return;

            using (releaser.Value)
            {
                try
                {
                    await _dispatcher
                        .InvokeAsync(
                            () =>
                            {
                                CurrentLeaderboard = _config.CalcLeaderboard;

                                UpdateMods();
                            },
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    _targetPp = await UpdateTargetPpAsync(cancellationToken).ConfigureAwait(false);

                    await UpdateTabDisplayAsync(forceRefresh, cancellationToken).ConfigureAwait(false);

                    await UpdatePlayTableAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Error($"There was an issue refreshing " + $"{TabName}:\n{exception}");
                }
            }
        }

        private async Task<float> UpdateTargetPpAsync(CancellationToken cancellationToken)
        {
            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            SelectedMapInfo selectedMap = await _dispatcher
                .InvokeAsync(CaptureSelectedMap, cancellationToken)
                .ConfigureAwait(false);

            if (!selectedMap.IsUsable)
                return 0f;

            _currentDiff = default;

            JToken? scoreData = await CurrentApi
                .GetScoreData(
                    _targeter.Target.TargetID,
                    selectedMap.Hash,
                    selectedMap.Difficulty.ToString().Replace("+", "Plus"),
                    selectedMap.Mode,
                    quiet: true,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            _targetHasScore = scoreData is not null;

            if (scoreData is null)
            {
                await UpdateDiffAsync(mapFailed: false, cancellationToken).ConfigureAwait(false);

                return 0f;
            }

            string cacheMode = SelectCacheMode(selectedMap.Mode, CurrentLeaderboard);

            Map? map = await _mapRepository
                .GetAsync(selectedMap.Hash, cacheMode, CurrentLeaderboard, forceRemoteLoad: true, cancellationToken)
                .ConfigureAwait(false);

            if (map is null || !map.TryGet(cacheMode, selectedMap.Difficulty, out MapDiffModel? mapDifficulty))
            {
                await UpdateDiffAsync(mapFailed: true, cancellationToken).ConfigureAwait(false);

                return 0f;
            }

            ICacheModel difficultyData = mapDifficulty.Data;

            if (!CurrentApi.MapIsUsable(difficultyData, _config.UseUnranked))
            {
                await UpdateDiffAsync(mapFailed: true, cancellationToken).ConfigureAwait(false);

                return 0f;
            }

            float targetPp = CurrentApi.GetPP(scoreData);

            if (targetPp <= 0f && !CurrentApi.AreRatingsNull(difficultyData))
            {
                int maximumScore = CurrentApi.GetMaxScore(difficultyData);

                if (maximumScore > 0)
                {
                    float accuracy = CurrentApi.GetScore(scoreData) / (float)maximumScore;

                    targetPp = CurrentCalculator.Inflate(
                        CurrentCalculator.GetSummedPp(accuracy, CurrentApi.GetRatings(difficultyData))
                    );
                }
            }

            _currentDiff = (difficultyData, scoreData);

            if (!BetterString.IsNullOrWhiteSpace(_unformattedCurrentMods))
            {
                _currentModMultiplier = _pathUtils.GetMultiAmounts(
                    difficultyData,
                    _unformattedCurrentMods.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                );
            }

            await UpdateMapDisplayAsync(selectedMap, mapDifficulty, cancellationToken).ConfigureAwait(false);

            return targetPp;
        }

        private async Task UpdateDiffAsync(bool mapFailed, CancellationToken cancellationToken)
        {
            try
            {
                SelectedMapInfo selectedMap = await _dispatcher
                    .InvokeAsync(CaptureSelectedMap, cancellationToken)
                    .ConfigureAwait(false);

                if (!selectedMap.IsUsable)
                {
                    _currentDiff = default;
                    return;
                }

                string cacheMode = SelectCacheMode(selectedMap.Mode, CurrentLeaderboard);

                Map? map = mapFailed
                    ? null
                    : await _mapRepository
                        .GetAsync(
                            selectedMap.Hash,
                            cacheMode,
                            CurrentLeaderboard,
                            forceRemoteLoad: true,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                MapDiffModel? mapDifficulty = null;

                bool usedFallback = map is null || !map.TryGet(cacheMode, selectedMap.Difficulty, out mapDifficulty);

                if (usedFallback)
                {
                    map = await _mapRepository
                        .GetAsync(
                            selectedMap.Hash,
                            selectedMap.Mode,
                            Leaderboards.Beatleader,
                            forceRemoteLoad: true,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (map is null || !map.TryGet(selectedMap.Mode, selectedMap.Difficulty, out mapDifficulty))
                    {
                        Plugin.Log.Error(
                            "Completely failed to load map data. "
                                + "The map may be unranked, the network may "
                                + "be unavailable, or the API may be down."
                        );

                        _currentDiff = default;
                        return;
                    }
                }

                ICacheModel cacheModel = mapDifficulty!.Data;

                _currentDiff =
                    usedFallback || CurrentApi.AreRatingsNull(cacheModel)
                        ? default
                        : (cacheModel, _currentDiff.ScoreData);

                await UpdateMapDisplayAsync(selectedMap, mapDifficulty, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error(
                    "Error updating map difficulty data. "
                        + "The map may not be ranked on the selected "
                        + $"leaderboard:\n{exception}"
                );

                _currentDiff = default;
            }
        }

        private async Task UpdateMapDisplayAsync(
            SelectedMapInfo selectedMap,
            MapDiffModel mapDifficulty,
            CancellationToken cancellationToken
        )
        {
            string trueMapId = await ResolveTrueMapIdAsync(selectedMap, mapDifficulty.SongId, cancellationToken)
                .ConfigureAwait(false);

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        BeatmapID = mapDifficulty.SongId;

                        TrueBeatmapID = trueMapId;

                        MapDiffText = HelpfulMisc.AddSpaces(selectedMap.Difficulty.ToString().Replace("+", "Plus"));

                        MapModeText = HelpfulMisc.AddSpaces(selectedMap.Mode!);

                        BeatmapName = selectedMap.MapName!;
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private async Task<string> ResolveTrueMapIdAsync(
            SelectedMapInfo selectedMap,
            string fallbackSongId,
            CancellationToken cancellationToken
        )
        {
            if (CurrentLeaderboard == Leaderboards.Beatleader)
            {
                return CleanBeatLeaderId(fallbackSongId);
            }

            Map? beatLeaderMap = await _mapRepository
                .GetAsync(
                    selectedMap.Hash!,
                    selectedMap.Mode,
                    Leaderboards.Beatleader,
                    forceRemoteLoad: true,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (
                beatLeaderMap is not null
                && beatLeaderMap.TryGet(
                    selectedMap.Mode!,
                    selectedMap.Difficulty,
                    out MapDiffModel? beatLeaderDifficulty
                )
            )
            {
                return CleanBeatLeaderId(beatLeaderDifficulty.SongId);
            }

            return CleanBeatLeaderId(fallbackSongId);
        }

        private static string SelectCacheMode(string? characteristic, Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Scoresaber => Map.SS_MODE_NAME,

                Leaderboards.Accsaber => Map.AP_MODE_NAME,

                _ when BetterString.IsNullOrWhiteSpace(characteristic) => Profile.DEFAULT_MODE,

                _ => characteristic,
            };
        }

        private static string CleanBeatLeaderId(string songId)
        {
            if (BetterString.IsNullOrWhiteSpace(songId))
            {
                return "";
            }

            int separatorIndex = songId.IndexOf('x');

            if (separatorIndex >= 0)
                return songId[..separatorIndex];

            return songId.Length > 2 ? songId[..^2] : songId;
        }

        private SelectedMapInfo CaptureSelectedMap()
        {
            MainThreadDispatcher.AssertOnMainThread();

#if NEW_VERSION
            _currentMap = _levelDetailView.beatmapKey;

            if (_currentMap == default)
                return default;

            string levelId = _levelDetailView.beatmapLevel.levelID;

            if (
                BetterString.IsNullOrWhiteSpace(levelId)
                || !levelId.StartsWith("custom", StringComparison.OrdinalIgnoreCase)
            )
            {
                return default;
            }

            return new SelectedMapInfo(
                levelId.Split('_')[2],
                _levelDetailView.beatmapLevel.songName,
                _currentMap.difficulty,
                _currentMap.beatmapCharacteristic.serializedName
            );
#else
            _currentMap = _levelDetailView.selectedDifficultyBeatmap;

            if (_currentMap is null)
                return default;

            string levelId = _currentMap.level.levelID;

            if (
                BetterString.IsNullOrWhiteSpace(levelId)
                || !levelId.StartsWith("custom", StringComparison.OrdinalIgnoreCase)
            )
            {
                return default;
            }

            return new SelectedMapInfo(
                levelId.Split('_')[2],
                _currentMap.level.songName,
                _currentMap.difficulty,
                _currentMap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName
            );
#endif
        }

        #endregion

        #region Tab presentation

        private async Task UpdateTabDisplayAsync(bool forceUpdate, CancellationToken cancellationToken)
        {
            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        if (BetterString.IsNullOrEmpty(_currentTab))
                        {
                            return;
                        }

                        if (
                            !forceUpdate
                            && EqualityComparer<
#if NEW_VERSION
                            BeatmapKey
#else
                            IDifficultyBeatmap
#endif
                            >.Default.Equals(_tabMapInfo[_currentTab], _currentMap)
                        )
                        {
                            return;
                        }

                        if (ChangeTabSettings)
                        {
                            ChangeTabSettings = false;
                            UpdateDisplayVisibility(false);
                        }

                        if (_currentTab == "Settings")
                            return;

                        _tabMapInfo[_currentTab] = _currentMap;

                        switch (_currentTab)
                        {
                            case "Info":
                                UpdateInfo();
                                break;

                            case "Capture":
                                UpdateCaptureValues();
                                break;

                            case "Relative":
                                UpdateRelativeValues();
                                break;

                            case "Custom":
                                UpdateCustomAccuracy();
                                break;

                            case "Profile":
                                UpdateProfile();
                                break;
                        }

                        BuildTable();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private void UpdateInfo()
        {
            MainThreadDispatcher.AssertOnMainThread();

            const string delimiter = "\n";
            const string lineHeight = "80";

            PrefixLabels.SetText($"<line-height={lineHeight}%>" + string.Join(delimiter, CurrentCalculator.StarLabels));

            ICacheModel? difficultyData = _currentDiff.DiffData;

            if (difficultyData is not null && CurrentApi.MapIsUsable(difficultyData, _config.UseUnranked))
            {
                const char star = (char)9733;

                if (CurrentLeaderboard == Leaderboards.Beatleader)
                {
                    SongSpeed speed = _modifiersPanel.gameplayModifiers.songSpeed;

                    float accuracyRating =
                        _pathUtils.GetRating(difficultyData, PPType.Acc, Leaderboards.Beatleader, speed)
                        * _currentModMultiplier;

                    float passRating =
                        _pathUtils.GetRating(difficultyData, PPType.Pass, Leaderboards.Beatleader, speed)
                        * _currentModMultiplier;

                    float technologyRating =
                        _pathUtils.GetRating(difficultyData, PPType.Tech, Leaderboards.Beatleader, speed)
                        * _currentModMultiplier;

                    float starRating = _pathUtils.GetRating(
                        difficultyData,
                        PPType.Star,
                        Leaderboards.Beatleader,
                        speed
                    );

                    StarRatings.SetText(
                        $"<line-height={lineHeight}%>"
                            + $"{Math.Round(accuracyRating, _config.DecimalPrecision)} {star}"
                            + delimiter
                            + $"{Math.Round(passRating, _config.DecimalPrecision)} {star}"
                            + delimiter
                            + $"{Math.Round(technologyRating, _config.DecimalPrecision)} {star}"
                            + delimiter
                            + $"{Math.Round(starRating, _config.DecimalPrecision)} {star}"
                    );
                }
                else
                {
                    float rating = _pathUtils.GetRating(difficultyData, PPType.Star, CurrentLeaderboard);

                    StarRatings.SetText(
                        $"<line-height={lineHeight}%>" + $"{Math.Round(rating, _config.DecimalPrecision)} {star}"
                    );
                }
            }
            else
            {
                string failText = string.Join(
                    delimiter,
                    Enumerable.Repeat("Not Found!", CurrentCalculator.DisplayRatingCount)
                );

                StarRatings.SetText($"<line-height={lineHeight}%>" + failText);
            }

            SpeedModText.SetText(
                "<color=green>" + HelpfulMisc.AddSpaces(_modifiersPanel.gameplayModifiers.songSpeed.ToString())
            );

            ModMultText.SetText($"x{Math.Round(_currentModMultiplier, 2):N2}");
        }

        private void UpdateCaptureValues()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (
                !IsBeatLeader
                || _currentDiff.DiffData is null
                || !CurrentApi.MapIsUsable(_currentDiff.DiffData, _config.UseUnranked)
            )
            {
                return;
            }

            ClanTarget.SetText(_targetHasScore ? _getTarget?.Invoke() : _getNoScoreTarget?.Invoke());

            string mapId = BeatmapID;

            OwningClan.SetText("<color=#777>Loading clan data...</color>");

            PPTarget.SetText("<color=#777>Loading...</color>");

            _taskRunner.Run(
                cancellationToken => UpdateCaptureValuesAsync(mapId, cancellationToken),
                "Load PP calculator clan values",
                _lifetimeSource.Token
            );
        }

        private async Task UpdateCaptureValuesAsync(string mapId, CancellationToken cancellationToken)
        {
            ClanCaptureResult result = await _clanService
                .GetCaptureAsync(mapId, cancellationToken)
                .ConfigureAwait(false);

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        if (!string.Equals(BeatmapID, mapId, StringComparison.Ordinal))
                        {
                            return;
                        }

                        _ppToCapture = result.NeededPp;

                        OwningClan.SetText(
                            result.Status == ClanCaptureStatus.LoadFailed
                                ? "<color=red>Clan data failed to load.</color>"
                            : result.Status == ClanCaptureStatus.Unranked ? "<color=#999>Map is not ranked.</color>"
                            : result.IsCaptured
                                ? $"<color=green>{result.OwningClan}</color> " + "already owns this map."
                            : $"<color=red>{result.OwningClan}</color> " + "owns this map."
                        );

                        PPTarget.SetText(
                            result.IsSuccessful
                                ? $"<color=#0F0>"
                                    + $"{Math.Round(result.NeededPp, _config.DecimalPrecision)}"
                                    + $"</color> {GetPPLabel()}"
                                : "<color=#999>Unavailable</color>"
                        );

                        if (string.Equals(_currentTab, "Capture", StringComparison.Ordinal))
                        {
                            BuildTable();
                        }
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private void UpdateRelativeValues()
        {
            MainThreadDispatcher.AssertOnMainThread();

            RelativeText.SetText(_targetHasScore ? _getTarget?.Invoke() : _getNoScoreTarget?.Invoke());

            RelativeTarget.SetText(
                $"<color=#0F0>" + $"{Math.Round(_targetPp, _config.DecimalPrecision)}</color> " + GetPPLabel()
            );
        }

        private void UpdateCustomAccuracy()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (_currentDiff.DiffData is null)
                return;

            if (_isPpMode)
                _config.TestAccAmount = TestAcc;
            else
                _config.TestPPAmount = TestPp;
        }

        private void UpdateDisplayVisibility(bool waitForEndOfFrame = true)
        {
            void Update()
            {
                MainThreadDispatcher.AssertOnMainThread();

                AccSaberSetting.gameObject.SetActive(IsAccSaber);

                if (CaptureTab.IsVisible == IsBeatLeader && ProfileTab.IsVisible == ShowProfileTab)
                {
                    return;
                }

#if NEW_VERSION
                int currentTabNumber = MainTabSelector.TextSegmentedControl.selectedCellNumber;
#else
                int currentTabNumber = MainTabSelector.textSegmentedControl.selectedCellNumber;
#endif

                bool changed = CaptureTab.IsVisible != IsBeatLeader;

                bool keepPosition = !changed || _currentTab != CaptureTab.TabName;

                if (changed)
                {
                    CaptureTab.IsVisible = IsBeatLeader;

                    if (currentTabNumber >= _tabMapInfo.Keys.ToList().IndexOf(CaptureTab.TabName))
                    {
                        currentTabNumber += IsBeatLeader ? 1 : -1;
                    }
                }

                changed = ProfileTab.IsVisible != ShowProfileTab;

                keepPosition &= !changed || _currentTab != ProfileTab.TabName;

                if (changed)
                {
                    ProfileTab.IsVisible = ShowProfileTab;

                    if (currentTabNumber >= _tabMapInfo.Keys.ToList().IndexOf(ProfileTab.TabName))
                    {
                        currentTabNumber += ShowProfileTab ? 1 : -1;
                    }
                }

                MainTabSelector.ForceSelectAndNotify(keepPosition ? currentTabNumber : 0);

                if (!keepPosition)
                {
                    _currentTab = _tabMapInfo.Keys.First();
                }
            }

            if (!waitForEndOfFrame)
            {
                Update();
                return;
            }

            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                Update();
            }

            _dispatcher.EnqueueRoutine(WaitThenUpdate());
        }

        #endregion

        #region Tables and modifiers

        private bool BuildTable(
            Func<float[], string> valueCalculator,
            TextMeshProUGUI text,
            [NotNullWhen(true)] ref Table? table,
            string suffix = "%",
            string speedLabel = "<color=blue>Speed</color>",
            string accuracyLabel = "<color=#0D0>Acc</color> to Cap",
            string modifierLabel = "With <color=green>Selected Mods</color>"
        )
        {
            MainThreadDispatcher.AssertOnMainThread();

            ICacheModel? difficultyData = _currentDiff.DiffData;

            if (difficultyData is null)
                return false;

            Calculator calculator = CurrentCalculator;

            string[][] values = new[]
            {
                "<color=red>Slower</color>",
                "<color=#aaa>Normal</color>",
                "<color=#0F0>Faster</color>",
                "<color=#FFD700>Super Fast</color>",
            }.RowToColumn(3);

            float[] ratings;

            if (UsesMods)
            {
                ratings =
                [
                    .. _pathUtils
                        .GetAllRatings(difficultyData, calculator)
                        .SelectMany(rating => rating.GetRatings(calculator.Leaderboard))
                        .Where(rating => rating > 0f),
                ];
            }
            else
            {
                float rating = _pathUtils.GetRating(difficultyData, PPType.Star, calculator.Leaderboard);

                ratings = [rating, rating, rating, rating];
            }

            if (ratings.Length == 0 || ratings.Length % 4 != 0)
            {
                return false;
            }

            int ratingsPerSpeed = ratings.Length / 4;

            for (int index = 0; index < values.Length; index++)
            {
                float[] speedRatings = [.. ratings.Skip(index * ratingsPerSpeed).Take(ratingsPerSpeed)];

                values[index][1] = "<color=#0c0>" + valueCalculator(speedRatings) + "</color>" + suffix;

                if (Mathf.Approximately(_currentModMultiplier, 1f))
                {
                    values[index][2] = "N/A";
                    continue;
                }

                values[index][2] =
                    "<color=green>"
                    + valueCalculator([.. speedRatings.Select(rating => rating * _currentModMultiplier)])
                    + "</color>"
                    + suffix;
            }

            if (table is null)
            {
                table = _tableFactory.Create(text, values, speedLabel, accuracyLabel, modifierLabel);
                table.HasEndColumn = true;
            }
            else
            {
                table.SetValues(values);
            }

            Table capturedTable = table;

            IEnumerator UpdateTableAfterLayout()
            {
                yield return new WaitForEndOfFrame();

                capturedTable.UpdateTable();
                TurnOnSelectButtons();
            }

            _dispatcher.EnqueueRoutine(UpdateTableAfterLayout());

            return true;
        }

        private void BuildTable()
        {
            MainThreadDispatcher.AssertOnMainThread();

            switch (_currentTab)
            {
                case "Capture":
                    if (!IsBeatLeader)
                        return;

                    if (
                        BuildTable(
                            ratings =>
                                CurrentCalculator.GetAcc(_ppToCapture, _config.DecimalPrecision, ratings).ToString(),
                            ClanTable,
                            ref _clanTable
                        )
                    )
                    {
                        _clanTable.HighlightCell(
                            HelpfulMisc.OrderSongSpeedCorrectly(_modifiersPanel.gameplayModifiers.songSpeed) + 1,
                            0
                        );
                    }

                    break;

                case "Relative":
                    if (
                        BuildTable(
                            ratings =>
                                CurrentCalculator.GetAcc(_targetPp, _config.DecimalPrecision, ratings).ToString(),
                            RelativeTable,
                            ref _relativeTable,
                            accuracyLabel: "<color=#0D0>Acc</color> to Beat"
                        )
                    )
                    {
                        _relativeTable.HighlightCell(
                            HelpfulMisc.OrderSongSpeedCorrectly(_modifiersPanel.gameplayModifiers.songSpeed) + 1,
                            0
                        );
                    }

                    break;

                case "Custom":
                    if (_isPpMode)
                    {
                        if (
                            BuildTable(
                                ratings =>
                                    CurrentCalculator.GetAcc(TestPp, _config.DecimalPrecision, ratings).ToString(),
                                PercentTable,
                                ref _percentTable,
                                accuracyLabel: "<color=#0D0>Acc</color>"
                            )
                        )
                        {
                            _percentTable.HighlightCell(
                                HelpfulMisc.OrderSongSpeedCorrectly(_modifiersPanel.gameplayModifiers.songSpeed) + 1,
                                0
                            );
                        }
                    }
                    else
                    {
                        if (
                            BuildTable(
                                ratings =>
                                    CurrentCalculator
                                        .GetSummedPp(TestAcc / 100f, _config.DecimalPrecision, ratings)
                                        .ToString(),
                                PPTable,
                                ref _ppTable,
                                $" {GetPPLabel()}",
                                accuracyLabel: $"<color=#0D0>" + $"{GetPPLabel().ToUpperInvariant()}</color>"
                            )
                        )
                        {
                            _ppTable.HighlightCell(
                                HelpfulMisc.OrderSongSpeedCorrectly(_modifiersPanel.gameplayModifiers.songSpeed) + 1,
                                0
                            );
                        }
                    }

                    break;
            }
        }

        private void SelectMod(SongSpeed speed)
        {
            MainThreadDispatcher.AssertOnMainThread();

            _modifiersPanel.ChangeSongSpeed(speed);

            UpdateMods();
            BuildTable();
        }

        private void UpdateMods()
        {
            MainThreadDispatcher.AssertOnMainThread();

            string formattedModifiers = "";
            _unformattedCurrentMods = "";

            GameplayModifiers modifiers = _modifiersPanel.gameplayModifiers;

            switch (modifiers.songSpeed)
            {
                case SongSpeed.Slower:
                    formattedModifiers += "Slower Song, ";
                    break;

                case SongSpeed.Faster:
                    formattedModifiers += "Faster Song, ";
                    break;

                case SongSpeed.SuperFast:
                    formattedModifiers += "Super Fast Song, ";
                    break;
            }

            AddModifier(modifiers.ghostNotes, "gn", "Ghost Notes");

            AddModifier(modifiers.disappearingArrows, "da", "Disappearing Arrows");

            if (modifiers.energyType == EnergyType.Battery)
            {
                formattedModifiers += "Four Lives, ";
            }

            AddModifier(modifiers.noArrows, "na", "No Arrows");

            if (modifiers.noFailOn0Energy)
            {
                formattedModifiers += "No Fail, ";
            }

            if (modifiers.zenMode)
            {
                formattedModifiers += "Zen Mode, ";
            }

            if (modifiers.instaFail)
            {
                formattedModifiers += "One Life, ";
            }

            AddModifier(modifiers.noBombs, "nb", "No Bombs");

            AddModifier(modifiers.proMode, "pm", "Pro Mode");

            AddModifier(modifiers.smallCubes, "sc", "Small Cubes");

            AddModifier(modifiers.strictAngles, "sa", "Strict Angles");

            AddModifier(modifiers.enabledObstacleType == EnabledObstacleType.NoObstacles, "no", "No Walls");

            _currentMods = formattedModifiers.Length > 1 ? Grammarize(formattedModifiers[..^2]) : "";

            _unformattedCurrentMods = _unformattedCurrentMods.Trim();

            if (!UsesMods)
            {
                _currentModMultiplier = 1f;
                return;
            }

            float previousMultiplier = _currentModMultiplier;

            if (_currentDiff.DiffData is not null)
            {
                _currentModMultiplier = _pathUtils.GetMultiAmounts(
                    _currentDiff.DiffData,
                    _unformattedCurrentMods.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                );
            }

            if (!Mathf.Approximately(previousMultiplier, _currentModMultiplier))
            {
                _tabMapInfo["Capture"] = default;

                _tabMapInfo["Relative"] = default;
            }

            void AddModifier(bool enabled, string shortName, string displayName)
            {
                if (!enabled)
                    return;

                _unformattedCurrentMods += shortName + " ";

                formattedModifiers += displayName + ", ";
            }
        }

        private static string Grammarize(string modifiers)
        {
            int commaCount = modifiers.Count(character => character == ',');

            if (commaCount == 0)
                return modifiers;

            if (commaCount == 1)
            {
                return modifiers.Replace(", ", " and ");
            }

            int lastComma = modifiers.LastIndexOf(',');

            return modifiers[..lastComma] + " and" + modifiers[(lastComma + 1)..];
        }

        private void TurnOnSelectButtons()
        {
            if (_selectButtonsOn)
                return;

            foreach (string tag in SelectionButtonTags)
            {
                foreach (GameObject gameObject in Parser.GetObjectsWithTag(tag))
                {
                    gameObject.SetActive(true);
                }
            }

            _selectButtonsOn = true;
        }

        #endregion

        #region Formatting

        [MemberNotNull(nameof(_noScoreTargetWrapper), nameof(_getNoScoreTarget), nameof(_getTarget))]
        private void InitFormatters()
        {
            _noScoreTargetWrapper = new FormatWrapper((typeof(string), 't'));

            Formatter formatter = _formatterFactory.Create(
                _tokenParser.ParseTokens(
                    _config.MessageSettings.TargetHasNoScoreMessage,
                    new Dictionary<string, char> { { "Target", 't' } }
                ),
                _noScoreTargetWrapper
            );

            formatter.PromiseValueForTokens();

            Printer printer = formatter.GetOutput();

            _getNoScoreTarget = () =>
            {
                string targetName = _targeter.IsInitialized ? _targeter.Target.TargetName : Targeter.NO_TARGET;

                _noScoreTargetWrapper.SetValue('t', targetName, typeof(string));

                return printer.Print();
            };

            _getTarget = () =>
            {
                string targetName = _targeter.IsInitialized ? _targeter.Target.TargetName : Targeter.NO_TARGET;

                return _mainFormatting.FormatTarget(targetName.ClampString(_config.MaxNameLength), "");
            };
        }

        private float GetAccToBeatTarget()
        {
            if (
                _currentDiff.DiffData is null
                || _currentDiff.ScoreData is null
                || CurrentApi.AreRatingsNull(_currentDiff.DiffData)
            )
            {
                return 0f;
            }

            return CurrentCalculator.GetAcc(
                CurrentApi.GetPP(_currentDiff.ScoreData),
                _config.DecimalPrecision,
                CurrentApi.GetRatings(_currentDiff.DiffData)
            );
        }

        public string GetPPLabel()
        {
            return IsAccSaber ? "ap" : "pp";
        }

        #endregion

        #region Miscellaneous

        private void ChangeMapListeners(bool enable)
        {
            if (enable)
            {
                BSEvents.levelCleared += SucceededMap;
            }
            else
            {
                BSEvents.levelCleared -= SucceededMap;
            }
        }

        public void ClearMapTabs()
        {
            _tabMapInfo["Capture"] = default;

            _tabMapInfo["Relative"] = default;

            _clanTable?.ClearTable();
            _relativeTable?.ClearTable();

            _currentMap = default;
        }

        public void ResetTabs()
        {
            foreach (string tabName in _tabMapInfo.Keys.ToArray())
            {
                _tabMapInfo[tabName] = default;
            }

            _ppTable?.ClearTable();
            _percentTable?.ClearTable();
            _clanTable?.ClearTable();
            _relativeTable?.ClearTable();

            _currentMap = default;
            _currentDiff = default;
        }

        private void UpdateTabSliders(int amount = -1)
        {
            if (!_postParsed)
                return;

            if (amount <= 0)
            {
                amount = _config.SliderIncrementNum;
            }

            HelpfulMisc.SetIncrements(amount, MinPPSlider, MaxPPSlider, CA_PPSliderSlider, ProfilePPSlider);

            HelpfulMisc.SetIncrements(amount, 0.01f, MinAccSlider, MaxAccSlider, CA_PercentSliderSlider);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PpInfoTabHandler));
            }
        }

        private readonly struct SelectedMapInfo
        {
            internal SelectedMapInfo(string hash, string mapName, BeatmapDifficulty difficulty, string mode)
            {
                Hash = hash;
                MapName = mapName;
                Difficulty = difficulty;

                Mode = BetterString.IsNullOrWhiteSpace(mode) ? Profile.DEFAULT_MODE : mode;
            }

            internal string? Hash { get; }

            internal string? MapName { get; }

            internal BeatmapDifficulty Difficulty { get; }

            internal string? Mode { get; }

            [MemberNotNullWhen(true, nameof(Hash), nameof(MapName), nameof(Mode))]
            internal bool IsUsable =>
                !BetterString.IsNullOrWhiteSpace(Hash)
                && !BetterString.IsNullOrWhiteSpace(MapName)
                && !BetterString.IsNullOrWhiteSpace(Mode);
        }

        #endregion
    }
}
