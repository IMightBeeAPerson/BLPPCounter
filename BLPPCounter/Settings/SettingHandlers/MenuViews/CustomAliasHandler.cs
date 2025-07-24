using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using BLPPCounter.Utils.Special_Utils;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class CustomAliasHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0051, IDE0044
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.AliasSettings.bsml";
        public static CustomAliasHandler Instance { get; } = new CustomAliasHandler();
        private static PluginConfig PC => PluginConfig.Instance;
        private CustomAliasHandler() { }
        private bool loaded = false;
        private FormatRelation CurrentFormatInfo => MenuSettingsHandler.AllFormatInfo[(_FormatName, _Counter)];
        private Table AliasTable;
        #region UI Components
        [UIParams]
        private BSMLParserParams ParserParams;
        [UIComponent(nameof(ChooseFormat))]
        private DropDownListSetting ChooseFormat;
        [UIComponent(nameof(InfoTable))]
        private TextMeshProUGUI InfoTable;
        [UIComponent(nameof(AliasEditor))]
        private CustomCellListTableData AliasEditor;
        [UIComponent(nameof(CounterText))]
        private TextMeshProUGUI CounterText;
        [UIComponent(nameof(FormatText))]
        private TextMeshProUGUI FormatText;
        [UIComponent(nameof(AliasNamePicker))]
        private DropDownListSetting AliasNamePicker;
        [UIComponent(nameof(NewAlias))]
        private StringSetting NewAlias;
        #endregion
        #region UI Values
#if NEW_VERSION
        [UIValue(nameof(Counter))]
        private string Counter
        {
            get => _Counter;
            set { _Counter = value; UpdateFormatOptions(); }
        }
        private string _Counter = null;
        [UIValue(nameof(FormatName))] private string FormatName
        {
            get => _FormatName;
            set { _FormatName = value; UpdateTable(); }
        }
        private string _FormatName = null;
        [UIValue(nameof(OldAliasName))] private string OldAliasName
        {
            get => _OldAliasName;
            set { _OldAliasName = value; }
        } // 1.37.0 and above
#else
        [UIValue(nameof(Counter))]
        private string Counter
        {
            get => _Counter ?? CounterNames[0] as string;
            set { _Counter = value; UpdateFormatOptions(); }
        }
        private string _Counter = null;
        [UIValue(nameof(FormatName))] private string FormatName
        {
            get => _FormatName ?? FormatNameContainer.Value as string;
            set { _FormatName = value; UpdateTable(); }
        }
        private string _FormatName = null;
        [UIValue(nameof(OldAliasName))] private string OldAliasName
        {
            get => _OldAliasName ?? AliasNameContainer.Value as string;
            set { _OldAliasName = value; }
        } // 1.34.2 and below
#endif
        private string _OldAliasName;
        [UIValue(nameof(NewAliasName))] private string NewAliasName;
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames[Leaderboards.Beatleader].Where(a => MenuSettingsHandler.AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
            .Append(TheCounter.DisplayName).Cast<object>().ToList();
#if NEW_VERSION
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames = new List<object>();
        [UIValue(nameof(AliasNames))]
        private List<object> AliasNames = new List<object>();
        [UIValue(nameof(AliasInfos))]
        private List<object> AliasInfos = new List<object>(); // 1.37.0 and above
#else
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames => FormatNameContainer.List;
        private FilledList FormatNameContainer = new FilledList();
        [UIValue(nameof(AliasNames))]
        private List<object> AliasNames => AliasNameContainer.List;
        private FilledList AliasNameContainer = new FilledList();
        [UIValue(nameof(AliasInfos))]
        private List<object> AliasInfos => AliasInfoContainer.List;
        private readonly FilledList AliasInfoContainer = new FilledList(placeholder: new AliasListInfo(default)); // 1.34.2 and below
#endif
#endregion
#region UI Actions
        [UIAction("#back")] private void GoBack() => MenuSettingsHandler.Instance.GoBack();
        [UIAction(nameof(Load))]
        private void Load()
        {
            if (loaded) return;
            loaded = true;
            Counter = CounterNames[0] as string;
            AliasListInfo.RemoveSelf = ali =>
            {
                AliasInfos.Remove(ali);
                ali.Unapply(MenuSettingsHandler.AllFormatInfo);
#if NEW_VERSION
                AliasEditor.TableView.ReloadData(); // 1.37.0 and above

#else
                AliasEditor.tableView.ReloadData(); // 1.34.2 and below
#endif
                PC.TokenSettings.TokenAliases.Remove(ali.Alias);
                UpdateTable();
                UpdateRefs(ali.Alias, true);
            };
            AliasInfos.AddRange(PC.TokenSettings.TokenAliases.Select(ca => new AliasListInfo(ca)));
#if NEW_VERSION
            AliasEditor.TableView.ReloadData(); // 1.37.0 and above

#else
            if (AliasInfos.Count == 0) AliasInfos.Add(""); //this is done because BSML missed a null check. Only needed on 1.34.2 and below
            AliasEditor.tableView.ReloadData(); // 1.34.2 and below
#endif
        }
        [UIAction(nameof(AddAlias))]
        private void AddAlias()
        {
            if (NewAlias.Text is null || NewAlias.Text.Length == 0) return;
            ParserParams.EmitEvent("CloseWindow");
            AliasListInfo ali = new AliasListInfo(new CustomAlias(_Counter, _FormatName, CurrentFormatInfo.Alias[_OldAliasName], NewAlias.Text, _OldAliasName));
#if !NEW_VERSION
            if (AliasInfos.Count == 1 && AliasInfos[0].Equals("")) AliasInfos.Remove(""); // 1.34.2 and below
#endif
            AliasInfos.Add(ali);
            ali.Apply(CurrentFormatInfo);
#if NEW_VERSION
            AliasEditor.TableView.ReloadData(); // 1.37.0 and above
#else
            AliasEditor.tableView.ReloadData(); // 1.34.2 and below
#endif
            PC.TokenSettings.TokenAliases.Add(ali.Alias);
            Plugin.Log.Info(string.Join("\n", AliasInfos));
            UpdateTable();
            UpdateRefs(ali.Alias, false);
        }
        [UIAction("#AddNewAlias")]
        private void AddNewAlias()
        {
            CounterText.text = "<align=\"left\">Counter: <align=\"right\">" + _Counter;
            FormatText.text = "<align=\"left\">Format: <align=\"right\">" + _FormatName;
        }
#endregion
        #region Misc Functions
        private void UpdateFormatOptions()
        {
#if NEW_VERSION
            FormatNames = MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList();
            ChooseFormat.Values = FormatNames; // 1.37.0 and above
#else
            FormatNameContainer = new FilledList(MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList());
            ChooseFormat.values = FormatNames; // 1.34.2 and below
#endif
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        private void UpdateTable()
        {
            const string keys = "Alias Token Description";
            Dictionary<char, string> reversedAlias = CurrentFormatInfo.Alias.Swap();
            if (AliasTable is null)
                AliasTable = new Table(
                    InfoTable,
                    CurrentFormatInfo.Descriptions.Select(kvp => new string[3] { reversedAlias[kvp.Key], kvp.Key + "", kvp.Value }).ToArray(),
                    keys.Split(' ')
                    )
                {
                    MaxWidth = 200,
                    CenterText = false
                };
            else AliasTable.SetValues(CurrentFormatInfo.Descriptions.Select(kvp => new string[3] { reversedAlias[kvp.Key], kvp.Key + "", kvp.Value }).ToArray());
            AliasTable.UpdateTable();
#if NEW_VERSION
            AliasNames = CurrentFormatInfo.Alias.Keys.Cast<object>().ToList();
            AliasNamePicker.Values = AliasNames; // 1.37.0 and above
#else
            AliasNameContainer = new FilledList(CurrentFormatInfo.Alias.Keys.Cast<object>().ToList());
            AliasNamePicker.values = AliasNames; // 1.34.2 and below
#endif
            OldAliasName = AliasNames[0] as string;
            AliasNamePicker.UpdateChoices();
        }
        private void UpdateRefs(CustomAlias ca, bool removing)
        {
            if (!PC.AutoUpdateRefs) return;
            string toReplace = $"{HelpfulFormatter.ESCAPE_CHAR}{HelpfulFormatter.ALIAS}{(removing ? ca.AliasName : ca.OldAlias)}{HelpfulFormatter.ALIAS}";
            if (CurrentFormatInfo.Format.Contains(toReplace))
                CurrentFormatInfo.Format = CurrentFormatInfo.Format.Replace(toReplace,
                    $"{HelpfulFormatter.ESCAPE_CHAR}{HelpfulFormatter.ALIAS}{(removing ? ca.OldAlias : ca.AliasName)}{HelpfulFormatter.ALIAS}");
            if (FormatEditorHandler.Instance.IsLoaded)
                FormatEditorHandler.Instance.UpdateFormatDisplay();
        }
        #endregion
    }
}
