using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.List_Settings;
using HMUI;
using ModestTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        #region Misc Variables
        private readonly Dictionary<(string, string), FormatRelation> AllFormatInfo = new Dictionary<(string, string), FormatRelation>()
        {
            { TheCounter.DefaultFormatRelation.GetKey, TheCounter.DefaultFormatRelation },
            { TheCounter.TargetFormatRelation.GetKey, TheCounter.TargetFormatRelation },
            { TheCounter.PercentNeededFormatRelation.GetKey, TheCounter.PercentNeededFormatRelation },
            { ClanCounter.ClanFormatRelation.GetKey, ClanCounter.ClanFormatRelation },
            { ClanCounter.WeightedFormatRelation.GetKey, ClanCounter.WeightedFormatRelation },
            { ClanCounter.MessageFormatRelation.GetKey, ClanCounter.MessageFormatRelation },
            { RelativeCounter.DefaultFormatRelation.GetKey, RelativeCounter.DefaultFormatRelation }
        };
        private FormatRelation CurrentFormatInfo => AllFormatInfo[(_FormatName, _Counter)];
        private bool loaded2 = false;
        private string rawFormat;
        private bool saveable = false;
        #endregion
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
        [UIComponent(nameof(TheSaveButton))]
        private UnityEngine.UI.Button TheSaveButton;
        [UIComponent(nameof(SaveMessage))]
        private TextMeshProUGUI SaveMessage;
        [UIComponent(nameof(AliasTable))]
        private TextMeshProUGUI AliasTable;
        #endregion
        #region Value Editor
        [UIComponent(nameof(ValueEditor))]
        private CustomCellListTableData ValueEditor;
        [UIComponent(nameof(ValuePreviewDisplay))]
        private TextMeshProUGUI ValuePreviewDisplay;
        #endregion
        #endregion
        #region UI Values
        #region Main Menu
        [UIValue(nameof(Counter))]
        private string Counter { get => _Counter; set { _Counter = value; UpdateFormatOptions(); } }
        private string _Counter;
        [UIValue(nameof(FormatName))]
        private string FormatName { get => _FormatName; set { _FormatName = value; UpdateFormatDisplay(); } }
        private string _FormatName;
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames.Where(a => AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
            .Append(TheCounter.DisplayName).Cast<object>().ToList();
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames = new List<object>();
        #endregion
        #region Format Editor
        [UIValue(nameof(FormatChunks))]
        public List<object> FormatChunks { get; } = new List<object>();
        #endregion
        #region Value Editor
        [UIValue(nameof(FormatValues))]
        private List<object> FormatValues { get; } = new List<object>();
        #endregion
        #endregion
        #region UI Actions & UI Called Functions
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
            FormattedText.text = CurrentFormatInfo.GetQuickFormat();
            RawFormatText.text = FormatListInfo.ColorFormat(CurrentFormatInfo.Format.Replace("\n", "\\n"));
            //Plugin.Log.Debug(RawFormatText.text);
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
            UpdatePreviewDisplay(true);
        }
        public void UpdatePreviewDisplay(bool forceUpdate = false)
        {
            if (!forceUpdate && !pc.UpdatePreview) return;
            string outp = "", colorOutp = "";
            saveable = true;
            foreach (FormatListInfo fli in FormatChunks.Cast<FormatListInfo>())
            {
                outp += fli.GetDisplay();
                colorOutp += fli.GetColorDisplay();
                saveable &= fli.Updatable();
            }
            PreviewDisplay.text = saveable ? CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")) : "Can not format.";
            RawPreviewDisplay.text = colorOutp;
            rawFormat = outp.Replace("\\n", "\n");
            FormatEditor.TableView.ClearSelection();
            UpdateSaveButton();
        }
        private void UpdateSaveButton()
        {
            TheSaveButton.interactable = saveable;
            SaveMessage.text = "";
        }
        private void UpdateAliasTable()
        {
            float maxLength = CurrentFormatInfo.Descriptions.Keys.Aggregate(0.0f, (a, b) => Math.Max(a, AliasTable.GetPreferredValues(b).x));
            string outp = $"Tokens<space={maxLength - AliasTable.GetPreferredValues("| Tokens").x}px>  | Descriptions\n";
            float maxLineLength = maxLength + CurrentFormatInfo.Descriptions.Values.Aggregate(0.0f, (a, b) => Math.Max(a, AliasTable.GetPreferredValues("  | " + b).x));
            for (int i = (int)Math.Ceiling(maxLineLength / AliasTable.GetPreferredValues("-").x); i > 0; i--)
                outp += '-';
            outp += '\n';
            string formatString = "| {0}<space={2}px>  | {1}\n";
            foreach (var val in CurrentFormatInfo.Descriptions)
                outp += string.Format(formatString, val.Key, val.Value, maxLength - AliasTable.GetPreferredValues(val.Key).x);
            AliasTable.text = outp;
        }
        [UIAction(nameof(SelectedCell))]
        private void SelectedCell(TableView _, object obj)
        {
            const string richEnd = "</mark>";
            string richStart = $"<mark={pc.HighlightColor.ToArgb():X8}>";
            FormatListInfo selectedFli = obj as FormatListInfo;
            string outp = "", colorOutp = "";
            saveable = true;
            foreach (FormatListInfo fli in FormatChunks.Cast<FormatListInfo>())
            {
                outp += selectedFli.Equals(fli) ? $"{richStart}{fli.GetDisplay()}{richEnd}" : fli.GetDisplay();
                colorOutp += selectedFli.Equals(fli) ? $"{richStart}{fli.GetColorDisplay()}{richEnd}" : fli.GetColorDisplay();
                saveable &= fli.Updatable();
            }
            //PreviewDisplay.text = saveable ? CurrentFormatInfo.Item4.Invoke(outp.Replace("\\n", "\n").Replace(richStart,$"{RICH_SHORT}mark{DELIMITER}{pc.HighlightColor.ToArgb():X8}{RICH_SHORT}").Replace(richEnd,""+RICH_SHORT)) : "Can not format.";
            PreviewDisplay.text = saveable ? CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")) : "Can not format.";
            RawPreviewDisplay.text = colorOutp;
            //Plugin.Log.Info("New Colored Formatted Text: " + RawPreviewDisplay.text);
            UpdateSaveButton();
        }
        [UIAction(nameof(AddDefaultChunk))]
        private void AddDefaultChunk()
        {
            FormatChunks.Add(FormatListInfo.DefaultVal);
            (FormatChunks[FormatChunks.Count - 1] as FormatListInfo).AboveInfo = FormatChunks[FormatChunks.Count - 2] as FormatListInfo;
            UpdateFormatTable(true);
        }
        [UIAction(nameof(ForceUpdatePreviewDisplay))]
        private void ForceUpdatePreviewDisplay() => UpdatePreviewDisplay(true); //dislike this function's need for existance.
      
        [UIAction(nameof(ScrollToTop))]
        private void ScrollToTop() => FormatEditor.TableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
        [UIAction(nameof(ScrollToBottom))]
        private void ScrollToBottom() => FormatEditor.TableView.ScrollToCellWithIdx(FormatChunks.Count - 1, TableView.ScrollPositionType.End, false);
        [UIAction(nameof(ParseCurrentFormat))]
        private void ParseCurrentFormat()
        {
            string currentFormat = CurrentFormatInfo.Format.Replace("\n", "\\n");
            FormatListInfo.InitStaticActions(CurrentFormatInfo.Alias, FormatChunks, () => UpdateFormatTable(), () => UpdatePreviewDisplay());
            foreach (KeyValuePair<string, char> item in GLOBAL_ALIASES) FormatListInfo.AliasConverter[item.Key] = item.Value;
            FormatChunks.Clear();
            FormatChunks.AddRange(FormatListInfo.InitAllFromChunks(FormatListInfo.ChunkItAll(currentFormat)).Cast<object>());
            //Plugin.Log.Info("THE CHUNKS\n" + string.Join("\n", FormatChunks));
            UpdateFormatTable(true);
            UpdateAliasTable();
        }
        [UIAction(nameof(SaveFormatToConfig))]
        private void SaveFormatToConfig()
        {
            Plugin.Log.Info("This was called");
            CurrentFormatInfo.Format = rawFormat;
            Type counter = _Counter.Equals(TheCounter.DisplayName) ? typeof(TheCounter) : Type.GetType(TheCounter.DisplayNameToCounter[_Counter]);
            if (counter == null) { Plugin.Log.Error("Counter is null"); return; }
            counter.GetMethods(BindingFlags.Public | BindingFlags.Static).First(a => a.Name.Equals("InitFormat")).Invoke(null, null);
            SaveMessage.text = "<color=green>Format saved successfully!</color>";
            TheSaveButton.interactable = false;
        }
        #endregion
        #region Value Editor
        private void UpdatePreviewForValue(bool forceUpdate = false)
        {
            if (!forceUpdate && !pc.UpdatePreview) return;
            Dictionary<char, object> newVals = ValueListInfo.GetNewTestVals(FormatValues.Cast<ValueListInfo>());
            ValuePreviewDisplay.text = CurrentFormatInfo.GetQuickFormat(newVals);
        }
        [UIAction(nameof(ParseValues))]
        private void ParseValues()
        {
            List<ValueListInfo> outp = new List<ValueListInfo>();
            Dictionary<char, object> testVals = CurrentFormatInfo.TestValues;
            IEnumerable<char> tokens = testVals.Keys;
            foreach (char token in tokens)
            {
                bool isWrapper = testVals[token].GetType().IsGenericType;
                Plugin.Log.Info("IsWrapper: " + isWrapper);
                outp.Add(new ValueListInfo(
                    isWrapper ? (testVals[token] as Func<object>).Invoke() : testVals[token],
                    token,
                    CurrentFormatInfo.GetName(token) ?? (token < 'a' ? "Option Number " + (int)token : ""),
                    isWrapper,
                    CurrentFormatInfo.GetTestValFormatter(token),
                    CurrentFormatInfo.GetExtraTestParams(token)));
            }
            FormatValues.Clear();
            FormatValues.AddRange(outp.Cast<object>());
            ValueEditor.TableView.ReloadData();
            UpdatePreviewForValue(true);
        }
        #endregion
        #endregion
        #endregion
        #endregion
    }
}
