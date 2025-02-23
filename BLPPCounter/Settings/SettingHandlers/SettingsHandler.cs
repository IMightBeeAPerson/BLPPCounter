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

namespace BLPPCounter.Settings.SettingHandlers
{
    /*<checkbox-setting text='Local Replays' apply-on-change='true' value='LocalReplay' hover-hint='Check for any local replays before loading from website' active='false'/>
     <dropdown-list-setting text='Playlists' apply-on-change='true' value='ChosenPlaylist' options='PlNames' hover-hint='A playlist to load' active='false'/>
    <button text='Load Playlist' on-click='LoadPlaylist' hover-hint='Loads the selected playlist into cache to prevent lag' active='false'/>*/
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
            };
            PropertyChanged += (obj, args) =>
            {
                if (args.PropertyName.Equals(nameof(UsingSS))) TypesOfPPChanged?.Invoke();
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
        [UIValue(nameof(UsingSS))]
        public bool UsingSS
        {
            get => PC.UsingSS;
            set {PC.UsingSS = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(UsingSS))); }
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
        [UIValue(nameof(PPType))]
        public string PPType
        {
            get => PC.PPType;
            set {PC.PPType = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(PPType))); }
        }
        #endregion
        #region Misc Settings
        [UIAction("ClearCache")]
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
        public double ClanPercentCeil
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
        [UIComponent("TargetList")]
        private DropDownListSetting targetList;
        [UIComponent("CustomTargetMessage")]
        private TextMeshProUGUI customTargetText;
        [UIComponent("CustomTargetInput")]
        private StringSetting customTargetInput;
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
                    customTargetText.SetText("<color=\"green\">Success!</color>");
                    customTargetInput.Text = "";
#if NEW_VERSION
                    targetList.Values = ToTarget; // 1.37.0 and above
#else
                    targetList.values = ToTarget; // 1.34.2 and below
#endif
                    targetList.UpdateChoices();
                }
                catch (ArgumentException e)
                {
                    Plugin.Log.Warn(e.Message);
                    customTargetText.SetText("<color=\"red\">Failure, id or alias not found.</color>");
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
        [UIValue(nameof(ToTarget))]
        public List<object> ToTarget => Targeter.theTargets.Prepend(Targeter.NO_TARGET).ToList();
        #endregion
        #region Unused Code
        /*[UIValue(nameof())]
        public bool LocalReplay
        {
            get => PC.LocalReplay;
            set {PC.LocalReplay = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof())); }
        }
        [UIValue(nameof())]
        public List<object> PlNames => new List<object>(PlaylistLoader.Instance.Names);
        [UIValue(nameof())]
        public string ChosenPlaylist
        {
            get => PC.ChosenPlaylist;
			set {PC.ChosenPlaylist = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof())); }
        }
        [UIAction("LoadPlaylist")]
        public void LoadPlaylist() {
            Plugin.Log.Info("Button works");
            ClanCounter cc = new ClanCounter(null, 0f, 0f, 0f);
            foreach (string s in PlaylistLoader.Instance.Playlists.Keys) Plugin.Log.Info(s);
            Plugin.Log.Info(ChosenPlaylist);
            MapSelection[] maps = PlaylistLoader.Instance.Playlists[ChosenPlaylist];
            foreach (MapSelection map in maps)
            {
                float[] pp = null;
                //Plugin.Log.Info("" + map + "\n" + map.Map);
                int status;
                try { status = (int)map.MapData.Item2["status"]; } catch { continue; }
                if (status != 3) { Plugin.Log.Info($"Status: {status}"); continue; }
                try { pp = cc.LoadNeededPp(map.MapData.Item1, out _); } catch (Exception e) { Plugin.Log.Info($"Error loading map {map.Map.Hash}: {e.Message}"); Plugin.Log.Debug(e); }
                if (pp != null)
                {
                    ClanCounter.AddToCache(map, pp);
                    Plugin.Log.Info($"map {map.Map.Hash} loaded!");
                }
            }
            Plugin.Log.Info("Loading completed!");
        }//*/
        #endregion
    }
}
