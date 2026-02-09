using BeatSaberMarkupLanguage.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using BeatSaberMarkupLanguage.Components.Settings;
using UnityEngine;
using TMPro;
using System.ComponentModel;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Helpfuls;
using BeatSaberMarkupLanguage.Components;

using static BLPPCounter.Utils.TokenParser.Tokens;
using static BLPPCounter.Utils.FormatListInfo.ChunkType;
using static BLPPCounter.Helpfuls.HelpfulFormatter;
using static BLPPCounter.Helpfuls.HelpfulMisc;

namespace BLPPCounter.Utils
{
    public class FormatListInfo : INotifyPropertyChanged
    {
#pragma warning disable IDE0044, CS0649, CS0414
        #region Static Variables
        public static Dictionary<string, char> AliasConverter { get; internal set; }
        private static List<object> ParentList;
        private static Action UpdateTable, UpdatePreview;
        private static readonly Color OriginalColor = new(0.8f, 0.8f, 0.8f);
        private static readonly Color SelectedColor = new(0, 0, 1);
        private static readonly Color ErrorColor = new(1, 0, 0);

        public static FormatListInfo DefaultVal => new("Default Text", false);

        #endregion
        #region UI Variables
        [UIValue(nameof(TypesOfChunks))] private List<object> TypesOfChunks = [.. Enum.GetNames(typeof(ChunkType)).Select(s => s.Replace('_', ' ')).Cast<object>()];
#if NEW_VERSION
        [UIValue(nameof(ChoiceOptions))] private List<object> ChoiceOptions = []; //1.37.0 and above
#else
        //This is done as a workaround to a bug with BSML in 1.29.0, where if DropDownListSetting tries to load from an empty list, it will break and throw an error.
        //Since the list here gets replaced when it is in use, it doesn't matter what I put in the list as long as there is something.
        [UIValue(nameof(ChoiceOptions))] private List<object> ChoiceOptions = ["Placeholder"]; //1.34.2 and below
#endif

        [UIValue(nameof(ChunkStr))] private string ChunkStr
        {
            get => Chunk.ToString().Replace('_', ' ');
            set
            {
                Chunk = (ChunkType)Enum.Parse(typeof(ChunkType), value.Replace(' ', '_'));
                UpdateView();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chunk)));
            }
        }
        public ChunkType Chunk { get; private set; }
        [UIValue(nameof(IncrementVal))] private int IncrementVal
        {
            get { if (int.TryParse(Text2, out int outp)) return outp; else return 1; }
            set => Text2 = value.ToString();
        }
        [UIValue(nameof(Text))] private string Text { get => _Text; set { _Text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); } }
        [UIValue(nameof(Text2))] private string Text2 { get => _Text2; set { _Text2 = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text2))); } }

        [UIValue(nameof(ShowTextComp))] private bool ShowTextComp;
        [UIValue(nameof(ShowText2Comp))] private bool ShowText2Comp;
        [UIValue(nameof(ShowIncrement))] private bool ShowIncrement;
        [UIValue(nameof(ShowChoice))] private bool ShowChoice;
        [UIValue(nameof(TextCompLabel))] private string TextCompLabel = "Input Text";
        [UIValue(nameof(ChoiceText))] private string ChoiceText = "Choose Token";
        [UIValue(nameof(IncrementText))] private string IncrementText = "Capture ID";
        [UIValue(nameof(InitialColor))] private string InitialColor => ConvertColorToHex(BGInitialColor);
        private Color BGInitialColor = OriginalColor;

        [UIComponent(nameof(TextComp))] private TextMeshProUGUI TextCompLabelObj;
        [UIComponent(nameof(TextComp))] private StringSetting TextComp;
        [UIComponent(nameof(Text2Comp))] private StringSetting Text2Comp;
        [UIComponent(nameof(Incrementer))] private IncrementSetting Incrementer;
        [UIComponent(nameof(Incrementer))] private TextMeshProUGUI IncrementerText;
        [UIObject(nameof(ChoiceContainer))] private GameObject ChoiceContainer;
        [UIComponent(nameof(Choicer))] private DropDownListSetting Choicer;
        [UIComponent(nameof(BGContainer))] private Backgroundable BGContainer;

#endregion
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        public FormatListInfo AboveInfo 
        { 
            get => _AboveInfo; 
            set 
            { 
                _AboveInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AboveInfo))); 
            } 
        }
        public FormatListInfo _AboveInfo = null; //This is so that parameters can find their parent.
        private string[] TokenParams; //This will be accessed by other instances other this class.
        private string _Text, _Text2;
        public FormatListInfo Child { get; private set; } = null;
        public bool HasChild => Child != null;
        #endregion
        #region Inits
        private FormatListInfo(ChunkType ct = default, string text = "", string text2 = "", string[] tokenParams = null,
            bool textComp = false, bool text2Comp = false, bool increment = false, bool choice = false)
        {
            Chunk = ct;
            _Text = text;
            _Text2 = text2;
            TokenParams = tokenParams;
            PropertyChanged += DoSomethingOnPropertyChange;

            ShowTextComp = textComp;
            ShowText2Comp = text2Comp;
            ShowIncrement = increment;
            ShowChoice = choice;
        }
        private FormatListInfo(bool isTokenValue, string name, string[] tokenParams = null) :
            this(
                  ct: isTokenValue ? Escaped_Token : Escaped_Character,
                  text: isTokenValue ? ConvertFromAlias(name) : name,
                  tokenParams: tokenParams,
                  choice: true
                  )
        {
            ChoiceOptions = isTokenValue ? [.. AliasConverter.Keys.Cast<object>()] : [.. SPECIAL_CHARS.Select(c => c.ToString()).Cast<object>()];
            if (!isTokenValue) ChoiceText = "Choose Escaped Character";
        }
#pragma warning disable IDE0060
        private FormatListInfo(bool isOpen, string token, ChunkType ct, string[] tokenParams = null) :
            this(
                ct: ct,
                text: ct == Group_Open ? ConvertFromAlias(token) : token,
                text2: token,
                increment: ct == Capture_Open,
                choice: ct == Group_Open,
                tokenParams: tokenParams
                )
        {
            if (ct == Group_Open) ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];
        }
#pragma warning restore IDE0060
        private FormatListInfo(bool isOpen, string richTextKey, string richTextValue) :
            this(
                ct: isOpen ? Rich_Text_Open : Rich_Text_Close,
                text: RICH_SHORTHANDS.TryGetValue(richTextKey, out string val) ? val : richTextKey,
                text2: richTextValue,
                textComp: isOpen,
                text2Comp: isOpen
                )
        {
            if (isOpen) TextCompLabel = "Enter Key";
        }
        private FormatListInfo(string text, bool isInsertSelf) :
            this(
                ct: isInsertSelf ? Insert_Group_Value : Regular_Text,
                text: text,
                textComp: !isInsertSelf
                )
        {}
        private FormatListInfo(string name, int index) :
            this(
                ct: Parameter,
                text: name, //Don't need to worry about the alias here because the function using this initializer takes care of it.
                text2: $"{index + 1}", //plus one because the average person doesn't use zero indexing and this number will be displayed.
                increment: true,
                choice: true
                )
        {
            ChoiceOptions = [.. AliasConverter.Keys.Cast<object>()];
            IncrementText = "Parameter Index";
        }
        #endregion
        #region Static Functions
        #region Inits
        public static List<FormatListInfo> InitAllFromChunks((Match, ChunkType)[] chunks)
        {
            List<FormatListInfo> outp = [];
            foreach ((Match, ChunkType) chunk in chunks)
            {
                outp.Add(InitFromGivenChunk(chunk, out FormatListInfo[] extras));
                if (extras is not null) outp.AddRange(extras);
            }
            for (int i = 1; i < outp.Count; i++) 
                outp[i].AboveInfo = outp[i - 1];
            return outp;
        }
        public static FormatListInfo InitFromGivenChunk((Match, ChunkType) chunk, out FormatListInfo[] extraInfo)
        {
            extraInfo = null;
            //Plugin.Log.Info(chunk.ToString());
            switch (chunk.Item2)
            {
                case Regular_Text: return new FormatListInfo(chunk.Item1.Value, false);
                case Escaped_Character: return new FormatListInfo(false, chunk.Item1.Value[1] + "");
                case Escaped_Token:
                    if (!chunk.Item1.Groups["Params"].Success) return new FormatListInfo(true, chunk.Item1.Groups["Token"].Value);
                    string[] theParams = ParseParams(chunk.Item1, out extraInfo);
                    return new FormatListInfo(true, chunk.Item1.Groups["Token"].Value, theParams);
                case Capture_Open:
                case Capture_Close:
                    return new FormatListInfo(chunk.Item2 == Capture_Open, chunk.Item1.Value.Substring(1), chunk.Item2);
                case Group_Open:
                    if (!chunk.Item1.Groups["Params"].Success) return new FormatListInfo(true, chunk.Item1.Groups["Token"].Value, chunk.Item2);
                    string[] groupParams = ParseParams(chunk.Item1, out extraInfo);
                    return new FormatListInfo(true, chunk.Item1.Groups["Token"].Value, chunk.Item2, groupParams);
                case Group_Close:
                    return new FormatListInfo(false, chunk.Item1.Value.Substring(1), chunk.Item2);
                case Rich_Text_Open:
                    return new FormatListInfo(true, chunk.Item1.Groups["Key"].Value, chunk.Item1.Groups["Value"].Value);
                case Rich_Text_Close: return new FormatListInfo(false, "", "");
                case Insert_Group_Value: return new FormatListInfo(INSERT_SELF.ToString(), true);
                default: return null;
            }
        }
        private static string[] ParseParams(Match m, out FormatListInfo[] extraInfo)
        {
            string[] theParams = [.. m.Groups["Params"].Value.Split(DELIMITER).Select(ConvertFromAlias)];
            extraInfo = new FormatListInfo[theParams.Length];
            for (int i = 0; i < theParams.Length; i++)
                extraInfo[i] = new FormatListInfo(theParams[i], i);
            return theParams;
        }
        internal static void InitStaticActions(List<object> parentList, Action updateTable, Action updatePreview)
        {
            ParentList = parentList;
            UpdateTable = updateTable;
            UpdatePreview = updatePreview;
        }
        #endregion
        #region Misc
        private static string ConvertFromAlias(string str)
        {
            //Plugin.Log.Info("Converting from alias: " + str);
            if (str[0] == ALIAS) return str.Substring(1, str.Length - 2);
            if (str.Length > 1) return str;
            return GetKeyFromDictionary(AliasConverter, str[0]);
        }
        public static (Match, ChunkType)[] ChunkItAll(string format)
        {
            MatchCollection mc = HelpfulRegex.CollectiveFormatRegex.Matches(format);
            (Match, ChunkType)[] outp = new (Match, ChunkType)[mc.Count];
            for (int i = 0; i < outp.Length; i++)
#if NEW_VERSION
                outp[i] = (mc[i], Enum.Parse<ChunkType>(mc[i].Groups.First(g => g.Success && g.Name.Contains("_")).Name));
#else
                outp[i] = (mc[i], (ChunkType)Enum.Parse(typeof(ChunkType), mc[i].Groups.OfType<Group>().First(g => g.Success && g.Name.Contains("_")).Name));
#endif
            return outp;
        }
        internal static Regex GetRegexForAllChunks()
        {
            string outp = "\\G(?:";
            List<ChunkType> arr = [Insert_Group_Value, Group_Open];
            arr.AddRange((Enum.GetValues(typeof(ChunkType)) as IEnumerable<ChunkType>).Where(ct => !(arr.Contains(ct) || ct.Equals(Parameter))));
            foreach (ChunkType ct in arr)
                outp += $"(?<{ct}>{GetRegexForChunk(ct)})|";
            //Plugin.Log.Info(outp.Substring(0, outp.Length - 1) + ")");
            return new Regex(outp.Substring(0, outp.Length - 1) + ")");
            // \G(?:(?<Insert_Group_Value>\$)|(?<Group_Open>\[(?<Token>'[^']+'|[^'])(?:\((?<Params>[^\)]+)\))?)|(?<Regular_Text>[^$&*[\]<>]+)|(?<Escaped_Character>&[&*[\]<>])|(?<Escaped_Token>&(?<Token>[^']|'[^']+')(?:\((?<Params>[^\)]+)\))?)|(?<Capture_Open><\d+)|(?<Capture_Close>>)|(?<Group_Close>])|(?<Rich_Text_Open>\*(?<Key>[^,\*]+),(?<Value>[^\*]+)\*|<(?<Key>[^=]+)=(?<Value>[^>]+)>)|(?<Rich_Text_Close>\*|<[^>]+>))
        }
        internal static string GetRegexForChunk(ChunkType ct) => ct switch
        {
            Regular_Text => "[^" + INSERT_SELF + RegexSpecialChars.Substring(1) + "+",//[^$&*[\]<>]+
            Escaped_Character => $"{Regex.Escape(ESCAPE_CHAR.ToString())}{RegexSpecialChars}",//&[&*[\]<>]
            Escaped_Token => string.Format("{0}(?<Token>[^{1}]|{1}[^{1}]+{1})(?:{2}(?<Params>[^{3}]+){3})?", Regex.Escape($"{ESCAPE_CHAR}"), Regex.Escape($"{ALIAS}"), Regex.Escape($"{PARAM_OPEN}"), Regex.Escape($"{PARAM_CLOSE}")),//(?<Token>&.|&'[^']+')\((?<Params>[^\)]+)\)|(?<Token>&'[^']+'|&.)
            Capture_Open => $"{Regex.Escape(CAPTURE_OPEN + "")}\\d+",//<\d+
            Capture_Close => Regex.Escape(CAPTURE_CLOSE + ""),//>
            Group_Open => string.Format("{0}(?<Token>{1}[^{1}]+{1}|[^{1}])(?:{2}(?<Params>[^{3}]+){3})?", Regex.Escape($"{GROUP_OPEN}"), Regex.Escape($"{ALIAS}"), Regex.Escape($"{PARAM_OPEN}"), Regex.Escape($"{PARAM_CLOSE}")),//(?:(?<Alias>\['[^']+')|(?<Token>\[[^']))(?:\((?<Params>[^\)]+)\))?
            Group_Close => Regex.Escape(GROUP_CLOSE + ""),//\]
            Rich_Text_Open => string.Format("{0}(?<Key>[^{1}{0}]+){1}(?<Value>[^{0}]+){0}|<(?<Key>[^=]+)=(?<Value>[^>]+)>", Regex.Escape(RICH_SHORT + ""), Regex.Escape(DELIMITER + "")),//\*(?<Key>[^,]+),(?<Value>[^\*]+)\*|<(?<Key>[^=]+)=(?<Value>[^>]+)>
            Rich_Text_Close => $"{Regex.Escape(RICH_SHORT + "")}|<[^>]+>",//\*|<[^>]+>
            Insert_Group_Value => Regex.Escape(INSERT_SELF + ""),//$
            _ => "",
        };
        public static string ColorFormat(string format)
        {
            var arr = ChunkItAll(format);
            format = "";
            foreach (var chunk in arr)
                    format += ColorFormatChunk(chunk);
            return format;
        }
        public static string ColorFormatChunk((Match, ChunkType) chunk) => InitFromGivenChunk(chunk, out _).GetColorDisplay();
        public static string ColorFormatChunk(string text, ChunkType ct)
        {
            PluginConfig pc = PluginConfig.Instance;
            //Plugin.Log.Info($"Coloring chunk: {{Type: {ct}, Text: {text}}}");
            string outp;
            switch (ct)
            {
                case Regular_Text:
                    return "<color=white>" + text.Replace("\\n", $"{ConvertColorToMarkup(pc.SpecialCharacterColor)}\\n</color>");
                case Escaped_Character:
                    return $"{ColorSpecialChar(ESCAPE_CHAR)}{ConvertColorToMarkup(pc.AliasColor)}{text[1]}";
                case Escaped_Token:
                case Group_Open:
                    int paramIndex = text.IndexOf(PARAM_OPEN);
                    return ColorSpecialChar(text[0]) + (paramIndex > -1 ? ColorEscapeToken(text.Substring(1, paramIndex - 1)) + ColorParams(text) : ColorEscapeToken(text.Substring(1)));
                case Capture_Open:
                    return $"{ColorSpecialChar(CAPTURE_OPEN)}{ConvertColorToMarkup(pc.CaptureIdColor)}{text.Substring(1)}";
                case Capture_Close:
                    return ColorSpecialChar(CAPTURE_CLOSE);
                case Group_Close:
                    return ColorSpecialChar(GROUP_CLOSE);
                case Rich_Text_Open:
                    int index;
                    if (text[0] == '<')
                    {
                        index = text.IndexOf('=');
                        return $"{ConvertColorToMarkup(pc.ShorthandColor)}<{ConvertColorToMarkup(pc.SpecialCharacterColor)}{text.Substring(1, index - 1)}" + 
                            $"{ConvertColorToMarkup(pc.DelimeterColor)}={ConvertColorToMarkup(pc.ParamVarColor)}{text.Substring(index)}{ConvertColorToMarkup(pc.ShorthandColor)}>";
                    }
                    index = text.IndexOf(DELIMITER);
                    outp = $"{ColorSpecialChar(RICH_SHORT)}{{0}}{ColorSpecialChar(DELIMITER)}{ConvertColorToMarkup(pc.ParamVarColor)}{text.Substring(index + 1, text.Length - index - 2)}{ColorSpecialChar(RICH_SHORT)}";
                    text = text.Substring(1, index - 1);
                    return RICH_SHORTHANDS.ContainsValue(text) ? string.Format(outp, RICH_SHORTHANDS.First(p => p.Value.Equals(text)).Key) : string.Format(outp, text);
                case Rich_Text_Close:
                    return ColorSpecialChar(RICH_SHORT);
                case Insert_Group_Value:
                    return ColorSpecialChar(INSERT_SELF);
                default: return text;
            }
        }
        private static string ColorEscapeToken(string text)
        {
            if (text[0] == ALIAS)
                return string.Format(ColorDefaultFormatToColor("'cAlias0'"), ConvertFromAlias(text));
            return string.Format(ColorFormatToColor("'cAlias0'"), GetKeyFromDictionary(AliasConverter, text[0]));
        }
        private static string ColorParams(string text)
        {
            string outp = ColorSpecialChar(PARAM_OPEN);
            text = text.Substring(text.IndexOf(PARAM_OPEN) + 1);
            text = text.Substring(0, text.Length - 1); //Remove closing param char
            string[] parameters = text.Split(DELIMITER);
            outp += ColorEscapeToken(parameters[0]);
            for (int i = 1; i < parameters.Length; i++)
                outp += $"{ColorSpecialChar(DELIMITER)}{ColorEscapeToken(parameters[i])}";
            return outp + ColorSpecialChar(PARAM_CLOSE);
        }
#endregion
#endregion
        #region UI Functions
        [UIAction(nameof(Centerer))] private string Centerer(string strIn) => $"<align=\"center\">{strIn}";
        [UIAction(nameof(MoveChunkUp))] private void MoveChunkUp()
        {
            int index = ParentList.IndexOf(this);
            if (index > 0)
            {
                FormatListInfo other = ParentList[index - 1] as FormatListInfo;
                ParentList[index] = other;
                ParentList[index - 1] = this;
                AboveInfo = other.AboveInfo;
                other.AboveInfo = this;
                UpdateTable();
                FormatEditorHandler.Instance.GotoCell(index - 1);
            }
        }
        [UIAction(nameof(MoveChunkDown))] private void MoveChunkDown()
        {
            int index = ParentList.IndexOf(this);
            if (index < ParentList.Count - 1)
            {
                FormatListInfo other = ParentList[index + 1] as FormatListInfo;
                ParentList[index] = other;
                ParentList[index + 1] = this;
                other.AboveInfo = AboveInfo;
                AboveInfo = other;
                UpdateTable();
                FormatEditorHandler.Instance.GotoCell(index + 1);
            }
        }
        [UIAction(nameof(RemoveChunk))] private void RemoveChunk()
        {
            int index = ParentList.IndexOf(this);
            if (index < ParentList.Count - 1)
                (ParentList[index + 1] as FormatListInfo).AboveInfo = index > 0 ? (ParentList[index - 1] as FormatListInfo) : null;
            TellParentTheyHaveAChild(true);
            ParentList.Remove(this);
            UpdateTable();
            FormatEditorHandler.Instance.GotoCell(index);
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
        public void Selected() => SetBGColor(SelectedColor);
        public void ResetBackgroundColor() => SetBGColor(OriginalColor);
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
            if (parent != null && parent.Chunk == open) parent.Child = childIsDead ? null : this;
        }
        private void DoSomethingOnPropertyChange(object sender, PropertyChangedEventArgs e) 
        {
            if (e.PropertyName.Equals(nameof(Text))) SetParentToken();
            if (e.PropertyName.Equals(nameof(AboveInfo))) TellParentTheyHaveAChild();
            else UpdatePreview?.Invoke(); //All other properties, when changed, affect the preview.
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
                    ChoiceOptions = [.. SPECIAL_CHARS.Select(c => c.ToString()).Cast<object>()];
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
                if (!Choicer.Values.Contains(Text)) { Choicer.Value = Choicer.Values[0]; Text = Choicer.Values[0] as string; }  //1.37.0 and above */
#else
                Choicer.values = ChoiceOptions;
                if (!Choicer.values.Contains(Text)) { Choicer.Value = Choicer.values[0]; Text = Choicer.values[0] as string; }  //1.34.2 and below */
#endif
                else Choicer.Value = Text;
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
                if (!outp) error = "Cannot declare child then not accept that it exists";
                goto End;
            }
            if ((parents & Chunk) != 0) 
            {
                outp = HasChild;
                if (!outp) error = "Parents must have a children (are you missing a closing bracket?)";
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
            ChunkType open, close;
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
            FormatListInfo parent = AboveInfo;
            while (parent != null && ((open | close) & parent.Chunk) == 0) parent = parent.AboveInfo;
            outp = parent != null && (parent.Chunk & open) != 0;
            if (!outp) error = "No valid parent found for this chunk (are you missing an opening bracket?)";
            //---------------------------------------------------------------------------------------

            //End
            //---------------------------------------------------------------------------------------
            End:
            if (!outp) 
                SetBGColor(ErrorColor);
            return outp;
            //---------------------------------------------------------------------------------------
        }
        public string GetDisplay() => Chunk switch
        {
            Regular_Text => Text,
            Escaped_Character => $"{ESCAPE_CHAR}{Text}",
            Escaped_Token => $"{ESCAPE_CHAR}{ALIAS}{Text}{ALIAS}{(TokenParams is not null ? TokenParametersToString() : "")}",
            Capture_Open => $"{CAPTURE_OPEN}{Text2}",
            Capture_Close => CAPTURE_CLOSE.ToString(),
            Group_Open => $"{GROUP_OPEN}{ALIAS}{Text}{ALIAS}{(TokenParams is not null ? TokenParametersToString() : "")}",
            Group_Close => GROUP_CLOSE.ToString(),
            Rich_Text_Open => string.Format($"{RICH_SHORT}{{0}}{DELIMITER}{Text2}{RICH_SHORT}", RICH_SHORTHANDS.ContainsValue(Text) ? RICH_SHORTHANDS.First(p => p.Value.Equals(Text)).Key : Text),
            Rich_Text_Close => RICH_SHORT.ToString(),
            Insert_Group_Value => INSERT_SELF.ToString(),
            _ => "",
        };
        public string GetColorDisplay() => ColorFormatChunk(GetDisplay(), Chunk);
        public string TokenParametersToString() => $"{PARAM_OPEN}{string.Join($"{DELIMITER}", TokenParams.Select(p => $"{ALIAS}{p}{ALIAS}"))}{PARAM_CLOSE}";
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
            Insert_Group_Value = 512
        }
        #endregion
    }
}
