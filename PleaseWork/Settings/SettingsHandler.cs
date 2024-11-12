using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using CountersPlus.ConfigModels;
using PleaseWork.Counters;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;

namespace PleaseWork.Settings
{
    /*<checkbox-setting text='Local Replays' apply-on-change='true' value='LocalReplay' hover-hint='Check for any local replays before loading from website' active='false'/>
     <dropdown-list-setting text='Playlists' apply-on-change='true' value='ChosenPlaylist' options='PlNames' hover-hint='A playlist to load' active='false'/>
    <button text='Load Playlist' on-click='LoadPlaylist' hover-hint='Loads the selected playlist into cache to prevent lag' active='false'/>*/
    public class SettingsHandler: ConfigModel
    {
        #region Variables
        public static event Action SettingsUpdated;
        private event PropertyChangedEventHandler PropertyChanged;
        private static PluginConfig pc => PluginConfig.Instance;
        #endregion
        #region Init
        public SettingsHandler()
        {
            PropertyChanged += (a,b) => SettingsUpdated.Invoke();
        }
        #endregion
        #region General Settings
        [UIValue("DecimalPrecision")]
        public int DecimalPrecision
        {
            get => pc.DecimalPrecision;
            set => pc.DecimalPrecision = value;
        }
        [UIValue("FontSize")]
        public double FontSize
        {
            get => pc.FontSize;
            set => pc.FontSize = value;
        }
        [UIValue("ShowLbl")]
        public bool ShowLbl
        {
            get => pc.ShowLbl;
            set => pc.ShowLbl = value;
        }
        [UIValue("PPFC")]
        public bool PPFC
        {
            get => pc.PPFC;
            set => pc.PPFC = value;
        }
        [UIValue("SplitVals")]
        public bool SplitPPVals
        {
            get => pc.SplitPPVals;
            set => pc.SplitPPVals = value;
        }
        [UIValue("ExtraInfo")]
        public bool ExtraInfo
        {
            get => pc.ExtraInfo;
            set => pc.ExtraInfo = value;
        }
        [UIValue("UseGrad")]
        public bool UseGrad
        {
            get => pc.UseGrad;
            set => pc.UseGrad = value;
        }
        [UIValue("GradVal")]
        public int GradVal
        {
            get => pc.GradVal;
            set => pc.GradVal = value;
        }
        [UIValue("TypesOfPP")]
        public List<object> TypesOfPP => Plugin.BLInstalled ?
            new List<object>(TheCounter.ValidDisplayNames) :
            new List<object>() { "Normal", "Progressive" };
        [UIValue("PPType")]
        public string PPType
        {
            get => pc.PPType;
            set => pc.PPType = value;
        }
        #endregion
        #region Misc Settings
        [UIAction("ClearCache")]
        public void ClearCache() { ClanCounter.ClearCache(); TheCounter.ClearCounter(); }
        #endregion
        #region Clan Counter Settings
        [UIValue("ShowClanMessage")]
        public bool ShowClanMessage
        {
            get => pc.ShowClanMessage;
            set => pc.ShowClanMessage = value;
        }
        [UIValue("MapCache")]
        public int MapCache
        {
            get => pc.MapCache;
            set => pc.MapCache = value;
        }
        [UIValue("ClanPercentCeil")]
        public double ClanPercentCeil
        {
            get => pc.ClanPercentCeil;
            set => pc.ClanPercentCeil = value;
        }
        [UIValue("CeilEnabled")]
        public bool CeilEnabled
        {
            get => pc.CeilEnabled;
            set => pc.CeilEnabled = value;
        }
        #endregion
        #region Relative Counter Settings
        
        [UIValue("ShowRank")]
        public bool ShowRank
        {
            get => pc.ShowRank;
            set => pc.ShowRank = value;
        }
        [UIValue("RelativeDefault")]
        public string RelativeDefault
        {
            get
            {
                if (!RelativeDefaultList.Contains(pc.RelativeDefault)) if (RelativeDefaultList.Count > 0)
                        pc.RelativeDefault = (string)RelativeDefaultList[0];
                    else pc.RelativeDefault = Targeter.NO_TARGET; return pc.RelativeDefault;
            }
            set => pc.RelativeDefault = value;
        }
        [UIValue("RelativeDefaultList")]
        public List<object> RelativeDefaultList => TypesOfPP.Where(a => a is string b && !RelativeCounter.DisplayName.Equals(b)).ToList();
        #endregion
        #region Rank Counter Settings
        [UIValue("MinRank")]
        public int MinRank
        {
            get => pc.MinRank;
            set => pc.MinRank = value;
        }
        [UIValue("MaxRank")]
        public int MaxRank
        {
            get => pc.MaxRank;
            set => pc.MaxRank = value;
        }
        [UIValue("AdaptableRank")]
        public bool AdaptableRank
        {
            get => pc.AdaptableRank;
            set => pc.AdaptableRank = value;
        }
        #endregion
        #region Target Settings
        [UIComponent("TargetList")]
        private DropDownListSetting targetList;
        [UIComponent("CustomTargetMessage")]
        private TextMeshProUGUI customTargetText;
        [UIComponent("CustomTargetInput")]
        private StringSetting customTargetInput;
        [UIValue("CustomTarget")]
        public string CustomTarget
        {
            get => "";
            set
            {
                SettingsUpdated?.Invoke();
                try
                {
                    var converted = Utils.CustomTarget.ConvertToId(value);
                    pc.CustomTargets.Add(converted);
                    Targeter.AddTarget(converted.Name, $"{converted.ID}");//*/
                    //Targeter.AddTarget("This is a test", "1234");
                    customTargetText.SetText("<color=\"green\">Success!</color>");
                    customTargetInput.Text = "";
                    //targetList.Values = ToTarget; /* 1.37.4 and up*/
                    targetList.values = ToTarget; /* 1.34.2 and below*/
                    targetList.UpdateChoices();
                }
                catch (ArgumentException e)
                {
                    Plugin.Log.Warn(e.Message);
                    customTargetText.SetText("<color=\"red\">Failure, id or alias not found.</color>");
                }
                
            }
        }
        [UIValue("ShowEnemy")]
        public bool ShowEnemy
        {
            get => pc.ShowEnemy;
            set => pc.ShowEnemy = value;
        }
        [UIValue("Target")]
        public string Target
        {
            get => pc.Target;
            set => pc.Target = value;
        }
        [UIValue("toTarget")]
        public List<object> ToTarget => Targeter.theTargets.Prepend(Targeter.NO_TARGET).ToList();
        #endregion
        #region Unused Code
        /*[UIValue("LocalReplay")]
        public bool LocalReplay
        {
            get => pc.LocalReplay;
            set => pc.LocalReplay = value;
        }
        [UIValue("PlNames")]
        public List<object> PlNames => new List<object>(PlaylistLoader.Instance.Names);
        [UIValue("ChosenPlaylist")]
        public string ChosenPlaylist
        {
            get => pc.ChosenPlaylist;
			set => pc.ChosenPlaylist = value;
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
