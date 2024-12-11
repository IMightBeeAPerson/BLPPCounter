using BeatSaberMarkupLanguage.Attributes;
using System.Collections.Generic;
using BLPPCounter.Helpfuls;
using System.Text.RegularExpressions;
using static BLPPCounter.Helpfuls.HelpfulFormatter;
using System;
using System.Linq;
using BeatSaberMarkupLanguage.Components.Settings;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System.ComponentModel;
using static BLPPCounter.Utils.FormatListInfo.ChunkType;
using static BLPPCounter.Helpfuls.HelpfulMisc;
using BLPPCounter.Settings.Configs;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace BLPPCounter.Utils
{
    public class FormatListInfo: INotifyPropertyChanged
    {
#pragma warning disable IDE0044, CS0649, CS0414
        #region Static Variables
        public static Dictionary<string, char> AliasConverter { get; internal set; }
        internal static Action<FormatListInfo, bool> MovePlacement;
        internal static Action<FormatListInfo> RemoveSelf;
        internal static Action UpdateParentView;

        private static readonly string AliasRegex = 
            "^(?:" + RegexAliasPattern.Replace($"(?={Regex.Escape("" + PARAM_CLOSE)})", $"{Regex.Escape("" + PARAM_CLOSE)}")
            + $"|({Regex.Escape($"{ESCAPE_CHAR}")}.))".Replace($"{Regex.Escape(""+GROUP_OPEN)}",""); //^(?:(&.|&'[^']+?')\((.+)\)|([&]'[^']+?')|(&.))
        private static readonly string RegularTextRegex = "^[^" + Regex.Escape(string.Join("", SPECIAL_CHARS)).Replace($"{GROUP_CLOSE}",$"\\{GROUP_CLOSE}") + "]+"; //^[^<>\[\]$&]+
        private static readonly string GroupRegex = $"^(?:{Regex.Escape(""+GROUP_OPEN)}(?:'.+?'|.)|{Regex.Escape(""+GROUP_CLOSE)})";//^(?:\[(?:'.+?'|.)|\])
        #endregion
        #region UI Variables
        [UIValue(nameof(TypesOfChunks))] private List<object> TypesOfChunks => Enum.GetNames(typeof(ChunkType)).Select(s => s.Replace('_', ' ')).Cast<object>().ToList();
        [UIValue(nameof(ChoiceOptions))] private List<object> ChoiceOptions = new List<object>();

        [UIValue(nameof(ChunkStr))] private string ChunkStr
        { 
            get => Chunk.ToString().Replace('_', ' ');
            set 
            { 
                Chunk = (ChunkType)Enum.Parse(typeof(ChunkType), value.Replace(' ', '_'));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chunk)));
                UpdateView();
            }
        }
        private ChunkType Chunk;
        [UIValue(nameof(IncrementVal))] private int IncrementVal
        {
            get { if (int.TryParse(Text2, out int outp)) return outp; else return 50; }
            set => Text2 = "" + value;
        }
        [UIValue(nameof(Text))] private string Text { get => _Text; set { _Text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); } }
        [UIValue(nameof(Text2))] private string Text2 { get => _Text2; set { _Text2 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text2))); } }

        [UIValue(nameof(ShowTextComp))] private bool ShowTextComp = true;
        [UIValue(nameof(ShowText2Comp))] private bool ShowText2Comp = false;
        [UIValue(nameof(ShowIncrement))] private bool ShowIncrement = false;
        [UIValue(nameof(ShowChoice))] private bool ShowChoice = false;
        [UIValue(nameof(TextCompLabel))] private string TextCompLabel = "Input Text";
        [UIValue(nameof(ChoiceText))] private string ChoiceText = "Choose Token";
        [UIValue(nameof(IncrementText))] private string IncrementText = "Capture ID";

        [UIComponent(nameof(TextComp))] private TextMeshProUGUI TextCompLabelObj;
        [UIComponent(nameof(TextComp))] private StringSetting TextComp;
        [UIComponent(nameof(Text2Comp))] private StringSetting Text2Comp;
        [UIComponent(nameof(Incrementer))] private IncrementSetting Incrementer;
        [UIComponent(nameof(Incrementer))] private TextMeshProUGUI IncrementerText;
        [UIObject(nameof(ChoiceContainer))] private GameObject ChoiceContainer;
        [UIComponent(nameof(Choicer))] private DropDownListSetting Choicer;

        #endregion
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        public FormatListInfo AboveInfo { get => _AboveInfo; set { _AboveInfo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AboveInfo))); } }
        public FormatListInfo _AboveInfo = null; //This is so that parameters can find their parent.
        private string[] TokenParams; //This will be accessed by other instances other this class.
        private string _Text, _Text2;
        private bool hasChild = false;
        #endregion
        #region Inits
        private FormatListInfo(bool isTokenValue, string name, string[] tokenParams)
        {
            PropertyChanged += DoSomethingOnPropertyChange;
            Chunk = isTokenValue ? Escaped_Token : Escaped_Character;
            Text = name;
            Text2 = default;
            ChoiceOptions = isTokenValue ? AliasConverter.Keys.Cast<object>().ToList() : SPECIAL_CHARS.Select(c => "" + c).Cast<object>().ToList();
            TokenParams = tokenParams;
            ShowTextComp = false;
            ShowChoice = true;
            if (!isTokenValue) ChoiceText = "Choose Escaped Character";
        }
        private FormatListInfo(bool isOpen, string token, ChunkType ct) 
        {
            PropertyChanged += DoSomethingOnPropertyChange;
            Chunk = ct;
            Text = token;
            Text2 = token;
            TokenParams = null;
            ShowTextComp = false;
            if (isOpen) if (ct == Capture_Open) ShowIncrement = true; else ShowChoice = true;
        }
        private FormatListInfo(bool isOpen, string richTextKey, string richTextValue)
        {
            PropertyChanged += DoSomethingOnPropertyChange;
            Chunk = isOpen ? Rich_Text_Open : Rich_Text_Close;
            Text = richTextKey;
            Text2 = richTextValue;
            TokenParams = null;
            if (isOpen)
            {
                ShowText2Comp = true;
                TextCompLabel = "Enter Key";
            }
            else ShowTextComp = false;
            
        }
        private FormatListInfo(string text, bool isInsertSelf)
        {
            PropertyChanged += DoSomethingOnPropertyChange;
            Chunk = isInsertSelf ? Insert_Group_Value : Regular_Text;
            Text = text;
            Text2 = default;
            TokenParams = null;
            if (isInsertSelf) ShowTextComp = false;
        }
        private FormatListInfo(string name, int index)
        {
            PropertyChanged += DoSomethingOnPropertyChange;
            Chunk = Parameter;
            Text = name;
            Text2 = $"{index + 1}"; //plus one because the average person doesn't use zero indexing and this number will be displayed.
            TokenParams = null;
            ShowTextComp = false;
            ShowChoice = true;
            ChoiceOptions = AliasConverter.Keys.Cast<object>().ToList();
            ShowIncrement = true;
            IncrementText = "Parameter Index";
        }
        #endregion
        #region Static Functions
        #region Inits
        public static FormatListInfo InitEscapedCharacter(bool isTokenValue, string name, params string[] tokenParams) => 
            new FormatListInfo(isTokenValue, name, tokenParams);
        public static FormatListInfo InitGroup(bool isOpen, string token) => 
            new FormatListInfo(isOpen, token, isOpen ? Group_Open : Group_Close);
        public static FormatListInfo InitInsertSelf() => new FormatListInfo("" + INSERT_SELF, true);
        public static FormatListInfo InitCapture(bool isOpen, string token) => 
            new FormatListInfo(isOpen, token, isOpen ? Capture_Open : Capture_Close);
        public static FormatListInfo InitRichText(bool isOpen, string richTextKey, string richTextValue) => 
            new FormatListInfo(isOpen, richTextKey, richTextValue);
        public static FormatListInfo InitRegularText(string text) => 
            new FormatListInfo(text, false);
        public static FormatListInfo InitParameter(string name, int index = 0) =>
            new FormatListInfo(name, index);
        #endregion
        #region Misc
        public static (string, ChunkType) ChunkItAll(string format)
        {

        }
        private static string ChunkItOnce(string format)
        {

        }
        internal static string GetRegexForChunk(ChunkType ct)
        {
            switch (ct)
            {
                case Regular_Text: return RegularTextRegex;
                default: return "";
            }
        }
        #endregion
        #endregion
        #region UI Functions
        [UIAction(nameof(Centerer))] private string Centerer(string strIn) => $"<align=\"center\">{strIn}";
        [UIAction(nameof(MoveChunkUp))] private void MoveChunkUp() => MovePlacement(this, true);
        [UIAction(nameof(MoveChunkDown))] private void MoveChunkDown() => MovePlacement(this, false);
        [UIAction(nameof(RemoveChunk))] private void RemoveChunk() => RemoveSelf(this);
        [UIAction(nameof(UpdateParentViewCaller))] private void UpdateParentViewCaller() => UpdateParentView();
        #endregion
        #region Functions
        public void SetParentToken() //For ChunkType.Parameter
        {
            if (Chunk != Parameter) return;
            if (!int.TryParse(_Text2, out int index)) { index = 1; _Text2 = "1"; }
            FormatListInfo parent = this;
            while (parent != null && parent.Chunk == Parameter) parent = parent.AboveInfo;
            index--;
            if (parent == null || parent.TokenParams == null || parent.TokenParams.Length >= index) return;
            parent.TokenParams[index] = _Text;
        }
        public void TellParentTheyHaveAChild(bool childIsDead = false)
        {
            if (((Capture_Close | Group_Close | Rich_Text_Close | Parameter) & Chunk) == 0) return;
            FormatListInfo parent = AboveInfo;
            ChunkType open = Chunk == Parameter ? Escaped_Token : (ChunkType)((int)Chunk / 2), close = Chunk == Parameter ? Regular_Text : Chunk;
            while (parent != null && ((open | close) & parent.Chunk) == 0) parent = parent.AboveInfo;
            if (parent != null && parent.Chunk == open) parent.hasChild = !childIsDead;
        }
        private void DoSomethingOnPropertyChange(object sender, PropertyChangedEventArgs e) 
        {
            if (e.PropertyName.Equals(nameof(Text))) SetParentToken();
            if (e.PropertyName.Equals(nameof(AboveInfo))) TellParentTheyHaveAChild();
        }
        private void UpdateView()
        {
            switch (Chunk)
            {
                case Rich_Text_Close:
                case Rich_Text_Open:
                    TextCompLabelObj.text = "Enter Key";
                    break;
                case Regular_Text:
                    TextCompLabelObj.text = "Input Text";
                    break;
                case Capture_Open:
                    TextCompLabelObj.text = "Enter Capture ID";
                    break;
                case Parameter:
                    IncrementerText.text = "Parameter Index";
                    ChoiceOptions = AliasConverter.Keys.Cast<object>().ToList();
                    break;
                case Group_Open:
                case Escaped_Token:
                    ChoiceOptions = AliasConverter.Keys.Cast<object>().ToList();
                    break;
                case Escaped_Character:
                    ChoiceOptions = SPECIAL_CHARS.Select(c => "" + c).Cast<object>().ToList();
                    break;
            }
            if (((Escaped_Token | Escaped_Character | Group_Open | Parameter) & Chunk) > 0)
            {
                Choicer.Values = ChoiceOptions;
                if (!Choicer.Values.Contains(Choicer.Value)) { Choicer.Value = Choicer.Values[0]; Text = Choicer.Values[0] as string; }
                Choicer.UpdateChoices();
                ChoiceContainer.SetActive(true);
            } else ChoiceContainer.SetActive(false);
            TextComp.gameObject.SetActive(((Regular_Text | Rich_Text_Open) & Chunk) > 0);
            Text2Comp.gameObject.SetActive(Rich_Text_Open == Chunk);
            Incrementer.gameObject.SetActive(((Capture_Open | Parameter) & Chunk) > 0);
            if (((Capture_Close | Group_Close | Rich_Text_Close | Parameter) & Chunk) != 0)
                TellParentTheyHaveAChild();
        }
        public bool Updatable()
        {
            FormatListInfo parent = AboveInfo;
            if (((Capture_Open | Group_Open | Rich_Text_Open | Escaped_Token) & Chunk) != 0) return hasChild || (Chunk == Escaped_Token && TokenParams == null);
            if (((Capture_Close | Group_Close | Rich_Text_Close | Parameter) & Chunk) == 0) return true;
            ChunkType open = (ChunkType)((int)Chunk / 2);
            if (Chunk == Group_Close) open |= Capture_Open | Capture_Close;
            ChunkType close = Chunk == Parameter ? Regular_Text : Chunk;
            while (parent != null && ((open | close) & parent.Chunk) == 0) parent = parent.AboveInfo;
            return parent != null && (parent.Chunk & open) != 0;
        }
        public string GetDisplay()
        {
            string outp;
            switch (Chunk)
            {
                case Regular_Text:
                    return Text;
                case Escaped_Character:
                    return $"{ESCAPE_CHAR}{Text}";
                case Escaped_Token:
                    outp = $"{ESCAPE_CHAR}{ALIAS}{Text}{ALIAS}";
                    if (TokenParams == null) return outp;
                    outp += PARAM_OPEN;
                    for (int i = 0; i < TokenParams.Length; i++) outp += (i != 0 ? "," : "") + $"{ALIAS}{TokenParams[i]}{ALIAS}";
                    return outp + PARAM_CLOSE;
                case Capture_Open:
                    return $"{CAPTURE_OPEN}{Text}";
                case Capture_Close:
                    return "" + CAPTURE_CLOSE;
                case Group_Open:
                    return $"{GROUP_OPEN}{ALIAS}{Text}{ALIAS}";
                case Group_Close:
                    return "" + GROUP_CLOSE;
                case Rich_Text_Open:
                    outp = $"{RICH_SHORT}{{0}}{DELIMITER}{Text2}{RICH_SHORT}";
                    return RICH_SHORTHANDS.ContainsValue(Text) ? string.Format(outp, RICH_SHORTHANDS.First(p => p.Value.Equals(Text)).Key) : string.Format(outp, Text);
                case Rich_Text_Close:
                    return "" + RICH_SHORT;
                case Insert_Group_Value:
                    return "" + INSERT_SELF;
                default: return "";
            }
        }
        public string GetColorDisplay()
        {
            PluginConfig pc = PluginConfig.Instance;
            string outp;
            switch (Chunk)
            {
                case Regular_Text:
                    return "<color=white>" + GetDisplay().Replace("\\n", $"{ConvertColorToMarkup(pc.SpecialCharacterColor)}\\n</color>");
                case Escaped_Character:
                    return $"{ColorSpecialChar(ESCAPE_CHAR)}{ConvertColorToMarkup(pc.AliasColor)}{Text}";
                case Escaped_Token:
                    outp = $"{ColorSpecialChar(ESCAPE_CHAR)}{ColorSpecialChar(ALIAS)}{ConvertColorToMarkup(pc.AliasColor)}{Text}{ColorSpecialChar(ALIAS)}";
                    if (TokenParams != null)
                    {
                        outp += ColorSpecialChar(PARAM_OPEN);
                        for (int i=0;i<TokenParams.Length;i++) 
                            outp += $"{(i != 0 ? ColorSpecialChar(DELIMITER) : "")}{ColorSpecialChar(ALIAS)}{ConvertColorToMarkup(pc.AliasColor)}{TokenParams[i]}{ColorSpecialChar(ALIAS)}"; 
                        outp += ColorSpecialChar(PARAM_CLOSE);
                    }
                    return outp;
                case Capture_Open:
                    return $"{ColorSpecialChar(CAPTURE_OPEN)}{ConvertColorToMarkup(pc.CaptureIdColor)}{Text2}";
                case Capture_Close:
                    return ColorSpecialChar(CAPTURE_CLOSE);
                case Group_Open:
                    return $"{ColorSpecialChar(GROUP_OPEN)}{ColorSpecialChar(ALIAS)}{ConvertColorToMarkup(pc.AliasColor)}{Text}{ColorSpecialChar(ALIAS)}";
                case Group_Close:
                    return ColorSpecialChar(GROUP_CLOSE);
                case Rich_Text_Open:
                    outp = $"{ColorSpecialChar(RICH_SHORT)}{{0}}{ColorSpecialChar(DELIMITER)}{ConvertColorToMarkup(pc.ParamVarColor)}{Text2}{ColorSpecialChar(RICH_SHORT)}";
                    return RICH_SHORTHANDS.ContainsValue(Text) ? string.Format(outp, RICH_SHORTHANDS.First(p => p.Value.Equals(Text)).Key) : string.Format(outp, Text);
                case Rich_Text_Close:
                    return ColorSpecialChar(RICH_SHORT);
                case Insert_Group_Value:
                    return ColorSpecialChar(INSERT_SELF);
                default: return GetDisplay();
            }
        }
        #endregion
        #region Overrides
        public override string ToString()
        {
            return $"{{Chunk: {Chunk}, Text: {Text}, Secondary Text: {Text2}, Token Params: [{(TokenParams != null ? string.Join(", ", TokenParams) : "")}]}}";
        }
        #endregion
        #region Inner Classes
        public enum ChunkType //Not using Flags attribute because this isn't a real bitmask. This is done simply to parse it easier.
        {
            Regular_Text = 0,
            Escaped_Character = 1, 
            Escaped_Token = 2,
            Parameter = 4,
            Capture_Open = 8, 
            Capture_Close = 16, 
            Group_Open = 32,
            Group_Close = 64,
            Rich_Text_Open = 128,
            Rich_Text_Close = 256,
            Insert_Group_Value = 512,
        }
        #endregion
    }
}
