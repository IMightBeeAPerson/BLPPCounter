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

namespace BLPPCounter.Settings.SettingHandlers
{
    /*<checkbox-setting text='Local Replays' apply-on-change='true' value='LocalReplay' hover-hint='Check for any local replays before loading from website' active='false'/>
     <dropdown-list-setting text='Playlists' apply-on-change='true' value='ChosenPlaylist' options='PlNames' hover-hint='A playlist to load' active='false'/>
    <button text='Load Playlist' on-click='LoadPlaylist' hover-hint='Loads the selected playlist into cache to prevent lag' active='false'/>*/
    public class SettingsHandler: ConfigModel, INotifyPropertyChanged
    {
#pragma warning disable CS0649
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        private static PluginConfig pc => PluginConfig.Instance;
        public static SettingsHandler Instance { get; private set; } = new SettingsHandler();
        public static event Action<SettingsHandler> NewInstance;
        #endregion
        #region Init
        public SettingsHandler()
        {
            NewInstance?.Invoke(this);
        }
        #endregion
        #region General Settings
        [UIValue(nameof(DecimalPrecision))]
        public int DecimalPrecision
        {
            get => pc.DecimalPrecision;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(DecimalPrecision))); pc.DecimalPrecision = value; }
        }
        [UIValue(nameof(FontSize))]
        public double FontSize
        {
            get => pc.FontSize;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(FontSize))); pc.FontSize = value; }
        }
        [UIValue(nameof(ShowLbl))]
        public bool ShowLbl
        {
            get => pc.ShowLbl;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowLbl))); pc.ShowLbl = value; }
        }
        [UIValue(nameof(PPFC))]
        public bool PPFC
        {
            get => pc.PPFC;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(PPFC))); pc.PPFC = value; }
        }
        [UIValue(nameof(SplitVals))]
        public bool SplitVals
        {
            get => pc.SplitPPVals;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(SplitVals))); pc.SplitPPVals = value; }
        }
        [UIValue(nameof(ExtraInfo))]
        public bool ExtraInfo
        {
            get => pc.ExtraInfo;
            set { PropertyChanged(this, new PropertyChangedEventArgs(nameof(ExtraInfo))); pc.ExtraInfo = value; }
        }
        [UIValue(nameof(UseGrad))]
        public bool UseGrad
        {
            get => pc.UseGrad;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(UseGrad))); pc.UseGrad = value; }
        }
        [UIValue(nameof(GradVal))]
        public int GradVal
        {
            get => pc.GradVal;
            set { PropertyChanged(this, new PropertyChangedEventArgs(nameof(GradVal))); pc.GradVal = value; }
        }
        [UIValue(nameof(TypesOfPP))]
        public List<object> TypesOfPP => Plugin.BLInstalled ?
            new List<object>(TheCounter.ValidDisplayNames) :
            new List<object>() { "Normal", "Progressive" };
        [UIValue(nameof(PPType))]
        public string PPType
        {
            get => pc.PPType;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(PPType))); pc.PPType = value; }
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
            get => pc.ShowClanMessage;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowClanMessage))); pc.ShowClanMessage = value; }
        }
        [UIValue(nameof(MapCache))]
        public int MapCache
        {
            get => pc.MapCache;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(MapCache))); pc.MapCache = value; }
        }
        [UIValue(nameof(ClanPrecentCeil))]
        public double ClanPrecentCeil
        {
            get => pc.ClanPrecentCeil;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(ClanPrecentCeil))); pc.ClanPrecentCeil = value; }
        }
        [UIValue(nameof(CeilEnabled))]
        public bool CeilEnabled
        {
            get => pc.CeilEnabled;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(CeilEnabled))); pc.CeilEnabled = value; }
        }
        #endregion
        #region Relative Counter Settings
        
        [UIValue(nameof(ShowRank))]
        public bool ShowRank
        {
            get => pc.ShowRank;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowRank))); pc.ShowRank = value; }
        }
        [UIValue(nameof(RelativeDefault))]
        public string RelativeDefault
        {
            get
            {
                if (!RelativeDefaultList.Contains(pc.RelativeDefault)) if (RelativeDefaultList.Count > 0)
                        pc.RelativeDefault = (string)RelativeDefaultList[0];
                    else pc.RelativeDefault = Targeter.NO_TARGET; return pc.RelativeDefault;
            }
            set { PropertyChanged(this, new PropertyChangedEventArgs(nameof(RelativeDefault))); pc.RelativeDefault = value; }
        }
        [UIValue(nameof(RelativeDefaultList))]
        public List<object> RelativeDefaultList => TypesOfPP.Where(a => a is string b && !RelativeCounter.DisplayName.Equals(b)).ToList();
        #endregion
        #region Rank Counter Settings
        [UIValue(nameof(MinRank))]
        public int MinRank
        {
            get => pc.MinRank;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(MinRank))); pc.MinRank = value; }
        }
        [UIValue(nameof(MaxRank))]
        public int MaxRank
        {
            get => pc.MaxRank;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(MaxRank))); pc.MaxRank = value; }
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
                    pc.CustomTargets.Add(converted);
                    Targeter.AddTarget(converted.Name, $"{converted.ID}");
                    customTargetText.SetText("<color=\"green\">Success!</color>");
                    customTargetInput.Text = "";
                    targetList.Values = ToTarget; /* 1.37.4 and up*/
                    //targetList.values = ToTarget; /* 1.34.2 and below*/
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
            get => pc.ShowEnemy;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof(ShowEnemy))); pc.ShowEnemy = value; }
        }
        [UIValue(nameof(Target))]
        public string Target
        {
            get => pc.Target;
            set {pc.Target = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(Target))); }
        }
        [UIValue(nameof(ToTarget))]
        public List<object> ToTarget => Targeter.theTargets.Prepend(Targeter.NO_TARGET).ToList();
        #endregion
        #region Unused Code
        /*[UIValue(nameof())]
        public bool LocalReplay
        {
            get => pc.LocalReplay;
            set {PropertyChanged(this, new PropertyChangedEventArgs(nameof())); pc.LocalReplay = value; }
        }
        [UIValue(nameof())]
        public List<object> PlNames => new List<object>(PlaylistLoader.Instance.Names);
        [UIValue(nameof())]
        public string ChosenPlaylist
        {
            get => pc.ChosenPlaylist;
			set {PropertyChanged(this, new PropertyChangedEventArgs(nameof())); pc.ChosenPlaylist = value; }
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
