using BeatSaberMarkupLanguage.Attributes;
using CountersPlus.ConfigModels;
using PleaseWork.Counters;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;

namespace PleaseWork.Settings
{
    /*<dropdown-list-setting text='Playlists' apply-on-change='true' value='ChosenPlaylist' options='PlNames' hover-hint='A playlist to load' active='false'/>
    <button text='Load Playlist' on-click='LoadPlaylist' hover-hint='Loads the selected playlist into cache to prevent lag' active='false'/>*/
    public class SettingsHandler : ConfigModel
    {
        public static event Action SettingsUpdated;
        private static PluginConfig pc => PluginConfig.Instance;
        [UIValue("SplitVals")]
        public bool SplitPPVals
        {
            get => pc.SplitPPVals;
			set { SettingsUpdated?.Invoke(); pc.SplitPPVals = value; }
        }
        [UIValue("DecimalPrecision")]
        public int DecimalPrecision
        {
            get => pc.DecimalPrecision;
            set { SettingsUpdated?.Invoke(); pc.DecimalPrecision = value; }
        }
        [UIValue("FontSize")]
        public double FontSize
        {
            get => pc.FontSize;
            set { SettingsUpdated?.Invoke(); pc.FontSize = value; }
        }
        [UIValue("TypesOfPP")]
        public List<object> TypesOfPP => Plugin.BLInstalled ?
            new List<object>(TheCounter.ValidDisplayNames)  :
            new List<object>() { "Normal", "Progressive" };
        [UIValue("PPType")]
        public string PPType
        {
            get => pc.PPType;
			set { SettingsUpdated?.Invoke(); pc.PPType = value; }
        }
        [UIValue("ShowLbl")]
        public bool ShowLbl
        {
            get => pc.ShowLbl;
			set { SettingsUpdated?.Invoke(); pc.ShowLbl = value; }
        }
        [UIValue("PPFC")]
        public bool PPFC
        {
            get => pc.PPFC;
			set { SettingsUpdated?.Invoke(); pc.PPFC = value; }
        }
        [UIValue("ExtraInfo")]
        public bool ExtraInfo
        {
            get => pc.ExtraInfo;
			set { SettingsUpdated?.Invoke(); pc.ExtraInfo = value; }
        }
        [UIValue("UseGrad")]
        public bool UseGrad
        {
            get => pc.UseGrad;
			set { SettingsUpdated?.Invoke(); pc.UseGrad = value; }
        }
        [UIValue("GradVal")]
        public int GradVal
        {
            get => pc.GradVal;
			set { SettingsUpdated?.Invoke(); pc.GradVal = value; }
        }
        [UIValue("Target")]
        public string Target
        {
            get => pc.Target;
			set { SettingsUpdated?.Invoke(); pc.Target = value; }
        }
        [UIValue("toTarget")]
        public List<object> ToTarget => Targeter.theTargets;
        [UIValue("ShowClanList")]
        public bool ShowClanList => !pc.ShowCustomTargets;
        [UIValue("CustomTarget")]
        public string CustomTarget
        {
            get => "";
			set { 
                SettingsUpdated?.Invoke();
                try
                {
                    var converted = Utils.CustomTarget.ConvertToId(value);
                    pc.CustomTarget = converted.ID;
                    pc.CustomTargets.Add(converted);
                    Targeter.AddTarget(converted.Name, $"{converted.ID}");
                } catch (ArgumentException e)
                {
                    Plugin.Log.Warn(e.Message);
                }
            }
        }
        [UIValue("ShowEnemy")]
        public bool ShowEnemy
        {
            get => pc.ShowEnemy;
			set { SettingsUpdated?.Invoke(); pc.ShowEnemy = value; }
        }
        [UIValue("LocalReplay")]
        public bool LocalReplay
        {
            get => pc.LocalReplay;
			set { SettingsUpdated?.Invoke(); pc.LocalReplay = value; }
        }
        [UIValue("ShowClanMessage")]
        public bool ShowClanMessage
        {
            get => pc.ShowClanMessage;
			set { SettingsUpdated?.Invoke(); pc.ShowClanMessage = value; }
        }
        [UIValue("MapCache")]
        public int MapCache
        {
            get => pc.MapCache;
			set { SettingsUpdated?.Invoke(); pc.MapCache = value; }
        }
        [UIAction("ClearCache")]
        public void ClearCache() { ClanCounter.ClearCache(); TheCounter.ClearCounter(); }
        /*[UIValue("PlNames")]
        public List<object> PlNames => new List<object>(PlaylistLoader.Instance.Names);
        [UIValue("ChosenPlaylist")]
        public string ChosenPlaylist
        {
            get => pc.ChosenPlaylist;
			set { SettingsUpdated?.Invoke(); pc.ChosenPlaylist = value; }
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
        [UIValue("ClanPercentCeil")]
        public double ClanPercentCeil
        {
            get => pc.ClanPercentCeil;
			set { SettingsUpdated?.Invoke(); pc.ClanPercentCeil = value; }
        }
        [UIValue("CeilEnabled")]
        public bool CeilEnabled
        {
            get => pc.CeilEnabled;
			set { SettingsUpdated?.Invoke(); pc.CeilEnabled = value; }
        }
        [UIValue("ShowRank")]
        public bool ShowRank
        {
            get => pc.ShowRank;
			set { SettingsUpdated?.Invoke(); pc.ShowRank = value; }
        }
    }
}