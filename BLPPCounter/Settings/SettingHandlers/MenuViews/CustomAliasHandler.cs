using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Serializable_Classes;
using BLPPCounter.Utils.TokenParser;
using TMPro;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    internal sealed class CustomAliasHandler : BSMLResourceViewController
    {
#pragma warning disable IDE0051, IDE0044, CS0649

        private PluginConfig _config = null!;
        private IFormatRegistry _formatRegistry = null!;
        private MenuSettingsHandler _menuSettingsHandler = null!;
        private LazyInject<FormatEditorHandler> _formatEditorHandler = null!;

        private readonly List<object> _formatNames = [];
        private readonly List<object> _aliasNames = [];
        private readonly List<object> _aliasInfos = [];

        private bool _loaded;
        private string _counter = "";
        private string _formatName = "";
        private string _oldAliasName = "";

        private Table? _aliasTable;

        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.AliasSettings.bsml";

        [Inject]
        private void Construct(
            PluginConfig config,
            IFormatRegistry formatRegistry,
            MenuSettingsHandler menuSettingsHandler,
            LazyInject<FormatEditorHandler> formatEditorHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));

            _menuSettingsHandler = menuSettingsHandler ?? throw new ArgumentNullException(nameof(menuSettingsHandler));

            _formatEditorHandler = formatEditorHandler ?? throw new ArgumentNullException(nameof(formatEditorHandler));
        }

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

        [UIParams]
        private BSMLParserParams ParserParams = null!;

        [UIComponent(nameof(ChooseFormat))]
        private DropDownListSetting ChooseFormat = null!;

        [UIComponent(nameof(InfoTable))]
        private TextMeshProUGUI InfoTable = null!;

        [UIComponent(nameof(AliasEditor))]
        private CustomCellListTableData AliasEditor = null!;

        [UIComponent(nameof(CounterText))]
        private TextMeshProUGUI CounterText = null!;

        [UIComponent(nameof(FormatText))]
        private TextMeshProUGUI FormatText = null!;

        [UIComponent(nameof(AliasNamePicker))]
        private DropDownListSetting AliasNamePicker = null!;

        [UIComponent(nameof(NewAlias))]
        private StringSetting NewAlias = null!;

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
                    UpdateTable();
                }
            }
        }

        [UIValue(nameof(OldAliasName))]
        private string OldAliasName
        {
            get
            {
                if (BetterString.IsNullOrWhiteSpace(_oldAliasName) && _aliasNames.Count > 0)
                {
                    _oldAliasName = _aliasNames[0].ToString();
                }

                return _oldAliasName;
            }
            set => _oldAliasName = value;
        }

        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => [.. _formatRegistry.CounterNames.Cast<object>()];

        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames => _formatNames;

        [UIValue(nameof(AliasNames))]
        private List<object> AliasNames => _aliasNames;

        [UIValue(nameof(AliasInfos))]
        private List<object> AliasInfos => _aliasInfos;

        #endregion

        #region UI Actions

        [UIAction("#back")]
        private void GoBack()
        {
            _menuSettingsHandler.GoBack();
        }

        [UIAction(nameof(Load))]
        private void Load()
        {
            if (_loaded)
                return;

            _loaded = true;

            RebuildAliasInfoList();

            if (CounterNames.Count == 0)
            {
                Plugin.Log.Warn("The format registry contains no counters.");

                return;
            }

            Counter = CounterNames[0].ToString();

            ReloadAliasEditor();
        }

        [UIAction(nameof(AddAlias))]
        private void AddAlias()
        {
            string newAlias = NewAlias.Text;

            if (
                BetterString.IsNullOrWhiteSpace(newAlias)
                || BetterString.IsNullOrWhiteSpace(_oldAliasName)
                || BetterString.IsNullOrWhiteSpace(_counter)
                || BetterString.IsNullOrWhiteSpace(_formatName)
            )
            {
                return;
            }

            if (!CurrentFormatInfo.Alias.TryGetValue(_oldAliasName, out char aliasCharacter))
            {
                Plugin.Log.Warn($"Alias \"{_oldAliasName}\" does not " + "exist in the selected format.");

                return;
            }

            if (
                _config.TokenSettings.TokenAliases.Any(existing =>
                    string.Equals(existing.CounterName, _counter, StringComparison.Ordinal)
                    && string.Equals(existing.FormatName, _formatName, StringComparison.Ordinal)
                    && string.Equals(existing.AliasName, newAlias, StringComparison.Ordinal)
                )
            )
            {
                Plugin.Log.Warn($"Alias \"{newAlias}\" already exists " + "for the selected format.");

                return;
            }

            ParserParams.EmitEvent("CloseWindow");

            CustomAlias alias = new(_counter, _formatName, aliasCharacter, newAlias, _oldAliasName);

            _config.TokenSettings.TokenAliases.Add(alias);

            AliasListInfo info = CreateAliasInfo(alias);

            _aliasInfos.Add(info);

            _formatRegistry.ReapplyAliases();
            _formatRegistry.RebuildAll();

            NewAlias.Text = "";

            ReloadAliasEditor();
            UpdateTable();
            UpdateFormatEditorPreview();

            Plugin.Log.Info($"Added custom alias {alias}.");
        }

        [UIAction("#AddNewAlias")]
        private void AddNewAlias()
        {
            CounterText.SetText("<align=\"left\">Counter: " + "<align=\"right\">" + _counter);

            FormatText.SetText("<align=\"left\">Format: " + "<align=\"right\">" + _formatName);
        }

        #endregion

        #region Alias Operations

        private void RemoveAlias(AliasListInfo info)
        {
            if (info is null)
                return;

            info.RemoveRequested -= RemoveAlias;

            CustomAlias alias = info.Alias;

            if (_formatRegistry.TryGet(alias.FormatName, alias.CounterName, out FormatRelation? relation))
            {
                info.Unapply(relation);

                UpdateReferences(alias, relation, removing: true);
            }

            _config.TokenSettings.TokenAliases.Remove(alias);

            _aliasInfos.Remove(info);

            _formatRegistry.ReapplyAliases();
            _formatRegistry.RebuildAll();

            ReloadAliasEditor();

            if (
                string.Equals(alias.CounterName, _counter, StringComparison.Ordinal)
                && string.Equals(alias.FormatName, _formatName, StringComparison.Ordinal)
            )
            {
                UpdateTable();
            }

            UpdateFormatEditorPreview();
        }

        private void RebuildAliasInfoList()
        {
            foreach (AliasListInfo existing in _aliasInfos.OfType<AliasListInfo>())
            {
                existing.RemoveRequested -= RemoveAlias;
            }

            _aliasInfos.Clear();

            foreach (CustomAlias alias in _config.TokenSettings.TokenAliases)
            {
                _aliasInfos.Add(CreateAliasInfo(alias));
            }
        }

        private AliasListInfo CreateAliasInfo(CustomAlias alias)
        {
            AliasListInfo info = new(alias);

            info.RemoveRequested += RemoveAlias;

            return info;
        }

        private void UpdateReferences(CustomAlias alias, FormatRelation relation, bool removing)
        {
            if (!_config.AutoUpdateRefs)
                return;

            string oldAlias = removing ? alias.AliasName : alias.OldAlias;

            string newAlias = removing ? alias.OldAlias : alias.AliasName;

            string oldToken = $"{Tokens.ESCAPE_CHAR}" + $"{Tokens.ALIAS}" + $"{oldAlias}" + $"{Tokens.ALIAS}";

            string newToken = $"{Tokens.ESCAPE_CHAR}" + $"{Tokens.ALIAS}" + $"{newAlias}" + $"{Tokens.ALIAS}";

            if (relation.Format.Contains(oldToken))
            {
                relation.Format = relation.Format.Replace(oldToken, newToken);

                _formatRegistry.Rebuild(relation);
            }
        }

        #endregion

        #region Format Selection

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
                ClearTable();
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
            UpdateTable();
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

        private void UpdateTable()
        {
            if (
                BetterString.IsNullOrWhiteSpace(_counter)
                || BetterString.IsNullOrWhiteSpace(_formatName)
                || InfoTable is null
            )
            {
                return;
            }

            FormatRelation relation = CurrentFormatInfo;

            Dictionary<char, string> reversedAliases = relation.Alias.Swap();

            string[][] rows =
            [
                .. relation.Descriptions.Select(pair =>
                    new[]
                    {
                        reversedAliases.TryGetValue(pair.Key, out string? alias) ? alias : pair.Key.ToString(),
                        pair.Key.ToString(),
                        pair.Value,
                    }
                ),
            ];

            if (_aliasTable is null)
            {
                _aliasTable = new Table(InfoTable, rows, "Alias", "Token", "Description")
                {
                    MaxWidth = 200,
                    CenterText = false,
                };
            }
            else
            {
                _aliasTable.SetValues(rows);
            }

            _aliasTable.UpdateTable();

            _aliasNames.Clear();

            _aliasNames.AddRange(relation.Alias.Keys.Cast<object>());

            if (_aliasNames.Count == 0)
            {
                _oldAliasName = "";
                RefreshAliasChoices();
                return;
            }

            bool currentStillValid = _aliasNames.Any(value =>
                string.Equals(value.ToString(), _oldAliasName, StringComparison.Ordinal)
            );

            if (!currentStillValid)
            {
                _oldAliasName = _aliasNames[0].ToString();
            }

            RefreshAliasChoices();
        }

        private void ClearTable()
        {
            _aliasNames.Clear();
            _oldAliasName = "";

            _aliasTable?.SetValues([]);

            _aliasTable?.UpdateTable();

            RefreshAliasChoices();
        }

        private void RefreshAliasChoices()
        {
            if (AliasNamePicker is null)
                return;

            List<object> choices = _aliasNames.Count > 0 ? _aliasNames : [string.Empty];

#if NEW_VERSION
            AliasNamePicker.Values = choices;
#else
            AliasNamePicker.values = choices;
#endif

            AliasNamePicker.Value = _aliasNames.Count > 0 ? _oldAliasName : string.Empty;

            AliasNamePicker.UpdateChoices();
        }

        #endregion

        private void ReloadAliasEditor()
        {
            if (AliasEditor is null)
                return;

#if NEW_VERSION
            AliasEditor.Data = _aliasInfos;
            AliasEditor.TableView.ReloadData();
#else
            AliasEditor.data = _aliasInfos.Count > 0 ? _aliasInfos : [string.Empty];

            AliasEditor.tableView.ReloadData();
#endif
        }

        private void UpdateFormatEditorPreview()
        {
            FormatEditorHandler editor = _formatEditorHandler.Value;

            if (editor.IsLoaded)
                editor.UpdateFormatDisplay();
        }
    }
}
