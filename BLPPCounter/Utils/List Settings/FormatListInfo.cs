using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.TokenParser;
using TMPro;
using UnityEngine;
using Zenject;
using static BLPPCounter.Helpfuls.HelpfulMisc;
using static BLPPCounter.Utils.FormatListInfo.ChunkType;

namespace BLPPCounter.Utils
{
    public class FormatListInfo : SafeNotifyPropertyChanged
    {
        #region Injects

        private readonly Tokens token = null!;
        private readonly HelpfulFormatter formatter = null!;
        private readonly PluginConfig pc = null!;
        private readonly FormatEditorHandler handler = null!;

        #endregion
        #region Variables
        public static Dictionary<string, char> AliasConverter { get; internal set; } = [];
        private static Regex? CollectiveFormatRegex;

        private List<object> ParentList;
        private static readonly Color OriginalColor = new(0.8f, 0.8f, 0.8f);
        private static readonly Color SelectedColor = new(0, 0, 1);
        private static readonly Color ErrorColor = new(1, 0, 0);
        #endregion
        #region UI Variables
        [UIValue(nameof(TypesOfChunks))]
        private List<object> TypesOfChunks =
        [
            .. Enum.GetNames(typeof(ChunkType)).Select(s => s.Replace('_', ' ')).Cast<object>(),
        ];

#if NEW_VERSION
        [UIValue(nameof(ChoiceOptions))]
        private List<object> ChoiceOptions = []; //1.37.0 and above
#else
        //This is done as a workaround to a bug with BSML in 1.29.0, where if DropDownListSetting tries to load from an empty list, it will break and throw an error.
        //Since the list here gets replaced when it is in use, it doesn't matter what I put in the list as long as there is something.
        [UIValue(nameof(ChoiceOptions))]
        private List<object> ChoiceOptions = ["Placeholder"]; //1.34.2 and below
#endif

        [UIValue(nameof(ChunkStr))]
        private string ChunkStr
        {
            get => Chunk.ToString().Replace('_', ' ');
            set
            {
                Chunk = (ChunkType)Enum.Parse(typeof(ChunkType), value.Replace(' ', '_'));
                UpdateView();
                NotifyPropertyChanged(nameof(Chunk));
            }
        }
        public ChunkType Chunk { get; private set; }

        [UIValue(nameof(IncrementVal))]
        private int IncrementVal
        {
            get
            {
                if (int.TryParse(Text2, out int outp))
                    return outp;
                else
                    return 1;
            }
            set => Text2 = value.ToString();
        }

        [UIValue(nameof(Text))]
        private string Text
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(Text));
            }
        }

        [UIValue(nameof(Text2))]
        private string Text2
        {
            get;
            set
            {
                field = value;
                NotifyPropertyChanged(nameof(Text2));
            }
        }

        [UIValue(nameof(ShowTextComp))]
        private bool ShowTextComp;

        [UIValue(nameof(ShowText2Comp))]
        private bool ShowText2Comp;

        [UIValue(nameof(ShowIncrement))]
        private bool ShowIncrement;

        [UIValue(nameof(ShowChoice))]
        private bool ShowChoice;

        [UIValue(nameof(TextCompLabel))]
        private string TextCompLabel = "Input Text";

        [UIValue(nameof(ChoiceText))]
        private string ChoiceText = "Choose Token";

        [UIValue(nameof(IncrementText))]
        private string IncrementText = "Capture ID";

        [UIValue(nameof(InitialColor))]
        private string InitialColor => ConvertColorToHex(BGInitialColor);
        private Color BGInitialColor = OriginalColor;

        [UIComponent(nameof(TextComp))]
        private TextMeshProUGUI TextCompLabelObj = null!;

        [UIComponent(nameof(TextComp))]
        private StringSetting TextComp = null!;

        [UIComponent(nameof(Text2Comp))]
        private StringSetting Text2Comp = null!;

        [UIComponent(nameof(Incrementer))]
        private IncrementSetting Incrementer = null!;

        [UIComponent(nameof(Incrementer))]
        private TextMeshProUGUI IncrementerText = null!;

        [UIObject(nameof(ChoiceContainer))]
        private GameObject ChoiceContainer = null!;

        [UIComponent(nameof(Choicer))]
        private DropDownListSetting Choicer = null!;

        [UIComponent(nameof(BGContainer))]
        private Backgroundable BGContainer = null!;

        #endregion
        #region Variables
        public FormatListInfo? AboveInfo
        {
            get;
            set
            {
                field = value;

                NotifyPropertyChanged(nameof(AboveInfo));
            }
        }
        public FormatListInfo? _AboveInfo = null; //This is so that parameters can find their parent.
        private string[]? TokenParams; //This will be accessed by other instances other this class.
        public FormatListInfo? Child { get; private set; } = null;
        public bool HasChild => Child != null;
        #endregion
        #region Inits

        internal FormatListInfo(
            Tokens tokens,
            HelpfulFormatter formatter,
            PluginConfig pc,
            FormatEditorHandler handler,
            List<object> parentList
        )
            : this(parentList, ct: Regular_Text, text: "Default Text", textComp: true)
        {
            token = tokens;
            this.formatter = formatter;
            this.pc = pc;
            this.handler = handler;

            CollectiveFormatRegex ??= GetRegexForAllChunks();
        }

        private FormatListInfo(
            List<object> parentList,
            ChunkType ct = default,
            string text = "",
            string text2 = "",
            string[]? tokenParams = null,
            bool textComp = false,
            bool text2Comp = false,
            bool increment = false,
            bool choice = false
        )
        {
            ParentList = parentList;
            Chunk = ct;
            Text = text;
            Text2 = text2;
            TokenParams = tokenParams;
            PropertyChanged += DoSomethingOnPropertyChange;

            ShowTextComp = textComp;
            ShowText2Comp = text2Comp;
            ShowIncrement = increment;
            ShowChoice = choice;
        }
        #endregion
        #region Static Functions
        #region Misc
        private string ConvertFromAlias(string str)
        {
            return ConvertFromAlias(str, token);
        }

        private static string ConvertFromAlias(string str, Tokens token)
        {
            //Plugin.Log.Info("Converting from alias: " + str);
            if (str[0] == token.ALIAS)
                return str[1..^1];
            if (str.Length > 1)
                return str;
            return GetKeyFromDictionary(AliasConverter, str[0]);
        }

        internal static (Match, ChunkType)[] ChunkItAllSafe(string format, Factory factory)
        {
            if (CollectiveFormatRegex is null)
                _ = factory.Create([]);

            return ChunkItAll(format);
        }

        public static (Match, ChunkType)[] ChunkItAll(string format)
        {
            if (CollectiveFormatRegex is null)
            {
                Plugin.Log.Warn("Cannot chunk it all when the collective format regex has not been setup yet!");
                return [];
            }

            MatchCollection mc = CollectiveFormatRegex.Matches(format);
            (Match, ChunkType)[] outp = new (Match, ChunkType)[mc.Count];
            for (int i = 0; i < outp.Length; i++)
#if NEW_VERSION
                outp[i] = (
                    mc[i],
                    Enum.Parse<ChunkType>(
                        mc[i].Groups.OfType<Group>().First(g => g.Success && g.Name.Contains("_")).Name
                    )
                );
#else
                outp[i] = (
                    mc[i],
                    (ChunkType)
                        Enum.Parse(
                            typeof(ChunkType),
                            mc[i].Groups.OfType<Group>().First(g => g.Success && g.Name.Contains("_")).Name
                        )
                );
#endif
            return outp;
        }

        internal Regex GetRegexForAllChunks()
        {
            string outp = "\\G(?:";
            List<ChunkType> arr = [Insert_Group_Value, Group_Open];
            arr.AddRange(
                (Enum.GetValues(typeof(ChunkType)) as IEnumerable<ChunkType>).Where(ct =>
                    !(arr.Contains(ct) || ct.Equals(Parameter))
                )
            );
            foreach (ChunkType ct in arr)
                outp += $"(?<{ct}>{GetRegexForChunk(ct)})|";
            //Plugin.Log.Info(outp.Substring(0, outp.Length - 1) + ")");
            return new Regex(outp[..^1] + ")");
            // \G(?:(?<Insert_Group_Value>\$)|(?<Group_Open>\[(?<Token>'[^']+'|[^'])(?:\((?<Params>[^\)]+)\))?)|(?<Regular_Text>[^$&*[\]<>]+)|(?<Escaped_Character>&[&*[\]<>])|(?<Escaped_Token>&(?<Token>[^']|'[^']+')(?:\((?<Params>[^\)]+)\))?)|(?<Capture_Open><\d+)|(?<Capture_Close>>)|(?<Group_Close>])|(?<Rich_Text_Open>\*(?<Key>[^,\*]+),(?<Value>[^\*]+)\*|<(?<Key>[^=]+)=(?<Value>[^>]+)>)|(?<Rich_Text_Close>\*|<[^>]+>))
        }

        internal string GetRegexForChunk(ChunkType ct)
        {
            return ct switch
            {
                Regular_Text => "[^" + token.INSERT_SELF + formatter.RegexSpecialChars[1..] + "+", //[^$&*[\]<>]+
                Escaped_Character => $"{Regex.Escape(token.ESCAPE_CHAR.ToString())}{formatter.RegexSpecialChars}", //&[&*[\]<>]
                Escaped_Token => string.Format(
                    "{0}(?<Token>[^{1}]|{1}[^{1}]+{1})(?:{2}(?<Params>[^{3}]+){3})?",
                    Regex.Escape($"{token.ESCAPE_CHAR}"),
                    Regex.Escape($"{token.ALIAS}"),
                    Regex.Escape($"{token.PARAM_OPEN}"),
                    Regex.Escape($"{token.PARAM_CLOSE}")
                ), //(?<Token>&.|&'[^']+')\((?<Params>[^\)]+)\)|(?<Token>&'[^']+'|&.)
                Capture_Open => $"{Regex.Escape(token.CAPTURE_OPEN + "")}\\d+", //<\d+
                Capture_Close => Regex.Escape(token.CAPTURE_CLOSE + ""), //>
                Group_Open => string.Format(
                    "{0}(?<Token>{1}[^{1}]+{1}|[^{1}])(?:{2}(?<Params>[^{3}]+){3})?",
                    Regex.Escape($"{token.GROUP_OPEN}"),
                    Regex.Escape($"{token.ALIAS}"),
                    Regex.Escape($"{token.PARAM_OPEN}"),
                    Regex.Escape($"{token.PARAM_CLOSE}")
                ), //(?:(?<Alias>\['[^']+')|(?<Token>\[[^']))(?:\((?<Params>[^\)]+)\))?
                Group_Close => Regex.Escape(token.GROUP_CLOSE + ""), //\]
                Rich_Text_Open => string.Format(
                    "{0}(?<Key>[^{1}{0}]+){1}(?<Value>[^{0}]+){0}|<(?<Key>[^=]+)=(?<Value>[^>]+)>",
                    Regex.Escape(token.RICH_SHORT + ""),
                    Regex.Escape(token.DELIMITER + "")
                ), //\*(?<Key>[^,]+),(?<Value>[^\*]+)\*|<(?<Key>[^=]+)=(?<Value>[^>]+)>
                Rich_Text_Close => $"{Regex.Escape(token.RICH_SHORT + "")}|<[^>]+>", //\*|<[^>]+>
                Insert_Group_Value => Regex.Escape(token.INSERT_SELF + ""), //$
                _ => "",
            };
        }

        public string ColorFormatChunk(string text, ChunkType ct)
        {
            return ColorFormatChunk(text, ct, pc, token, formatter);
        }

        internal static string ColorFormatChunk(
            string text,
            ChunkType ct,
            PluginConfig pc,
            Tokens token,
            HelpfulFormatter formatter
        )
        {
            //Plugin.Log.Info($"Coloring chunk: {{Type: {ct}, Text: {text}}}");
            string outp;
            switch (ct)
            {
                case Regular_Text:
                    return "<color=white>"
                        + text.Replace("\\n", $"{ConvertColorToMarkup(pc.SpecialCharacterColor)}\\n</color>");
                case Escaped_Character:
                    return $"{formatter.ColorSpecialChar(token.ESCAPE_CHAR)}{ConvertColorToMarkup(pc.AliasColor)}{text[1]}";
                case Escaped_Token:
                case Group_Open:
                    int paramIndex = text.IndexOf(token.PARAM_OPEN);
                    return formatter.ColorSpecialChar(text[0])
                        + (
                            paramIndex > -1
                                ? ColorEscapeToken(text[1..paramIndex], token, formatter)
                                    + ColorParams(text, token, formatter)
                                : ColorEscapeToken(text[1..], token, formatter)
                        );
                case Capture_Open:
                    return $"{formatter.ColorSpecialChar(token.CAPTURE_OPEN)}{ConvertColorToMarkup(pc.CaptureIdColor)}{text[1..]}";
                case Capture_Close:
                    return formatter.ColorSpecialChar(token.CAPTURE_CLOSE);
                case Group_Close:
                    return formatter.ColorSpecialChar(token.GROUP_CLOSE);
                case Rich_Text_Open:
                    int index;
                    if (text[0] == '<')
                    {
                        index = text.IndexOf('=');
                        return $"{ConvertColorToMarkup(pc.ShorthandColor)}<{ConvertColorToMarkup(pc.SpecialCharacterColor)}{text[1..index]}"
                            + $"{ConvertColorToMarkup(pc.DelimeterColor)}={ConvertColorToMarkup(pc.ParamVarColor)}{text[index..]}{ConvertColorToMarkup(pc.ShorthandColor)}>";
                    }
                    index = text.IndexOf(token.DELIMITER);
                    outp =
                        $"{formatter.ColorSpecialChar(token.RICH_SHORT)}{{0}}{formatter.ColorSpecialChar(token.DELIMITER)}{ConvertColorToMarkup(pc.ParamVarColor)}{text.Substring(index + 1, text.Length - index - 2)}{formatter.ColorSpecialChar(token.RICH_SHORT)}";
                    text = text[1..index];
                    return formatter.RICH_SHORTHANDS.ContainsValue(text)
                        ? string.Format(outp, formatter.RICH_SHORTHANDS.First(p => p.Value.Equals(text)).Key)
                        : string.Format(outp, text);
                case Rich_Text_Close:
                    return formatter.ColorSpecialChar(token.RICH_SHORT);
                case Insert_Group_Value:
                    return formatter.ColorSpecialChar(token.INSERT_SELF);
                default:
                    return text;
            }
        }

        private string ColorEscapeToken(string text)
        {
            return ColorEscapeToken(text, token, formatter);
        }

        private static string ColorEscapeToken(string text, Tokens token, HelpfulFormatter formatter)
        {
            if (text[0] == token.ALIAS)
                return string.Format(formatter.ColorDefaultFormatToColor("'cAlias0'"), ConvertFromAlias(text, token));
            return string.Format(
                formatter.ColorFormatToColor("'cAlias0'"),
                GetKeyFromDictionary(AliasConverter, text[0])
            );
        }

        private string ColorParams(string text)
        {
            return ColorParams(text, token, formatter);
        }

        private static string ColorParams(string text, Tokens token, HelpfulFormatter formatter)
        {
            string outp = formatter.ColorSpecialChar(token.PARAM_OPEN);
            text = text[(text.IndexOf(token.PARAM_OPEN) + 1)..];
            text = text[..^1]; //Remove closing param char
            string[] parameters = text.Split(token.DELIMITER);
            outp += ColorEscapeToken(parameters[0], token, formatter);
            for (int i = 1; i < parameters.Length; i++)
                outp +=
                    $"{formatter.ColorSpecialChar(token.DELIMITER)}{ColorEscapeToken(parameters[i], token, formatter)}";
            return outp + formatter.ColorSpecialChar(token.PARAM_CLOSE);
        }
        #endregion
        #endregion
        #region UI Functions
        [UIAction(nameof(Centerer))]
        private string Centerer(string strIn)
        {
            return $"<align=\"center\">{strIn}";
        }

        [UIAction(nameof(MoveChunkUp))]
        private void MoveChunkUp()
        {
            int index = ParentList.IndexOf(this);
            if (index > 0)
            {
                if (ParentList[index - 1] is not FormatListInfo other)
                {
                    Plugin.Log.Critical("There is a null in the ParentList somehow!");
                    return;
                }

                ParentList[index] = other;
                ParentList[index - 1] = this;

                AboveInfo = other.AboveInfo;
                other.AboveInfo = this;

                handler.UpdateFormatTable();
                handler.GotoCell(index - 1);
            }
        }

        [UIAction(nameof(MoveChunkDown))]
        private void MoveChunkDown()
        {
            int index = ParentList.IndexOf(this);
            if (index < ParentList.Count - 1)
            {
                if (ParentList[index + 1] is not FormatListInfo other)
                {
                    Plugin.Log.Critical("There is a null in the ParentList somehow!");
                    return;
                }

                ParentList[index] = other;
                ParentList[index + 1] = this;

                other.AboveInfo = AboveInfo;
                AboveInfo = other;

                handler.UpdateFormatTable();
                handler.GotoCell(index + 1);
            }
        }

        [UIAction(nameof(RemoveChunk))]
        private void RemoveChunk()
        {
            int index = ParentList.IndexOf(this);
            if (index < ParentList.Count - 1 && ParentList[index + 1] is FormatListInfo fli)
                fli.AboveInfo = index > 0 ? (ParentList[index - 1] as FormatListInfo) : null;

            TellParentTheyHaveAChild(true);
            ParentList.Remove(this);

            handler.UpdateFormatTable();
            handler.GotoCell(index);
        }
        #endregion
        #region Functions
        private void SetBGColor(Color color)
        {
            BGInitialColor = color;
#if NEW_VERSION
            BGContainer?.ApplyColor(color); //1.37.0 and above
#else
            BGContainer?.background.color = color; //1.34.2 and below
#endif
        }

        public void Selected()
        {
            SetBGColor(SelectedColor);
        }

        public void ResetBackgroundColor()
        {
            SetBGColor(OriginalColor);
        }

        public void SetParentToken() //For ChunkType.Parameter
        {
            if (Chunk != Parameter)
                return;

            if (!int.TryParse(Text2, out int index))
            {
                index = 1;
                Text2 = "1";
            }

            FormatListInfo? parent = this;
            while (parent is not null && parent.Chunk == Parameter)
                parent = parent.AboveInfo;

            index--;

            if (parent is null || parent.TokenParams is null || parent.TokenParams.Length >= index)
                return;

            parent.TokenParams[index] = Text;
        }

        public void TellParentTheyHaveAChild(bool childIsDead = false)
        {
            if (((Capture_Close | Group_Close | Rich_Text_Close | Parameter) & Chunk) == 0)
                return;

            FormatListInfo? parent = AboveInfo;

            ChunkType open = Chunk == Parameter ? Escaped_Token : (ChunkType)((int)Chunk / 2),
                close = Chunk == Parameter ? Regular_Text : Chunk;

            while (parent is not null && ((open | close) & parent.Chunk) == 0)
                parent = parent.AboveInfo;

            if (parent is not null && parent.Chunk == open)
                parent.Child = childIsDead ? null : this;
        }

        private void DoSomethingOnPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(Text)))
                SetParentToken();
            if (e.PropertyName.Equals(nameof(AboveInfo)))
                TellParentTheyHaveAChild();
            else
                handler.UpdatePreviewDisplay(); //All other properties, when changed, affect the preview.
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
                    Text = Text2 = IncrementVal.ToString();
                    break;
                case Parameter:
                    IncrementerText.text = "Parameter Index";
                    ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];
                    break;
                case Group_Open:
                case Escaped_Token:
                    ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];
                    break;
                case Escaped_Character:
                    ChoiceOptions = [.. formatter.SPECIAL_CHARS.Select(c => c.ToString()).Cast<object>()];
                    break;
            }

            ShowChoice = ((Escaped_Token | Escaped_Character | Group_Open | Parameter) & Chunk) > 0;
            ShowTextComp = ((Regular_Text | Rich_Text_Open) & Chunk) > 0;
            ShowText2Comp = Rich_Text_Open == Chunk;
            ShowIncrement = ((Capture_Open | Parameter) & Chunk) > 0;

            if (ShowChoice)
            {
#if NEW_VERSION
                Choicer.Values = ChoiceOptions;
                if (!Choicer.Values.Contains(Text))
                {
                    Choicer.Value = Choicer.Values[0];
                    Text = Choicer.Values[0].ToString();
                } //1.37.0 and above */
#else
                Choicer.values = ChoiceOptions;
                if (!Choicer.values.Contains(Text))
                {
                    Choicer.Value = Choicer.values[0];
                    Text = Choicer.values[0].ToString();
                } //1.34.2 and below */
#endif
                else
                    Choicer.Value = Text;
                Choicer.UpdateChoices();
            }

            ChoiceContainer.SetActive(ShowChoice);
            TextComp.gameObject.SetActive(ShowTextComp);
            Text2Comp.gameObject.SetActive(ShowText2Comp);
            Incrementer.gameObject.SetActive(ShowIncrement);

            if (((Capture_Close | Group_Close | Rich_Text_Close | Parameter) & Chunk) != 0)
                TellParentTheyHaveAChild();
        }

        public bool Updatable(out string error)
        {
            bool outp;
            error = "";

            //ChunkType Groups
            //---------------------------------------------------------------------------------------
            const ChunkType children = Capture_Close | Group_Close | Rich_Text_Close | Parameter;
            const ChunkType parents = Capture_Open | Group_Open | Rich_Text_Open;
            //---------------------------------------------------------------------------------------

            //Chunk Checks
            //---------------------------------------------------------------------------------------
            if (Chunk == Escaped_Token)
            {
                outp = TokenParams is null || HasChild;
                if (!outp)
                    error = "Cannot declare child then not accept that it exists";
                goto End;
            }
            if ((parents & Chunk) != 0)
            {
                outp = HasChild;
                if (!outp)
                    error = "Parents must have a children (are you missing a closing bracket?)";
                goto End;
            }
            if ((children & Chunk) == 0) //Note: This includes Regular_Text (it has a value of 0).
            {
                outp = true;
                goto End;
            }
            //---------------------------------------------------------------------------------------

            //Children Bounds
            //---------------------------------------------------------------------------------------
            ChunkType open,
                close;
            if (Chunk == Parameter)
            {
                open = Escaped_Token | Group_Open;
                close = Regular_Text | Group_Close;
            }
            else
            {
                open = (ChunkType)((uint)Chunk >> 1);
                close = Chunk == Parameter ? Regular_Text : Chunk;
            }
            //---------------------------------------------------------------------------------------

            //Parent Search
            //---------------------------------------------------------------------------------------
            FormatListInfo? parent = AboveInfo;

            while (parent is not null && ((open | close) & parent.Chunk) == 0)
                parent = parent.AboveInfo;

            outp = parent is not null && (parent.Chunk & open) != 0;

            if (!outp)
                error = "No valid parent found for this chunk (are you missing an opening bracket?)";
            //---------------------------------------------------------------------------------------

            //End
            //---------------------------------------------------------------------------------------
            End:
            if (!outp)
                SetBGColor(ErrorColor);
            return outp;
            //---------------------------------------------------------------------------------------
        }

        public string GetDisplay()
        {
            return Chunk switch
            {
                Regular_Text => Text,
                Escaped_Character => $"{token.ESCAPE_CHAR}{Text}",
                Escaped_Token =>
                    $"{token.ESCAPE_CHAR}{token.ALIAS}{Text}{token.ALIAS}{(TokenParams is not null ? TokenParametersToString() : "")}",
                Capture_Open => $"{token.CAPTURE_OPEN}{Text2}",
                Capture_Close => token.CAPTURE_CLOSE.ToString(),
                Group_Open =>
                    $"{token.GROUP_OPEN}{token.ALIAS}{Text}{token.ALIAS}{(TokenParams is not null ? TokenParametersToString() : "")}",
                Group_Close => token.GROUP_CLOSE.ToString(),
                Rich_Text_Open => string.Format(
                    $"{token.RICH_SHORT}{{0}}{token.DELIMITER}{Text2}{token.RICH_SHORT}",
                    formatter.RICH_SHORTHANDS.ContainsValue(Text)
                        ? formatter.RICH_SHORTHANDS.First(p => p.Value.Equals(Text)).Key
                        : Text
                ),
                Rich_Text_Close => token.RICH_SHORT.ToString(),
                Insert_Group_Value => token.INSERT_SELF.ToString(),
                _ => "",
            };
        }

        public string GetColorDisplay()
        {
            return ColorFormatChunk(GetDisplay(), Chunk);
        }

        public string TokenParametersToString()
        {
            return $"{token.PARAM_OPEN}{string.Join($"{token.DELIMITER}", TokenParams.Select(p => $"{token.ALIAS}{p}{token.ALIAS}"))}{token.PARAM_CLOSE}";
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

        internal sealed class Factory : PlaceholderFactory<List<object>, FormatListInfo>
        {
            private readonly Tokens _token;
            private readonly HelpfulFormatter _formatter;

            public Factory(Tokens token, HelpfulFormatter formatter)
            {
                _token = token;
                _formatter = formatter;
            }

            private FormatListInfo Create(
                List<object> parentList,
                ChunkType ct = default,
                string text = "",
                string text2 = "",
                string[]? tokenParams = null,
                bool textComp = false,
                bool text2Comp = false,
                bool increment = false,
                bool choice = false
            )
            {
                FormatListInfo outp = base.Create(parentList);

                outp.Chunk = ct;
                outp.Text = text;
                outp.Text2 = text2;
                outp.TokenParams = tokenParams;

                outp.ShowTextComp = textComp;
                outp.ShowText2Comp = text2Comp;
                outp.ShowIncrement = increment;
                outp.ShowChoice = choice;

                return outp;
            }

            private FormatListInfo Create(
                List<object> parentList,
                bool isTokenValue,
                string name,
                string[]? tokenParams = null
            )
            {
                FormatListInfo outp = Create(
                    parentList,
                    ct: isTokenValue ? Escaped_Token : Escaped_Character,
                    text: isTokenValue ? ConvertFromAlias(name, _token) : name,
                    tokenParams: tokenParams,
                    choice: true
                );

                outp.ChoiceOptions = isTokenValue
                    ? [.. AliasConverter.Keys.Cast<object>()]
                    : [.. _formatter.SPECIAL_CHARS.Select(c => c.ToString()).Cast<object>()];

                if (!isTokenValue)
                    outp.ChoiceText = "Choose Escaped Character";

                return outp;
            }

#pragma warning disable IDE0060
            private FormatListInfo Create(
                List<object> parentList,
                bool isOpen,
                string token,
                ChunkType ct,
                string[]? tokenParams = null
            )
            {
                FormatListInfo outp = Create(
                    parentList,
                    ct: ct,
                    text: ct == Group_Open ? ConvertFromAlias(token, _token) : token,
                    text2: token,
                    increment: ct == Capture_Open,
                    choice: ct == Group_Open,
                    tokenParams: tokenParams
                );

                if (ct == Group_Open)
                    outp.ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];

                return outp;
            }
#pragma warning restore IDE0060

            private FormatListInfo Create(
                List<object> parentList,
                bool isOpen,
                string richTextKey,
                string richTextValue
            )
            {
                FormatListInfo outp = Create(
                    parentList,
                    ct: isOpen ? Rich_Text_Open : Rich_Text_Close,
                    text: _formatter.RICH_SHORTHANDS.TryGetValue(richTextKey, out string val) ? val : richTextKey,
                    text2: richTextValue,
                    textComp: isOpen,
                    text2Comp: isOpen
                );

                if (isOpen)
                    outp.TextCompLabel = "Enter Key";

                return outp;
            }

            private FormatListInfo Create(List<object> parentList, string text, bool isInsertSelf)
            {
                return Create(
                    parentList,
                    ct: isInsertSelf ? Insert_Group_Value : Regular_Text,
                    text: text,
                    textComp: !isInsertSelf
                );
            }

            private FormatListInfo Create(List<object> parentList, string name, int index)
            {
                FormatListInfo outp = Create(
                    parentList,
                    ct: Parameter,
                    text: name, //Don't need to worry about the alias here because the function using this initializer takes care of it.
                    text2: $"{index + 1}", //plus one because the average person doesn't use zero indexing and this number will be displayed.
                    increment: true,
                    choice: true
                );

                outp.ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];
                outp.IncrementText = "Parameter Index";

                return outp;
            }

            public FormatListInfo? InitFromGivenChunk(
                List<object> parentList,
                (Match, ChunkType) chunk,
                out FormatListInfo[]? extraInfo
            )
            {
                extraInfo = null;
                //Plugin.Log.Info(chunk.ToString());
                switch (chunk.Item2)
                {
                    case Regular_Text:
                        return Create(parentList, chunk.Item1.Value, false);
                    case Escaped_Character:
                        return Create(parentList, false, chunk.Item1.Value[1] + "");
                    case Escaped_Token:
                        if (!chunk.Item1.Groups["Params"].Success)
                            return Create(parentList, true, chunk.Item1.Groups["Token"].Value);
                        string[] theParams = ParseParams(parentList, chunk.Item1, out extraInfo);
                        return Create(parentList, true, chunk.Item1.Groups["Token"].Value, theParams);
                    case Capture_Open:
                    case Capture_Close:
                        return Create(parentList, chunk.Item2 == Capture_Open, chunk.Item1.Value[1..], chunk.Item2);
                    case Group_Open:
                        if (!chunk.Item1.Groups["Params"].Success)
                            return Create(parentList, true, chunk.Item1.Groups["Token"].Value, chunk.Item2);
                        string[] groupParams = ParseParams(parentList, chunk.Item1, out extraInfo);
                        return Create(parentList, true, chunk.Item1.Groups["Token"].Value, chunk.Item2, groupParams);
                    case Group_Close:
                        return Create(parentList, false, chunk.Item1.Value[1..], chunk.Item2);
                    case Rich_Text_Open:
                        return Create(
                            parentList,
                            true,
                            chunk.Item1.Groups["Key"].Value,
                            chunk.Item1.Groups["Value"].Value
                        );
                    case Rich_Text_Close:
                        return Create(parentList, false, "", "");
                    case Insert_Group_Value:
                        return Create(parentList, _token.INSERT_SELF.ToString(), true);
                    default:
                        return null;
                }
            }

            public List<FormatListInfo> InitAllFromChunks(List<object> parentList, (Match, ChunkType)[] chunks)
            {
                List<FormatListInfo> outp = [];
                foreach ((Match, ChunkType) chunk in chunks)
                {
                    FormatListInfo? info = InitFromGivenChunk(parentList, chunk, out FormatListInfo[]? extras);

                    if (info is not null)
                        outp.Add(info);

                    if (extras is not null)
                        outp.AddRange(extras);
                }
                for (int i = 1; i < outp.Count; i++)
                    outp[i].AboveInfo = outp[i - 1];
                return outp;
            }

            private string[] ParseParams(List<object> parentList, Match m, out FormatListInfo[] extraInfo)
            {
                string[] theParams =
                [
                    .. m.Groups["Params"].Value.Split(_token.DELIMITER).Select(str => ConvertFromAlias(str, _token)),
                ];
                extraInfo = new FormatListInfo[theParams.Length];
                for (int i = 0; i < theParams.Length; i++)
                    extraInfo[i] = Create(parentList, theParams[i], i);
                return theParams;
            }

            public string? ColorFormatChunk((Match, ChunkType) chunk)
            {
                return InitFromGivenChunk([], chunk, out _)?.GetColorDisplay();
            }

            public string ColorFormat(string format)
            {
                (Match, ChunkType)[] arr = ChunkItAllSafe(format, this);
                format = "";
                foreach ((Match, ChunkType) chunk in arr)
                    format += ColorFormatChunk(chunk);
                return format;
            }
        }
        #endregion
    }
}
