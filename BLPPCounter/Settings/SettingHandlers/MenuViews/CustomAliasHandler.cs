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

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class CustomAliasHandler: BSMLResourceViewController
    {
#pragma warning disable IDE0051, IDE0044
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.AliasSettings.bsml";
        public static CustomAliasHandler Instance { get; private set; } = new CustomAliasHandler();
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
            get => _Counter;
            set { _Counter = value; UpdateFormatOptions(); }
        }
        private string _Counter;
        [UIValue(nameof(FormatName))] private string FormatName
        {
            get => _FormatName;
            set { _FormatName = value; UpdateTable(); }
        }
        private string _FormatName;
        [UIValue(nameof(OldAliasName))] private string OldAliasName
        {
            get => _OldAliasName;
            set { _OldAliasName = value; }
        }
        private string _OldAliasName;
        [UIValue(nameof(NewAliasName))] private string NewAliasName;
        [UIValue(nameof(CounterNames))]
        private List<object> CounterNames => TheCounter.ValidDisplayNames.Where(a => MenuSettingsHandler.AllFormatInfo.Any(b => b.Key.Item2.Equals(a)))
            .Append(TheCounter.DisplayName).Cast<object>().ToList();
        [UIValue(nameof(FormatNames))]
        private List<object> FormatNames = new List<object>();
        [UIValue(nameof(AliasNames))]
        private List<object> AliasNames = new List<object>();
        [UIValue(nameof(AliasInfos))]
        private readonly List<object> AliasInfos = new List<object>();
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
                AliasEditor.TableView.ReloadData();
                PC.TokenSettings.TokenAliases.Remove(ali.Alias);
                UpdateTable();
                UpdateRefs(ali.Alias, true);
            };
            AliasInfos.AddRange(PC.TokenSettings.TokenAliases.Select(ca => new AliasListInfo(ca)));
            AliasEditor.TableView.ReloadData();
        }
        [UIAction(nameof(AddAlias))]
        private void AddAlias()
        {
            if (NewAlias.Text is null || NewAlias.Text.Length == 0) return;
            ParserParams.EmitEvent("CloseWindow");
            AliasListInfo ali = new AliasListInfo(new CustomAlias(_Counter, _FormatName, CurrentFormatInfo.Alias[_OldAliasName], NewAlias.Text, _OldAliasName));
            AliasInfos.Add(ali);
            ali.Apply(CurrentFormatInfo);
            AliasEditor.TableView.ReloadData();
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
            FormatNames = MenuSettingsHandler.AllFormatInfo.Where(pair => pair.Key.Item2.Equals(_Counter)).Select(pair => pair.Key.Item1).Cast<object>().ToList();
            ChooseFormat.Values = FormatNames;
            if (FormatNames.Count > 0) FormatName = FormatNames[0] as string;
            ChooseFormat.Value = FormatName;
            ChooseFormat.UpdateChoices();
        }
        private void UpdateTable()
        {
            const string keys = "Alias Token Description";
            Dictionary<char, string> reversedAlias = CurrentFormatInfo.Alias.Swap();
            HelpfulMisc.SetupTable(
                InfoTable,
                200,
                CurrentFormatInfo.Descriptions.Select(kvp => new string[3] { reversedAlias[kvp.Key], kvp.Key+"", kvp.Value }).ToArray(),
                false,
                false,
                keys.Split(' ')
                );
            AliasNames = CurrentFormatInfo.Alias.Keys.Cast<object>().ToList();
            AliasNamePicker.Values = AliasNames;
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
