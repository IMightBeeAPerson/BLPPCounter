﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using BLPPCounter.Utils;
using System.Drawing;
using System.Reflection;
using System.Linq;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace BLPPCounter.Settings.Configs
{
    internal class PluginConfig
    {
        #region Non-Settings Variables
        public static PluginConfig Instance { get; set; }
        public virtual TokenFormatSettings TokenSettings { get; set; } = new TokenFormatSettings();
        public virtual MessageSettings MessageSettings { get; set; } = new MessageSettings();
        public virtual TextFormatSettings FormatSettings { get; set; } = new TextFormatSettings();
        #endregion
        #region General Settings
        public virtual int DecimalPrecision { get; set; } = 2;
        public virtual float FontSize { get; set; } = 3;
        public virtual bool ShowLbl { get; set; } = true;
        public virtual bool PPFC { get; set; } = true;
        public virtual bool SplitPPVals { get; set; } = false;
        public virtual bool ExtraInfo { get; set; } = true;
        public virtual bool UseGrad { get; set; } = true;
        public virtual int GradVal { get; set; } = 100;
        public virtual bool UpdateAfterTime { get; set; } = false;
        public virtual float UpdateTime { get; set; } = 0.5f;
        public virtual string PPType { get; set; } = "Normal";
        #endregion
        #region Leaderboard Settings
        public virtual bool DefaultToLeaderboard { get; set; } = true;
        [UseConverter]
        public virtual Leaderboards Leaderboard { get; set; } = Leaderboards.Beatleader;
        [UseConverter]
        public virtual Leaderboards DefaultLeaderboard { get; set; } = Leaderboards.Scoresaber;
        public virtual bool UseUnranked { get; set; } = true;
        public virtual bool LeaderInLabel { get; set; } = true;
        #endregion
        #region Clan Counter Settings
        public virtual bool ShowClanMessage { get; set; } = true;
        public virtual int MapCache { get; set; } = 10;
        public virtual float ClanPercentCeil { get; set; } = 99.0f;
        public virtual bool CeilEnabled { get; set; } = true;
        #endregion
        #region Relative Counter Settings
        public virtual bool UseReplay { get; set; } = true;
        public virtual bool DynamicAcc { get; set; } = true;
        public virtual bool ShowRank { get; set; } = true;
        public virtual string RelativeDefault { get; set; } = "Normal";
        #endregion
        #region Rank Counter Settings
        public virtual int MinRank { get; set; } = 100;
        public virtual int MaxRank { get; set; } = 0;
        #endregion
        #region Target Settings
        public virtual bool ShowEnemy { get; set; } = true;
        public virtual string Target { get; set; } = Targeter.NO_TARGET;

        //The below list is not in order so that in the config file there is nothing below this that gets obstructed.
        [UseConverter(typeof(ListConverter<CustomTarget>))]
        public virtual List<CustomTarget> CustomTargets { get; set; } = new List<CustomTarget>();
        #endregion
        #region Unused Code
        //public virtual bool LocalReplay { get; set; } = false;
        //public virtual string ChosenPlaylist { get; set; } = "";
        #endregion
        #region Menu Settings
        #region Simple Settings
        public virtual bool SimpleUI { get; set; } = true;
        public virtual long SimpleMenuConfig { get; set; } = 0; //Don't worry about this, nothing janky at all going on here :)
        public virtual int SimpleMenuConfigLength { get; set; } = 0; //Nothing janky at all
        #endregion
        #region Format Settings
        public virtual bool UpdatePreview { get; set; } = true;
        public virtual bool AutoUpdateRefs { get; set; } = true;

        #region Colors
        [UseConverter(typeof(SystemColorConverter))] public virtual Color EscapeCharacterColor { get; set; } = Color.FromArgb(235, 33, 235); //#eb21eb
        [UseConverter(typeof(SystemColorConverter))] public virtual Color SpecialCharacterColor { get; set; } = Color.Goldenrod;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color AliasColor { get; set; } = Color.FromArgb(187, 242, 46); //#bbf22e
        [UseConverter(typeof(SystemColorConverter))] public virtual Color AliasQuoteColor { get; set; } = Color.FromArgb(32, 171, 51); //#20ab33
        [UseConverter(typeof(SystemColorConverter))] public virtual Color ParamColor { get; set; } = Color.DarkCyan;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color ParamVarColor { get; set; } = Color.Brown;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color DelimeterColor { get; set; } = Color.DarkSlateGray;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color CaptureColor { get; set; } = Color.FromArgb(44, 241, 245); //#2cf1f5
        [UseConverter(typeof(SystemColorConverter))] public virtual Color CaptureIdColor { get; set; } = Color.LightBlue;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color GroupColor { get; set; } = Color.FromArgb(27, 40, 224); //#1b28e0
        [UseConverter(typeof(SystemColorConverter))] public virtual Color GroupReplaceColor { get; set; } = Color.FromArgb(255, 75, 43); //#ff4b2b
        [UseConverter(typeof(SystemColorConverter))] public virtual Color ShorthandColor { get; set; } = Color.DarkMagenta;
        [UseConverter(typeof(SystemColorConverter))] public virtual Color HighlightColor { get; set; } = Color.FromArgb(119, 255, 255, 0); //#77ffff00
        [UseConverter(typeof(SystemColorConverter))] public virtual Color SecondHighlightColor { get; set; } = Color.FromArgb(119, 18, 252, 255); //#7712fcff
        [Ignore] private readonly Dictionary<string, PropertyInfo> Colors = new Dictionary<string, PropertyInfo>(
            typeof(PluginConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.Equals(typeof(Color)))
                .Select(p => new KeyValuePair<string, PropertyInfo>(p.Name.Substring(0, p.Name.Length - 5), p)));
        public Color GetColorFromName(string name) => (Color)Colors[name].GetValue(this);
        [Ignore] public IEnumerable<PropertyInfo> ColorInfos => Colors.Values;
        #endregion
        #endregion
        #endregion
        #region BL Calculator Settings
        public virtual int TestPPAmount { get; set; } = 450;
        public virtual float TestAccAmount { get; set; } = 95.0f;
        public virtual float PercentSliderMin { get; set; } = 75.0f;
        public virtual float PercentSliderMax { get; set; } = 100.0f;
        public virtual int PPSliderMin { get; set; } = 0;
        public virtual int PPSliderMax { get; set; } = 1000;
        public virtual int SliderIncrementNum {  get; set; } = 5;
        #endregion
    }
}
