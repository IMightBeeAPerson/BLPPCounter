using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using CountersPlus.ConfigModels;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers
{
    /*<checkbox-setting text='Local Replays' apply-on-change='true' value='LocalReplay' hover-hint='Check for any local replays before loading from website' active='false'/>
     <dropdown-list-setting text='Playlists' apply-on-change='true' value='ChosenPlaylist' options='PlNames' hover-hint='A playlist to load' active='false'/>
    <button text='Load Playlist' on-click='LoadPlaylist' hover-hint='Loads the selected playlist into cache to prevent lag' active='false'/>*/
    //(?<att1>\[[^"\]]+)"[^"\]]+"(?<att2>\)]).*(?<bre>[\n\r]\s*)(?<words>(?:\w+ )+)(?!;)(?<name>\w+)
    //${att1}nameof(${name})${att2}${bre}${words}${name}
    public class SettingsHandler: ConfigModel, INotifyPropertyChanged
    {
#pragma warning disable CS0649, IDE0044
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        internal Action TypesOfPPChanged;
        private static PluginConfig PC => PluginConfig.Instance;
        public static SettingsHandler Instance { get; private set; } = new SettingsHandler();
        public static event Action<SettingsHandler> NewInstance;
        #endregion
        #region Init
        public SettingsHandler()
        {
            NewInstance?.Invoke(this);
            TypesOfPPChanged += () =>
            {
                if (CounterList != null) CounterList.UpdateListSetting(TypesOfPP.Cast<string>().ToList());
                else if (!TypesOfPP.Any(obj => ((string)obj).Equals(PPType))) PPType = (string)TypesOfPP[0];
                if (DefaultCounterList != null) DefaultCounterList.UpdateListSetting(RelativeDefaultList.Cast<string>().ToList());
                else if (!RelativeDefaultList.Any(obj => ((string)obj).Equals(RelativeDefault))) RelativeDefault = (string)RelativeDefaultList[0];
                PpInfoTabHandler.Instance.ChangeTabSettings = true;
                PpInfoTabHandler.Instance.ResetTabs();
                TheCounter.theCounter = null;
            };
            PropertyChanged += (obj, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Leaderboard):
                        TypesOfPPChanged?.Invoke();
                        break;
                    case nameof(UseUnranked):
                        TheCounter.theCounter = null;
                        break;
                    case nameof(DecimalPrecision):
                        string hold = "";
                        for (int i = 0; i < PC.DecimalPrecision; i++) hold += "#";
                        HelpfulFormatter.NUMBER_TOSTRING_FORMAT = PC.DecimalPrecision > 0 ? PC.FormatSettings.NumberFormat.Replace("#", "#." + hold) : PC.FormatSettings.NumberFormat;
                        break;
                    case nameof(Target):
                        PpInfoTabHandler.Instance.ResetTabs();
                        break;

                }
            };
        }
        #endregion
        #region General Settings

        [UIValue(nameof(UseUnranked))]
        public bool UseUnranked
        {
            get => PC.UseUnranked;
            set {PC.UseUnranked = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseUnranked))); }
        }
        [UIValue(nameof(DecimalPrecision))]
        public int DecimalPrecision
        {
            get => PC.DecimalPrecision;
            set {PC.DecimalPrecision = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(DecimalPrecision))); }
        }
        [UIValue(nameof(FontSize))]
        public float FontSize
        {
            get => PC.FontSize;
            set {PC.FontSize = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(FontSize))); }
        }
        [UIValue(nameof(ShowLbl))]
        public bool ShowLbl
        {
            get => PC.ShowLbl;
            set {PC.ShowLbl = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowLbl))); }
        }
        [UIValue(nameof(PPFC))]
        public bool PPFC
        {
            get => PC.PPFC;
            set {PC.PPFC = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(PPFC))); }
        }
        [UIValue(nameof(SplitVals))]
        public bool SplitVals
        {
            get => PC.SplitPPVals;
            set {PC.SplitPPVals = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(SplitVals))); }
        }
        [UIValue(nameof(ExtraInfo))]
        public bool ExtraInfo
        {
            get => PC.ExtraInfo;
            set { PC.ExtraInfo = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ExtraInfo))); }
        }
        [UIComponent(nameof(CounterList))]
        private ListSetting CounterList;
        [UIValue(nameof(TypesOfPP))]
        public List<object> TypesOfPP => new List<object>(TheCounter.DisplayNames);

        [UIValue(nameof(UpdateAfterTime))]
        public bool UpdateAfterTime
        {
            get => PC.UpdateAfterTime;
            set {PC.UpdateAfterTime = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UpdateAfterTime))); }
        }
        [UIValue(nameof(UpdateTime))]
        public float UpdateTime
        {
            get => PC.UpdateTime;
            set {PC.UpdateTime = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UpdateTime))); }
        }
        [UIValue(nameof(PPType))]
        public string PPType
        {
            get => PC.PPType;
            set {PC.PPType = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(PPType))); }
        }
        #endregion
        #region Leaderboard Settings
        [UIComponent(nameof(DefLeader))] 
        private ListSetting DefLeader;
        [UIValue(nameof(Leaderboard))]
        public string Leaderboard
        {
            get => PC.Leaderboard.ToString();
            set 
            {
                PC.Leaderboard = (Leaderboards)Enum.Parse(typeof(Leaderboards), value);
                if (!(DefLeader is null))
                {
#if NEW_VERSION
                    DefLeader.Values = DefaultLeaderboards;
                    if (!DefLeader.Values.Contains(DefaultLeaderboard))
                        DefaultLeaderboard = DefLeader.Values[0] as string;
#else
                    DefLeader.values = DefaultLeaderboards;
                    if (!DefLeader.values.Contains(DefaultLeaderboard))
                        DefaultLeaderboard = DefLeader.values[0] as string;
#endif
                    DefLeader.ReceiveValue();
                }
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Leaderboard)));
            }
        }
        [UIValue(nameof(LeaderboardList))]
        public List<object> LeaderboardList
        {
            get
            {
                List<object> outp = new List<object>((int)Math.Log((int)Leaderboards.All + 1, 2));
                for (int i = 1; i < (int)Leaderboards.All; i <<= 1)
                    outp.Add(((Leaderboards)i).ToString());
                return outp;
            }
        }
        [UIValue(nameof(DefaultToLeaderboard))]
        public bool DefaultToLeaderboard
        {
            get => PC.DefaultToLeaderboard;
            set { PC.DefaultToLeaderboard = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(DefaultToLeaderboard))); }
        }
        [UIValue(nameof(DefaultLeaderboard))]
        public string DefaultLeaderboard
        {
            get => PC.DefaultLeaderboard.ToString();
            set { PC.DefaultLeaderboard = (Leaderboards)Enum.Parse(typeof(Leaderboards), value); PropertyChanged(this, new PropertyChangedEventArgs(nameof(DefaultLeaderboard))); }
        }
        [UIValue(nameof(DefaultLeaderboards))]
        public List<object> DefaultLeaderboards => LeaderboardList.Where(l => !l.Equals(Leaderboard)).ToList();
        [UIValue(nameof(LeaderInLabel))] 
        public bool LeaderInLabel
        {
            get => PC.LeaderInLabel;
            set { PC.LeaderInLabel = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(LeaderInLabel))); }
        }
#endregion
        #region Misc Settings
        [UIAction(nameof(ClearCache))]
        public void ClearCache() { ClanCounter.ClearCache(); TheCounter.ClearCounter(); }
        [UIValue(nameof(UseGrad))]
        public bool UseGrad
        {
            get => PC.UseGrad;
            set { PC.UseGrad = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseGrad))); }
        }
        [UIValue(nameof(ColorGradMin))] private Color ColorGradMin
        {
            get => HelpfulMisc.ConvertColor(PC.ColorGradMin);
            set { PC.ColorGradMin = HelpfulMisc.ConvertColor(value); PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradMin))); }
        }
        [UIValue(nameof(ColorGradMax))]
        private Color ColorGradMax
        {
            get => HelpfulMisc.ConvertColor(PC.ColorGradMax);
            set { PC.ColorGradMax = HelpfulMisc.ConvertColor(value); PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradMax))); }
        }
        [UIValue(nameof(ColorGradZero))]
        private Color ColorGradZero
        {
            get => HelpfulMisc.ConvertColor(PC.ColorGradZero);
            set { PC.ColorGradZero = HelpfulMisc.ConvertColor(value); PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradZero))); }
        }
        [UIValue(nameof(ColorGradMinDark))]
        private float ColorGradMinDark
        {
            get => PC.ColorGradMinDark;
            set { PC.ColorGradMinDark = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradMinDark))); }
        }
        [UIValue(nameof(ColorGradBlending))]
        private bool ColorGradBlending
        {
            get => PC.ColorGradBlending;
            set { PC.ColorGradBlending = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradBlending))); }
        }
        [UIValue(nameof(BlendMiddleColor))]
        private bool BlendMiddleColor
        {
            get => PC.BlendMiddleColor;
            set { PC.BlendMiddleColor = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(BlendMiddleColor))); }
        }
        [UIValue(nameof(ColorGradFlipPercent))]
        private float ColorGradFlipPercent
        {
            get => PC.ColorGradFlipPercent;
            set { PC.ColorGradFlipPercent = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradFlipPercent))); }
        }
        [UIValue(nameof(ColorGradMaxDiff))]
        private int ColorGradMaxDiff
        {
            get => PC.ColorGradMaxDiff;
            set { PC.ColorGradMaxDiff = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ColorGradMaxDiff))); }
        }
        #endregion
        #region Clan Counter Settings
        [UIValue(nameof(ShowClanMessage))]
        public bool ShowClanMessage
        {
            get => PC.ShowClanMessage;
            set {PC.ShowClanMessage = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowClanMessage))); }
        }
        [UIValue(nameof(MapCache))]
        public int MapCache
        {
            get => PC.MapCache;
            set {PC.MapCache = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(MapCache))); }
        }
        [UIValue(nameof(ClanPercentCeil))]
        public float ClanPercentCeil
        {
            get => PC.ClanPercentCeil;
            set {PC.ClanPercentCeil = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ClanPercentCeil))); }
        }
        [UIValue(nameof(CeilEnabled))]
        public bool CeilEnabled
        {
            get => PC.CeilEnabled;
            set {PC.CeilEnabled = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(CeilEnabled))); }
        }
        #endregion
        #region Relative Counter Settings
        [UIValue(nameof(UseReplay))]
        public bool UseReplay
        {
            get => PC.UseReplay;
            set { PC.UseReplay = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseReplay))); }
        }
        [UIValue(nameof(DynamicAcc))]
        public bool DynamicAcc
        {
            get => PC.DynamicAcc;
            set { PC.DynamicAcc = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(DynamicAcc))); }
        }
        [UIValue(nameof(ShowRank))]
        public bool ShowRank
        {
            get => PC.ShowRank;
            set {PC.ShowRank = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowRank))); }
        }
        [UIValue(nameof(RelativeDefault))]
        public string RelativeDefault
        {
            get
            {
                if (RelativeDefaultList.Count == 0)
                    PC.RelativeDefault = Targeter.NO_TARGET;
                else if (!RelativeDefaultList.Contains(PC.RelativeDefault))
                    PC.RelativeDefault = (string)RelativeDefaultList[0];
                return PC.RelativeDefault;
            }
            set { PC.RelativeDefault = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(RelativeDefault))); }
        }
        [UIComponent(nameof(DefaultCounterList))]
        private ListSetting DefaultCounterList;
        [UIValue(nameof(RelativeDefaultList))]
        public List<object> RelativeDefaultList => TypesOfPP.Where(a => a is string b && !RelativeCounter.DisplayName.Equals(b)).Prepend(Targeter.NO_TARGET).ToList();
        #endregion
        #region Rank Counter Settings
        [UIValue(nameof(MinRank))]
        public int MinRank
        {
            get => PC.MinRank;
            set {PC.MinRank = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(MinRank))); }
        }
        [UIValue(nameof(MaxRank))]
        public int MaxRank
        {
            get => PC.MaxRank;
            set {PC.MaxRank = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(MaxRank))); }
        }
        #endregion
        #region Target Settings
        [UIValue(nameof(TargeterStartupWarnings))]
        public bool TargeterStartupWarnings
        {
            get => PC.TargeterStartupWarnings;
            set { PC.TargeterStartupWarnings = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(TargeterStartupWarnings))); }
        }
        [UIValue(nameof(ShowEnemy))]
        public bool ShowEnemy
        {
            get => PC.ShowEnemy;
            set { PC.ShowEnemy = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowEnemy))); }
        }

        [UIComponent(nameof(CustomTargetText))]
        private TextMeshProUGUI CustomTargetText;
        [UIComponent(nameof(CustomTargetInput))]
        private StringSetting CustomTargetInput;
        [UIComponent(nameof(CustomRankText))]
        private TextMeshProUGUI CustomRankText;
        [UIComponent(nameof(CustomRankInput))]
        private StringSetting CustomRankInput;

        [UIComponent(nameof(ClanTargetList))]
        private CustomCellListTableData ClanTargetList;
        [UIComponent(nameof(FollowerTargetList))]
        private CustomCellListTableData FollowerTargetList;
        [UIComponent(nameof(CustomTargetList))]
        private CustomCellListTableData CustomTargetList;
        [UIComponent(nameof(DisplayList))]
        private CustomCellListTableData DisplayList;
        [UIComponent(nameof(ReloadCustomRanksButton))]
        private Button ReloadCustomRanksButton;
        [UIComponent(nameof(ReloadFollowersButton))]
        private Button ReloadFollowersButton;
#if !NEW_VERSION
        [UIObject(nameof(TargetModal))]
        private GameObject TargetModal;
#endif
        private AsyncLock CustomInputLock = new AsyncLock();

        [UIValue(nameof(CustomTarget))]
        public string CustomTarget
        {
            get => "";
            set
            {
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(CustomTarget)));
                Task.Run(async () =>
                {
                    AsyncLock.Releaser? theLock = await CustomInputLock.TryLockAsync().ConfigureAwait(false);
                    if (theLock is null) return;
                    using (theLock.Value)
                    {
                        CustomTargetText.SetText("<color=\"yellow\">Loading...</color>");
                        try
                        {
                            CustomTarget converted = await Utils.CustomTarget.ConvertToId(value);
                            if (Targeter.UsedIDs.Contains(converted.ID))
                            {
                                if (AutoSelectAddedTarget)
                                {
                                    SelectedTarget = IdToTarget[converted.ID];
                                    UpdateSelectedTarget();
                                    CustomTargetText.SetText("<color=#FFA500>Set as target, ID is in use.</color>");
                                    CustomTargetInput.Text = "";
                                    return;
                                }
                                throw new ArgumentException("this ID is already in use.");
                            }
                            PC.CustomTargets.AddSorted(converted);
                            Targeter.AddTarget(converted);
                            TargetInfo ti = GetTargetInfo((converted.ID.ToString(), converted.Rank));
                            IdToTarget.Add(converted.ID, ti);
                            if (AutoSelectAddedTarget)
                            {
                                SelectedTarget = ti;
                                UpdateSelectedTarget();
                            }
                            CustomTargetText.SetText("<color=\"green\">Success!</color>");
                            CustomTargetInput.Text = "";
                            IEnumerator WaitThenUpdate()
                            {
                                yield return new WaitForEndOfFrame();
                                UpdateTargetLists();
                            }
                            await WaitThenUpdate().AsTask(CoroutineHost.Instance);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.Warn(e.Message);
                            CustomTargetText.SetText($"<color=\"red\">Failure, {e.Message}</color>");
                        }
                    }
                });
            }
        }
        [UIValue(nameof(CustomRank))]
        public string CustomRank
        {
            get => "";
            set
            {
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(CustomRank)));
                Task.Run(async () =>
                {
                    AsyncLock.Releaser? theLock = await CustomInputLock.TryLockAsync().ConfigureAwait(false);
                    if (theLock is null) return;
                    using (theLock.Value)
                    {
                        CustomRankText.SetText("<color=\"yellow\">Loading...</color>");
                        try
                        {
                            CustomTarget converted = await Utils.CustomTarget.ConvertFromRank(value);
                            if (Targeter.UsedIDs.Contains(converted.ID))
                            {
                                if (AutoSelectAddedTarget)
                                {
                                    SelectedTarget = IdToTarget[converted.ID];
                                    UpdateSelectedTarget();
                                    CustomRankText.SetText("<color=#FFA500>Set as target, ID is in use.</color>");
                                    CustomRankInput.Text = "";
                                    return;
                                }
                                throw new ArgumentException("this ID is already in use.");
                            }
                            PC.CustomTargets.AddSorted(converted);
                            Targeter.AddTarget(converted);
                            TargetInfo ti = GetTargetInfo((converted.ID.ToString(), converted.Rank));
                            IdToTarget.Add(converted.ID, ti);
                            if (AutoSelectAddedTarget)
                            {
                                SelectedTarget = ti;
                                UpdateSelectedTarget();
                            }
                            CustomRankText.SetText("<color=\"green\">Success!</color>");
                            CustomRankInput.Text = "";
                            IEnumerator WaitThenUpdate()
                            {
                                yield return new WaitForEndOfFrame();
                                UpdateTargetLists();
                            }
                            await WaitThenUpdate().AsTask(CoroutineHost.Instance);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.Warn(e.Message);
                            CustomRankText.SetText($"<color=\"red\">Failure, {e.Message}</color>");
                        }
                    }
                });
            }
        }
        [UIValue(nameof(UseSSRank))]
        private bool UseSSRank
        {
            get => PC.UseSSRank;
            set { PC.UseSSRank = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseSSRank))); }
        }
        [UIValue(nameof(Target))]
        public string Target
        {
            get => PC.Target;
            set 
            {
                PC.Target = value;
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(Target)));
            }
        }
        [UIValue(nameof(AutoSelectAddedTarget))]
        public bool AutoSelectAddedTarget
        {
            get => PC.AutoSelectAddedTarget;
            set 
            {
                PC.AutoSelectAddedTarget = value;
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(AutoSelectAddedTarget)));
            }
        }
        [UIValue(nameof(ClanTargetInfos))]
        private List<object> ClanTargetInfos => GetTargetList(Targeter.ClanTargets);
        [UIValue(nameof(FollowerTargetInfos))]
        private List<object> FollowerTargetInfos => GetTargetList(Targeter.FollowerTargets);
        [UIValue(nameof(CustomTargetInfos))]
        private List<object> CustomTargetInfos => GetTargetList(Targeter.CustomTargets);
        [UIValue(nameof(SelectedTargetInfo))]
        private List<object> SelectedTargetInfo => SelectedTarget is null ? new List<object>(0) : new List<object>(1) { SelectedTarget };
        private object SelectedTarget = null;
        private SelectableCell LastCellSelected;
        private bool TargetMenuHasBeenOpened = false;
        private readonly Dictionary<long, TargetInfo> IdToTarget = new Dictionary<long, TargetInfo>();

        [UIAction(nameof(ResetTarget))]
        private void ResetTarget()
        {
            Target = Targeter.NO_TARGET;
            PC.TargetID = -1;
            SelectedTarget = null;
            UpdateSelectedTarget();
        }
        [UIAction(nameof(ReloadCustomRanks))]
        private void ReloadCustomRanks()
        {
            ReloadCustomRanksButton.interactable = false;
            Task.Run(() =>
            {
                Targeter.ReloadCustomPlayers();
                ReloadCustomRanksButton.interactable = true;
            });
        }
        [UIAction(nameof(ReloadFollowers))]
        private void ReloadFollowers()
        {
            ReloadFollowersButton.interactable = false;
            Task.Run(() =>
            {
                Targeter.ReloadFollowers();
                ReloadFollowersButton.interactable = true;
            });
        }
#pragma warning disable IDE0051
        [UIAction("#ShowTargetMenu")]
        private void ShowTargetMenu()
        {
            if (!TargetMenuHasBeenOpened)
            {
                TargetMenuHasBeenOpened = true;
                IEnumerator WaitThenUpdate()
                {
                    yield return new WaitForEndOfFrame();
                    UpdateTargetLists();
                }
                CoroutineHost.Start(WaitThenUpdate());
            }
        }
#pragma warning restore IDE0051
        public void UpdateTargetLists()
        {
            if (!TargetMenuHasBeenOpened) return;
#if NEW_VERSION
            ClanTargetList.Data = ClanTargetInfos;
            FollowerTargetList.Data = FollowerTargetInfos;
            CustomTargetList.Data = CustomTargetInfos;
            DisplayList.Data = SelectedTargetInfo;

            ClanTargetList.TableView.ReloadData();
            FollowerTargetList.TableView.ReloadData();
            CustomTargetList.TableView.ReloadData();
            DisplayList.TableView.ReloadData();
#else
            ClanTargetList.data = ClanTargetInfos;
            FollowerTargetList.data = FollowerTargetInfos;
            CustomTargetList.data = CustomTargetInfos;
            DisplayList.data = SelectedTargetInfo;

            ClanTargetList.tableView.ReloadData();
            FollowerTargetList.tableView.ReloadData();
            CustomTargetList.tableView.ReloadData();
            DisplayList.tableView.ReloadData();
#endif
        }
        private void UpdateSelectedTarget()
        {
            if (SelectedTarget is TargetInfo ti && !(ti is null))
                ti.SetAsTarget();
            else
            {
                Target = Targeter.NO_TARGET;
                PC.TargetID = -1;
            }
            if (!TargetMenuHasBeenOpened) return;
#if NEW_VERSION
            DisplayList.Data = SelectedTargetInfo;
            DisplayList.TableView.ReloadData();
#else
            DisplayList.data = SelectedTargetInfo;
            DisplayList.tableView.ReloadData();
#endif
        }
        private void UpdateSelectedTarget(SelectableCell setCell)
        {
            LastCellSelected?.SetSelected(false, SelectableCell.TransitionType.Instant, null, false);
            LastCellSelected = setCell;
            UpdateSelectedTarget();
        }
        private List<object> GetTargetList(IEnumerable<(string ID, int Rank)> ids)
        {
            if (ids is null) return new List<object>(0);
            List<object> outp = new List<object>(ids.Count());
            foreach (var (id, rank) in ids)
            {
                if (!Targeter.IDtoNames.TryGetValue(id, out string name))
                {
                    Plugin.Log.Warn($"ID \"{id}\" not found to have a display name.");
                    continue;
                }
                outp.Add(new TargetInfo(name, id, (Leaderboards.Beatleader, rank)));
            }
            return outp;
        }
        private TargetInfo GetTargetInfo((string ID, int Rank) id)
        {
            if (!Targeter.IDtoNames.TryGetValue(id.ID, out string name))
            {
                Plugin.Log.Warn($"ID \"{id}\" not found to have a display name.");
                return null;
            }
            return new TargetInfo(name, id.ID, (Leaderboards.Beatleader, id.Rank));
        }
#endregion
#pragma warning disable IDE0051
        [UIAction("#post-parse")]
        private void PostParse()
        {
#if NEW_VERSION
            ClanTargetList.TableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = ClanTargetInfos[index]; UpdateSelectedTarget(view.GetCellAtIndex(index)); };
            FollowerTargetList.TableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = FollowerTargetInfos[index]; UpdateSelectedTarget(view.GetCellAtIndex(index)); };
            CustomTargetList.TableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = CustomTargetInfos[index]; UpdateSelectedTarget(view.GetCellAtIndex(index)); };
#else
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                (TargetModal.transform as RectTransform).sizeDelta = new Vector2(200, 80);
            }
            CoroutineHost.Start(WaitThenUpdate());
            ClanTargetList.tableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = ClanTargetInfos[index]; UpdateSelectedTarget(ClanTargetList.CellForIdx(view, index)); };
            FollowerTargetList.tableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = FollowerTargetInfos[index]; UpdateSelectedTarget(FollowerTargetList.CellForIdx(view, index)); };
            CustomTargetList.tableView.didSelectCellWithIdxEvent += (view, index) => { SelectedTarget = CustomTargetInfos[index]; UpdateSelectedTarget(CustomTargetList.CellForIdx(view, index)); };
#endif
        }
        internal void SetSelectedTargetRelation()
        {
            //This will be slow, but it only runs once so who cares.
            foreach (object cell in CustomTargetInfos.Union(FollowerTargetInfos).Union(ClanTargetInfos))
                if (cell is TargetInfo ti && long.TryParse(ti.RealID, out long ID))
                    IdToTarget.Add(ID, ti);
            if (PC.TargetID > -1)
            {
                SelectedTarget = IdToTarget[PC.TargetID];
                if (TargetMenuHasBeenOpened)
                    UpdateSelectedTarget();
            }
        }
    }
}
