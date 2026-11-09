using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.Serializable_Classes;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers
{
    internal sealed class SettingsHandler : SafeNotifyPropertyChanged, IInitializable, IDisposable
    {
        private readonly PluginConfig _config;
        private readonly ITargeter _targeter;
        private readonly TargetInfoFactory _targetInfoFactory;
        private readonly LeaderboardListInfo.Factory _leaderboardInfoFactory;
        private readonly LeaderboardSettingsHandler _leaderboardSettings;
        private readonly IBackgroundTaskRunner _taskRunner;
        private readonly LazyInject<PpInfoTabHandler> _ppInfoTabHandler;
        private readonly CancellationTokenSource _lifetimeSource = new();
        private readonly AsyncLock _customInputLock = new();
        private readonly ICounterCatalog _counterCatalog;
        private readonly IClanService _clanService;
        private readonly List<object> _clanTargetInfos = [];
        private readonly List<object> _followerTargetInfos = [];
        private readonly List<object> _customTargetInfos = [];
        private readonly Dictionary<long, TargetInfo> _idToTarget = [];

        private TargetInfo? _selectedTarget;
        private SelectableCell? _lastCellSelected;
        private bool _targetMenuIsOpen;
        private bool _disposed;

        internal Action? OnTypesOfPPChanged;

#if NEW_VERSION
        internal const int MENU_HEIGHT = 70;
        internal const int MENU_ANCHOR = 0;
#else
        internal const int MENU_HEIGHT = 60;
        internal const int MENU_ANCHOR = 5;
#endif

        public SettingsHandler(
            PluginConfig config,
            ITargeter targeter,
            TargetInfoFactory targetInfoFactory,
            LeaderboardListInfo.Factory leaderboardInfoFactory,
            LeaderboardSettingsHandler leaderboardSettings,
            IBackgroundTaskRunner taskRunner,
            LazyInject<PpInfoTabHandler> ppInfoTabHandler,
            ICounterCatalog counterCatalog,
            IClanService clanService
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _targetInfoFactory = targetInfoFactory ?? throw new ArgumentNullException(nameof(targetInfoFactory));

            _leaderboardInfoFactory =
                leaderboardInfoFactory ?? throw new ArgumentNullException(nameof(leaderboardInfoFactory));

            _leaderboardSettings = leaderboardSettings ?? throw new ArgumentNullException(nameof(leaderboardSettings));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));

            _ppInfoTabHandler = ppInfoTabHandler ?? throw new ArgumentNullException(nameof(ppInfoTabHandler));

            _counterCatalog = counterCatalog ?? throw new ArgumentNullException(nameof(counterCatalog));

            _clanService = clanService ?? throw new ArgumentNullException(nameof(clanService));

            OnTypesOfPPChanged += HandleTypesOfPpChanged;

            PropertyChanged += HandlePropertyChanged;
        }

        public void Initialize()
        {
            ThrowIfDisposed();

            _leaderboardSettings.LeaderboardUpdated += HandleLeaderboardUpdated;

            _targeter.Initialized += HandleTargeterInitialized;
            _targeter.TargetListReloaded += HandleTargetListReloaded;

            _taskRunner.Run(InitializeTargetsAsync, "Initialize target settings", _lifetimeSource.Token);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _leaderboardSettings.LeaderboardUpdated -= HandleLeaderboardUpdated;

            _targeter.Initialized -= HandleTargeterInitialized;
            _targeter.TargetListReloaded -= HandleTargetListReloaded;

            PropertyChanged -= HandlePropertyChanged;
            OnTypesOfPPChanged -= HandleTypesOfPpChanged;

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
            _customInputLock.Dispose();
        }

        private async Task InitializeTargetsAsync(CancellationToken cancellationToken)
        {
            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            await Dispatcher
                .InvokeAsync(
                    () =>
                    {
                        RefreshTargetInfoCaches();
                        SetSelectedTargetRelation();
                        UpdateTargetLists();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private void HandleTargeterInitialized()
        {
            Dispatcher.EnqueueAction(() =>
            {
                RefreshTargetInfoCaches();
                SetSelectedTargetRelation();
                UpdateTargetLists();
            });
        }

        private void HandleTargetListReloaded()
        {
            Dispatcher.EnqueueAction(() =>
            {
                RefreshTargetInfoCaches();
                SetSelectedTargetRelation();
                UpdateTargetLists();
            });
        }

        private void HandleLeaderboardUpdated()
        {
            Dispatcher.EnqueueAction(() =>
            {
                RefreshLeaderboardControls();

                NotifyPropertyChanged(nameof(Leaderboard));
                NotifyPropertyChanged(nameof(LeaderboardList));
                NotifyPropertyChanged(nameof(LeaderboardOptions));

                OnTypesOfPPChanged?.Invoke();
            });
        }

        private void HandleTypesOfPpChanged()
        {
            if (TypesOfPP.Count == 0)
                return;

            if (CounterList is not null)
            {
                CounterList.UpdateListSetting(TypesOfPP);
            }
            else if (!TypesOfPP.Any(value => Equals(value, PPType)))
            {
                PPType = (string)TypesOfPP[0];
            }

            if (RelativeDefaultList.Count == 0)
                return;

            if (DefaultCounterList is not null)
            {
                DefaultCounterList.UpdateListSetting(RelativeDefaultList);
            }
            else if (!RelativeDefaultList.Any(value => Equals(value, RelativeDefault)))
            {
                RelativeDefault = (string)RelativeDefaultList[0];
            }

            TheCounter.SettingChanged = true;
        }

        private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(Leaderboard):
                    OnTypesOfPPChanged?.Invoke();
                    break;

                case nameof(CalcLeaderboard):
                    _ppInfoTabHandler.Value.ChangeTabSettings = true;
                    _ppInfoTabHandler.Value.CalcSelector.ReceiveValue();
                    _ppInfoTabHandler.Value.ResetTabs();
                    break;

                case nameof(UseUnranked):
                case nameof(LocalReplaysOnly):
                    TheCounter.SettingChanged = true;
                    break;

                case nameof(DecimalPrecision):
                    HelpfulFormatter.NUMBER_TOSTRING_FORMAT = CreateNumberFormat();
                    break;

                case nameof(Target):
                    _ppInfoTabHandler.Value.ResetTabs();
                    break;

                case nameof(APITimeout):
                    APIHandler.ClientTimeout = TimeSpan.FromSeconds(_config.APITimeout);
                    break;

                case nameof(LocalReplays):
                    if (_config.LocalReplays)
                        LocalReplayHandler.LoadReplays();

                    TheCounter.SettingChanged = true;
                    break;
            }
        }

        private string CreateNumberFormat()
        {
            if (_config.DecimalPrecision <= 0)
                return _config.FormatSettings.NumberFormat;

            string decimalPlaces = new('#', _config.DecimalPrecision);

            return _config.FormatSettings.NumberFormat.Replace("#", $"#.{decimalPlaces}");
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            TargetPostParse();
            RefreshLeaderboardControls();

#if !NEW_VERSION
            LeaderboardPostParse();
#endif
        }

        #region General Settings

        [UIValue(nameof(DecimalPrecision))]
        public int DecimalPrecision
        {
            get => _config.DecimalPrecision;
            set
            {
                _config.DecimalPrecision = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(FontSize))]
        public float FontSize
        {
            get => _config.FontSize;
            set
            {
                _config.FontSize = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ShowLbl))]
        public bool ShowLbl
        {
            get => _config.ShowLbl;
            set
            {
                _config.ShowLbl = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PPFC))]
        public bool PPFC
        {
            get => _config.PPFC;
            set
            {
                _config.PPFC = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(SplitVals))]
        public bool SplitVals
        {
            get => _config.SplitPPVals;
            set
            {
                _config.SplitPPVals = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ExtraInfo))]
        public bool ExtraInfo
        {
            get => _config.ExtraInfo;
            set
            {
                _config.ExtraInfo = value;
                NotifyPropertyChanged();
            }
        }

        [UIComponent(nameof(CounterList))]
        private readonly ListSetting? CounterList;

        [UIValue(nameof(TypesOfPP))]
        public List<object> TypesOfPP
        {
            get
            {
                Leaderboards leaderboard = _config.LeaderboardsInUse.FirstOrDefault();

                return [.. _counterCatalog.GetDisplayNames(leaderboard).Cast<object>()];
            }
        }

        [UIValue(nameof(UpdateAfterTime))]
        public bool UpdateAfterTime
        {
            get => _config.UpdateAfterTime;
            set
            {
                _config.UpdateAfterTime = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(UpdateTime))]
        public float UpdateTime
        {
            get => _config.UpdateTime;
            set
            {
                _config.UpdateTime = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(PPType))]
        public string PPType
        {
            get => _config.PPType;
            set
            {
                _config.PPType = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region Leaderboard Settings

        [UIObject(nameof(LeaderboardModal))]
        private readonly GameObject LeaderboardModal = null!;

        [UIComponent(nameof(LeaderboardTable))]
        internal CustomCellListTableData LeaderboardTable = null!;

        [UIComponent(nameof(LeaderboardSelector))]
        internal ListSetting LeaderboardSelector = null!;

        [UIComponent(nameof(CalcSelector))]
        internal ListSetting CalcSelector = null!;

        [UIValue(nameof(Leaderboard))]
        private string Leaderboard
        {
            get => _leaderboardSettings.NextLeaderboardToAdd.ToString();
            set
            {
                _leaderboardSettings.NextLeaderboardToAdd = Enum.Parse<Leaderboards>(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(LeaderboardList))]
        private List<object> LeaderboardList
        {
            get
            {
                IReadOnlyList<Leaderboards> available = _leaderboardSettings.AvailableLeaderboards;

                if (available.Count == 0)
                    return [Leaderboards.None.ToString()];

                return [.. available.Select(leaderboard => (object)leaderboard.ToString())];
            }
        }

        [UIValue(nameof(LeaderboardOptions))]
        private List<object> LeaderboardOptions =>
            [
                .. _leaderboardSettings.SelectedLeaderboards.Select(leaderboard =>
                    (object)_leaderboardInfoFactory.Create(leaderboard)
                ),
            ];

        [UIAction(nameof(AddLeaderboard))]
        private void AddLeaderboard()
        {
            _leaderboardSettings.AddCell();
        }

        [UIValue(nameof(CalcLeaderboard))]
        private string CalcLeaderboard
        {
            get => _config.CalcLeaderboard.ToString();
            set
            {
                _config.CalcLeaderboard = Enum.Parse<Leaderboards>(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(CalcLeaderboards))]
        private List<object> CalcLeaderboards =>
            [.. _leaderboardSettings.UsableLeaderboards.Select(leaderboard => (object)leaderboard.ToString())];

        [UIValue(nameof(UseUnranked))]
        public bool UseUnranked
        {
            get => _config.UseUnranked;
            set
            {
                _config.UseUnranked = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(LeaderInLabel))]
        public bool LeaderInLabel
        {
            get => _config.LeaderInLabel;
            set
            {
                _config.LeaderInLabel = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(HuntLoads))]
        public bool HuntLoads
        {
            get => _config.HuntLoads;
            set
            {
                _config.HuntLoads = value;
                NotifyPropertyChanged();
            }
        }

        private void RefreshLeaderboardControls()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (LeaderboardTable is null || LeaderboardSelector is null)
            {
                return;
            }

            List<object> options = LeaderboardOptions;
            List<object> available = LeaderboardList;

#if NEW_VERSION
            LeaderboardTable.Data = options;
            LeaderboardSelector.Values = available;

            LeaderboardTable.TableView.ReloadData();
#else
            LeaderboardTable.data = options;
            LeaderboardSelector.values = available;

            LeaderboardTable.tableView.ReloadData();
#endif

            LeaderboardSelector.Value = available[0];
            LeaderboardSelector.ApplyValue();
        }

#if !NEW_VERSION
        private void LeaderboardPostParse()
        {
            IEnumerator ResizeModal()
            {
                yield return new WaitForEndOfFrame();

                if (LeaderboardModal.transform is RectTransform rectTransform)
                {
                    rectTransform.sizeDelta = new Vector2(100, 80);
                }
            }

            Dispatcher.EnqueueRoutine(ResizeModal());
        }
#endif

        #endregion

        #region Misc Settings

        [UIAction(nameof(ClearCache))]
        public void ClearCache()
        {
            _clanService.ClearCache();
            TheCounter.ClearCounter();
        }

        [UIValue(nameof(MaxNameLength))]
        public int MaxNameLength
        {
            get => _config.MaxNameLength;
            set
            {
                _config.MaxNameLength = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(APITimeout))]
        public int APITimeout
        {
            get => _config.APITimeout;
            set
            {
                _config.APITimeout = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(UseGrad))]
        public bool UseGrad
        {
            get => _config.UseGrad;
            set
            {
                _config.UseGrad = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradMin))]
        private Color ColorGradMin
        {
            get => HelpfulMisc.ConvertColor(_config.ColorGradMin);
            set
            {
                _config.ColorGradMin = HelpfulMisc.ConvertColor(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradMax))]
        private Color ColorGradMax
        {
            get => HelpfulMisc.ConvertColor(_config.ColorGradMax);
            set
            {
                _config.ColorGradMax = HelpfulMisc.ConvertColor(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradZero))]
        private Color ColorGradZero
        {
            get => HelpfulMisc.ConvertColor(_config.ColorGradZero);
            set
            {
                _config.ColorGradZero = HelpfulMisc.ConvertColor(value);

                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradMinDark))]
        private float ColorGradMinDark
        {
            get => _config.ColorGradMinDark;
            set
            {
                _config.ColorGradMinDark = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradBlending))]
        private bool ColorGradBlending
        {
            get => _config.ColorGradBlending;
            set
            {
                _config.ColorGradBlending = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(BlendMiddleColor))]
        private bool BlendMiddleColor
        {
            get => _config.BlendMiddleColor;
            set
            {
                _config.BlendMiddleColor = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradFlipPercent))]
        private float ColorGradFlipPercent
        {
            get => _config.ColorGradFlipPercent;
            set
            {
                _config.ColorGradFlipPercent = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ColorGradMaxDiff))]
        private int ColorGradMaxDiff
        {
            get => _config.ColorGradMaxDiff;
            set
            {
                _config.ColorGradMaxDiff = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region Clan Counter Settings

        [UIValue(nameof(ShowClanMessage))]
        public bool ShowClanMessage
        {
            get => _config.ShowClanMessage;
            set
            {
                _config.ShowClanMessage = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(MapCache))]
        public int MapCache
        {
            get => _config.MapCache;
            set
            {
                _config.MapCache = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ClanPercentCeil))]
        public float ClanPercentCeil
        {
            get => _config.ClanPercentCeil;
            set
            {
                _config.ClanPercentCeil = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(CeilEnabled))]
        public bool CeilEnabled
        {
            get => _config.CeilEnabled;
            set
            {
                _config.CeilEnabled = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region Relative Counter Settings

        [UIValue(nameof(UseReplay))]
        public bool UseReplay
        {
            get => _config.UseReplay;
            set
            {
                _config.UseReplay = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ReplayMods))]
        public bool ReplayMods
        {
            get => _config.ReplayMods;
            set
            {
                _config.ReplayMods = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(DynamicAcc))]
        public bool DynamicAcc
        {
            get => _config.DynamicAcc;
            set
            {
                _config.DynamicAcc = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ShowRank))]
        public bool ShowRank
        {
            get => _config.ShowRank;
            set
            {
                _config.ShowRank = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(LocalReplays))]
        public bool LocalReplays
        {
            get => _config.LocalReplays;
            set
            {
                _config.LocalReplays = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(LocalReplaysOnly))]
        public bool LocalReplaysOnly
        {
            get => _config.LocalReplaysOnly;
            set
            {
                _config.LocalReplaysOnly = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(RelativeDefault))]
        public string RelativeDefault
        {
            get
            {
                if (RelativeDefaultList.Count == 0)
                {
                    _config.RelativeDefault = Targeter.NO_TARGET;
                }
                else if (!RelativeDefaultList.Contains(_config.RelativeDefault))
                {
                    _config.RelativeDefault = (string)RelativeDefaultList[0];
                }

                return _config.RelativeDefault;
            }
            set
            {
                _config.RelativeDefault = value;
                NotifyPropertyChanged();
            }
        }

        [UIComponent(nameof(DefaultCounterList))]
        private readonly ListSetting? DefaultCounterList;

        [UIValue(nameof(RelativeDefaultList))]
        public List<object> RelativeDefaultList =>
            [
                .. TypesOfPP
                    .Where(value => value is string name && !RelativeCounter.DisplayName.Equals(name))
                    .Prepend(Targeter.NO_TARGET),
            ];

        #endregion

        #region Rank Counter Settings

        [UIValue(nameof(MinRank))]
        public int MinRank
        {
            get => _config.MinRank;
            set
            {
                _config.MinRank = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(MaxRank))]
        public int MaxRank
        {
            get => _config.MaxRank;
            set
            {
                _config.MaxRank = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        #region Target Settings

        [UIValue(nameof(TargeterStartupWarnings))]
        public bool TargeterStartupWarnings
        {
            get => _config.TargeterStartupWarnings;
            set
            {
                _config.TargeterStartupWarnings = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(UseSteamFriends))]
        public bool UseSteamFriends
        {
            get => _config.UseSteamFriends;
            set
            {
                _config.UseSteamFriends = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ShowEnemy))]
        public bool ShowEnemy
        {
            get => _config.ShowEnemy;
            set
            {
                _config.ShowEnemy = value;
                NotifyPropertyChanged();
            }
        }

        [UIComponent(nameof(CustomTargetText))]
        private readonly TextMeshProUGUI CustomTargetText = null!;

        [UIComponent(nameof(CustomTargetInput))]
        private readonly StringSetting CustomTargetInput = null!;

        [UIComponent(nameof(CustomRankText))]
        private readonly TextMeshProUGUI CustomRankText = null!;

        [UIComponent(nameof(CustomRankInput))]
        private readonly StringSetting CustomRankInput = null!;

        [UIComponent(nameof(ClanTargetList))]
        private readonly CustomCellListTableData ClanTargetList = null!;

        [UIComponent(nameof(FollowerTargetList))]
        private readonly CustomCellListTableData FollowerTargetList = null!;

        [UIComponent(nameof(CustomTargetList))]
        private readonly CustomCellListTableData CustomTargetList = null!;

        [UIComponent(nameof(DisplayList))]
        private readonly CustomCellListTableData DisplayList = null!;

        [UIComponent(nameof(ReloadCustomRanksButton))]
        private readonly Button ReloadCustomRanksButton = null!;

        [UIComponent(nameof(ReloadFollowersButton))]
        private readonly Button ReloadFollowersButton = null!;

        [UIComponent(nameof(DeleteTarget))]
        private readonly Button DeleteTarget = null!;

#if !NEW_VERSION
        [UIObject(nameof(TargetModal))]
        private GameObject TargetModal = null!;
#endif

        [UIValue(nameof(CustomTargetName))]
        public string CustomTargetName
        {
            get => "";
            set
            {
                NotifyPropertyChanged();

                if (BetterString.IsNullOrWhiteSpace(value))
                    return;

                _taskRunner.Run(
                    cancellationToken => AddTargetByNameAsync(value, cancellationToken),
                    "Add custom target by name",
                    _lifetimeSource.Token
                );
            }
        }

        [UIValue(nameof(CustomRank))]
        public string CustomRank
        {
            get => "";
            set
            {
                NotifyPropertyChanged();

                if (BetterString.IsNullOrWhiteSpace(value))
                    return;

                _taskRunner.Run(
                    cancellationToken => AddTargetByRankAsync(value, cancellationToken),
                    "Add custom target by rank",
                    _lifetimeSource.Token
                );
            }
        }

        [UIValue(nameof(UseSSRank))]
        private bool UseSSRank
        {
            get => _config.UseSSRank;
            set
            {
                _config.UseSSRank = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(Target))]
        public string Target
        {
            get => _config.Target;
            set
            {
                _config.Target = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(AutoSelectAddedTarget))]
        public bool AutoSelectAddedTarget
        {
            get => _config.AutoSelectAddedTarget;
            set
            {
                _config.AutoSelectAddedTarget = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue(nameof(ClanTargetInfos))]
        private List<object> ClanTargetInfos => _clanTargetInfos;

        [UIValue(nameof(FollowerTargetInfos))]
        private List<object> FollowerTargetInfos => _followerTargetInfos;

        [UIValue(nameof(CustomTargetInfos))]
        private List<object> CustomTargetInfos => _customTargetInfos;

        [UIValue(nameof(SelectedTargetInfo))]
        private List<object> SelectedTargetInfo => _selectedTarget is null ? [] : [_selectedTarget];

        [UIAction(nameof(ResetTarget))]
        private void ResetTarget()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _targeter.ResetTarget();
            _selectedTarget = null;

            NotifyPropertyChanged(nameof(Target));
            UpdateSelectedTargetDisplay();
        }

        [UIAction(nameof(ReloadCustomRanks))]
        private void ReloadCustomRanks()
        {
            _taskRunner.Run(ReloadCustomRanksAsync, "Reload custom target ranks", _lifetimeSource.Token);
        }

        [UIAction(nameof(ReloadFollowers))]
        private void ReloadFollowers()
        {
            _taskRunner.Run(ReloadFollowersAsync, "Reload follower targets", _lifetimeSource.Token);
        }

        [UIAction(nameof(RemoveTarget))]
        private void RemoveTarget()
        {
            _taskRunner.Run(RemoveSelectedTargetAsync, "Remove custom target", _lifetimeSource.Token);
        }

        [UIAction("#ShowTargetMenu")]
        private void ShowTargetMenu()
        {
            _targetMenuIsOpen = true;

            IEnumerator RefreshAfterLayout()
            {
                yield return new WaitForEndOfFrame();
                UpdateTargetLists();
            }

            Dispatcher.EnqueueRoutine(RefreshAfterLayout());
        }

        [UIAction("#HideTargetMenu")]
        private void HideTargetMenu()
        {
            _targetMenuIsOpen = false;
        }

        public void UpdateTargetLists()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (!_targetMenuIsOpen)
                return;

#if NEW_VERSION
            ClanTargetList.Data = _clanTargetInfos;
            FollowerTargetList.Data = _followerTargetInfos;
            CustomTargetList.Data = _customTargetInfos;
            DisplayList.Data = SelectedTargetInfo;

            ClanTargetList.TableView.ReloadData();
            FollowerTargetList.TableView.ReloadData();
            CustomTargetList.TableView.ReloadData();
            DisplayList.TableView.ReloadData();
#else
            ClanTargetList.data = _clanTargetInfos;
            FollowerTargetList.data = _followerTargetInfos;
            CustomTargetList.data = _customTargetInfos;
            DisplayList.data = SelectedTargetInfo;

            ClanTargetList.tableView.ReloadData();
            FollowerTargetList.tableView.ReloadData();
            CustomTargetList.tableView.ReloadData();
            DisplayList.tableView.ReloadData();
#endif
        }

        private async Task AddTargetByNameAsync(string input, CancellationToken cancellationToken)
        {
            AsyncLock.Releaser? releaser = await _customInputLock.TryLockAsync(cancellationToken).ConfigureAwait(false);

            if (releaser is null)
                return;

            using (releaser.Value)
            {
                await Dispatcher
                    .InvokeAsync(
                        () => CustomTargetText.SetText("<color=\"yellow\">" + "Loading...</color>"),
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                try
                {
                    CustomTarget converted = await CustomTarget.ConvertToId(input).ConfigureAwait(false);

                    await AddConvertedTargetAsync(converted, CustomTargetText, CustomTargetInput, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Warn(exception.Message);
                    Plugin.Log.Debug(exception);

                    await Dispatcher
                        .InvokeAsync(
                            () =>
                                CustomTargetText.SetText(
                                    "<color=\"red\">" + $"Failure, {exception.Message}" + "</color>"
                                ),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task AddTargetByRankAsync(string input, CancellationToken cancellationToken)
        {
            AsyncLock.Releaser? releaser = await _customInputLock.TryLockAsync(cancellationToken).ConfigureAwait(false);

            if (releaser is null)
                return;

            using (releaser.Value)
            {
                await Dispatcher
                    .InvokeAsync(
                        () => CustomRankText.SetText("<color=\"yellow\">" + "Loading...</color>"),
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                try
                {
                    CustomTarget converted = await CustomTarget.ConvertFromRank(input).ConfigureAwait(false);

                    await AddConvertedTargetAsync(converted, CustomRankText, CustomRankInput, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Warn(exception.Message);
                    Plugin.Log.Debug(exception);

                    await Dispatcher
                        .InvokeAsync(
                            () =>
                                CustomRankText.SetText(
                                    "<color=\"red\">" + $"Failure, {exception.Message}" + "</color>"
                                ),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task AddConvertedTargetAsync(
            CustomTarget converted,
            TextMeshProUGUI statusText,
            StringSetting input,
            CancellationToken cancellationToken
        )
        {
            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            bool alreadyExists = _targeter.Target.UsedIDs.Contains(converted.ID);

            if (alreadyExists)
            {
                await Dispatcher
                    .InvokeAsync(
                        () =>
                        {
                            if (
                                AutoSelectAddedTarget && _idToTarget.TryGetValue(converted.ID, out TargetInfo? existing)
                            )
                            {
                                _selectedTarget = existing;
                                UpdateSelectedTarget();

                                statusText.SetText(
                                    "<color=#FFA500>" + "Set as target, ID is " + "already in use.</color>"
                                );

                                input.Text = "";
                                return;
                            }

                            statusText.SetText("<color=#FFA500>" + "This ID is already in use." + "</color>");
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                return;
            }

            await _targeter.AddTargetAsync(converted, cancellationToken).ConfigureAwait(false);

            await Dispatcher
                .InvokeAsync(
                    () =>
                    {
                        RefreshTargetInfoCaches();

                        if (AutoSelectAddedTarget && _idToTarget.TryGetValue(converted.ID, out TargetInfo? added))
                        {
                            _selectedTarget = added;
                            UpdateSelectedTarget();
                        }

                        statusText.SetText(
                            "<color=\"green\">Success"
                                + (AutoSelectAddedTarget ? ", player set as target." : "!")
                                + "</color>"
                        );

                        input.Text = "";
                        UpdateTargetLists();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private async Task ReloadCustomRanksAsync(CancellationToken cancellationToken)
        {
            await Dispatcher
                .InvokeAsync(() => ReloadCustomRanksButton.interactable = false, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _targeter.ReloadCustomPlayersAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Dispatcher
                        .InvokeAsync(() => ReloadCustomRanksButton.interactable = true)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task ReloadFollowersAsync(CancellationToken cancellationToken)
        {
            await Dispatcher
                .InvokeAsync(() => ReloadFollowersButton.interactable = false, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _targeter.ReloadFollowersAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(() => ReloadFollowersButton.interactable = true).ConfigureAwait(false);
                }
            }
        }

        private async Task RemoveSelectedTargetAsync(CancellationToken cancellationToken)
        {
            TargetInfo? selected = await Dispatcher
                .InvokeAsync(() => _selectedTarget, cancellationToken)
                .ConfigureAwait(false);

            if (selected is null)
                return;

            bool removed = await _targeter.DeleteTargetAsync(selected.RealID, cancellationToken).ConfigureAwait(false);

            if (!removed)
                return;

            await Dispatcher
                .InvokeAsync(
                    () =>
                    {
                        _selectedTarget = null;

                        RefreshTargetInfoCaches();
                        SetSelectedTargetRelation();
                        UpdateSelectedTargetDisplay();
                        UpdateTargetLists();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private void UpdateSelectedTarget()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (_selectedTarget is not null)
            {
                _selectedTarget.SetAsTarget();

                DeleteTarget.interactable = _targeter.Target.CustomTargets.Any(target =>
                    target.Id.Equals(_selectedTarget.RealID, StringComparison.Ordinal)
                );
            }
            else
            {
                _targeter.ResetTarget();
                DeleteTarget.interactable = false;

                NotifyPropertyChanged(nameof(Target));
            }

            UpdateSelectedTargetDisplay();
        }

        private void UpdateSelectedTargetDisplay()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (!_targetMenuIsOpen)
                return;

#if NEW_VERSION
            DisplayList.Data = SelectedTargetInfo;
            DisplayList.TableView.ReloadData();
#else
            DisplayList.data = SelectedTargetInfo;
            DisplayList.tableView.ReloadData();
#endif
        }

        private void UpdateSelectedTarget(SelectableCell selectedCell)
        {
            MainThreadDispatcher.AssertOnMainThread();

            _lastCellSelected?.SetSelected(false, SelectableCell.TransitionType.Instant, null, false);

            _lastCellSelected = selectedCell;
            UpdateSelectedTarget();
        }

        private void RefreshTargetInfoCaches()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (!_targeter.IsInitialized)
                return;

            TargeterInfo target = _targeter.Target;

            _clanTargetInfos.Clear();
            _followerTargetInfos.Clear();
            _customTargetInfos.Clear();
            _idToTarget.Clear();

            AddTargetInfos(target.ClanTargets, _clanTargetInfos, target);

            AddTargetInfos(target.FollowerTargets, _followerTargetInfos, target);

            AddTargetInfos(target.CustomTargets, _customTargetInfos, target);
        }

        private void AddTargetInfos(
            IEnumerable<BLPPCounter.Utils.Misc_Classes.TargetPair> targets,
            ICollection<object> destination,
            TargeterInfo targeterInfo
        )
        {
            foreach (BLPPCounter.Utils.Misc_Classes.TargetPair target in targets)
            {
                if (!targeterInfo.IDtoNames.TryGetValue(target.Id, out string? name))
                {
                    Plugin.Log.Warn($"ID \"{target.Id}\" does not have " + "a display name.");

                    continue;
                }

                TargetInfo info = _targetInfoFactory.Create(
                    name,
                    target.Id,
                    new TargetRank(Leaderboards.Beatleader, target.Rank)
                );

                destination.Add(info);

                if (long.TryParse(target.Id, out long id))
                    _idToTarget[id] = info;
            }
        }

        private void SetSelectedTargetRelation()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (!_targeter.IsInitialized)
                return;

            if (_config.TargetID > 0 && _idToTarget.TryGetValue(_config.TargetID, out TargetInfo? selected))
            {
                _selectedTarget = selected;
            }
            else
            {
                _selectedTarget = null;
            }

            DeleteTarget?.interactable =
                _selectedTarget is not null
                && _targeter.Target.CustomTargets.Any(target =>
                    target.Id.Equals(_selectedTarget.RealID, StringComparison.Ordinal)
                );
        }

        private void TargetPostParse()
        {
            RefreshTargetInfoCaches();
            SetSelectedTargetRelation();

#if NEW_VERSION
            ClanTargetList.TableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_clanTargetInfos[index];

                UpdateSelectedTarget(view.GetCellAtIndex(index));
            };

            FollowerTargetList.TableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_followerTargetInfos[index];

                UpdateSelectedTarget(view.GetCellAtIndex(index));
            };

            CustomTargetList.TableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_customTargetInfos[index];

                UpdateSelectedTarget(view.GetCellAtIndex(index));
            };
#else
            IEnumerator ResizeModal()
            {
                yield return new WaitForEndOfFrame();

                if (TargetModal.transform is RectTransform rectTransform)
                {
                    rectTransform.sizeDelta = new Vector2(200, 80);
                }
            }

            Dispatcher.EnqueueRoutine(ResizeModal());

            ClanTargetList.tableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_clanTargetInfos[index];

                UpdateSelectedTarget(ClanTargetList.CellForIdx(view, index));
            };

            FollowerTargetList.tableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_followerTargetInfos[index];

                UpdateSelectedTarget(FollowerTargetList.CellForIdx(view, index));
            };

            CustomTargetList.tableView.didSelectCellWithIdxEvent += (view, index) =>
            {
                _selectedTarget = (TargetInfo)_customTargetInfos[index];

                UpdateSelectedTarget(CustomTargetList.CellForIdx(view, index));
            };
#endif
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsHandler));
            }
        }
    }
}
