using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using BLPPCounter.Settings.Configs;
using UnityEngine.UI;
using static BLPPCounter.Helpfuls.HelpfulFormatter;

namespace BLPPCounter.Settings.SettingHandlers
{
    public class FormatEditorHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0044
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.MenuFormatSettings.bsml";
        public static FormatEditorHandler Instance { get; private set; } = new FormatEditorHandler();
        private static PluginConfig PC => PluginConfig.Instance;
        private FormatEditorHandler() { }
        #region Format Settings Editor
        #region Misc Variables
        private FormatRelation CurrentFormatInfo => MenuSettingsHandler.AllFormatInfo[(_FormatName, _Counter)];
        private bool loaded = false;
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
        private Button TheSaveButton;
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
        [UIComponent(nameof(ValueSaveButton))]
        private Button ValueSaveButton;
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
        private List<object> CounterNames => TheCounter.ValidDisplayNames.Where(a => MenuSettingsHandler.AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
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
        [UIAction("#back")] private void GoBack() => MenuSettingsHandler.Instance.GoBack();
        private void UpdateFormatOptions()
        {
            FormatNames = MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList();
            ChooseFormat.Values = FormatNames;
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        internal void UpdateFormatDisplay()
        {
            FormatListInfo.AliasConverter = CurrentFormatInfo.Alias;
            foreach (KeyValuePair<string, char> item in GLOBAL_ALIASES) FormatListInfo.AliasConverter[item.Key] = item.Value;
            FormattedText.text = CurrentFormatInfo.GetQuickFormat();
            if (FormattedText.text.Contains("\nPossible")) FormattedText.text = FormattedText.text.Split("\nPossible")[0]; //This is so that ui doesn't break from a specific error.
            RawFormatText.text = FormatListInfo.ColorFormat(CurrentFormatInfo.Format.Replace("\n", "\\n"));
            //Plugin.Log.Debug(RawFormatText.text);
        }
        internal void LoadMenu()
        {
            if (loaded) return;
            loaded = true;
            ValueListInfo.UpdatePreview = () => UpdatePreviewForValue();
            FormatListInfo.InitStaticActions(FormatChunks, () => UpdateFormatTable(), () => UpdatePreviewDisplay());
            Counter = CounterNames[0] as string;
        }
        #endregion
        #region Format Editor
        private void UpdateFormatTable(bool forceUpdate = false)
        {
            if (!forceUpdate && !PC.UpdatePreview) return;
            FormatEditor.TableView.ReloadData();
            UpdatePreviewDisplay(true);
        }
        public void UpdatePreviewDisplay(bool forceUpdate = false)
        {
            if (!forceUpdate && !PC.UpdatePreview) return;
            string outp = "", colorOutp = "";
            saveable = true;
            foreach (FormatListInfo fli in FormatChunks.Cast<FormatListInfo>())
            {
                outp += fli.GetDisplay();
                colorOutp += fli.GetColorDisplay();
                saveable &= fli.Updatable();
            }
            PreviewDisplay.text = saveable ? CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")) : "Can not format.";
            if (PreviewDisplay.text.Contains("\nPossible")) PreviewDisplay.text = PreviewDisplay.text.Split("\nPossible")[0];
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
        
        [UIAction(nameof(SelectedCell))]
        private void SelectedCell(TableView _, object obj)
        {
            const string richEnd = "</mark>";
            string richStart = $"<mark={HelpfulMisc.ConvertColorToHex(PC.HighlightColor)}>",
                otherRichStart = $"<mark={HelpfulMisc.ConvertColorToHex(PC.SecondHighlightColor)}>";
            FormatListInfo selectedFli = obj as FormatListInfo;
            IEnumerable<FormatListInfo> arr = FormatChunks.Cast<FormatListInfo>();
            FormatListInfo endFli = (((FormatListInfo.ChunkType.Group_Open | FormatListInfo.ChunkType.Capture_Open) & selectedFli.Chunk) > 0)
                ? selectedFli.Child : selectedFli;
            string outp = "", colorOutp = "";
            saveable = true;
            foreach (FormatListInfo fli in arr)
            {
                saveable &= fli.Updatable();
                if (selectedFli.Equals(fli))
                {
                    colorOutp += richStart + fli.GetColorDisplay() + richEnd;
                    if (!selectedFli.Equals(endFli)) { outp += fli.GetDisplay() + otherRichStart; colorOutp += otherRichStart; }
                    else outp += richStart + fli.GetDisplay() + richEnd;
                }
                else
                {
                    colorOutp += fli.GetColorDisplay();
                    if (fli.Equals(endFli)) { outp += richEnd; colorOutp += richEnd; }
                    outp += fli.GetDisplay();
                }
            }
            if (endFli == null) colorOutp += richEnd; //if endFli is null, then this is not a saveable format, therefore outp doesn't need to be updated.
            PreviewDisplay.text = saveable ? CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")) : "Can not format.";
            RawPreviewDisplay.text = colorOutp;
            Plugin.Log.Debug(colorOutp);
            UpdateSaveButton();
        }
        [UIAction(nameof(AddDefaultChunk))]
        private void AddDefaultChunk()
        {
            FormatChunks.Add(FormatListInfo.DefaultVal);
            if (FormatChunks.Count >= 2)
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
            FormatChunks.Clear();
            FormatChunks.AddRange(FormatListInfo.InitAllFromChunks(FormatListInfo.ChunkItAll(currentFormat)).Cast<object>());
            //Plugin.Log.Info("THE CHUNKS\n" + string.Join("\n", FormatChunks));
            UpdateFormatTable(true);
            //MenuSettingsHandler.UpdateAliasTable(AliasTable, CurrentFormatInfo.Descriptions);
            Dictionary<char, string> reversedAlias = CurrentFormatInfo.Alias.Swap();
            HelpfulMisc.SetupTable(AliasTable, -1, CurrentFormatInfo.Descriptions.Select(kvp => new KeyValuePair<string, string>(reversedAlias[kvp.Key], kvp.Value)), "Tokens", "Descriptions");
        }
        [UIAction(nameof(SaveFormatToConfig))]
        private void SaveFormatToConfig()
        {
            CurrentFormatInfo.Format = rawFormat;
            Type counter = _Counter.Equals(TheCounter.DisplayName) ? typeof(TheCounter) : Type.GetType(TheCounter.DisplayNameToCounter[_Counter]);
            if (counter == null) { Plugin.Log.Error("Counter is null"); return; }
            MethodInfo[] miArr = counter.GetMethods(BindingFlags.Public | BindingFlags.Static);
            miArr.First(a => a.Name.Equals("ResetFormat")).Invoke(null, null);
            miArr.First(a => a.Name.Equals("InitFormat")).Invoke(null, null);
            SaveMessage.text = "<color=green>Format saved successfully!</color>";
            TheSaveButton.interactable = false;
            UpdateFormatDisplay();
        }
        #endregion
        #region Value Editor
        private void UpdatePreviewForValue(bool forceUpdate = false)
        {
            if (forceUpdate || PC.UpdatePreview)
                ValuePreviewDisplay.text = CurrentFormatInfo.GetQuickFormat(ValueListInfo.GetNewTestVals(FormatValues.Cast<ValueListInfo>()));
            ValueSaveButton.interactable = true;
        }
        [UIAction(nameof(ForceUpdateValuePreviewDisplay))]
        private void ForceUpdateValuePreviewDisplay() => UpdatePreviewForValue(true);
        [UIAction(nameof(SaveValues))]
        private void SaveValues()
        {
            ValueListInfo.GetNewTestVals(FormatValues.Cast<ValueListInfo>(), false, CurrentFormatInfo.TestValues);
            //Plugin.Log.Info(string.Join(", ", CurrentFormatInfo.TestValues));
            ValueSaveButton.interactable = false;
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
                outp.Add(new ValueListInfo(
                    isWrapper ? (testVals[token] as Func<object>).Invoke() : testVals[token],
                    token,
                    CurrentFormatInfo.GetName(token) ?? (token < 'a' ? "Option Number " + (int)token : ""),
                    isWrapper,
                    CurrentFormatInfo.GetTestValFormatter(token),
                    CurrentFormatInfo.GetExtraTestParams(token),
                    CurrentFormatInfo.GetValueType(token)));
                //Plugin.Log.Info(outp.Last().ToString());
            }
            FormatValues.Clear();
            FormatValues.AddRange(outp.Cast<object>());
            ValueEditor.TableView.ReloadData();
            UpdatePreviewForValue(true);
            ValueSaveButton.interactable = false;
        }
        #endregion
        #endregion
        #endregion
    }
}
