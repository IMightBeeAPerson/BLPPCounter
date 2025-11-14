using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using ModestTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static BLPPCounter.Helpfuls.HelpfulMisc;

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
        #region Parser & Helpers
        public static Tuple<string,
                    Dictionary<TokenKey, string>,
                    Dictionary<int, char>,
                    Dictionary<TokenKey, string[]>>
    ParseCounterFormat(string format, Dictionary<string, char> aliasConverter, string counterName)
        {
            string aliasError;
            if (FindAliasErrorsInFormat(format, out aliasError))
                throw new FormatException(aliasError);

            if (format.Contains(ESCAPE_CHAR.ToString() + ALIAS) ||
                format.Contains(GROUP_OPEN.ToString() + ALIAS))
                format = ExpandAliases(format, aliasConverter, counterName);

            StringBuilder formatted = new StringBuilder();
            StringBuilder captureSb = new StringBuilder();

            Dictionary<TokenKey, string> tokens = new Dictionary<TokenKey, string>();
            Dictionary<TokenKey, string[]> extraArgs = new Dictionary<TokenKey, string[]>();
            Dictionary<int, char> priority = new Dictionary<int, char>();

            int repIndex = 0;
            int forRepIndex = 0;
            int sortIndex = 0;
            bool inCapture = false;
            char captureId = '\0';
            int capturePriority = -1;
            string pendingClose = string.Empty;

            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];

                // Skip escaped specials
                if (c == ESCAPE_CHAR && i + 1 < format.Length &&
                    (IsSpecialChar(format[i + 1]) || format[i + 1] == PARAM_OPEN))
                {
                    Append(captureSb, formatted, inCapture, format[++i]);
                    continue;
                }

                // Normal text
                if (!IsSpecialChar(c))
                {
                    Append(captureSb, formatted, inCapture, c);
                    continue;
                }

                // Rich shorthand
                if (c == RICH_SHORT)
                {
                    string tag = ReplaceShorthand(format, ref i, ref pendingClose);
                    Append(captureSb, formatted, inCapture, tag);
                    continue;
                }

                // All other cases from here on will be replaced during the first layer, and thus need a placeholder
                if (!inCapture) formatted.Append($"{{{forRepIndex++}}}");

                // Capture brackets
                if (c == CAPTURE_OPEN || c == CAPTURE_CLOSE)
                {
                    HandleCapture(format, ref i, ref inCapture, ref captureId, ref capturePriority,
                                  captureSb, tokens, priority, ref sortIndex);
                    continue;
                }

                // Groups
                if (c == GROUP_OPEN)
                {
                    HandleGroup(format, ref i, ref repIndex, ref sortIndex,
                                tokens, priority, extraArgs,
                                captureSb, inCapture, ref pendingClose);
                    continue;
                }

                // Escaped tokens (&x)
                if (c == ESCAPE_CHAR && i + 1 < format.Length)
                {
                    HandleEscaped(format, ref i, ref repIndex, ref sortIndex,
                                  tokens, priority, extraArgs,
                                  captureSb, inCapture);
                    continue;
                }

                // Literal fallback
                Append(captureSb, formatted, inCapture, c);
            }

            if (inCapture)
                throw new FormatException("Unclosed capture block starting with " + CAPTURE_OPEN + captureId);

            return Tuple.Create(formatted.ToString(), tokens, priority, extraArgs);
        }

        private static string ReplaceShorthand(string format, ref int i, ref string pendingClose)
        {
            // If a close tag is pending, return it and clear state
            if (!string.IsNullOrEmpty(pendingClose))
            {
                string close = pendingClose;
                pendingClose = string.Empty;
                return close;
            }

            // Parse keyword until delimiter
            int start = ++i;
            while (i < format.Length && format[i] != DELIMITER)
                i++;

            if (i >= format.Length)
                throw new FormatException("Invalid rich text shorthand: missing delimiter '" + DELIMITER + "'.");

            string keyword = format.Substring(start, i - start);
            keyword = ConvertRichShorthand(keyword);

            // Parse value until the next RICH_SHORT
            int valStart = ++i;
            while (i < format.Length && format[i] != RICH_SHORT)
                i++;

            if (i >= format.Length)
                throw new FormatException("Invalid rich text shorthand: missing closing '" + RICH_SHORT + "'.");

            string value = format.Substring(valStart, i - valStart);
            if (value.IndexOf(' ') >= 0)
                value = "\"" + value + "\"";

            pendingClose = "</" + keyword + ">";
            return "<" + keyword + "=" + value + ">";
        }
        private static void HandleCapture(string format, ref int i, ref bool inCapture, ref char captureId, ref int capturePriority,
        StringBuilder captureSb, Dictionary<TokenKey, string> tokens, Dictionary<int, char> priority, ref int sortIndex)
        {
            if (!inCapture)
            {
                inCapture = true;
                i++;
                if (i >= format.Length || !char.IsDigit(format[i]))
                    throw new FormatException("Capture must start with a number after '" + CAPTURE_OPEN + "'.");

                captureId = format[i];
                capturePriority = sortIndex++;
                captureSb.Length = 0;
            }
            else
            {
                inCapture = false;
                tokens[new TokenKey(captureId, capturePriority)] = captureSb.ToString();
                priority[capturePriority] = captureId;
            }
        }
        private static void HandleGroup(
        string format,
        ref int i,
        ref int repIndex,
        ref int sortIndex,
        Dictionary<TokenKey, string> tokens,
        Dictionary<int, char> priority,
        Dictionary<TokenKey, string[]> extraArgs,
        StringBuilder captureSb,
        bool inCapture,
        ref string pendingClose)
        {
            // Move past GROUP_OPEN ('[' or similar)
            if (++i >= format.Length)
                throw new FormatException("Unexpected end after group open.");

            char symbol = format[i];
            StringBuilder groupContent = new StringBuilder();

            // Must start with a letter or shorthand marker
            if (!char.IsLetter(symbol) && symbol != RICH_SHORT && symbol != '<')
                throw new FormatException("Invalid group: must start with a letter or shorthand.");

            //Make sure shorthand or tag given is valid.
            bool isRich = symbol == RICH_SHORT;
            if (isRich || symbol == '<')
            {
                if (isRich)
                {
                    string tag = ReplaceShorthand(format, ref i, ref pendingClose);
                    groupContent.Append(tag);
                }
                else if (i + 1 < format.Length && char.IsLetter(format[i + 1])) 
                {
                    while (format[i] != '>' && i < format.Length)
                    {
                        groupContent.Append(format[i]); //add the tag in full to group content.
                        i++;
                    }
                    groupContent.Append(format[i]); //append the closing '>'
                    i++;
                }
                else throw new FormatException($"Invalid group format, must define what letter group corresponds to.\nSyntax: {GROUP_OPEN}<letter> ... {GROUP_CLOSE}");
            }

            // Read parameters if any (&x(...))
            if (i + 1 < format.Length && format[i + 1] == PARAM_OPEN)
            {
                char originChar = symbol;
                string[] args = ParseExtraParams(format, ref i, originChar);
                extraArgs[new TokenKey(originChar, sortIndex)] = args;
            }

            int groupStartPriority = sortIndex++;
            int localRepIndex = repIndex++;

            // Read group content until closing bracket
            while (++i < format.Length)
            {
                char c = format[i];

                if (c == GROUP_CLOSE)
                    break;

                if (c == INSERT_SELF)
                {
                    groupContent.Append("{").Append(localRepIndex).Append("}");
                    continue;
                }

                if (c == RICH_SHORT)
                {
                    string tag = ReplaceShorthand(format, ref i, ref pendingClose);
                    groupContent.Append(tag);
                    continue;
                }

                if (c == ESCAPE_CHAR)
                {
                    if (i + 1 < format.Length && !IsSpecialChar(format[i + 1]))
                    {
                        i++;
                        TokenKey tk = new TokenKey(format[i], FORMAT_SPLIT + sortIndex);
                        tokens[tk] = "{" + repIndex + "}";
                        priority[FORMAT_SPLIT + sortIndex] = format[i];

                        if (i + 1 < format.Length && format[i + 1] == PARAM_OPEN)
                        {
                            string[] args = ParseExtraParams(format, ref i, format[i]);
                            extraArgs[tk] = args;
                        }

                        groupContent.Append("{").Append(repIndex++).Append("}");
                    }
                    else
                    {
                        i++;
                        groupContent.Append(format[i]);
                    }
                    continue;
                }

                groupContent.Append(c);
            }

            if (i >= format.Length)
                throw new FormatException("Unclosed group: missing '" + GROUP_CLOSE + "'.");

            if (sortIndex == groupStartPriority) sortIndex++;
            if (repIndex == localRepIndex) repIndex++;

            if (inCapture)
            {
                captureSb.Append(ESCAPE_CHAR).Append(symbol);
                groupStartPriority += FORMAT_SPLIT;
            }

            priority[groupStartPriority] = symbol;
            tokens[new TokenKey(symbol, groupStartPriority)] = groupContent.ToString();
        }
        private static void HandleEscaped(
        string format,
        ref int i,
        ref int repIndex,
        ref int sortIndex,
        Dictionary<TokenKey, string> tokens,
        Dictionary<int, char> priority,
        Dictionary<TokenKey, string[]> extraArgs,
        StringBuilder captureSb,
        bool inCapture)
        {
            i++; // skip the ESCAPE_CHAR

            if (i >= format.Length || !char.IsLetter(format[i]))
                throw new FormatException("Invalid escape: must be followed by a letter or special char.");

            char symbol = format[i];
            int currentPriority = sortIndex++;

            if (inCapture)
            {
                captureSb.Append(ESCAPE_CHAR).Append(symbol);
                currentPriority += FORMAT_SPLIT;
            }

            tokens[new TokenKey(symbol, currentPriority)] = "{" + repIndex++ + "}";
            priority[currentPriority] = symbol;

            if (i + 1 < format.Length && format[i + 1] == PARAM_OPEN)
            {
                string[] args = ParseExtraParams(format, ref i, symbol);
                extraArgs[new TokenKey(symbol, currentPriority)] = args;
            }
        }
        private static string[] ParseExtraParams(string format, ref int i, char originChar)
        {
            List<string> args = new List<string>();
            i++; // skip '('

            string current = string.Empty;

            while (++i < format.Length)
            {
                char c = format[i];
                if (c == DELIMITER || c == PARAM_CLOSE)
                {
                    args.Add(current);
                    current = string.Empty;
                    if (c == PARAM_CLOSE)
                        break;
                }
                else
                {
                    current += c;
                }
            }

            if (i >= format.Length || format[i] != PARAM_CLOSE)
                throw new FormatException(
                    "Missing closing '" + PARAM_CLOSE + "' for parameters of " + originChar + ".");

            return args.ToArray();
        }
        private static string ExpandAliases(string format, Dictionary<string, char> aliasConverter, string counterName)
        {
            if (aliasConverter == null)
                throw new ArgumentNullException("No alias converter given while format contains aliases! Please remove aliases from the format as there is no way to parse them.\nFormat: " + format);

            // Add global aliases and custom token aliases (caller should already have merged globals, but keep safe)
            foreach (var e in GLOBAL_ALIASES)
                aliasConverter.TryAdd(e.Key, e.Value);

            // Use the regex pattern you built earlier (TestRegexAliasPattern).
            // That pattern expects named groups "Token" and optionally "Params"
            Regex aliasRegex = new Regex(TestRegexAliasPattern, RegexOptions.Singleline);

            string Resolver(Match m)
            {
                bool invalidName = false;
                string tokenText = m.Groups["Token"].Value; // e.g. &'alias' or &x etc.
                char resolvedChar = (char)0;

                // If the token is in the long alias form (e.g. &'Name') the second character will be the ALIAS char
                if (tokenText.Length >= 2 && tokenText[1] == ALIAS)
                {
                    // long form: tokenText like "&'AliasName'" or "[ 'Alias' ]" depending on syntax
                    // alias name is between the ALIAS chars; strip outer wrapper
                    int nameStart = 2;
                    int nameLength = tokenText.Length - 3; // skip the starting "<escape><alias>" and trailing alias char
                    string aliasName = tokenText.Substring(nameStart, nameLength);
                    if (!aliasConverter.TryGetValue(aliasName, out resolvedChar))
                        invalidName = true;
                    else
                        tokenText = tokenText[0] + resolvedChar.ToString(); // replace with short form e.g. &x
                }
                // else it's already a short alias like &x (leave tokenText as-is)

                string outp = tokenText;

                // If there are params group, convert any alias reference inside them
                if (!invalidName && m.Groups["Params"].Success)
                {
                    outp += PARAM_OPEN.ToString();
                    string paramStr = m.Groups["Params"].Value;
                    string[] parts = paramStr.Split(DELIMITER);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string param = parts[i];
                        if (param.Length > 0 && param[0] == ALIAS)
                        {
                            string aliasName = param.Substring(1, param.Length - 2); // remove surrounding alias markers
                            if (aliasConverter.TryGetValue(aliasName, out char paramResolved))
                            {
                                outp += paramResolved;
                            }
                            else
                            {
                                invalidName = true;
                                break;
                            }
                        }
                        else
                        {
                            outp += param;
                        }
                        if (i + 1 < parts.Length) outp += DELIMITER;
                    }
                }

                if (invalidName)
                {
                    // Build a helpful message listing available aliases
                    var aliasList = aliasConverter.Keys.ToList();
                    string available = string.Empty;
                    for (int ii = 0; ii < aliasList.Count; ii++)
                    {
                        available += "\"" + aliasList[ii] + "\" as " + aliasConverter[aliasList[ii]];
                        if (ii + 1 < aliasList.Count) available += "\n";
                    }

                    throw new FormatException(
                        "Incorrect aliasing used. The alias name '" + (tokenText.Length > 2 ? tokenText : tokenText) + "' does not exist for " + counterName + " counter." +
                        "\nCorrect Format: " + ESCAPE_CHAR + ALIAS + "<Alias Name>" + ALIAS + " OR " + GROUP_OPEN + ALIAS + "<Alias Name>" + ALIAS + " ... " + GROUP_CLOSE +
                        "\nPossible alias names are listed below:\n" + available);
                }

                return outp;
            }

            return aliasRegex.Replace(format, new MatchEvaluator(Resolver));
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
        private static void Append(StringBuilder captureSb, StringBuilder formatSb, bool inCapture, char c)
        {
            if (inCapture) captureSb.Append(c); else formatSb.Append(c);
        }

        private static void Append(StringBuilder captureSb, StringBuilder formatSb, bool inCapture, string s)
        {
            if (inCapture) captureSb.Append(s); else formatSb.Append(s);
        }

        #endregion
        public static Func<Func<FormatWrapper, string>> GetBasicTokenParser(
            string format,
            Dictionary<string, char> aliasConverter,
            string counterName,
            Action<TokenParser> settings,
            Action<Dictionary<TokenKey, string>, Dictionary<TokenKey, string>, Dictionary<int, char>, FormatWrapper> varSettings,
            out string errorStr,
            bool applySettings = true)
            => GetBasicTokenParser(format, aliasConverter, counterName, settings, varSettings, null, null, out errorStr, applySettings);
        public static Func<Func<FormatWrapper, string>> GetBasicTokenParser(
            string format,
            Dictionary<string, char> aliasConverter,
            string counterName,
            Action<TokenParser> settings,
            Action<Dictionary<TokenKey, string>, Dictionary<TokenKey, string>, Dictionary<int, char>, FormatWrapper> varSettings,
            Func<char, string[], bool> confirmFormat,
            Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> implementArgs,
            out string errorStr,
            bool applySettings = true)
        {
            Dictionary<TokenKey, string> tokens;
            Dictionary<TokenKey, string[]> extraArgs;
            Dictionary<int, char> priority;
            string formatted;
            confirmFormat = GetParentConfirmFormat(confirmFormat);
            implementArgs = GetParentImplementArgs(implementArgs);
            try
            {
                (formatted, tokens, priority, extraArgs) = ParseCounterFormat(format, new Dictionary<string, char>(aliasConverter), counterName);
                foreach (var val in extraArgs.Keys)
                {
                    if (!confirmFormat.Invoke(val.Symbol, extraArgs[val]))
                        throw new FormatException($"Invalid extra parameter format, one of three things happened, too many arguments, too few arguments, or a non reference where a reference should be.\nThis is for the char '{val.Symbol}'");
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
            FormatWrapper extraArgsWrapper = extraArgs.Keys.Count > 0 ? new FormatWrapper(extraArgs.Keys.Select(val => (typeof(string), val.Symbol)).ToArray()) : null;
            return () =>
            {
                TokenParser thing = TokenParser.UnrapTokens(tokens, true, formatted);
                if (applySettings) settings.Invoke(thing);
                string newFormatted = thing.Formatted;
                Dictionary<TokenKey, string> tokensCopy1 = thing.GetReference();
                //Plugin.Log.Info(thing.ToString());
                List <TokenKey> first = new List<TokenKey>();
                List<TokenKey> second = new List<TokenKey>();
                List<TokenKey> captureChars = new List<TokenKey>();
                foreach (TokenKey val in tokensCopy1.Keys)
                {
                    if (char.IsDigit(val.Symbol)) { captureChars.Add(val); first.Add(val); continue; }
                    if (val.Priority < FORMAT_SPLIT) { first.Add(val); second.Add(val); }
                    else second.Add(new TokenKey(val.Symbol, val.Priority - FORMAT_SPLIT));
                }
                second.Sort((a, b) => a.Priority - b.Priority);
                first.Sort((a, b) => a.Priority - b.Priority);
                /*Plugin.Log.Info(string.Join(",", first));
                Plugin.Log.Info(string.Join(",", second));//*/
                return (vals) =>
                {
                    /*Plugin.Log.Info("---------------");
                    foreach (var token in tokensCopy1)
                        Plugin.Log.Info($"{token.Key} || " + ToLiteral(token.Value));
                    Plugin.Log.Info("Formatted || " + formatted);//*/
                    Dictionary<TokenKey, string> tokensCopy2 = new Dictionary<TokenKey, string>(tokensCopy1);
                    varSettings.Invoke(tokensCopy1, tokensCopy2, priority, vals);
                    if (!(extraArgsWrapper is null)) 
                        foreach (var val in extraArgs.Keys)
                            extraArgsWrapper.SetValue(val.Symbol, implementArgs.Invoke(val.Symbol, extraArgs[val], vals, tokensCopy2));
                    foreach (TokenKey val in captureChars)
                    {
                        string newVal = "", toParse = tokensCopy2[val];
                        int priorityCount = val.Priority;
                        if (toParse.Length == 0) continue;
                        for (int j = 0; j < toParse.Length; j++)
                            if (toParse[j] == ESCAPE_CHAR)
                            {
                                string toTry = null;
                                char temp = toParse[++j];
                                while (!tokensCopy2.TryGetValue(new TokenKey(temp, ++priorityCount + FORMAT_SPLIT), out toTry));
                                newVal += toTry;
                            }
                            else newVal += toParse[j];
                        tokensCopy2[val] = newVal;
                    }
                    object[] firstArr = new object[first.Count];
                    int i = 0;
                    foreach (TokenKey val in first) firstArr[i++] = tokensCopy2[val];
                    object[] secondArr = new object[second.Count];
                    i = 0;
                    foreach (TokenKey val in second)
                    {
                        object o = null;
                        if (!vals.TryGetValue(val.Symbol, out o) && !(extraArgsWrapper is null)) 
                            extraArgsWrapper.TryGetValue(val.Symbol, out o);
                        if (o != null) secondArr[i++] = o;
                    }
                    //Plugin.Log.Info(HelpfulMisc.Print(firstArr) + '\n' + HelpfulMisc.Print(secondArr));
                    //Plugin.Log.Info(string.Format(newFormatted, secondArr));
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
        private static Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> GetParentImplementArgs
            (Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> child)
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
                            List<TokenKey> arr = tokens.Keys.Where(a => a.Symbol == values[0][0]).ToList();
                            foreach (TokenKey item in arr)
                                tokens[item] = "";
                        }
                        return "";
                    default: return "";
                }
            };
        }
        public static void SetText(Dictionary<TokenKey, string> tokens, char c, string text = "") 
        { 
            List<TokenKey> toModify = new List<TokenKey>();
            foreach (var item in tokens.Keys) if (item.Symbol == c) toModify.Add(new TokenKey(c, item.Priority));
            foreach (var item in toModify)
                if (item.Priority < FORMAT_SPLIT || text.IsEmpty())
                    tokens[new TokenKey(item.Symbol, item.Priority)] = text;
                else
                    tokens[new TokenKey(item.Symbol, item.Priority)] = Regex.Replace(tokens[new TokenKey(item.Symbol, item.Priority)], "\\{\\d\\}", text);
            
        }
        public static void SurroundText(Dictionary<TokenKey, string> tokens, char c, string preText, string postText) 
        {
            IEnumerable<int> keys = new List<int>(tokens.Keys.Where(val => val.Symbol == c).Select(val => val.Priority)); 
            //The lengths I have to to avoid cocerrent modification exceptions :(
            foreach (var item in keys)
                tokens[new TokenKey(c, item)] = preText + tokens[new TokenKey(c, item)] + postText;
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
                {
                    if (!PC.BlendMiddleColor)
                        num = variance / 2f;
                }
                else return ConvertColorToMarkup(PC.ColorGradZero);
            bool neg = num < 0;
            float toConvert = Math.Min(Math.Max(Math.Abs(neg && !PC.ColorGradBlending ? 1.0f - -num / variance : num / variance), PC.ColorGradMinDark) + (PC.BlendMiddleColor ? 0 : PC.ColorGradFlipPercent), 1f);
            if (PC.ColorGradBlending)
                return ConvertColorToMarkup(PC.BlendMiddleColor ?
                            neg ?
                                Blend(PC.ColorGradMin, PC.ColorGradZero, toConvert) :
                                Blend(PC.ColorGradZero, PC.ColorGradMax, 1.0f - toConvert) :
                            Blend(PC.ColorGradMin, PC.ColorGradMax, neg ? toConvert : 1.0f - toConvert));
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
            private readonly Dictionary<char, List<int>> topLevelTokens;
            private readonly Dictionary<char, List<int>> bottomLevelTokens;
            private readonly Dictionary<TokenKey, List<TokenKey>> tokenRelations;
            private readonly Dictionary<TokenKey, string> tokenValues;

            public string Formatted { get; private set; }

            private TokenParser(
                Dictionary<char, List<int>> topLevelTokens,
                Dictionary<char, List<int>> bottomLevelTokens,
                Dictionary<TokenKey, List<TokenKey>> tokenRelations,
                Dictionary<TokenKey, string> tokenValues)
            {
                this.topLevelTokens = topLevelTokens;
                this.bottomLevelTokens = bottomLevelTokens;
                this.tokenRelations = tokenRelations;
                this.tokenValues = tokenValues;
                this.Formatted = string.Empty;
            }

            /// <summary>
            /// Build a TokenParser from the flat token dictionary.
            /// Preserves the original ordering logic: tokens with Priority  FORMAT_SPLIT are top-level,
            /// tokens with Priority > FORMAT_SPLIT are bottom-level (child) tokens. Token relations are built
            /// by grouping bottom-level tokens under the most recent top-level token in priority order.
            /// </summary>
            public static TokenParser UnrapTokens(Dictionary<TokenKey, string> tokens, bool makeNewReference = true, string formatted = "")
            {
                var top = new Dictionary<char, List<int>>();
                var bot = new Dictionary<char, List<int>>();
                var relations = new Dictionary<TokenKey, List<TokenKey>>();

                // sort keys by raw priority
                var keys = tokens.Keys.ToList();
                keys.Sort((a, b) => a.Priority - b.Priority);

                // populate top/bottom lists
                foreach (var k in keys)
                {
                    if (k.Priority <= FORMAT_SPLIT)
                    {
                        if (!top.ContainsKey(k.Symbol)) top[k.Symbol] = new List<int>();
                        top[k.Symbol].Add(k.Priority);
                    }
                    else
                    {
                        if (!bot.ContainsKey(k.Symbol)) bot[k.Symbol] = new List<int>();
                        bot[k.Symbol].Add(k.Priority);
                    }
                }

                // re-sort keys so that bottom priorities are considered relative to the last top-level token
                keys.Sort((a, b) =>
                {
                    int aKey = (a.Priority > FORMAT_SPLIT) ? a.Priority - FORMAT_SPLIT : a.Priority;
                    int bKey = (b.Priority > FORMAT_SPLIT) ? b.Priority - FORMAT_SPLIT : b.Priority;
                    return aKey - bKey;
                });

                TokenKey lastTop = default;
                var children = new List<TokenKey>();
                bool hasLastTop = false;

                foreach (var k in keys)
                {
                    if (k.Priority <= FORMAT_SPLIT)
                    {
                        if (hasLastTop)
                        {
                            // finalize the last top-level entry
                            relations[lastTop] = new List<TokenKey>(children);
                        }
                        lastTop = k;
                        children.Clear();
                        hasLastTop = true;
                    }
                    else
                    {
                        children.Add(k);
                    }
                }

                // add final relation for the last top-level token
                if (hasLastTop)
                    relations[lastTop] = new List<TokenKey>(children);

                // create tokenValues dictionary (optionally copy)
                var valuesCopy = makeNewReference ? new Dictionary<TokenKey, string>(tokens) : tokens;

                return new TokenParser(top, bot, relations, valuesCopy) { Formatted = formatted ?? string.Empty };
            }

            public Dictionary<TokenKey, string> RerapTokens()
            {
                return new Dictionary<TokenKey, string>(tokenValues);
            }

            public Dictionary<TokenKey, string> GetReference()
            {
                return tokenValues;
            }

            /// <summary>
            /// Convert a bottom-level token (child) into a constant string inside its parent, or inline it where necessary.
            /// This function mirrors the original behavior but is split into clearer steps.
            /// </summary>
            public bool MakeTokenConstant(char token, string value = "")
            {
                // If Formatted has not been set, operation not valid (matches original guard)
                if (Formatted == default) return false;

                // iterate every top-level key symbol
                foreach (var kv in topLevelTokens)
                {
                    char topSymbol = kv.Key;
                    var priorities = kv.Value;

                    int removedPriority = -1;

                    // for each top-level priority for this symbol, check its relations
                    foreach (int topPriority in priorities)
                    {
                        var rootKey = new TokenKey(topSymbol, topPriority);
                        if (!tokenRelations.ContainsKey(rootKey)) continue;

                        var relationsList = tokenRelations[rootKey];
                        // find child index whose symbol matches the requested token
                        int idx = relationsList.FindIndex(tk => tk.Symbol == token);
                        if (idx == -1) continue;

                        int childPriority = relationsList[idx].Priority;

                        // If the topSymbol is a digit (a capture), the logic differs slightly in how replacement occurs
                        if (char.IsDigit(topSymbol))
                        {
                            // Replace numeric placeholders inside the child token value
                            string newChildValue = Regex.Replace(tokenValues[new TokenKey(token, childPriority)], "\\{\\d+\\}", value);

                            // If parent token contains an escaped instance of the token, replace that escaped segment with the new value
                            var parentKey = new TokenKey(topSymbol, topPriority);
                            string parentValue = tokenValues[parentKey];

                            if (parentValue.Contains(string.Format("{0}{1}", ESCAPE_CHAR, token)))
                            {
                                tokenValues[parentKey] = parentValue.Replace(string.Format("{0}{1}", ESCAPE_CHAR, token), newChildValue);
                            }
                            else
                            {
                                // If not escaped, find the relation entry corresponding to the child string and replace that placeholder
                                string repStr = tokenValues[new TokenKey(token, childPriority)];
                                int relationIndex = relationsList.FindIndex(tk => tokenValues[tk] == repStr);
                                if (relationIndex == -1)
                                    throw new ArgumentException("The token given has been parsed incorrectly somehow. There is a bug in the code somewhere.");

                                tokenValues[relationsList[relationIndex]] = Regex.Replace(tokenValues[relationsList[relationIndex]], "\\{\\d+\\}", value);
                            }
                        }
                        else
                        {
                            // Non-capture parent: simply replace placeholder tokens in the parent value
                            tokenValues[new TokenKey(topSymbol, topPriority)] =
                                Regex.Replace(tokenValues[new TokenKey(topSymbol, topPriority)], "\\{\\d+\\}", value);
                        }

                        // Remove the child token value and remember which priority was removed
                        tokenValues.Remove(new TokenKey(token, childPriority));
                        removedPriority = childPriority;
                        break;
                    }

                    // If we removed a child priority, we must adjust placeholders that relied on positional indexes
                    if (removedPriority != -1)
                    {
                        // Remove the priority from top-level tracking
                        topLevelTokens[topSymbol].Remove(removedPriority);

                        int comp = (removedPriority > FORMAT_SPLIT) ? removedPriority - FORMAT_SPLIT : removedPriority;

                        // Find all tokenValues which contain placeholders and adjust their indices
                        var toAdjust = new List<Tuple<TokenKey, int>>();
                        foreach (var tv in tokenValues)
                        {
                            var matches = Regex.Matches(tv.Value, "\\{\\d+\\}");
                            if (matches.Count == 0) continue;
                            int num = (tv.Key.Priority > FORMAT_SPLIT) ? tv.Key.Priority - FORMAT_SPLIT : tv.Key.Priority;
                            if (num > comp)
                            {
                                int placeholderIndex = int.Parse(matches[0].Value.Substring(1, matches[0].Length - 2));
                                toAdjust.Add(Tuple.Create(tv.Key, placeholderIndex));
                            }
                        }

                        foreach (var adj in toAdjust)
                        {
                            var keyToAdj = adj.Item1;
                            int oldPlaceholder = adj.Item2;
                            tokenValues[keyToAdj] = Regex.Replace(tokenValues[keyToAdj], "\\{\\d+\\}", "{" + (oldPlaceholder - 1) + "}");
                        }
                    }
                }

                // finally remove bottom-level token mapping for the token char (if present)
                bottomLevelTokens.Remove(token);

                return true;
            }

            public void SetText(char c, string text = "")
            {
                var toModify = new List<TokenKey>();
                foreach (var key in tokenValues.Keys)
                    if (key.Symbol == c) toModify.Add(new TokenKey(c, key.Priority));

                foreach (var key in toModify)
                {
                    if (key.Priority < FORMAT_SPLIT || string.IsNullOrEmpty(text))
                        tokenValues[new TokenKey(key.Symbol, key.Priority)] = text;
                    else
                        tokenValues[new TokenKey(key.Symbol, key.Priority)] =
                            Regex.Replace(tokenValues[new TokenKey(key.Symbol, key.Priority)], "\\{\\d\\}", text);
                }
            }

            public void SurroundText(char c, string preText, string postText)
            {
                var updates = new List<KeyValuePair<TokenKey, string>>();
                foreach (var k in tokenValues.Keys)
                {
                    if (k.Symbol != c) continue;
                    updates.Add(new KeyValuePair<TokenKey, string>(k, preText + tokenValues[k] + postText));
                }
                foreach (var u in updates) tokenValues[u.Key] = u.Value;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("----------------------------");
                sb.Append("Top level tokens: ");
                foreach (var t in topLevelTokens)
                    sb.AppendLine().AppendFormat("{0} || [{1}]", t.Key, string.Join(", ", t.Value));
                sb.AppendLine();
                sb.Append("Bottom level tokens: ");
                foreach (var t in bottomLevelTokens)
                    sb.AppendLine().AppendFormat("{0} || [{1}]", t.Key, string.Join(", ", t.Value));
                sb.AppendLine();
                sb.Append("Token relations: ");
                foreach (var rel in tokenRelations)
                    sb.AppendLine().AppendFormat("{0} || [{1}]", rel.Key.Symbol, string.Join(", ", rel.Value));
                sb.AppendLine();
                sb.Append("Token values: ");
                foreach (var tv in tokenValues)
                    sb.AppendLine().AppendFormat("{0} || {1}", tv.Key, ToLiteral(tv.Value));
                sb.AppendLine("----------------------------");
                return sb.ToString();
            }
        }

    }
}
