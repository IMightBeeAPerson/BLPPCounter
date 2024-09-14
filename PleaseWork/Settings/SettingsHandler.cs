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
        [UIValue("SplitVals")]
        public bool SplitPPVals
        {
            get => PluginConfig.Instance.SplitPPVals;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.SplitPPVals = value; }
        }
        [UIValue("DecimalPrecision")]
        public int DecimalPrecision
        {
            get => PluginConfig.Instance.DecimalPrecision;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.DecimalPrecision = value; }
        }
        [UIValue("FontSize")]
        public double FontSize
        {
            get => PluginConfig.Instance.FontSize;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.FontSize = value; }
        }
        [UIValue("TypesOfPP")]
        public List<object> TypesOfPP => Plugin.BLInstalled ?
            new List<object>() { "Normal", "Progressive", "Relative", "Relative w/ normal", "Clan PP", "Clan w/ normal" } :
            new List<object>() { "Normal", "Progressive" };
        [UIValue("PPType")]
        public string PPType
        {
            get => PluginConfig.Instance.PPType;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.PPType = value; }
        }
        [UIValue("ShowLbl")]
        public bool ShowLbl
        {
            get => PluginConfig.Instance.ShowLbl;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.ShowLbl = value; }
        }
        [UIValue("PPFC")]
        public bool PPFC
        {
            get => PluginConfig.Instance.PPFC;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.PPFC = value; }
        }
        [UIValue("Debug")]
        public bool Debug
        {
            get => PluginConfig.Instance.Debug;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.Debug = value; }
        }
        [UIValue("UseGrad")]
        public bool UseGrad
        {
            get => PluginConfig.Instance.UseGrad;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.UseGrad = value; }
        }
        [UIValue("GradVal")]
        public int GradVal
        {
            get => PluginConfig.Instance.GradVal;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.GradVal = value; }
        }
        [UIValue("Target")]
        public string Target
        {
            get => PluginConfig.Instance.Target;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.Target = value; }
        }
        [UIValue("toTarget")]
        public List<object> ToTarget => Targeter.clanNames;
        [UIValue("ShowEnemy")]
        public bool ShowEnemy
        {
            get => PluginConfig.Instance.ShowEnemy;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.ShowEnemy = value; }
        }
        [UIValue("LocalReplay")]
        public bool LocalReplay
        {
            get => PluginConfig.Instance.LocalReplay;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.LocalReplay = value; }
        }
        [UIValue("CaptureTypes")]
        public List<object> CaptureTypes = new List<object>() { "None", "Percentage", "PP", "Both", "Custom" };
        [UIValue("CaptureType")]
        public string CaptureType
        {
            get => PluginConfig.Instance.CaptureType;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.CaptureType = value; }
        }
        [UIValue("MapCache")]
        public int MapCache
        {
            get => PluginConfig.Instance.MapCache;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.MapCache = value; }
        }
        [UIAction("ClearCache")]
        public void ClearCache() { ClanCounter.ClearCache(); TheCounter.ClearCounter(); }
        [UIValue("PlNames")]
        public List<object> PlNames => new List<object>(PlaylistLoader.Instance.Names);
        [UIValue("ChosenPlaylist")]
        public string ChosenPlaylist
        {
            get => PluginConfig.Instance.ChosenPlaylist;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.ChosenPlaylist = value; }
        }
        [UIAction("LoadPlaylist")]
        public void LoadPlaylist() {
            Plugin.Log.Info("Button works");
            ClanCounter cc = new ClanCounter(null, 0f, 0f, 0f);
            /*foreach (string s in PlaylistLoader.Instance.Playlists.Keys) Plugin.Log.Info(s);
            Plugin.Log.Info(ChosenPlaylist);*/
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
        }
        [UIValue("ClanPercentCeil")]
        public double ClanPercentCeil
        {
            get => PluginConfig.Instance.ClanPercentCeil;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.ClanPercentCeil = value; }
        }
        [UIValue("CeilEnabled")]
        public bool CeilEnabled
        {
            get => PluginConfig.Instance.CeilEnabled;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.CeilEnabled = value; }
        }
        [UIValue("ShowRank")]
        public bool ShowRank
        {
            get => PluginConfig.Instance.ShowRank;
            set { SettingsUpdated?.Invoke(); PluginConfig.Instance.ShowRank = value; }
        }
    }
}