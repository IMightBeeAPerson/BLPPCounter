using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using CountersPlus.ConfigModels;
using BLPPCounter.Counters;
using BLPPCounter.Utils;
using BLPPCounter.Settings.Configs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using BLPPCounter.Helpfuls;
using System.Threading.Tasks;

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
        [UIValue(nameof(UseGrad))]
        public bool UseGrad
        {
            get => PC.UseGrad;
            set {PC.UseGrad = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseGrad))); }
        }
        [UIValue(nameof(GradVal))]
        public int GradVal
        {
            get => PC.GradVal;
            set { PC.GradVal = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(GradVal))); }
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
                if (!RelativeDefaultList.Contains(PC.RelativeDefault)) if (RelativeDefaultList.Count > 0)
                        PC.RelativeDefault = (string)RelativeDefaultList[0];
                    else PC.RelativeDefault = Targeter.NO_TARGET; return PC.RelativeDefault;
            }
            set { PC.RelativeDefault = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(RelativeDefault))); }
        }
        [UIComponent(nameof(DefaultCounterList))]
        private ListSetting DefaultCounterList;
        [UIValue(nameof(RelativeDefaultList))]
        public List<object> RelativeDefaultList => TypesOfPP.Where(a => a is string b && !RelativeCounter.DisplayName.Equals(b)).ToList();
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
        [UIComponent(nameof(TargetList))]
        private DropDownListSetting TargetList;
        [UIComponent(nameof(CustomTargetText))]
        private TextMeshProUGUI CustomTargetText;
        [UIComponent(nameof(CustomTargetInput))]
        private StringSetting CustomTargetInput;
        [UIValue(nameof(CustomTarget))]
        public string CustomTarget
        {
            get => "";
            set
            {
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(CustomTarget)));
                try
                {
                    var converted = Utils.CustomTarget.ConvertToId(value);
                    PC.CustomTargets.Add(converted);
                    Targeter.AddTarget(converted.Name, $"{converted.ID}");
                    CustomTargetText.SetText("<color=\"green\">Success!</color>");
                    CustomTargetInput.Text = "";
#if NEW_VERSION
                    TargetList.Values = Targeter.CustomTargets; // 1.37.0 and above
#else
                    TargetList.values = Targeter.CustomTargets; // 1.34.2 and below
#endif
                    TargetList.UpdateChoices();
                }
                catch (ArgumentException e)
                {
                    Plugin.Log.Warn(e.Message);
                    CustomTargetText.SetText("<color=\"red\">Failure, id or alias not found.</color>");
                }
                
            }
        }
        [UIValue(nameof(ShowEnemy))]
        public bool ShowEnemy
        {
            get => PC.ShowEnemy;
            set {PC.ShowEnemy = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowEnemy))); }
        }
        [UIValue(nameof(Target))]
        public string Target
        {
            get => PC.Target;
            set {PC.Target = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(Target))); }
        }
        [UIValue(nameof(ClanTargets))]
        public List<object> ClanTargets => (Targeter.ClanTargets ?? new List<object>()).Prepend(Targeter.NO_TARGET).ToList();
        [UIValue(nameof(FollowerTargets))]
        public List<object> FollowerTargets => (Targeter.FollowerTargets ?? new List<object>()).Prepend(Targeter.NO_TARGET).ToList();
        [UIValue(nameof(CustomTargets))]
        public List<object> CustomTargets => (Targeter.CustomTargets ?? new List<object>()).Prepend(Targeter.NO_TARGET).ToList();
        [UIAction(nameof(ResetTarget))]
        public void ResetTarget()
        {
            TargetList.Value = "None";
            Target = "None";
        }
        #endregion
    }
}
