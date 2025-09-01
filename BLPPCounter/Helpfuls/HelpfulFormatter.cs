using ModestTree;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static BLPPCounter.Helpfuls.HelpfulMisc;
using System.Collections;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulFormatter
    {
        private static PluginConfig PC => PluginConfig.Instance;

        public static readonly int FORMAT_SPLIT = 100;
        public static int GRAD_VARIANCE => PC.ColorGradMaxDiff;
        public static char ESCAPE_CHAR => PC.TokenSettings.EscapeCharacter;
        public static char RICH_SHORT => PC.TokenSettings.RichTextShorthand;
        public static char DELIMITER => PC.TokenSettings.Delimiter;
        public static char GROUP_OPEN => PC.TokenSettings.GroupBracketOpen;
        public static char INSERT_SELF => PC.TokenSettings.GroupInsertSelf;
        public static char GROUP_CLOSE => PC.TokenSettings.GroupBracketClose;
        public static char CAPTURE_OPEN => PC.TokenSettings.CaptureBracketOpen;
        public static char CAPTURE_CLOSE => PC.TokenSettings.CaptureBracketClose;
        public static char PARAM_OPEN => PC.TokenSettings.EscapeCharParamBracketOpen;
        public static char PARAM_CLOSE => PC.TokenSettings.EscapeCharParamBracketClose;
        public static char ALIAS => PC.TokenSettings.NicknameIndicator;
        public static readonly HashSet<char> SPECIAL_CHARS, ALL_SPECIAL_CHARS; 
        //difference is SPECIAL_CHARS only contains characters that modifies the format by itself, unlike chars like OPEN_PARAM which are normal unless used after an escaped token.
        public static Dictionary<string, string> RICH_SHORTHANDS => PC.TokenSettings.RichShorthands;
        public static string NUMBER_TOSTRING_FORMAT { get; internal set; }
        public static readonly Dictionary<string, char> GLOBAL_ALIASES;
        public static readonly Func<char, int> GLOBAL_PARAM_AMOUNT = c => GetGlobalParamAmount(c);

        private static readonly string RegexAliasPattern = "({0}.|{0}{2}[^{2}]+{2}){3}(.+)(?={4})|([{0}{1}]{2}[^{2}]+?{2})";
        private static readonly string TestRegexAliasPattern = "(?<Token>[{0}{1}]{2}[^{2}]+{2})(?!{3})|(?<Token>{0}.|{0}{2}[^{2}]+{2}){3}(?<Params>[^{4}]+)";
        //(?<Token>[&[]'[^']+')(?!\()|(?<Token>&.|&'[^']+')\((?<Params>[^\)]+)
        //(?<Token>[{0}{1}]{2}[^{2}]+{2})(?!{3})|(?<Token>{0}.|{0}{2}[^{2}]+{2}){3}(?<Params>[^{4}]+)
        private static readonly string RegexAliasErrorFinder = "[{0}{1}]{2}(?=[^{2}]+?(?:[{0}{1}]|(?!.)))";
        internal static readonly string RegexAllSpecialChars, RegexSpecialChars;
        //0 = ESCAPE_CHAR, 1 = GROUP_OPEN, 2 = ALIAS, 3 = PARAM_OPEN, 4 = PARAM_CLOSE (for both expressions above)

        static HelpfulFormatter()
        {
            RegexAliasPattern = string.Format(RegexAliasPattern, Regex.Escape(ESCAPE_CHAR + ""), Regex.Escape(GROUP_OPEN + ""), Regex.Escape(ALIAS + ""), Regex.Escape(PARAM_OPEN + ""), Regex.Escape(PARAM_CLOSE + ""));
            TestRegexAliasPattern = string.Format(TestRegexAliasPattern, Regex.Escape(ESCAPE_CHAR + ""), Regex.Escape(GROUP_OPEN + ""), Regex.Escape(ALIAS + ""), Regex.Escape(PARAM_OPEN + ""), Regex.Escape(PARAM_CLOSE + ""));
            RegexAliasErrorFinder = string.Format(RegexAliasErrorFinder, Regex.Escape(ESCAPE_CHAR + ""), Regex.Escape(GROUP_OPEN + ""), Regex.Escape(ALIAS + ""), Regex.Escape(PARAM_OPEN + ""));
            string hold = "";
            for (int i = 0; i < PC.DecimalPrecision; i++) hold += "#";
            NUMBER_TOSTRING_FORMAT = PC.DecimalPrecision > 0 ? PC.FormatSettings.NumberFormat.Replace("#", "#." + hold) : PC.FormatSettings.NumberFormat;
            SPECIAL_CHARS = new HashSet<char>() { ESCAPE_CHAR, RICH_SHORT, GROUP_OPEN, GROUP_CLOSE, CAPTURE_OPEN, CAPTURE_CLOSE };
            ALL_SPECIAL_CHARS = new HashSet<char> { ESCAPE_CHAR, RICH_SHORT, DELIMITER, GROUP_OPEN, GROUP_CLOSE, INSERT_SELF, CAPTURE_OPEN, CAPTURE_CLOSE, PARAM_OPEN, PARAM_CLOSE, ALIAS };
            RegexAllSpecialChars = "[" + string.Join("", ALL_SPECIAL_CHARS).Replace("]", "\\]") + "]";
            RegexSpecialChars = "[" + string.Join("", SPECIAL_CHARS).Replace("]", "\\]") + "]";
            GLOBAL_ALIASES = new Dictionary<string, char>()
            {
                {"Dynamic s", 's' },
                {"Hide", 'h' }
            };
        }
        public static (string, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<(char, int), string[]>) ParseCounterFormat(string format, Dictionary<string, char> aliasConverter, string counterName)
        {
            Dictionary<(char, int), string> tokens = new Dictionary<(char, int), string>();
            Dictionary<(char, int), string[]> extraArgs = new Dictionary<(char, int), string[]>();
            Dictionary<int, char> priority = new Dictionary<int, char>();
            aliasConverter = new Dictionary<string, char>(aliasConverter); //so that it doesn't edit the original

            foreach (var e in GLOBAL_ALIASES) aliasConverter.TryAdd(e.Key, e.Value);
            CustomAlias.ApplyAliases(PluginConfig.Instance.TokenSettings.TokenAliases, aliasConverter, counterName);

            string formatted = "";
            int repIndex = 0, forRepIndex = 0, sortIndex = 0;
            bool capture = false;
            string captureStr = "", richVal = "";
            int ssIndex = -1;
            char num = (char)0;
            if (FindAliasErrorsInFormat(format, out string errorMessage))
                throw new FormatException(errorMessage);
            if (format.Contains($"{ESCAPE_CHAR}{ALIAS}") || format.Contains($"{GROUP_OPEN}{ALIAS}"))
            {
                if (aliasConverter == null)
                    throw new ArgumentNullException("No alias converter given while format contains aliases! Please remove aliases from the format as there is no way to parse them.\nFormat: " + format);
                string AliasReplace(Match m)//(?<Token>[&[]'[^']+')(?!\()|(?<Token>&.|&'[^']+')\((?<Params>[^\)]+)
                {
                    bool invalidName = false;
                    string val = m.Groups["Token"].Value;
                    char t;
                    if (val[1] == ALIAS) if (aliasConverter.TryGetValue(val.Substring(2, val.Length - 3), out t)) val = $"{val[0]}{t}";
                        else invalidName = true;
                    string outp = val;
                    if (!invalidName && m.Groups["Params"].Success)
                    {
                        outp += PARAM_OPEN;
                        foreach (string param in m.Groups["Params"].Value.Split(DELIMITER))
                            if (param[0] == ALIAS)
                            {
                                if (aliasConverter.TryGetValue(param.Substring(1, param.Length-2), out t)) outp += $"{t}{DELIMITER}";
                                else { val = param.Substring(1, param.Length - 2); invalidName = true; break; }
                            }
                            else outp += $"{param}{DELIMITER}";
                        outp = outp.Substring(0, outp.Length - 1);
                    }
                    if (invalidName) throw new FormatException($"Incorrect aliasing used. The alias name '{val}' does not exist for {counterName} counter." +
                        $"\nCorrect Format: {ESCAPE_CHAR}{ALIAS}<Alias Name>{ALIAS} OR {GROUP_OPEN}{ALIAS}<Alias Name>{ALIAS} ... {GROUP_CLOSE}" +
                        $"\nPossible alias names are listed below:\n{string.Join("\n", aliasConverter).Replace("[", "\"").Replace("]", "").Replace(", ", "\" as ")}");
                    return outp;
                }
                format = Regex.Replace(format, TestRegexAliasPattern, AliasReplace);
            }//*/
            if (aliasConverter == null)
                Plugin.Log.Debug("No alias converter given! Thankfully, there are no aliases present so there will not be an error.");
            for (int i = 0; i < format.Length; i++)//[p$ ]&[[c&x]&]<1 / [o$ ]&[[f&y]&] >&l<2\n&m[t\n$]>
            {
                if (Regex.Match(format.Substring(i), "^<(?:(?<Key>[^=]+)=[^>]+>(?=.*?<\\/\\k<Key>>)|\\/[^>]+>)", RegexOptions.Singleline) is Match m && m.Success)
                {
                    if (capture)
                        captureStr += format.Substring(i, m.Length);
                    else
                        formatted += format.Substring(i, m.Length);
                    i += m.Length - 1;
                    //Plugin.Log.Info("Formatted: " + formatted + " || i = " + i);
                    continue;
                }
                if (!IsSpecialChar(format[i]) ||
                    (format[i] == ESCAPE_CHAR && (IsSpecialChar(format[i + 1]) || format[i + 1] == PARAM_OPEN)))
                {
                    if (format[i] == ESCAPE_CHAR) i++;
                    if (capture)
                        captureStr += format[i];
                    else
                        formatted += format[i];
                    continue;
                }
                if (format[i] == RICH_SHORT)
                {
                    string toSet = ReplaceShorthand(format, richVal, i, out i, out richVal);
                    if (capture) captureStr += toSet; else formatted += toSet;
                    continue;
                }
                if (!capture) formatted += $"{{{forRepIndex++}}}";
                if (format[i] == GROUP_OPEN)
                {
                    string bracket = "";
                    char symbol = format[++i];
                    if (!char.IsLetter(symbol) || symbol == RICH_SHORT)
                    {
                        bool isRich = symbol == RICH_SHORT, badFormat = false;
                        if (isRich || symbol == '<')
                        {
                            if (isRich) bracket += ReplaceShorthand(format, richVal, i, out i, out richVal);
                            else
                                if (char.IsLetter(format[i + 1]))
                                {
                                    while (format[i] != '>') bracket += format[i++];
                                    bracket += format[i++]; //grabs closing bracket
                                    Plugin.Log.Info("Formatted: " + bracket + " || i = " + i);
                            }
                            else badFormat = true;
                        }
                        else badFormat = true;
                        if (badFormat) throw new FormatException($"Invalid group format, must define what letter group corresponds to.\nSyntax: {GROUP_OPEN}<letter> ... {GROUP_CLOSE}");
                    }
                    if (i + 1 < format.Length && format[i + 1] == PARAM_OPEN)
                    {
                        var vals = HandleExtraParams(format, i, sortIndex, out i);
                        extraArgs.Add((vals.Item1, vals.Item2), vals.Item3);
                    }
                    int index = repIndex++, sIndex = sortIndex++;
                    while (format[++i] != GROUP_CLOSE && i < format.Length)
                    {
                        if (format[i] == INSERT_SELF) { bracket += $"{{{index}}}"; continue; }
                        if (format[i] == RICH_SHORT) { bracket += ReplaceShorthand(format, richVal, i, out i, out richVal); continue; }
                        if (format[i] == ESCAPE_CHAR)
                        {
                            if (!IsSpecialChar(format[i + 1]))
                            {
                                tokens[(format[++i], FORMAT_SPLIT + sortIndex)] = $"{{{repIndex}}}";
                                priority[FORMAT_SPLIT + sortIndex++] = format[i];
                                bracket += $"{{{repIndex++}}}";
                                if (i + 1 < format.Length && format[i + 1] == PARAM_OPEN)
                                {
                                    var vals = HandleExtraParams(format, i, sortIndex - 1 + FORMAT_SPLIT, out i);
                                    extraArgs.Add((vals.Item1, vals.Item2), vals.Item3);
                                }
                                    
                            } else bracket += format[++i];
                            continue;
                        }
                        else bracket += format[i];
                    }
                    if (i == format.Length)
                        throw new FormatException($"Invalid group format, must close group bracket.\nSyntax: {GROUP_OPEN}<letter> ... {GROUP_CLOSE}");
                    if (sortIndex == sIndex) sortIndex++;
                    if (repIndex == index) repIndex++;
                    if (capture)
                    {
                        captureStr += $"{ESCAPE_CHAR}{symbol}";
                        sIndex += FORMAT_SPLIT;
                    }
                    priority[sIndex] = symbol;
                    tokens[(symbol, sIndex)] = bracket;

                    continue;
                }
                if (format[i] == CAPTURE_OPEN || format[i] == CAPTURE_CLOSE)
                {
                    if (!capture)
                    {
                        capture = true;
                        captureStr = "";
                        ssIndex = sortIndex++;
                        num = format[++i];
                        if (!char.IsDigit(num))
                            throw new FormatException($"Invalid capture format, must have number after open bracket.\nSyntax: {CAPTURE_OPEN}<number> ... {CAPTURE_CLOSE}");
                        continue;
                    }
                    else
                    {
                        if (format[i] != CAPTURE_CLOSE)
                            throw new FormatException($"Invalid capture format, you cannot nest capture statements.\nSyntax: {CAPTURE_OPEN}<number> ... {CAPTURE_CLOSE}");
                        capture = false;
                        tokens[(num, ssIndex)] = captureStr;
                        priority[ssIndex] = num;
                        continue;
                    }
                }
                int tempIndex = sortIndex++;
                i++;
                if (capture)
                {
                    captureStr += $"{ESCAPE_CHAR}{format[i]}";
                    tempIndex += FORMAT_SPLIT;
                }
                if (!char.IsLetter(format[i]))
                    throw new FormatException($"Invalid escape format, escape character must be followed by a special character or a letter.\nSyntax: {ESCAPE_CHAR}<letter> OR {ESCAPE_CHAR}<special character>");
                tokens[(format[i], tempIndex)] = $"{{{repIndex++}}}";
                priority[tempIndex] = format[i];
                if (i + 1 < format.Length && format[i+1] == PARAM_OPEN)
                {
                    var vals = HandleExtraParams(format, i, tempIndex, out i);
                    extraArgs.Add((vals.Item1, vals.Item2), vals.Item3);
                }
            }
            if (capture)
                throw new FormatException($"Invalid capture format, must close capture bracket.\nSyntax: {CAPTURE_OPEN}<number> ... {CAPTURE_CLOSE}");
            return (formatted, tokens, priority, extraArgs);
        }
        private static string ReplaceShorthand(string format, string richVal, int i, out int newCount, out string newRichVal)
        {
            if (!richVal.IsEmpty())
            {
                newRichVal = "";
                newCount = i;
                return richVal;
            }
            string keyword = "", value = "";
            while (++i < format.Length && format[i] != DELIMITER) keyword += format[i];
            if (i >= format.Length) throw new FormatException($"Invalid rich text shorthand, must put the delimiter ({DELIMITER}) between the keyword and value.");
            keyword = ConvertRichShorthand(keyword);
            newRichVal = $"</{keyword}>";
            while (++i < format.Length && format[i] != RICH_SHORT) value += format[i];
            if (i >= format.Length) throw new FormatException($"Invalid rich text shorthand, must put the rich shorthand symbol ({RICH_SHORT}) between the value and the contents.");
            if (value.Contains(' ')) value = $"\"{value}\"";
            newCount = i;
            return $"<{keyword}={value}>";
        }
        private static (char, int, string[]) HandleExtraParams(string format, int i, int priority, out int newCount)
        {
            char originChar = format[i];
            List<string> inp = new List<string>();
            i++;
            while (i < format.Length && format[i] != PARAM_CLOSE)
            {
                string hold = "";
                while (++i < format.Length && format[i] != DELIMITER && format[i] != PARAM_CLOSE)
                    hold += format[i];
                inp.Add(hold);
            }
            if (i >= format.Length)
                throw new FormatException($"Invalid extra parameter format, missing closing bracket ('{PARAM_CLOSE}').\nSyntax: {ESCAPE_CHAR}{originChar}{PARAM_OPEN}<character 1>{DELIMITER},<character 2>{DELIMITER}...{PARAM_CLOSE}");
            newCount = i;
            return (originChar, priority, inp.ToArray());
        }
        public static bool FindAliasErrorsInFormat(string format, out string errorMessage) //true = IS errors, false = NO errors
        {
            if (Regex.Matches(format, RegexAliasErrorFinder) is MatchCollection badMc && badMc.Count > 0)
            {
                errorMessage = "Incorrect alias format, there is missing closing brackets in the following places:\n";
                int surroundLength = format.Length / 5; //this is an arbitrary number, but after tweaking around I found it to look pretty good.
                foreach (Match match in badMc)
                {
                    int index = match.Index + 1;
                    errorMessage += index >= surroundLength ? format.Substring(index - surroundLength, surroundLength + 1) : index > 0 ? format.Substring(0, index) : "";
                    errorMessage += "HERE -->";
                    errorMessage += format.Substring(index, match.Length + surroundLength + index > format.Length ? format.Length - index : match.Length + surroundLength);
                    if (index > surroundLength) errorMessage = "..." + errorMessage;
                    if (match.Length + surroundLength + index <= format.Length) errorMessage += "...";
                    errorMessage += "\n";
                }
                return true;
            }
            errorMessage = "";
            return false;
        }
        public static Func<Func<Dictionary<char, object>, string>> GetBasicTokenParser(
            string format,
            Dictionary<string, char> aliasConverter,
            string counterName,
            Action<TokenParser> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings,
            out string errorStr,
            bool applySettings = true)
            => GetBasicTokenParser(format, aliasConverter, counterName, settings, varSettings, null, null, out errorStr, applySettings);
        public static Func<Func<Dictionary<char, object>, string>> GetBasicTokenParser(
            string format,
            Dictionary<string, char> aliasConverter,
            string counterName,
            Action<TokenParser> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings,
            Func<char, string[], bool> confirmFormat,
            Func<char, string[], Dictionary<char, object>, Dictionary<(char, int), string>, string> implementArgs,
            out string errorStr,
            bool applySettings = true)
        {
            Dictionary<(char, int), string> tokens;
            Dictionary<(char, int), string[]> extraArgs;
            Dictionary<int, char> priority;
            string formatted;
            confirmFormat = GetParentConfirmFormat(confirmFormat);
            implementArgs = GetParentImplementArgs(implementArgs);
            try
            {
                (formatted, tokens, priority, extraArgs) = ParseCounterFormat(format, new Dictionary<string, char>(aliasConverter), counterName);
                foreach (var val in extraArgs.Keys)
                {
                    if (!confirmFormat.Invoke(val.Item1, extraArgs[val]))
                        throw new FormatException($"Invalid extra parameter format, one of three things happened, too many arguments, too few arguments, or a non reference where a reference should be.\nThis is for the char '{val.Item1}'");
                }
            }
            catch (Exception e)
            {
                errorStr = "Formatting failed! " + e.Message;
                errorStr += "\nFormatting: " + ToLiteral(format).Replace("\\'", "'");
                Plugin.Log.Error(errorStr);
                //throw new FormatException(errorStr);
                return null;
            }
            errorStr = "";
            /*Plugin.Log.Info("---------------");
                    foreach (var token in tokens)
                        Plugin.Log.Info($"{token.Key} || " + ToLiteral(token.Value));
                    Plugin.Log.Info("Formatted || " + formatted);//*/
            return () =>
            {
                var thing = TokenParser.UnrapTokens(tokens, true, formatted);
                if (applySettings) settings.Invoke(thing);
                string newFormatted = thing.Formatted;
                Dictionary<(char, int), string> tokensCopy1 = thing.GetReference();
                //Plugin.Log.Info(thing.ToString());
                List <(char, int)> first = new List<(char, int)>();
                List<(char, int)> second = new List<(char, int)>();
                List<(char, int)> captureChars = new List<(char, int)>();
                foreach ((char, int) val in tokensCopy1.Keys)
                {
                    if (char.IsDigit(val.Item1)) { captureChars.Add(val); first.Add(val); continue; }
                    if (val.Item2 < FORMAT_SPLIT) { first.Add(val); second.Add(val); }
                    else second.Add((val.Item1, val.Item2 - FORMAT_SPLIT));
                }
                second.Sort((a, b) => a.Item2 - b.Item2);
                first.Sort((a, b) => a.Item2 - b.Item2);
                /*Plugin.Log.Info(string.Join(",", first));
                Plugin.Log.Info(string.Join(",", second));//*/
                return (vals) =>
                {
                    /*Plugin.Log.Info("---------------");
                    foreach (var token in tokensCopy1)
                        Plugin.Log.Info($"{token.Key} || " + ToLiteral(token.Value));
                    Plugin.Log.Info("Formatted || " + formatted);//*/
                    Dictionary<(char, int), string> tokensCopy2 = new Dictionary<(char, int), string>(tokensCopy1);
                    varSettings.Invoke(tokensCopy1, tokensCopy2, priority, vals);
                    foreach (var val in extraArgs.Keys)
                        vals.TryAdd(val.Item1, implementArgs.Invoke(val.Item1, extraArgs[val], vals, tokensCopy2));
                    foreach ((char, int) val in captureChars)
                    {
                        string newVal = "", toParse = tokensCopy2[val];
                        int priorityCount = val.Item2;
                        if (toParse.Length == 0) continue;
                        for (int j = 0; j < toParse.Length; j++)
                            if (toParse[j] == ESCAPE_CHAR)
                            {
                                string toTry = null;
                                char temp = toParse[++j];
                                while (!tokensCopy2.TryGetValue((temp, ++priorityCount + FORMAT_SPLIT), out toTry));
                                newVal += toTry;
                            }
                            else newVal += toParse[j];
                        tokensCopy2[val] = newVal;
                    }
                    object[] firstArr = new object[first.Count];
                    int i = 0;
                    foreach ((char, int) val in first) firstArr[i++] = tokensCopy2[val];
                    object[] secondArr = new object[second.Count];
                    i = 0;
                    foreach ((char, int) val in second) secondArr[i++] = vals[val.Item1];
                    return string.Format(string.Format(newFormatted, firstArr), secondArr);
                };
            };
        }
        public static int GetGlobalParamAmount(char paramChar)
        {
            switch (paramChar)
            {
                case 's': return 1;
                case 'h': return 2;
                default: return 0;
            }
        }
        private static Func<char, string[], bool> GetParentConfirmFormat(Func<char, string[], bool> child)
        {
            return (paramChar, values) =>
            {
                if (child != null && child.Invoke(paramChar, values)) return true;
                switch (paramChar)
                {
                    case 's':
                        return values.Length == 1 && IsReferenceChar(values[0]);
                    case 'h':
                        return (values.Length == 1 && IsReferenceChar(values[0])) || (values.Length == 2 && IsReferenceChar(values[0]) && decimal.TryParse(values[1], out _));
                    default: return false;
                }
            };
        }
        private static Func<char, string[], Dictionary<char, object>, Dictionary<(char, int), string>, string> GetParentImplementArgs
            (Func<char, string[], Dictionary<char, object>, Dictionary<(char, int), string>, string> child)
        {
            return (paramChar, values, vals, tokens) =>
            {
                if (child != null) {
                    string outp = child.Invoke(paramChar, values, vals, tokens);
                    if (outp.Length != 0) return outp;
                }
                switch (paramChar)
                {
                    case 's':
                        return decimal.Parse(vals[values[0][0]] + "") == 1 ? "" : "s";
                    case 'h':
                        decimal toCompare = values.Length == 2 ? decimal.Parse(values[1]) : 0;
                        if (decimal.Parse(vals[values[0][0]] + "") <= toCompare)
                        {
                            List<(char, int)> arr = tokens.Keys.Where(a => a.Item1 == values[0][0]).ToList();
                            foreach ((char, int) item in arr)
                                tokens[item] = "";
                        }
                        return "";
                    default: return "";
                }
            };
        }
        public static void SetText(Dictionary<(char, int), string> tokens, char c, string text = "") 
        { 
            List<(char, int)> toModify = new List<(char, int)>();
            foreach (var item in tokens.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2));
            foreach (var item in toModify)
                if (item.Item2 < FORMAT_SPLIT || text.IsEmpty())
                    tokens[(item.Item1, item.Item2)] = text;
                else
                    tokens[(item.Item1, item.Item2)] = Regex.Replace(tokens[(item.Item1, item.Item2)], "\\{\\d\\}", text);
            
        }
        public static void SurroundText(Dictionary<(char, int), string> tokens, char c, string preText, string postText) 
        {
            IEnumerable<int> keys = new List<int>(tokens.Keys.Where(val => val.Item1 == c).Select(val => val.Item2)); 
            //The lengths I have to to avoid cocerrent modification exceptions :(
            foreach (var item in keys)
                tokens[(c, item)] = preText + tokens[(c, item)] + postText;
        }
        public static bool IsReferenceChar(string s) => s.Length == 1 && char.IsLetter(s[0]) && !IsSpecialChar(s[0]);
        public static bool IsSpecialChar(char c) => SPECIAL_CHARS.Contains(c);
        public static string NumberToColor(float num) => num > 0 ? "<color=green>" : num == 0 ? "<color=yellow>" : "<color=red>";
        public static string EscapeNeededChars(string str)
        {
            string outp = "";
            foreach (char c in str)
            {
                if (IsSpecialChar(c)) outp += ESCAPE_CHAR;
                outp += c;
            }
            return outp;
        }
        public static string ConvertRichShorthand(string shorthand)
        {
            if (RICH_SHORTHANDS.Keys.Contains(shorthand)) return RICH_SHORTHANDS[shorthand];
            return shorthand;
        }
        public static string NumberToGradient(float variance, float num)
        {
            if (num == 0)
                if (PC.ColorGradBlending)
                    num = variance / 2f;
                else return ConvertColorToMarkup(PC.ColorGradZero);
            bool neg = num < 0;
            float toConvert = Math.Min(Math.Max(Math.Abs(neg && !PC.ColorGradBlending ? 1.0f - -num / variance : num / variance), PC.ColorGradMinDark) + PC.ColorGradFlipPercent, 1f);
            if (PC.ColorGradBlending)
                return ConvertColorToMarkup(Blend(PC.ColorGradMin, PC.ColorGradMax, neg ? toConvert : 1.0f - toConvert, neg ? 1.0f - toConvert : toConvert));
            return neg ? ConvertColorToMarkup(Multiply(PC.ColorGradMin, toConvert)) :
                ConvertColorToMarkup(Multiply(PC.ColorGradMax, toConvert));
        }
        public static string NumberToGradient(float num) => NumberToGradient(GRAD_VARIANCE, num);
        public static string GetWeightedRankColor(int rank)
        {
            int c = -1;
            var arr = PluginConfig.Instance.FormatSettings.WeightedRankColors.ToArray();
            while (arr[++c].Rank < rank && c + 1 < arr.Length) ;
            return "<color=#" + arr[c].Color + ">";
        }
        public static string DefaultToUsedChar(string str) => Regex.Replace(str, "[&*,[\\]$<>()']", m => ""+DefaultToUsedChar(m.Value[0]));
        public static char DefaultToUsedChar(char c)
        {
            switch (c)
            {
                case '&': return ESCAPE_CHAR;
                case '*': return RICH_SHORT;
                case ',': return DELIMITER;
                case '[': return GROUP_OPEN;
                case ']': return GROUP_CLOSE;
                case '$': return INSERT_SELF;
                case '<': return CAPTURE_OPEN;
                case '>': return CAPTURE_CLOSE;
                case '(': return PARAM_OPEN;
                case ')': return PARAM_CLOSE;
                case '\'': return ALIAS;
                default: return c;
            }
        }
        public static string ColorSpecialChar(char c)
        {
            switch (c)
            {
                case char v when v == ESCAPE_CHAR: return $"{ConvertColorToMarkup(PC.EscapeCharacterColor)}{v}";
                case char v when v == RICH_SHORT: return $"{ConvertColorToMarkup(PC.ShorthandColor)}{v}";
                case char v when v == DELIMITER: return $"{ConvertColorToMarkup(PC.DelimeterColor)}{v}";
                case char v when v == GROUP_OPEN || v == GROUP_CLOSE: return $"{ConvertColorToMarkup(PC.GroupColor)}{v}";
                case char v when v == INSERT_SELF: return $"{ConvertColorToMarkup(PC.GroupReplaceColor)}{v}";
                case char v when v == CAPTURE_OPEN || v == CAPTURE_CLOSE: return $"{ConvertColorToMarkup(PC.CaptureColor)}{v}";
                case char v when v == PARAM_OPEN || v == PARAM_CLOSE: return $"{ConvertColorToMarkup(PC.ParamColor)}{v}";
                case char v when v == ALIAS: return $"{ConvertColorToMarkup(PC.AliasQuoteColor)}{v}";
                default: throw new ArgumentException("Character given is not special");
            }
        }
        public static string ColorDefaultFormatToColor(string str) => ColorFormatToColor(DefaultToUsedChar(str));
        public static string ColorFormatToColor(string str)
        {
            string Converter(Match m) {
#if NEW_VERSION
                string name = m.Groups.First(g => g.Success && !char.IsDigit(g.Name[0])).Name; // 1.37.0 and above
#else
                string name = m.Groups.OfType<Group>().First(g => g.Success && !char.IsDigit(g.Name[0])).Name; //1.34.2 and below
#endif
                switch (name)
                {
                    case "Special": return ColorSpecialChar(m.Value[0]);
                    case "Color": return ConvertColorToMarkup(PluginConfig.Instance.GetColorFromName(m.Value.Substring(1)));
                    case "Replace": return "{" + m.Value + "}";
                    default: return m.Value;
                }
            }
            return Regex.Replace(str, "(?<Special>" + RegexAllSpecialChars + ")|c(?<Color>[A-Z][a-z]+)|(?<Replace>\\d)", Converter);
        }
        public class TokenParser
        {
            private readonly Dictionary<char, List<int>> topLevelTokens, bottomLevelTokens;
            private readonly Dictionary<(char, int), List<(char, int)>> tokenRelations;
            private readonly Dictionary<(char, int), string> tokenValues;
            public string Formatted { get; private set; }

            private TokenParser(Dictionary<char, List<int>> topLevelTokens, Dictionary<char, List<int>> bottomLevelTokens,
                Dictionary<(char, int), List<(char, int)>> tokenRelations, Dictionary<(char, int), string> tokenValues)
            {
                this.topLevelTokens = topLevelTokens;
                this.bottomLevelTokens = bottomLevelTokens;
                this.tokenRelations = tokenRelations;
                this.tokenValues = tokenValues;
                Formatted = "";
            }

            public static TokenParser UnrapTokens(Dictionary<(char, int), string> tokens, bool makeNewReference = true, string formatted = "")
            {
                Dictionary<char, List<int>> topLevelTokens = new Dictionary<char, List<int>>(), bottomLevelTokens = new Dictionary<char, List<int>>();
                Dictionary<(char, int), List<(char, int)>> tokenRelations = new Dictionary<(char, int), List<(char, int)>>();
                List<(char, int)> sortedKeys = tokens.Keys.ToList();
                sortedKeys.Sort((a, b) => a.Item2 - b.Item2);
                foreach (var key in sortedKeys)
                    if (key.Item2 <= FORMAT_SPLIT)
                        if (!topLevelTokens.ContainsKey(key.Item1))
                            topLevelTokens.Add(key.Item1, new List<int>() { key.Item2 });
                        else
                            topLevelTokens[key.Item1].Add(key.Item2);
                    else
                        if (!bottomLevelTokens.ContainsKey(key.Item1))
                        bottomLevelTokens.Add(key.Item1, new List<int>() { key.Item2 });
                        else
                            bottomLevelTokens[key.Item1].Add(key.Item2);
                sortedKeys.Sort((a, b) => (a.Item2 > FORMAT_SPLIT ? a.Item2 - FORMAT_SPLIT : a.Item2) - (b.Item2 > FORMAT_SPLIT ? b.Item2 - FORMAT_SPLIT : b.Item2));
                (char, int) lastVal = default;
                List<(char, int)> toAdd = new List<(char, int)>();
                foreach (var key in sortedKeys)
                    if (key.Item2 <= FORMAT_SPLIT)
                    {
                        if (lastVal != default) tokenRelations.Add(lastVal, new List<(char, int)>(toAdd));
                        lastVal = key;
                        toAdd.Clear();
                    }
                    else
                        toAdd.Add(key);
                tokenRelations.Add(lastVal, new List<(char, int)>(toAdd));
                return new TokenParser(topLevelTokens, bottomLevelTokens, tokenRelations,
                    makeNewReference ? new Dictionary<(char, int), string>(tokens) : tokens) { Formatted = formatted };
            }
            public Dictionary<(char, int), string> RerapTokens() => new Dictionary<(char, int), string>(tokenValues);
            public Dictionary<(char, int), string> GetReference() => tokenValues;

            public bool MakeTokenConstant(char token, string value = "")
            {
                if (Formatted == default) return false;
                foreach (char key in topLevelTokens.Keys)
                {
                    int toRemove = -1;
                    foreach (int priority in topLevelTokens[key])
                    {
                        List<(char, int)> relations = tokenRelations[(key, priority)];
                        int index = relations.FindIndex(a => a.Item1 == token);
                        if (index == -1) continue;
                        int tokenPriority = relations[index].Item2;
                        if (char.IsDigit(key))
                        {
                            string newValue = Regex.Replace(tokenValues[(token, tokenPriority)], "\\{\\d+\\}", value);
                            if (tokenValues[(key, priority)].Contains($"{ESCAPE_CHAR}{token}"))
                                tokenValues[(key, priority)] = tokenValues[(key, priority)].Replace($"{ESCAPE_CHAR}{token}", newValue);
                            else
                            {
                                string repStr = tokenValues[(token, tokenPriority)];
                                index = relations.FindIndex(a => tokenValues[a].Equals(repStr));
                                if (index == -1) throw new ArgumentException("The token given has been parsed incorrectly somehow. There is a bug in the code somewhere.");
                                tokenValues[relations[index]] = Regex.Replace(tokenValues[relations[index]], "\\{\\d+\\}", value);
                            }
                        }
                        else
                            tokenValues[(key, priority)] = Regex.Replace(tokenValues[(key, priority)], "\\{\\d+\\}", value);
                        tokenValues.Remove((token, tokenPriority));
                        toRemove = tokenPriority;
                        break;
                    }
                    if (toRemove != -1)
                    {
                        topLevelTokens[key].Remove(toRemove);
                        int toCompare = toRemove > FORMAT_SPLIT ? toRemove - FORMAT_SPLIT : toRemove;
                        List<(char, int, int)> toSubtract = new List<(char, int, int)>();
                        foreach (var v in tokenValues)
                        {
                            var matches = Regex.Matches(v.Value, "\\{\\d+\\}");
                            if (matches.Count == 0) continue;
                            var num = v.Key.Item2 > FORMAT_SPLIT ? v.Key.Item2 - FORMAT_SPLIT : v.Key.Item2;
                            if (num > toCompare)
                                toSubtract.Add((v.Key.Item1, v.Key.Item2, int.Parse(matches[0].Value.Substring(1, matches[0].Length - 2))));
                        }
                        foreach (var v in toSubtract)
                        {
                            var val = (v.Item1, v.Item2);
                            tokenValues[val] = Regex.Replace(tokenValues[val], "\\{\\d+\\}", $"{{{v.Item3 - 1}}}");
                        }
                    }
                }
                bottomLevelTokens.Remove(token);
                return true;
            }
            public void SetText(char c, string text = "")
            {
                List<(char, int)> toModify = new List<(char, int)>();
                foreach (var item in tokenValues.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2));
                foreach (var item in toModify)
                    if (item.Item2 < FORMAT_SPLIT || text.IsEmpty())
                        tokenValues[(item.Item1, item.Item2)] = text;
                    else
                        tokenValues[(item.Item1, item.Item2)] = Regex.Replace(tokenValues[(item.Item1, item.Item2)], "\\{\\d\\}", text);
            }
            public void SurroundText(char c, string preText, string postText)
            {
                List<(char, int, string)> toModify = new List<(char, int, string)>();
                foreach (var item in tokenValues.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2, preText + tokenValues[item] + postText));
                foreach (var item in toModify) tokenValues[(item.Item1, item.Item2)] = item.Item3;
            }
            public override string ToString()
            {
                string outp = "----------------------------\nTop level tokens: ";
                foreach (var token in topLevelTokens)
                    outp += $"\n{token.Key} || [{string.Join(", ", token.Value)}]";
                outp += "\nBottom level tokens: ";
                foreach (var token in bottomLevelTokens)
                    outp += $"\n{token.Key} || [{string.Join(", ", token.Value)}]";
                outp += "\nToken relations: ";
                foreach (var token in tokenRelations)
                    outp += $"\n{token.Key.Item1} || [{string.Join(", ", token.Value)}]";
                outp += "\nToken values: ";
                foreach (var token in tokenValues)
                    outp += $"\n{token.Key} || {ToLiteral(token.Value)}";
                return outp + "\n----------------------------";
            }
        }
    }
}
