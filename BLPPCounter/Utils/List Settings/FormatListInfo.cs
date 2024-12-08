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

namespace BLPPCounter.Utils
{
    public class FormatListInfo
    {
#pragma warning disable IDE0044, CS0649, CS0414
        public static Dictionary<string, char> AliasConverter { get; internal set; }
        internal static Action<FormatListInfo, bool> MovePlacement;
        internal static Action<FormatListInfo> RemoveSelf;

        internal static readonly string AliasRegex = 
            "^(?:" + RegexAliasPattern.Replace($"(?={Regex.Escape("" + PARAM_CLOSE)})", $"{Regex.Escape("" + PARAM_CLOSE)}")
            + $"|({Regex.Escape($"{ESCAPE_CHAR}")}.))".Replace($"{Regex.Escape(""+GROUP_OPEN)}",""); //^(?:(&.|&'[^']+?')\((.+)\)|([&]'[^']+?')|(&.))
        internal static readonly string RegularTextRegex = "^[^" + Regex.Escape(string.Join("", SPECIAL_CHARS)).Replace($"{GROUP_CLOSE}",$"\\{GROUP_CLOSE}") + "]+"; //^[^<>\[\]$&]+
        internal static readonly string GroupRegex = $"^(?:{Regex.Escape(""+GROUP_OPEN)}(?:'.+?'|.)|{Regex.Escape(""+GROUP_CLOSE)})";//^(?:\[(?:'.+?'|.)|\])

        [UIValue(nameof(TypesOfChunks))] private List<object> TypesOfChunks => Enum.GetNames(typeof(ChunkType)).Select(s => s.Replace('_', ' ')).Cast<object>().ToList();
        [UIValue(nameof(ChoiceOptions))] private List<object> ChoiceOptions =>
            Toggle ? AliasConverter.Keys.Cast<object>().ToList() : SPECIAL_CHARS.Select(c => "" + c).Cast<object>().ToList();

        [UIValue(nameof(ChunkStr))] private string ChunkStr
        { 
            get => Chunk.ToString().Replace('_', ' ');
            set 
            { 
                Chunk = (ChunkType)Enum.Parse(typeof(ChunkType), value.Replace(' ', '_'));
                UpdateView();
            }
        }
        private ChunkType Chunk;
        [UIValue(nameof(CaptureID))] private int CaptureID
        {
            get { if (int.TryParse(Text, out int outp)) return outp; else return 50; }
            set => Text = "" + value;
        }
        [UIValue(nameof(Toggle))] private bool Toggle;
        [UIValue(nameof(Text))] private string Text;
        [UIValue(nameof(Text2))] private string Text2;
        [UIValue(nameof(TokenParams))] private string[] TokenParams;

        [UIValue(nameof(ShowTextComp))] private bool ShowTextComp = true;
        [UIValue(nameof(ShowText2Comp))] private bool ShowText2Comp = false;
        [UIValue(nameof(ShowIncrement))] private bool ShowIncrement = false;
        [UIValue(nameof(ShowChoice))] private bool ShowChoice = false;
        [UIValue(nameof(TextCompLabel))] private string TextCompLabel = "Input Text";
        [UIValue(nameof(ChoiceText))] private string ChoiceText = "Choose Token";

        [UIComponent(nameof(TextComp))] private TextMeshProUGUI TextCompLabelObj;
        [UIComponent(nameof(TextComp))] private StringSetting TextComp;
        [UIComponent(nameof(Text2Comp))] private StringSetting Text2Comp;
        [UIComponent(nameof(Incrementer))] private IncrementSetting Incrementer;
        [UIObject(nameof(ChoiceContainer))] private GameObject ChoiceContainer;
        [UIComponent(nameof(Choicer))] private DropDownListSetting Choicer;

        private FormatListInfo(bool isTokenValue, string name, string[] tokenParams)
        {
            Chunk = isTokenValue ? ChunkType.Escaped_Token : ChunkType.Escaped_Character;
            Text = name;
            Text2 = default;
            Toggle = isTokenValue;
            TokenParams = tokenParams;
            ShowTextComp = false;
            ShowChoice = true;
            if (!isTokenValue) ChoiceText = "Choose Escaped Character";
        }
        private FormatListInfo(bool isOpen, string token, ChunkType ct) 
        {
            Chunk = ct;
            Text = token;
            Text2 = default;
            Toggle = isOpen;
            TokenParams = null;
            ShowTextComp = false;
            if (isOpen) if (ct == ChunkType.Capture_Open) ShowIncrement = true; else ShowChoice = true;
        }
        private FormatListInfo(bool isOpen, string richTextKey, string richTextValue)
        {
            Chunk = isOpen ? ChunkType.Rich_Text_Open : ChunkType.Rich_Text_Close;
            Text = richTextKey;
            Text2 = richTextValue;
            Toggle = isOpen;
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
            Chunk = isInsertSelf ? ChunkType.Insert_Group_Value : ChunkType.Regular_Text;
            Text = text;
            Text2 = default;
            Toggle = default;
            TokenParams = null;
            if (isInsertSelf) ShowTextComp = false;
        }
        public static FormatListInfo InitEscapedCharacter(bool isTokenValue, string name, params string[] tokenParams) => 
            new FormatListInfo(isTokenValue, name, tokenParams);
        public static FormatListInfo InitGroup(bool isOpen, string token) => 
            new FormatListInfo(isOpen, token, isOpen ? ChunkType.Group_Open : ChunkType.Group_Close);
        public static FormatListInfo InitInsertSelf() => new FormatListInfo("" + INSERT_SELF, true);
        public static FormatListInfo InitCapture(bool isOpen, string token) => 
            new FormatListInfo(isOpen, token, isOpen ? ChunkType.Capture_Open : ChunkType.Capture_Close);
        public static FormatListInfo InitRichText(bool isOpen, string richTextKey, string richTextValue) => 
            new FormatListInfo(isOpen, richTextKey, richTextValue);
        public static FormatListInfo InitRegularText(string text) => 
            new FormatListInfo(text, false);

        [UIAction(nameof(Centerer))] private string Centerer(string strIn) => $"<align=\"center\">{strIn}";
        [UIAction(nameof(MoveChunkUp))] private void MoveChunkUp() => MovePlacement(this, true);
        [UIAction(nameof(MoveChunkDown))] private void MoveChunkDown() => MovePlacement(this, false);
        [UIAction(nameof(RemoveChunk))] private void RemoveChunk() => RemoveSelf(this);

        private void UpdateView()
        {
            switch (Chunk)
            {
                case ChunkType.Rich_Text_Close:
                case ChunkType.Rich_Text_Open:
                    TextCompLabelObj.text = "Enter Key";
                    break;
                case ChunkType.Regular_Text:
                    TextCompLabelObj.text = "Input Text";
                    break;
                case ChunkType.Capture_Open:
                    TextCompLabelObj.text = "Enter Capture ID";
                    break;
                case ChunkType.Escaped_Token:
                case ChunkType.Group_Open:
                    Toggle = true;
                    break;
                case ChunkType.Escaped_Character:
                    Toggle = false;
                    break;
            }
            Incrementer.gameObject.SetActive(false);
            ChoiceContainer.SetActive(false);
            Text2Comp.gameObject.SetActive(false);
            TextComp.gameObject.SetActive(false);
            switch (Chunk)
            {
                case ChunkType.Escaped_Token:
                case ChunkType.Escaped_Character:
                case ChunkType.Group_Open:
                    Choicer.Values = ChoiceOptions;
                    if (!Choicer.Values.Contains(Choicer.Value)) Choicer.Value = Choicer.Values[0];
                    ChoiceContainer.SetActive(true);
                    break;
                case ChunkType.Regular_Text:
                    TextComp.gameObject.SetActive(true);
                    break;
                case ChunkType.Rich_Text_Open:
                    Text2Comp.gameObject.SetActive(true);
                    TextComp.gameObject.SetActive(true);
                    break;
                case ChunkType.Capture_Open:
                    Incrementer.gameObject.SetActive(true);
                    break;
            }
        }
        public string GetDisplay()
        {
            string outp;
            switch (Chunk)
            {
                case ChunkType.Regular_Text:
                    return Text;
                case ChunkType.Escaped_Character:
                    return $"{ESCAPE_CHAR}{Text}";
                case ChunkType.Escaped_Token:
                    outp = $"{ESCAPE_CHAR}{ALIAS}{Text}{ALIAS}";
                    if (TokenParams == null) return outp;
                    outp += PARAM_OPEN;
                    for (int i = 0; i < TokenParams.Length; i++) outp += (i != 0 ? "," : "") + $"{ALIAS}{TokenParams[i]}{ALIAS}";
                    return outp + PARAM_CLOSE;
                case ChunkType.Capture_Open:
                    return $"{CAPTURE_OPEN}{Text}";
                case ChunkType.Capture_Close:
                    return "" + CAPTURE_CLOSE;
                case ChunkType.Group_Open:
                    return $"{GROUP_OPEN}{ALIAS}{Text}{ALIAS}";
                case ChunkType.Group_Close:
                    return "" + GROUP_CLOSE;
                case ChunkType.Rich_Text_Open:
                    outp = $"{RICH_SHORT}{{0}}{DELIMITER}{Text2}{RICH_SHORT}";
                    return RICH_SHORTHANDS.ContainsValue(Text) ? string.Format(outp, RICH_SHORTHANDS.First(p => p.Value.Equals(Text)).Key) : string.Format(outp, Text);
                case ChunkType.Rich_Text_Close:
                    return "" + RICH_SHORT;
                case ChunkType.Insert_Group_Value:
                    return "" + INSERT_SELF;
                default: return "";
            }
        }
        public override string ToString()
        {
            return $"{{Chunk: {Chunk}, Text: {Text}, Toggle: {Toggle}, Token Params: [{(TokenParams != null ? string.Join(", ", TokenParams) : "")}]}}";
        }
        public enum ChunkType
        {
            Regular_Text, Escaped_Character, Escaped_Token, Capture_Open, Capture_Close, Group_Open, Group_Close, Rich_Text_Open, Rich_Text_Close, Insert_Group_Value
        }
    }
}
