using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using HMUI;
using ModestTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using static BLPPCounter.Helpfuls.HelpfulFormatter;
namespace BLPPCounter.Settings.SettingHandlers
{
    public class MenuSettingsHandler
    {
#pragma warning disable CS0649, IDE0044
        #region Variables
        #region Static
        private static PluginConfig pc => PluginConfig.Instance;
        public static MenuSettingsHandler Instance { get; private set; } = new MenuSettingsHandler();
        #endregion
        #region Settings
        private int changes = 0;
        private readonly Action<int, bool> AddChange;
        #endregion
        #endregion
        #region Init
        public MenuSettingsHandler()
        {
            SettingsToSave = new bool[pc.SimpleMenuConfigLength];
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, pc.SimpleMenuConfig);
            AddChange = (id, newVal) => { if (SettingsToSave[id] == newVal) changes &= ~(1 << id); else changes |= 1 << id; saveButton.interactable = changes > 0; };
        }
        #endregion
        #region Main Settings
        [UIValue(nameof(SimpleUI))]
        public bool SimpleUI
        {
            get => pc.SimpleUI;
            set => pc.SimpleUI = value;
        }
        [UIValue(nameof(UpdatePreview))]
        public bool UpdatePreview
        {
            get => pc.UpdatePreview;
            set => pc.UpdatePreview = value;
        }
        #region Simple Settings Editor
        public bool[] SettingsToSave { get; private set; }
        [UIComponent("UICustomizer")]
        private CustomCellListTableData UICustomizer;
        [UIComponent("SaveButton")]
        private UnityEngine.UI.Button saveButton;
        [UIValue("UISettings")]
        public List<object> UISettings { get; } = new List<object>();
        private bool loaded = false;
#pragma warning disable IDE0051
        [UIAction("SaveChanges")]
        private void SaveChanges()
        {
            pc.SimpleMenuConfig ^= changes;
            HelpfulMisc.ConvertInt32ToBools(SettingsToSave, pc.SimpleMenuConfig);
            changes = 0;
            saveButton.interactable = false;
            SimpleSettingsHandler.Instance.ReloadTab();
        }
        [UIAction("LoadMenu")]
        public void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            UISettings.AddRange(ConvertMenu().Cast<object>());
            for (int i = 0; i < UISettings.Count; i++) if (UISettings[i] is SettingToggleInfo sti) sti.Usable = SettingsToSave[i];
            changes = 0;
            UICustomizer.TableView.ReloadData();
        }
#pragma warning restore IDE0051
        private List<SettingToggleInfo> ConvertMenu()
        {
            const string resource = "BLPPCounter.Settings.BSML.MenuSettings.bsml";
            const string regex = "<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['\"])(.*?)\\2[^>]*?(?:(?<=hover-hint) *= *(['\"])(.*?)\\4[^>]*)?\\/>$";
            List<SettingToggleInfo> outp = new List<SettingToggleInfo>();
            MatchCollection mc = Regex.Matches(Utilities.GetResourceContent(System.Reflection.Assembly.GetExecutingAssembly(), resource), regex, RegexOptions.Multiline);
            if (pc.SimpleMenuConfigLength != mc.Count)
            {
                SettingsToSave = new bool[mc.Count];
                for (int i = 0; i < SettingsToSave.Length; i++) SettingsToSave[i] = true;
                pc.SimpleMenuConfigLength = mc.Count;
                pc.SimpleMenuConfig = HelpfulMisc.ConvertBoolsToInt32(SettingsToSave);
            }
            int count = 0;
            for (int i = 0; i < mc.Count; i++) 
                outp.Add(new SettingToggleInfo(
                    mc[i].Groups[3].Value,
                    mc[i].Groups.Count >= 6 ? mc[i].Groups[5].Value : "",
                    mc[i].Groups[1].Value.Replace('-', ' '),
                    count++,
                    AddChange
                    ));
            return outp;
        }
        #endregion
        #region Format Settings Editor
        //string #1: Name of message, string #2: Name of counter, string #3: Format string, dictionary: aliases
        private readonly Dictionary<(string, string), (string, Dictionary<string, char>, Func<string, string>, Func<char, int>)> AllFormatInfo =
            new Dictionary<(string, string), (string, Dictionary<string, char>, Func<string, string>, Func<char, int>)>()
        {
            {("Main Format", NormalCounter.DisplayName), (pc.FormatSettings.DefaultTextFormat, TheCounter.FormatAlias, TheCounter.QuickFormatDefault, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Target Format", NormalCounter.DisplayName), (pc.MessageSettings.TargetingMessage, TheCounter.TargetAlias, TheCounter.QuickFormatTarget, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Percent Needed Format", NormalCounter.DisplayName), (pc.MessageSettings.PercentNeededMessage, TheCounter.PercentNeededAlias, TheCounter.QuickFormatPercentNeeded, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Main Format", ClanCounter.DisplayName), (pc.FormatSettings.ClanTextFormat, ClanCounter.FormatAlias, ClanCounter.QuickFormatClan, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Weighted Format", ClanCounter.DisplayName), (pc.FormatSettings.WeightedTextFormat, ClanCounter.WeightedFormatAlias, ClanCounter.QuickFormatWeighted, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Custom Format", ClanCounter.DisplayName), (pc.MessageSettings.ClanMessage, ClanCounter.MessageFormatAlias, ClanCounter.QuickFormatMessage, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) },
            {("Main Format", RelativeCounter.DisplayName), (pc.FormatSettings.RelativeTextFormat, RelativeCounter.FormatAlias, RelativeCounter.QuickFormat, HelpfulFormatter.GLOBAL_PARAM_AMOUNT) }
        };
        private bool loaded2 = false;
        private (string, Dictionary<string, char>, Func<string, string>, Func<char, int>) CurrentFormatInfo => AllFormatInfo[(_FormatName, _Counter)];
        #region UI Components
        #region Main Menu
        [UIComponent(nameof(ChooseFormat))]
        private DropDownListSetting ChooseFormat;
        [UIComponent(nameof(RawFormatText))]
        private TextMeshProUGUI RawFormatText;
        [UIComponent(nameof(FormattedText))]
        private TextMeshProUGUI FormattedText;
        #endregion
        #region Format Editor
        [UIComponent(nameof(FormatEditor))]
        private CustomCellListTableData FormatEditor;
        [UIComponent(nameof(RawPreviewDisplay))]
        private TextMeshProUGUI RawPreviewDisplay;
        [UIComponent(nameof(PreviewDisplay))]
        private TextMeshProUGUI PreviewDisplay;
        #endregion
        #endregion
        #region UI Values
        [UIValue(nameof(FormatChunks))]
        public List<object> FormatChunks { get; } = new List<object>();
        [UIValue(nameof(Counter))]
        private string Counter { get => _Counter; set { _Counter = value; UpdateFormatOptions(); } }
        private string _Counter;
        [UIValue(nameof(FormatName))]
        private string FormatName { get => _FormatName; set { _FormatName = value; UpdateFormatDisplay(); } }
        private string _FormatName;
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames.Where(a => AllFormatInfo.Any(b => b.Key.Item2.Equals(a))).Cast<object>().ToList();
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames = new List<object>();
        #endregion
        #region UI Actions & UI Called Functions
#pragma warning disable IDE0051
        #region Main Menu
        private void UpdateFormatOptions()
        {
            FormatNames = AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList();
            ChooseFormat.Values = FormatNames;
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        private void UpdateFormatDisplay()
        {
            FormattedText.text = CurrentFormatInfo.Item3.Invoke(CurrentFormatInfo.Item1);
            RawFormatText.text = ColorFormatting(CurrentFormatInfo.Item1.Replace("\n", "\\n"));
            Plugin.Log.Debug(RawFormatText.text);
        }
        [UIAction("LoadMenu2")]
        private void LoadMenu2()
        {
            if (loaded2) return;
            loaded2 = true;
            Counter = CounterNames[0] as string;
        }
        #endregion
        #region Format Editor
        private void UpdateFormatTable(bool forceUpdate = false)
        {
            if (!forceUpdate && !pc.UpdatePreview) return;
            FormatEditor.TableView.ReloadData();
            UpdatePreviewDisplay();
        }
        [UIAction(nameof(SelectedCell))]
        private void SelectedCell(TableView _, object obj)
        {
            const string richEnd = "</mark>";
            string richStart = $"<mark={pc.HighlightColor.ToArgb():X8}>";
            FormatListInfo selectedFli = obj as FormatListInfo;
            string outp = "";
            foreach (FormatListInfo fli in FormatChunks.Cast<FormatListInfo>())
                outp += string.Format(selectedFli.Equals(fli) ? $"{richStart}{{0}}{richEnd}" : "{0}", fli.GetDisplay());
            RawPreviewDisplay.text = ColorFormatting(outp);
            PreviewDisplay.text = CurrentFormatInfo.Item3.Invoke(outp.Replace("\\n", "\n").Replace(richStart,$"{RICH_SHORT}mark{DELIMITER}{pc.HighlightColor.ToArgb():X8}{RICH_SHORT}").Replace(richEnd,""+RICH_SHORT));
            Plugin.Log.Info("New Formatted Text: " + PreviewDisplay.text);
        }
        [UIAction(nameof(AddDefaultChunk))]
        private void AddDefaultChunk()
        {
            FormatChunks.Add(FormatListInfo.InitRegularText("Default Text"));
            UpdateFormatTable(true);
        }
        [UIAction(nameof(UpdatePreviewDisplay))]
        public void UpdatePreviewDisplay()
        {
            string outp = "";
            foreach (FormatListInfo fli in FormatChunks.Cast<FormatListInfo>())
                outp += fli.GetDisplay();
            RawPreviewDisplay.text = ColorFormatting(outp);
            PreviewDisplay.text = CurrentFormatInfo.Item3.Invoke(outp.Replace("\\n", "\n"));
        }
        [UIAction(nameof(ParseCurrentFormat))]
        private void ParseCurrentFormat()
        {
            string currentFormat = CurrentFormatInfo.Item1.Replace("\n", "\\n");
            FormatListInfo.AliasConverter = CurrentFormatInfo.Item2;
            FormatListInfo.MovePlacement = (fli, goinUP) =>
            {
                int index = FormatChunks.IndexOf(fli);
                if (goinUP)
                {
                    if (index > 0)
                    {
                        FormatChunks[index] = FormatChunks[index - 1];
                        FormatChunks[index - 1] = fli;
                        UpdateFormatTable();
                    }
                } else if (index < FormatChunks.Count - 1)
                {
                    FormatChunks[index] = FormatChunks[index + 1];
                    FormatChunks[index + 1] = fli;
                    UpdateFormatTable();
                }
            };
            FormatListInfo.RemoveSelf = (fli) =>
            {
                FormatChunks.Remove(fli);
                UpdateFormatTable();
            };
            string editedFormat = currentFormat;
            List<FormatListInfo> outp = new List<FormatListInfo>();
            bool groupOpen = false, captureOpen = false;
            string richKey = "";
            while (editedFormat.Length > 0)
            {
                if (groupOpen && editedFormat[0] == INSERT_SELF)
                {
                    outp.Add(FormatListInfo.InitInsertSelf());
                    editedFormat = editedFormat.Substring(1);
                    continue;
                }
                Match m = Regex.Match(editedFormat, FormatListInfo.GroupRegex);
                if (m.Success)
                {
                    groupOpen = !groupOpen;
                    outp.Add(FormatListInfo.InitGroup(groupOpen, m.Value.Substring(1).Replace($"{ALIAS}", "")));
                    goto endOfLoop;
                }
                m = Regex.Match(editedFormat, FormatListInfo.AliasRegex);
                if (m.Success)
                {
                    outp.Add(FormatListInfo.InitEscapedCharacter(
                        m.Value[1] == ALIAS || !IsSpecialChar(m.Value[1]),
                        m.Groups[3].Success ? m.Groups[3].Value.Substring(2, m.Groups[3].Value.Length - 3) : m.Groups[4].Success ? m.Groups[4].Value[1] + "" : m.Groups[1].Value.Substring(1).Replace($"{ALIAS}", ""),
                        m.Groups[2].Success ? m.Groups[2].Value.Replace($"{ALIAS}", "").Split(DELIMITER) : null
                        ));
                    goto endOfLoop;
                }
                m = Regex.Match(editedFormat, FormatListInfo.RegularTextRegex);
                if (m.Success)
                {
                    outp.Add(FormatListInfo.InitRegularText( m.Value));
                    goto endOfLoop;
                }
                m = Regex.Match(editedFormat, $"^(?:{Regex.Escape($"{CAPTURE_OPEN}")}\\d+|{Regex.Escape($"{CAPTURE_CLOSE}")})"); //^(?:<\\d+|>)
                if (m.Success) 
                {
                    captureOpen = !captureOpen;
                    outp.Add(FormatListInfo.InitCapture(captureOpen, m.Value.Substring(1)));
                    goto endOfLoop;
                }
                m = Regex.Match(editedFormat, "^(?:{0}(.+?){1}(.+?){0}|{0})".Fmt(Regex.Escape(RICH_SHORT+""), Regex.Escape(DELIMITER+""))); //^(?:\*(.+?),(.+?)\*|\*)
                if (m.Success)
                {
                    bool isOpen = richKey.Length == 0;
                    if (isOpen) richKey = RICH_SHORTHANDS.TryGetValue(m.Groups[1].Value, out string val) ? val : m.Groups[1].Value;
                    string hasQuotes = m.Groups[2].Value.Contains(' ') ? "\"" : "";
                    outp.Add(FormatListInfo.InitRichText(isOpen, richKey, isOpen ? $"{hasQuotes}{m.Groups[2].Value}{hasQuotes}" : ""));
                    if (!isOpen) richKey = "";
                    goto endOfLoop;
                }
            endOfLoop:
                if (m.Success)
                    editedFormat = editedFormat.Remove(0, m.Length);
                else break;
            }
            FormatChunks.Clear();
            FormatChunks.AddRange(outp.Cast<object>());
            //Plugin.Log.Info("THE CHUNKS\n" + string.Join("\n", FormatChunks));
            FormatEditor.TableView.ReloadData();
            UpdatePreviewDisplay();
        }
        #endregion
#pragma warning restore IDE0051
        #endregion
        #endregion
        #endregion
    }
}
