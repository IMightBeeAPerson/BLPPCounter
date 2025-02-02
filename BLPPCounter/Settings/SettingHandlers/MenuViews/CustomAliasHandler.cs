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
        }
        private string _OldAliasName;
        [UIValue(nameof(NewAliasName))] private string NewAliasName;
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames.Where(a => MenuSettingsHandler.AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
            .Append(TheCounter.DisplayName).Cast<object>().ToList();
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames => FormatNameContainer.List;
        private FilledList FormatNameContainer = new FilledList();
        [UIValue(nameof(AliasNames))]
        private List<object> AliasNames => AliasNameContainer.List;
        private FilledList AliasNameContainer = new FilledList();
        [UIValue(nameof(AliasInfos))]
        private List<object> AliasInfos => AliasInfoContainer.List;
        private readonly FilledList AliasInfoContainer = new FilledList(placeholder: new AliasListInfo(default));
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
                //AliasEditor.TableView.ReloadData(); // 1.37.0 and above
                AliasEditor.tableView.ReloadData(); // 1.34.2 and below
                PC.TokenSettings.TokenAliases.Remove(ali.Alias);
                UpdateTable();
                UpdateRefs(ali.Alias, true);
            };
            AliasInfos.AddRange(PC.TokenSettings.TokenAliases.Select(ca => new AliasListInfo(ca)));
            //if (AliasInfos.Count == 0) AliasInfos.Add(""); //this is done because BSML missed a null check. Only needed on 1.34.2 and below
            //AliasEditor.TableView.ReloadData(); // 1.37.0 and above
            AliasEditor.tableView.ReloadData(); // 1.34.2 and below
        }
        [UIAction(nameof(AddAlias))]
        private void AddAlias()
        {
            if (NewAlias.Text is null || NewAlias.Text.Length == 0) return;
            ParserParams.EmitEvent("CloseWindow");
            AliasListInfo ali = new AliasListInfo(new CustomAlias(_Counter, _FormatName, CurrentFormatInfo.Alias[_OldAliasName], NewAlias.Text, _OldAliasName));
            if (AliasInfos.Count == 1 && AliasInfos[0].Equals("")) AliasInfos.Remove(""); // 1.34.2 and below
            AliasInfos.Add(ali);
            ali.Apply(CurrentFormatInfo);
            //AliasEditor.TableView.ReloadData(); // 1.37.0 and above
            AliasEditor.tableView.ReloadData(); // 1.34.2 and below
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
            FormatNameContainer = new FilledList(MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList());
            //ChooseFormat.Values = FormatNames; // 1.37.0 and above
            ChooseFormat.values = FormatNames; // 1.34.2 and below
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        private void UpdateTable()
        {
            const string keys = "Alias Token Description";
            Dictionary<char, string> reversedAlias = CurrentFormatInfo.Alias.Swap();
            new Table(
                InfoTable,
                CurrentFormatInfo.Descriptions.Select(kvp => new string[3] { reversedAlias[kvp.Key], kvp.Key + "", kvp.Value }).ToArray(),
                keys.Split(' ')
                )
            {
                MaxWidth = 200,
                CenterText = false
            };
            AliasNameContainer = new FilledList(CurrentFormatInfo.Alias.Keys.Cast<object>().ToList());
            //AliasNamePicker.Values = AliasNames; // 1.37.0 and above
            AliasNamePicker.values = AliasNames; // 1.34.2 and below
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
            FormatEditorHandler.Instance.UpdateFormatDisplay();
        }
        #endregion
    }
}
