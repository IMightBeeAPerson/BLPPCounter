using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.TokenParser;
using HMUI;
using TMPro;
using UnityEngine.UI;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    [ViewDefinition("BLPPCounter.Settings.BSML.MenuComponents.MenuFormatSettings.bsml")]
    [HotReload(RelativePathToLayout = @"..\..\BSML\MenuComponents\MenuFormatSettings.bsml")]
    internal sealed class FormatEditorHandler : BSMLSafeAutomaticViewController
    {
#pragma warning disable IDE0044, CS0649, IDE0051

        private PluginConfig _config = null!;
        private IFormatRegistry _formatRegistry = null!;
        private MenuSettingsHandler _menuSettingsHandler = null!;

        private bool _loaded;
        private bool _saveable;
        private string? _rawFormat;
        private string _counter = "";
        private string _formatName = "";

        private FormatListInfo? _lastSelectedInfo;
        private Table? _aliasTable;

        private readonly List<object> _formatNames = [];
        private readonly List<object> _formatChunks = [];
        private readonly List<object> _formatValues = [];

        [Inject]
        private void Construct(
            PluginConfig config,
            IFormatRegistry formatRegistry,
            MenuSettingsHandler menuSettingsHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));

            _menuSettingsHandler = menuSettingsHandler ?? throw new ArgumentNullException(nameof(menuSettingsHandler));
        }

        public bool IsLoaded => _loaded;

        private FormatRelation CurrentFormatInfo
        {
            get
            {
                if (BetterString.IsNullOrWhiteSpace(_counter) || BetterString.IsNullOrWhiteSpace(_formatName))
                {
                    throw new InvalidOperationException("No format relation is currently selected.");
                }

                return _formatRegistry.Get(_formatName, _counter);
            }
        }

        #region UI Components

        [UIComponent(nameof(ChooseFormat))]
        private DropDownListSetting ChooseFormat = null!;

        [UIComponent(nameof(RawFormatText))]
        private TextMeshProUGUI RawFormatText = null!;

        [UIComponent(nameof(FormattedText))]
        private TextMeshProUGUI FormattedText = null!;

        [UIComponent(nameof(FormatEditor))]
        private CustomCellListTableData FormatEditor = null!;

        [UIComponent(nameof(RawPreviewDisplay))]
        private TextMeshProUGUI RawPreviewDisplay = null!;

        [UIComponent(nameof(PreviewDisplay))]
        private TextMeshProUGUI PreviewDisplay = null!;

        [UIComponent(nameof(TheSaveButton))]
        private Button TheSaveButton = null!;

        [UIComponent(nameof(SaveMessage))]
        private TextMeshProUGUI SaveMessage = null!;

        [UIComponent(nameof(AliasTable))]
        private TextMeshProUGUI AliasTable = null!;

        [UIComponent(nameof(AddAboveSelectedButton))]
        private Button AddAboveSelectedButton = null!;

        [UIComponent(nameof(AddBelowSelectedButton))]
        private Button AddBelowSelectedButton = null!;

        [UIComponent(nameof(ValueEditor))]
        private CustomCellListTableData ValueEditor = null!;

        [UIComponent(nameof(ValuePreviewDisplay))]
        private TextMeshProUGUI ValuePreviewDisplay = null!;

        [UIComponent(nameof(ValueSaveButton))]
        private Button ValueSaveButton = null!;

        #endregion

        #region UI Values

        [UIValue(nameof(MenuHeight))]
        private int MenuHeight => SettingsHandler.MENU_HEIGHT;

        [UIValue(nameof(MenuAnchor))]
        private int MenuAnchor => SettingsHandler.MENU_ANCHOR;

        [UIValue(nameof(Counter))]
        private string Counter
        {
            get
            {
                if (BetterString.IsNullOrWhiteSpace(_counter) && CounterNames.Count > 0)
                {
                    _counter = CounterNames[0].ToString();
                }

                return _counter;
            }
            set
            {
                if (string.Equals(_counter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _counter = value;
                UpdateFormatOptions();
            }
        }

        [UIValue(nameof(FormatName))]
        private string FormatName
        {
            get
            {
                if (BetterString.IsNullOrWhiteSpace(_formatName) && _formatNames.Count > 0)
                {
                    _formatName = _formatNames[0].ToString();
                }

                return _formatName;
            }
            set
            {
                if (string.Equals(_formatName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _formatName = value;

                if (!BetterString.IsNullOrWhiteSpace(_formatName))
                {
                    UpdateFormatDisplay();
                }
            }
        }

        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => [.. _formatRegistry.CounterNames.Cast<object>()];

        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames => _formatNames;

        [UIValue(nameof(FormatChunks))]
        public List<object> FormatChunks => _formatChunks;

        [UIValue(nameof(FormatValues))]
        private List<object> FormatValues => _formatValues;

        #endregion

        #region Main Menu

        [UIAction("#back")]
        private void GoBack()
        {
            _menuSettingsHandler.GoBack();
        }

        private void UpdateFormatOptions()
        {
            _formatNames.Clear();

            if (BetterString.IsNullOrWhiteSpace(_counter))
            {
                RefreshFormatChoices();
                return;
            }

            _formatNames.AddRange(
                _formatRegistry.GetForCounter(_counter).Select(relation => (object)relation.GetKey.FormatName)
            );

            if (_formatNames.Count == 0)
            {
                _formatName = "";
                RefreshFormatChoices();
                return;
            }

            bool currentStillValid = _formatNames.Any(value =>
                string.Equals(value.ToString(), _formatName, StringComparison.Ordinal)
            );

            if (!currentStillValid)
            {
                _formatName = _formatNames[0].ToString();
            }

            RefreshFormatChoices();
            UpdateFormatDisplay();
        }

        private void RefreshFormatChoices()
        {
            if (ChooseFormat is null)
                return;

            List<object> choices = _formatNames.Count > 0 ? _formatNames : [string.Empty];

#if NEW_VERSION
            ChooseFormat.Values = choices;
#else
            ChooseFormat.values = choices;
#endif

            ChooseFormat.Value = _formatNames.Count > 0 ? _formatName : string.Empty;

            ChooseFormat.UpdateChoices();
        }

        internal void UpdateFormatDisplay()
        {
            if (
                BetterString.IsNullOrWhiteSpace(_counter)
                || BetterString.IsNullOrWhiteSpace(_formatName)
                || FormattedText is null
                || RawFormatText is null
            )
            {
                return;
            }

            FormatRelation relation = CurrentFormatInfo;

            Dictionary<string, char> aliases = [with(relation.Alias)];

            foreach (KeyValuePair<string, char> alias in Tokens.GlobalAliases)
            {
                aliases[alias.Key] = alias.Value;
            }

            FormatListInfo.AliasConverter = aliases;

            FormattedText.text = RemovePossibleErrorText(relation.GetQuickFormat());

            RawFormatText.text = FormatListInfo.ColorFormat(relation.Format.Replace("\n", "\\n"));
        }

        internal void LoadMenu()
        {
            if (_loaded)
                return;

            _loaded = true;

            FormatListInfo.InitStaticActions(_formatChunks, () => UpdateFormatTable(), () => UpdatePreviewDisplay());

            if (CounterNames.Count == 0)
            {
                Plugin.Log.Warn("The format registry contains no counters.");

                return;
            }

            Counter = CounterNames[0].ToString();
        }

        #endregion

        #region Format Editor

        private void UpdateFormatTable(bool forceUpdate = false)
        {
            if (!forceUpdate && !_config.UpdatePreview)
            {
                return;
            }

            if (FormatEditor is null)
                return;

#if NEW_VERSION
            FormatEditor.TableView.ReloadData();
#else
            FormatEditor.tableView.ReloadData();
#endif

            _lastSelectedInfo?.ResetBackgroundColor();

            _lastSelectedInfo = null;

            AddAboveSelectedButton.interactable = false;

            AddBelowSelectedButton.interactable = false;

            UpdatePreviewDisplay(true);
        }

        public void UpdatePreviewDisplay(bool forceUpdate = false)
        {
            if (!forceUpdate && !_config.UpdatePreview)
            {
                return;
            }

            if (PreviewDisplay is null || RawPreviewDisplay is null || TheSaveButton is null || SaveMessage is null)
            {
                return;
            }

            string output = "";
            string coloredOutput = "";

            _saveable = true;

            _lastSelectedInfo?.ResetBackgroundColor();

            _lastSelectedInfo = null;

            AddAboveSelectedButton.interactable = false;

            AddBelowSelectedButton.interactable = false;

            List<string> errors = [];

            foreach (FormatListInfo item in _formatChunks.Cast<FormatListInfo>())
            {
                output += item.GetDisplay();

                coloredOutput += item.GetColorDisplay();

                bool valid = item.Updatable(out string error);

                _saveable &= valid;

                if (!valid)
                {
                    errors.Add($"<color=#F00>{item.Chunk}</color>\n" + error);
                }
            }

            PreviewDisplay.text = _saveable
                ? RemovePossibleErrorText(CurrentFormatInfo.GetQuickFormat(output.Replace("\\n", "\n")))
                : string.Join("\n", errors);

            RawPreviewDisplay.text = coloredOutput;

            _rawFormat = output.Replace("\\n", "\n");

#if NEW_VERSION
            FormatEditor.TableView.ClearSelection();
#else
            FormatEditor.tableView.ClearSelection();
#endif

            UpdateSaveButton();
        }

        private void UpdateSaveButton()
        {
            TheSaveButton.interactable = _saveable;

            SaveMessage.text = "";
        }

        internal void GotoCell(int index)
        {
            if (index < 0 || _formatChunks.Count == 0)
            {
                return;
            }

            index = Math.Min(index, _formatChunks.Count - 1);

#if NEW_VERSION
            FormatEditor.TableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.Center, false);
#else
            FormatEditor.tableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.Center, false);
#endif
        }

        [UIAction(nameof(SelectedCell))]
        private void SelectedCell(TableView _, object selectedObject)
        {
            const string richEnd = "</mark>";

            string richStart = $"<mark={HelpfulMisc.ConvertColorToHex(_config.HighlightColor)}>";

            string secondaryRichStart = $"<mark={HelpfulMisc.ConvertColorToHex(_config.SecondHighlightColor)}>";

            FormatListInfo selected = (FormatListInfo)selectedObject;

            FormatListInfo? end =
                ((FormatListInfo.ChunkType.Group_Open | FormatListInfo.ChunkType.Capture_Open) & selected.Chunk) > 0
                    ? selected.Child
                    : selected;

            string output = "";
            string coloredOutput = "";

            _saveable = true;

            _lastSelectedInfo?.ResetBackgroundColor();

            _lastSelectedInfo = selected;

            AddAboveSelectedButton.interactable = true;

            AddBelowSelectedButton.interactable = true;

            List<string> errors = [];

            foreach (FormatListInfo item in _formatChunks.Cast<FormatListInfo>())
            {
                bool valid = item.Updatable(out string error);

                _saveable &= valid;

                if (!valid)
                {
                    errors.Add($"<color=#F00>{item.Chunk}</color>\n" + error);
                }

                if (ReferenceEquals(selected, item))
                {
                    coloredOutput += richStart + item.GetColorDisplay() + richEnd;

                    if (!ReferenceEquals(selected, end))
                    {
                        output += item.GetDisplay() + secondaryRichStart;

                        coloredOutput += secondaryRichStart;
                    }
                    else
                    {
                        output += richStart + item.GetDisplay() + richEnd;
                    }

                    continue;
                }

                coloredOutput += item.GetColorDisplay();

                if (ReferenceEquals(item, end))
                {
                    output += richEnd;
                    coloredOutput += richEnd;
                }

                output += item.GetDisplay();
            }

            selected.Selected();

            if (end is null)
                coloredOutput += richEnd;

            PreviewDisplay.text = _saveable
                ? RemovePossibleErrorText(CurrentFormatInfo.GetQuickFormat(output.Replace("\\n", "\n")))
                : string.Join("\n", errors);

            RawPreviewDisplay.text = coloredOutput;

            UpdateSaveButton();
        }

        [UIAction(nameof(AddBottomChunk))]
        private void AddBottomChunk()
        {
            FormatListInfo newItem = FormatListInfo.DefaultVal;

            if (_formatChunks.LastOrDefault() is FormatListInfo previous)
            {
                newItem.AboveInfo = previous;
            }

            _formatChunks.Add(newItem);
            UpdateFormatTable(true);
        }

        [UIAction(nameof(AddTopChunk))]
        private void AddTopChunk()
        {
            FormatListInfo newItem = FormatListInfo.DefaultVal;

            _formatChunks.Insert(0, newItem);

            if (_formatChunks.Count > 1 && _formatChunks[1] is FormatListInfo next)
            {
                next.AboveInfo = newItem;
            }

            UpdateFormatTable(true);
        }

        [UIAction(nameof(AddAboveSelected))]
        private void AddAboveSelected()
        {
            if (_lastSelectedInfo is null)
                return;

            int selectedIndex = _formatChunks.IndexOf(_lastSelectedInfo);

            if (selectedIndex < 0)
                return;

            FormatListInfo newItem = FormatListInfo.DefaultVal;

            if (selectedIndex > 0 && _formatChunks[selectedIndex - 1] is FormatListInfo previous)
            {
                newItem.AboveInfo = previous;
            }

            _lastSelectedInfo.AboveInfo = newItem;

            _formatChunks.Insert(selectedIndex, newItem);

            UpdateFormatTable(true);
            GotoCell(selectedIndex);
        }

        [UIAction(nameof(AddBelowSelected))]
        private void AddBelowSelected()
        {
            if (_lastSelectedInfo is null)
                return;

            int selectedIndex = _formatChunks.IndexOf(_lastSelectedInfo);

            if (selectedIndex < 0)
                return;

            FormatListInfo newItem = FormatListInfo.DefaultVal;

            newItem.AboveInfo = _lastSelectedInfo;

            _formatChunks.Insert(selectedIndex + 1, newItem);

            if (selectedIndex + 2 < _formatChunks.Count && _formatChunks[selectedIndex + 2] is FormatListInfo next)
            {
                next.AboveInfo = newItem;
            }

            UpdateFormatTable(true);
            GotoCell(selectedIndex + 1);
        }

        [UIAction(nameof(ForceUpdatePreviewDisplay))]
        private void ForceUpdatePreviewDisplay()
        {
            UpdatePreviewDisplay(true);
        }

        [UIAction(nameof(ScrollToTop))]
        private void ScrollToTop()
        {
            if (_formatChunks.Count == 0)
                return;

#if NEW_VERSION
            FormatEditor.TableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
#else
            FormatEditor.tableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
#endif
        }

        [UIAction(nameof(ScrollToBottom))]
        private void ScrollToBottom()
        {
            if (_formatChunks.Count == 0)
                return;

            int index = _formatChunks.Count - 1;

#if NEW_VERSION
            FormatEditor.TableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.End, false);
#else
            FormatEditor.tableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.End, false);
#endif
        }

        [UIAction(nameof(ParseCurrentFormat))]
        private void ParseCurrentFormat()
        {
            string currentFormat = CurrentFormatInfo.Format.Replace("\n", "\\n");

            _formatChunks.Clear();

            _formatChunks.AddRange(
                FormatListInfo.InitAllFromChunks(FormatListInfo.ChunkItAll(currentFormat)).Cast<object>()
            );

            _lastSelectedInfo = null;

            AddAboveSelectedButton.interactable = false;

            AddBelowSelectedButton.interactable = false;

            UpdateFormatTable(true);

            Dictionary<char, string> reversedAliases = CurrentFormatInfo.Alias.Swap();

            _aliasTable = new Table(
                AliasTable,
                CurrentFormatInfo.Descriptions.Select(pair => new KeyValuePair<string, string>(
                    reversedAliases.TryGetValue(pair.Key, out string? alias) ? alias : pair.Key.ToString(),
                    pair.Value
                )),
                "Tokens",
                "Descriptions"
            );

            Dispatcher.EnqueueAction(() => _aliasTable.UpdateTable());
        }

        [UIAction(nameof(SaveFormatToConfig))]
        private void SaveFormatToConfig()
        {
            if (!_saveable || _rawFormat is null)
            {
                SaveMessage.text = "<color=red>No valid format is available.</color>";

                return;
            }

            CurrentFormatInfo.Format = _rawFormat;

            _formatRegistry.Rebuild(CurrentFormatInfo);

            SaveMessage.text = "<color=green>Format saved successfully!</color>";

            TheSaveButton.interactable = false;

            UpdateFormatDisplay();
        }

        #endregion

        #region Value Editor

        private void UpdatePreviewForValue(bool forceUpdate = false)
        {
            if (!forceUpdate && !_config.UpdatePreview)
            {
                return;
            }

            ValuePreviewDisplay.text = CurrentFormatInfo.GetQuickFormat(
                ValueListInfo.GetNewTestValues(_formatValues.Cast<ValueListInfo>())
            );

            ValueSaveButton.interactable = true;
        }

        [UIAction(nameof(ForceUpdateValuePreviewDisplay))]
        private void ForceUpdateValuePreviewDisplay()
        {
            UpdatePreviewForValue(true);
        }

        [UIAction(nameof(SaveValues))]
        private void SaveValues()
        {
            ValueListInfo.GetNewTestValues(
                _formatValues.Cast<ValueListInfo>(),
                formatted: false,
                CurrentFormatInfo.TestValues
            );

            ValueSaveButton.interactable = false;
        }

        [UIAction(nameof(ParseValues))]
        private void ParseValues()
        {
            List<ValueListInfo> values = [];

            FormatWrapper testValues = CurrentFormatInfo.TestValues;

            foreach (char token in testValues.Keys)
            {
                object rawValue = testValues[token];

                bool isWrapper = rawValue.GetType().IsGenericType;

                object value = isWrapper ? ((Func<object>)rawValue).Invoke() : rawValue;

                values.Add(
                    new ValueListInfo(
                        value,
                        token,
                        CurrentFormatInfo.GetName(token) ?? (token < 'a' ? "Option Number " + (int)token : ""),
                        isWrapper,
                        CurrentFormatInfo.GetTestValFormatter(token),
                        CurrentFormatInfo.GetExtraTestParams(token),
                        CurrentFormatInfo.GetValueType(token),
                        () => UpdatePreviewForValue()
                    )
                );
            }

            _formatValues.Clear();

            _formatValues.AddRange(values.Cast<object>());

#if NEW_VERSION
            ValueEditor.TableView.ReloadData();
#else
            ValueEditor.tableView.ReloadData();
#endif

            UpdatePreviewForValue(true);

            ValueSaveButton.interactable = false;
        }

        #endregion

        private static string RemovePossibleErrorText(string value)
        {
            const string separator = "\nPossible";

            int index = value.IndexOf(separator, StringComparison.Ordinal);

            return index < 0 ? value : value[..index];
        }
    }
}
