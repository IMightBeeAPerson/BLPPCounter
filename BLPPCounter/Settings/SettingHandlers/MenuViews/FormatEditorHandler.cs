using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
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
using System.Collections;
using UnityEngine;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.Enums;

#if !NEW_VERSION
using BLPPCounter.Utils.Special_Utils;
#endif

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class FormatEditorHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0044, CS0649
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.MenuFormatSettings.bsml";
        public static FormatEditorHandler Instance { get; } = new FormatEditorHandler();
        private static PluginConfig PC => PluginConfig.Instance;
        private FormatEditorHandler() { }
        #region Misc Variables
        private FormatRelation CurrentFormatInfo => MenuSettingsHandler.AllFormatInfo[(_FormatName, _Counter)];
        private bool loaded = false;
        public bool IsLoaded => loaded;
        private string rawFormat;
        private bool saveable = false;
        private FormatListInfo lastSelectedInfo = null;
        private Table AliasTableTable;
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
        [UIComponent(nameof(AddAboveSelectedButton))]
        private Button AddAboveSelectedButton;
        [UIComponent(nameof(AddBelowSelectedButton))]
        private Button AddBelowSelectedButton;
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
        [UIValue(nameof(MenuHeight))]
        private int MenuHeight => SettingsHandler.MENU_HEIGHT;
        [UIValue(nameof(MenuAnchor))]
        private int MenuAnchor => SettingsHandler.MENU_ANCHOR;
        #region Main Menu
#if NEW_VERSION
        [UIValue(nameof(Counter))]
        private string Counter { get => _Counter; set { _Counter = value; UpdateFormatOptions(); } }
        private string _Counter;
        [UIValue(nameof(FormatName))]
        private string FormatName { get => _FormatName; set { _FormatName = value; UpdateFormatDisplay(); } } 
        private string _FormatName; // 1.37.0 and above
#else
        [UIValue(nameof(Counter))]
        private string Counter { get => _Counter ?? CounterNames[0] as string; set { _Counter = value; UpdateFormatOptions(); } }
        private string _Counter = null;
        [UIValue(nameof(FormatName))]
        private string FormatName { get => _FormatName ?? FormatNameContainer.Value as string; set { _FormatName = value; UpdateFormatDisplay(); } } 
        private string _FormatName = null; // 1.34.2 and below
#endif
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames[Leaderboards.Beatleader].Where(a => MenuSettingsHandler.AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
            .Append(TheCounter.DisplayName).Cast<object>().ToList();
#if NEW_VERSION
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames = new List<object>(); // 1.37.0 and above
#else
        private List<object> FormatNames => FormatNameContainer.List;
        private FilledList FormatNameContainer = new FilledList(); // 1.34.2 and below
#endif
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
#pragma warning disable IDE0051
        [UIAction("#back")] private void GoBack() => MenuSettingsHandler.Instance.GoBack();
#pragma warning restore IDE0051
        private void UpdateFormatOptions()
        {
#if NEW_VERSION
            FormatNames = MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList();
            //Plugin.Log.Info($"[{FormatNames.Aggregate("", (total, obj) => ", " + obj.ToString()).Substring(2)}]");
            ChooseFormat.Values = FormatNames; // 1.37.0 and above
#else
            FormatNameContainer = new FilledList(MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList());
            Plugin.Log.Info($"[{FormatNames.Aggregate("", (total, obj) => ", " + obj.ToString()).Substring(2)}]");
            ChooseFormat.values = FormatNames; // 1.34.2 and below
#endif
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        internal void UpdateFormatDisplay()
        {
            //Plugin.Log.Info($"Counter: {_Counter}, Format: {_FormatName}");
            FormatListInfo.AliasConverter = CurrentFormatInfo.Alias;
            //Plugin.Log.Info(HelpfulMisc.Print(CurrentFormatInfo.Alias));
            foreach (KeyValuePair<string, char> item in GLOBAL_ALIASES) FormatListInfo.AliasConverter[item.Key] = item.Value;
            FormattedText.text = CurrentFormatInfo.GetQuickFormat();
            //This is so that ui doesn't break from a specific error.
#if NEW_VERSION
            if (FormattedText.text.Contains("\nPossible")) FormattedText.text = FormattedText.text.Split("\nPossible")[0]; // 1.37.0 and above
#else
            if (FormattedText.text.Contains("\nPossible")) FormattedText.text = FormattedText.text.Split("\nPossible".ToCharArray())[0]; // 1.34.2 and below
#endif
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
#if NEW_VERSION
            FormatEditor.TableView.ReloadData(); // 1.37.0 and above
#else
            FormatEditor.tableView.ReloadData(); // 1.34.2 and below
#endif
            if (lastSelectedInfo != null)
            {
                lastSelectedInfo.Unselected();
                lastSelectedInfo = null;
            }
            AddAboveSelectedButton.interactable = false;
            AddBelowSelectedButton.interactable = false;
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
                //if (!fli.Updatable()) Plugin.Log.Info(fli.ToString() + "\nHas Child: " + fli.HasChild);
            }
            PreviewDisplay.text = saveable ? CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")) : "Can not format.";
            //if (!saveable) Plugin.Log.Info(CurrentFormatInfo.GetQuickFormat(outp.Replace("\\n", "\n")));
#if NEW_VERSION
            if (PreviewDisplay.text.Contains("\nPossible")) PreviewDisplay.text = PreviewDisplay.text.Split("\nPossible")[0]; // 1.37.0 and above
#else
            if (PreviewDisplay.text.Contains("\nPossible")) PreviewDisplay.text = PreviewDisplay.text.Split("\nPossible".ToCharArray())[0]; // 1.34.2 and below
#endif
            RawPreviewDisplay.text = colorOutp;
            rawFormat = outp.Replace("\\n", "\n");
#if NEW_VERSION
            FormatEditor.TableView.ClearSelection(); // 1.37.0 and above
#else
            FormatEditor.tableView.ClearSelection(); // 1.34.2 and below
#endif
            UpdateSaveButton();
        }
        private void UpdateSaveButton()
        {
            TheSaveButton.interactable = saveable;
            SaveMessage.text = "";
        }
        internal void GotoCell(int index)
        {
#if NEW_VERSION
            FormatEditor.TableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.Center, false);
#else
            FormatEditor.tableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.Center, false);
#endif
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
            selectedFli.Selected();
            lastSelectedInfo?.Unselected();
            lastSelectedInfo = selectedFli;
            AddAboveSelectedButton.interactable = true;
            AddBelowSelectedButton.interactable = true;
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
            //Plugin.Log.Debug(colorOutp);
            UpdateSaveButton();
        }
        [UIAction(nameof(AddBottomChunk))]
        private void AddBottomChunk()
        {
            FormatChunks.Add(FormatListInfo.DefaultVal);
            if (FormatChunks.Count >= 2)
                (FormatChunks[FormatChunks.Count - 1] as FormatListInfo).AboveInfo = FormatChunks[FormatChunks.Count - 2] as FormatListInfo;
            UpdateFormatTable(true);
        }
        [UIAction(nameof(AddTopChunk))]
        private void AddTopChunk()
        {
            FormatChunks.Insert(0, FormatListInfo.DefaultVal);
            if (FormatChunks.Count >= 2)
                (FormatChunks[1] as FormatListInfo).AboveInfo = FormatChunks[0] as FormatListInfo;
            UpdateFormatTable(true);
        }
        [UIAction(nameof(AddAboveSelected))]
        private void AddAboveSelected()
        { //lastSelectedInfo is already null checked before this is called, so no need to do it twice.
            int selectedIndex = FormatChunks.IndexOf(lastSelectedInfo);
            FormatChunks.Insert(selectedIndex, FormatListInfo.DefaultVal); //places given val ABOVE the val at the index.
            if (selectedIndex - 1 >= 0)
                (FormatChunks[selectedIndex] as FormatListInfo).AboveInfo = FormatChunks[selectedIndex - 1] as FormatListInfo;
            (FormatChunks[selectedIndex + 1] as FormatListInfo).AboveInfo = FormatChunks[selectedIndex] as FormatListInfo;
            UpdateFormatTable(true);
            GotoCell(selectedIndex);
        }
        [UIAction(nameof(AddBelowSelected))]
        private void AddBelowSelected()
        { //lastSelectedInfo is already null checked before this is called, so no need to do it twice.
            int selectedIndex = FormatChunks.IndexOf(lastSelectedInfo);
            FormatChunks.Insert(selectedIndex + 1, FormatListInfo.DefaultVal);
            if (FormatChunks.Count > selectedIndex + 1)
                (FormatChunks[selectedIndex + 1] as FormatListInfo).AboveInfo = FormatChunks[selectedIndex] as FormatListInfo;
            if (FormatChunks.Count > selectedIndex + 2)
                (FormatChunks[selectedIndex + 2] as FormatListInfo).AboveInfo = FormatChunks[selectedIndex + 1] as FormatListInfo;
            UpdateFormatTable(true);
            GotoCell(selectedIndex + 1);
        }
        [UIAction(nameof(ForceUpdatePreviewDisplay))]
        private void ForceUpdatePreviewDisplay() => UpdatePreviewDisplay(true); //dislike this function's need for existance.

        [UIAction(nameof(ScrollToTop))]
#if NEW_VERSION
        private void ScrollToTop() => FormatEditor.TableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false); // 1.37.0 and above
#else
        private void ScrollToTop() => FormatEditor.tableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false); // 1.34.2 and below
#endif
        [UIAction(nameof(ScrollToBottom))]
#if NEW_VERSION
        private void ScrollToBottom() => FormatEditor.TableView.ScrollToCellWithIdx(FormatChunks.Count - 1, TableView.ScrollPositionType.End, false); // 1.37.0 and above
#else
        private void ScrollToBottom() => FormatEditor.tableView.ScrollToCellWithIdx(FormatChunks.Count - 1, TableView.ScrollPositionType.End, false); // 1.34.2 and below
#endif
        [UIAction(nameof(ParseCurrentFormat))]
        private void ParseCurrentFormat()
        {
            string currentFormat = CurrentFormatInfo.Format.Replace("\n", "\\n");
            FormatChunks.Clear();
            FormatChunks.AddRange(FormatListInfo.InitAllFromChunks(FormatListInfo.ChunkItAll(currentFormat)).Cast<object>());
            lastSelectedInfo = null;
            AddAboveSelectedButton.interactable = false;
            AddBelowSelectedButton.interactable = false;
            //Plugin.Log.Info("THE CHUNKS\n" + string.Join("\n", FormatChunks));
            UpdateFormatTable(true);
            //MenuSettingsHandler.UpdateAliasTable(AliasTable, CurrentFormatInfo.Descriptions);
            Dictionary<char, string> reversedAlias = CurrentFormatInfo.Alias.Swap();
            AliasTableTable = new Table(AliasTable, CurrentFormatInfo.Descriptions.Select(kvp => new KeyValuePair<string, string>(reversedAlias[kvp.Key], kvp.Value)), "Tokens", "Descriptions");
            IEnumerator WaitThenUpdate()
            {
                yield return new WaitForEndOfFrame();
                AliasTableTable.UpdateTable();
            }
            CoroutineHost.Start(WaitThenUpdate());
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
            FormatWrapper testVals = CurrentFormatInfo.TestValues;
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
#if NEW_VERSION
            ValueEditor.TableView.ReloadData(); // 1.37.0 and above
#else
            ValueEditor.tableView.ReloadData(); // 1.34.2 and below
#endif
            UpdatePreviewForValue(true);
            ValueSaveButton.interactable = false;
        }
        #endregion
#endregion
    }
}
