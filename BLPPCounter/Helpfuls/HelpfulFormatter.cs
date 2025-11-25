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
    /// <summary>
    /// Provides utilities for parsing and formatting strings with custom tokens and aliases.
    /// </summary>
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

        /// <summary>
        /// Parses a counter format string into a structured format with tokens and priorities.
        /// </summary>
        /// <param name="format">The format string to parse.</param>
        /// <param name="aliasConverter">A converter for aliases in the format string.</param>
        /// <param name="counterName">The name of the counter (for error messaging).</param>
        /// <returns>A tuple containing the formatted string, token dictionary, priority dictionary, and extra arguments dictionary.</returns>
        /// <exception cref="FormatException">Thrown when the format string has errors.</exception>
        public static Tuple<string,
                        Dictionary<TokenKey, string>,
                        Dictionary<int, char>,
                        Dictionary<TokenKey, string[]>>
        ParseCounterFormat(string format, Dictionary<string, char> aliasConverter, string counterName)
        {
            if (FindAliasErrorsInFormat(format, out string aliasError))
                throw new FormatException(aliasError);

            if (format.Contains(ESCAPE_CHAR.ToString() + ALIAS) ||
                format.Contains(GROUP_OPEN.ToString() + ALIAS))
                format = ExpandAliases(format, aliasConverter, counterName);

            StringBuilder formatted = new();
            StringBuilder captureSb = new();

            Dictionary<TokenKey, string> tokens = [];
            Dictionary<TokenKey, string[]> extraArgs = [];
            Dictionary<int, char> priority = [];

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
                //Plugin.Log.Info($"i = {i}, format left = {format.Substring(i)}");

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

                // Rich tag insertion
                if (c == '<')
                {
                    string tag = ReplaceRichTags(format, ref i);
                    if (tag != null)
                    {
                        Append(captureSb, formatted, inCapture, tag);
                        continue;
                    }
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
        /// <summary>
        /// Replaces rich text tags in the format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <returns>The tag to add to the output format. If tag is not valid, returns null.</returns>
        private static string ReplaceRichTags(string format, ref int i)
        {
            Match richTag = Regex.Match(format.Substring(i), @"^<(?:(?<tag>[a-zA-Z]+)(?:=(?<value>(?:"".*?"")|(?:'[^']*')|(?:[^<>]*?)))?|/[a-zA-Z]+)>");
            if (richTag.Success)
            {
                i += richTag.Length - 1; // move index to end of tag
                return richTag.Value;
            }
            else 
                return null;
        }

        /// <summary>
        /// Replaces shorthand tags in the format string with their expanded equivalents.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <param name="pendingClose">A reference to a string that stores the pending close tag.</param>
        /// <returns>The tag with shorthand replaced.</returns>
        /// <exception cref="FormatException">Thrown when the shorthand tag is invalid.</exception>
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

        /// <summary>
        /// Handles the parsing of capture blocks in the format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <param name="inCapture">Indicates whether currently inside a capture block.</param>
        /// <param name="captureId">The ID of the capture block.</param>
        /// <param name="capturePriority">The priority of the capture block.</param>
        /// <param name="captureSb">StringBuilder for building the capture block content.</param>
        /// <param name="tokens">Dictionary of tokens.</param>
        /// <param name="priority">Dictionary of priorities.</param>
        /// <param name="sortIndex">Current sort index.</param>
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

        /// <summary>
        /// Handles the parsing of group blocks in the format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <param name="repIndex">The current repetition index.</param>
        /// <param name="sortIndex">The current sort index.</param>
        /// <param name="tokens">The token dictionary.</param>
        /// <param name="priority">The priority dictionary.</param>
        /// <param name="extraArgs">The extra arguments dictionary.</param>
        /// <param name="captureSb">The StringBuilder for capturing content.</param>
        /// <param name="inCapture">Indicates if currently in a capture block.</param>
        /// <param name="pendingClose">A reference to a string for the pending close tag.</param>
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

        /// <summary>
        /// Handles escaped tokens in the format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <param name="repIndex">The current repetition index.</param>
        /// <param name="sortIndex">The current sort index.</param>
        /// <param name="tokens">The token dictionary.</param>
        /// <param name="priority">The priority dictionary.</param>
        /// <param name="extraArgs">The extra arguments dictionary.</param>
        /// <param name="captureSb">The StringBuilder for capturing content.</param>
        /// <param name="inCapture">Indicates if currently in a capture block.</param>
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

        /// <summary>
        /// Parses extra parameters enclosed in parentheses in the format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="i">The current index in the format string.</param>
        /// <param name="originChar">The original character indicating the start of parameters.</param>
        /// <returns>An array of parsed parameter strings.</returns>
        /// <exception cref="FormatException">Thrown when the parameters are not correctly closed.</exception>
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

            return [.. args];
        }

        /// <summary>
        /// Expands aliases in the format string using the provided alias converter.
        /// </summary>
        /// <param name="format">The format string with aliases.</param>
        /// <param name="aliasConverter">The alias converter dictionary.</param>
        /// <param name="counterName">The name of the counter (for error messaging).</param>
        /// <returns>The format string with aliases expanded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when no alias converter is provided.</exception>
        /// <exception cref="FormatException">Thrown when an alias is incorrect or not found.</exception>
        private static string ExpandAliases(string format, Dictionary<string, char> aliasConverter, string counterName)
        {
            if (aliasConverter == null)
                throw new ArgumentNullException("No alias converter given while format contains aliases! Please remove aliases from the format as there is no way to parse them.\nFormat: " + format);

            // Add global aliases and custom token aliases (caller should already have merged globals, but keep safe)
            foreach (var e in GLOBAL_ALIASES)
                aliasConverter.TryAdd(e.Key, e.Value);

            //Apply custom aliases from config.
            CustomAlias.ApplyAliases(PluginConfig.Instance.TokenSettings.TokenAliases, aliasConverter, counterName);

            // Use the regex pattern you built earlier (TestRegexAliasPattern).
            // That pattern expects named groups "Token" and optionally "Params"
            Regex aliasRegex = new Regex(TestRegexAliasPattern, RegexOptions.Singleline);

            string Resolver(Match m)
            {
                bool invalidName = false;
                string tokenText = m.Groups["Token"].Value; // e.g. &'alias' or &x etc.
                char resolvedChar = (char)0;

                // If the token is in the long alias form (e.g. &'AliasName') the second character will be the ALIAS char
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

        /// <summary>
        /// Checks for alias errors in the format string.
        /// </summary>
        /// <param name="format">The format string to check.</param>
        /// <param name="errorMessage">Output parameter for the error message if errors are found.</param>
        /// <returns>True if errors are found, false otherwise.</returns>
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

        /// <summary>
        /// Appends a character to the appropriate StringBuilder based on the capture state.
        /// </summary>
        /// <param name="captureSb">The StringBuilder for capturing content.</param>
        /// <param name="formatSb">The StringBuilder for formatted content.</param>
        /// <param name="inCapture">Indicates if currently in a capture block.</param>
        /// <param name="c">The character to append.</param>
        private static void Append(StringBuilder captureSb, StringBuilder formatSb, bool inCapture, char c)
        {
            if (inCapture) captureSb.Append(c); else formatSb.Append(c);
        }

        /// <summary>
        /// Appends a string to the appropriate StringBuilder based on the capture state.
        /// </summary>
        /// <param name="captureSb">The StringBuilder for capturing content.</param>
        /// <param name="formatSb">The StringBuilder for formatted content.</param>
        /// <param name="inCapture">Indicates if currently in a capture block.</param>
        /// <param name="s">The string to append.</param>
        private static void Append(StringBuilder captureSb, StringBuilder formatSb, bool inCapture, string s)
        {
            if (inCapture) captureSb.Append(s); else formatSb.Append(s);
        }

        /// <summary>
        /// Creates a token parser factory for a format string.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="aliasConverter">The alias converter dictionary.</param>
        /// <param name="counterName">The counter name (for error messaging).</param>
        /// <param name="settings">Settings action for the token parser.</param>
        /// <param name="varSettings">Variable settings action.</param>
        /// <param name="errorStr">Output parameter for the error string in case of failure.</param>
        /// <param name="applySettings">Indicates if settings should be applied.</param>
        /// <returns>A function that produces the formatting delegate.</returns>
        /// <exception cref="Exception">Thrown when formatting fails.</exception>
        public static Func<Func<FormatWrapper, string>> GetBasicTokenParser(
            string format,
            Dictionary<string, char> aliasConverter,
            string counterName,
            Action<TokenParser> settings,
            Action<Dictionary<TokenKey, string>, Dictionary<TokenKey, string>, Dictionary<int, char>, FormatWrapper> varSettings,
            out string errorStr, out TokenInfo[] tokenInfos,
            bool applySettings = true)
            => GetBasicTokenParser(format, aliasConverter, counterName, settings, varSettings, null, null, out errorStr, out tokenInfos, applySettings);

        /// <summary>
        /// Creates a token parser factory with custom callbacks for extra arguments.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="aliasConverter">The alias converter dictionary.</param>
        /// <param name="counterName">The counter name (for error messaging).</param>
        /// <param name="settings">Settings action for the token parser.</param>
        /// <param name="varSettings">Variable settings action.</param>
        /// <param name="confirmFormat">Custom confirmation format callback.</param>
        /// <param name="implementArgs">Custom implement arguments callback.</param>
        /// <param name="errorStr">Output parameter for the error string in case of failure.</param>
        /// <param name="applySettings">Indicates if settings should be applied.</param>
        /// <returns>A function that produces the formatting delegate.</returns>
        /// <exception cref="Exception">Thrown when formatting fails.</exception>
        public static Func<Func<FormatWrapper, string>> GetBasicTokenParser(
        string format,
        Dictionary<string, char> aliasConverter,
        string counterName,
        Action<TokenParser> settings,
        Action<Dictionary<TokenKey, string>, Dictionary<TokenKey, string>, Dictionary<int, char>, FormatWrapper> varSettings,
        Func<char, string[], bool> confirmFormat,
        Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> implementArgs,
        out string errorStr, out TokenInfo[] tokenInfos,
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
                foreach (TokenKey val in extraArgs.Keys)
                {
                    if (!confirmFormat.Invoke(val.Symbol, extraArgs[val]))
                        throw new FormatException($"Invalid extra parameter format ... This is for the char '{val.Symbol}'");
                }
                Dictionary<char, TokenInfo> temp = [];
                List<(char capture, IEnumerable<char> symbols)> buffer = [];
                foreach (TokenKey val in tokens.Keys)
                {
                    if (char.IsDigit(val.Symbol))
                    {
                        MatchCollection mc = Regex.Matches(tokens[val], $"(?<={ESCAPE_CHAR}).");
                        List<char> toAdd = [];
                        foreach (Match item in mc)
                        {
                            char c = item.Value[0];
                            if (temp.ContainsKey(c))
                            {
                                TokenInfo ti = temp[c];
                                ti.AssignedCapture.Add(val.Symbol);
                                if (ti.Usage < TokenUsage.Dependent)
                                    ti.Usage = TokenUsage.Dependent;
                                temp[c] = ti;
                            }
                            else
                                toAdd.Add(c);
                        }
                        buffer.Add((val.Symbol, toAdd));
                        continue;
                    }
                    if (!temp.TryAdd(val.Symbol, new TokenInfo(val.Symbol, val.Priority > FORMAT_SPLIT ? TokenUsage.Dependent : TokenUsage.Always)))
                    {
                        TokenInfo s = temp[val.Symbol];

                        if (val.Priority < FORMAT_SPLIT)
                            s.Usage = TokenUsage.Always;
                        else
                        {
                            string rep = '{' + (val.Priority - FORMAT_SPLIT - 1).ToString() + '}';
                            foreach (KeyValuePair<TokenKey, string> kvp in tokens)
                                if (kvp.Value.Contains(rep))
                                    s.AssignedGroup.Add(kvp.Key.Symbol);
                        }

                        temp[val.Symbol] = s;
                    }
                }
                foreach (var (capture, symbols) in buffer)
                    foreach (char c in symbols)
                    {
                        TokenInfo s = temp[c];
                        s.AssignedCapture.Add(capture);
                        if (s.Usage < TokenUsage.Dependent)
                            s.Usage = TokenUsage.Dependent;
                        temp[c] = s;
                    }
                tokenInfos = [.. temp.Values];
            }
            catch (Exception e)
            {
                errorStr = "Formatting failed! " + e.Message;
                errorStr += "\nFormatting: " + ToLiteral(format).Replace("\\'", "'");
                Plugin.Log.Error(errorStr);
                tokenInfos = null;
                return null;
            }

            errorStr = "";

            // prepare an optional wrapper for extraArgs so implementArgs can set values later
            FormatWrapper extraArgsWrapper = extraArgs.Keys.Count > 0 ? new FormatWrapper(extraArgs.Keys.Select(k => (typeof(string), k.Symbol)).ToArray()) : null;

            // BUILD the factory that will create a formatter instance
            return () =>
            {
                // Build token parser and apply compile-time settings
                TokenParser parser = TokenParser.UnrapTokens(tokens, true, formatted);
                if (applySettings && settings != null) settings.Invoke(parser);

                // Keep a reference-to-template tokens (this is the "compiled" token map)
                Dictionary<TokenKey, string> compiledTokens = parser.GetReference();

                // analyze tokens once and reuse ordering lists
                TokenOrdering ordering = parser.AnalyzeTokens();

                // Return the runtime formatting delegate
                return (FormatWrapper runtimeVars) =>
                {
                    // Duplicate the compiled token values for this run (so we can mutate safely)
                    Dictionary<TokenKey, string> runtimeTokens = new Dictionary<TokenKey, string>(compiledTokens);

                    // apply var-level settings (mutate runtimeTokens as needed)
                    varSettings?.Invoke(compiledTokens, runtimeTokens, priority, runtimeVars);

                    // apply extra args via implementArgs -> populate extraArgsWrapper if present
                    if (extraArgsWrapper != null)
                    {
                        foreach (var key in extraArgs.Keys)
                        {
                            string result = implementArgs.Invoke(key.Symbol, extraArgs[key], runtimeVars, runtimeTokens);
                            extraArgsWrapper.SetValue(key.Symbol, result);
                        }
                    }

                    // rebuild captures (resolve escaped child tokens into captures)
                    parser.RebuildCaptures(runtimeTokens, ordering.Captures);

                    // Build arrays for formatting
                    BuildFormatArrays(ordering.FirstLayer, ordering.SecondLayer, runtimeTokens, extraArgsWrapper, runtimeVars, runtimeTokens, out object[] firstArr, out object[] secondArr);

                    // perform the two-stage format and return
                    return ApplyTwoStageFormat(parser.Formatted, firstArr, secondArr);
                };
            };
        }

        /// <summary>
        /// Builds arrays for the two-stage formatting process.
        /// </summary>
        /// <param name="first">The first layer token keys.</param>
        /// <param name="second">The second layer token keys.</param>
        /// <param name="tokens">The dictionary of tokens.</param>
        /// <param name="extraArgsWrapper">The wrapper for extra arguments.</param>
        /// <param name="runtimeVars">The runtime variables.</param>
        /// <param name="runtimeTokens">The runtime tokens.</param>
        /// <param name="firstArr">Output parameter for the first array.</param>
        /// <param name="secondArr">Output parameter for the second array.</param>
        /// <exception cref="Exception">Thrown when an error occurs during array building.</exception>
        private static void BuildFormatArrays(
            List<TokenKey> first,
            List<TokenKey> second,
            Dictionary<TokenKey, string> tokens,
            FormatWrapper extraArgsWrapper,
            FormatWrapper runtimeVars,
            Dictionary<TokenKey, string> runtimeTokens,
            out object[] firstArr,
            out object[] secondArr)
        {
            firstArr = new object[first.Count];
            int i = 0;
            foreach (TokenKey val in first)
            {
                // tokens should contain the prepared string fragments for first layer
                firstArr[i++] = runtimeTokens[val];
            }

            secondArr = new object[second.Count];
            i = 0;
            foreach (TokenKey val in second)
            {
                object o = null;
                // primary place: runtimeVars (these are the dynamic values provided by the caller)
                runtimeVars?.TryGetValue(val.Symbol, out o);

                // fallback: if runtimeVars had nothing, try the extra args wrapper (these are the processed extra args)
                if (o != null || (o == null && extraArgsWrapper.TryGetValue(val.Symbol, out o)))
                    secondArr[i++] = o;
            }
        }

        /// <summary>
        /// Applies the two-stage formatting process to the template string.
        /// </summary>
        /// <param name="template">The template string.</param>
        /// <param name="first">The first array of objects for formatting.</param>
        /// <param name="second">The second array of objects for formatting.</param>
        /// <returns>The formatted string.</returns>
        private static string ApplyTwoStageFormat(string template, object[] first, object[] second)
        {
            // first replaces the group placeholders (firstArr) then outer placeholders (secondArr)
            return string.Format(string.Format(template, first), second);
        }

        #endregion Parser & Helpers

        /// <summary>
        /// Gets the global parameter amount for a given parameter character.
        /// </summary>
        /// <param name="paramChar">The parameter character.</param>
        /// <returns>The global parameter amount.</returns>
        public static int GetGlobalParamAmount(char paramChar)
        {
            switch (paramChar)
            {
                case 's': return 1;
                case 'h': return 2;
                default: return 0;
            }
        }

        /// <summary>
        /// Wraps a child confirmation callback with parent default confirmations.
        /// </summary>
        /// <param name="child">The child confirmation callback.</param>
        /// <returns>The wrapped confirmation callback.</returns>
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

        /// <summary>
        /// Wraps a child implement-args callback with parent implementations.
        /// </summary>
        /// <param name="child">The child implement-args callback.</param>
        /// <returns>The wrapped implement-args callback.</returns>
        private static Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> GetParentImplementArgs(Func<char, string[], FormatWrapper, Dictionary<TokenKey, string>, string> child)
        {
            return (paramChar, values, vals, tokens) =>
            {
                if (child != null)
                {
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

        /// <summary>
        /// Sets the text for tokens matching the given character.
        /// </summary>
        /// <param name="tokens">The token dictionary.</param>
        /// <param name="c">The character to match.</param>
        /// <param name="text">The text to set.</param>
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

        /// <summary>
        /// Surrounds the text for tokens matching the given character with pre and post text.
        /// </summary>
        /// <param name="tokens">The token dictionary.</param>
        /// <param name="c">The character to match.</param>
        /// <param name="preText">The text to place before the existing text.</param>
        /// <param name="postText">The text to place after the existing text.</param>
        public static void SurroundText(Dictionary<TokenKey, string> tokens, char c, string preText, string postText)
        {
            IEnumerable<int> keys = new List<int>(tokens.Keys.Where(val => val.Symbol == c).Select(val => val.Priority));
            //The lengths I have to to avoid cocerrent modification exceptions :(
            foreach (var item in keys)
                tokens[new TokenKey(c, item)] = preText + tokens[new TokenKey(c, item)] + postText;
        }

        /// <summary>
        /// Checks if a string represents a reference character.
        /// </summary>
        /// <param name="s">The string to check.</param>
        /// <returns>True if the string is a reference character, false otherwise.</returns>
        public static bool IsReferenceChar(string s) => s.Length == 1 && char.IsLetter(s[0]) && !IsSpecialChar(s[0]);

        /// <summary>
        /// Checks if a character is a special formatting character.
        /// </summary>
        /// <param name="c">The character to check.</param>
        /// <returns>True if the character is special, false otherwise.</returns>
        public static bool IsSpecialChar(char c) => SPECIAL_CHARS.Contains(c);

        /// <summary>
        /// Converts a number to a color string based on its value.
        /// </summary>
        /// <param name="num">The number to convert.</param>
        /// <returns>A color string representing the number.</returns>
        public static string NumberToColor(float num) => num > 0 ? "<color=green>" : num == 0 ? "<color=yellow>" : "<color=red>";

        /// <summary>
        /// Escapes special characters in a string.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <returns>The string with special characters escaped.</returns>
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

        /// <summary>
        /// Converts a shorthand name to its rich text equivalent.
        /// </summary>
        /// <param name="shorthand">The shorthand name.</param>
        /// <returns>The rich text equivalent of the shorthand.</returns>
        public static string ConvertRichShorthand(string shorthand)
        {
            if (RICH_SHORTHANDS.Keys.Contains(shorthand)) return RICH_SHORTHANDS[shorthand];
            return shorthand;
        }

        /// <summary>
        /// Converts a number to a gradient color string.
        /// </summary>
        /// <param name="variance">The variance for the gradient.</param>
        /// <param name="num">The number to convert.</param>
        /// <returns>A gradient color string representing the number.</returns>
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

        /// <summary>
        /// Returns a color string for a weighted rank value based on configuration.
        /// </summary>
        /// <param name="rank">The rank value to evaluate.</param>
        /// <returns>Color markup string for the selected weighted rank color.</returns>
        public static string GetWeightedRankColor(int rank)
        {
            int c = -1;
            var arr = PluginConfig.Instance.FormatSettings.WeightedRankColors.ToArray();
            while (arr[++c].Rank < rank && c + 1 < arr.Length) ;
            return "<color=#" + arr[c].Color + ">";
        }

        /// <summary>
        /// Converts a string of default formatting characters to the currently configured used characters.
        /// </summary>
        /// <param name="str">Input string containing legacy default characters.</param>
        /// <returns>String with default characters replaced by configured tokens.</returns>
        public static string DefaultToUsedChar(string str) => Regex.Replace(str, "[&*,[\\]$<>()']", m => "" + DefaultToUsedChar(m.Value[0]));

        /// <summary>
        /// Converts a single default formatting character to the currently configured character token.
        /// </summary>
        /// <param name="c">The default character to convert.</param>
        /// <returns>The configured token character equivalent.</returns>
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

        /// <summary>
        /// Returns a colorized representation of a special formatting character using configured colors.
        /// </summary>
        /// <param name="c">Special character to colorize.</param>
        /// <returns>A string containing color markup and the character.</returns>
        /// <exception cref="ArgumentException">Thrown if the provided character is not recognized as special.</exception>
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

        /// <summary>
        /// Converts a default-format string into a colorized format string using configured token characters.
        /// </summary>
        /// <param name="str">Input format string using default characters.</param>
        /// <returns>Colorized format string with configured tokens replaced by colored tokens where applicable.</returns>
        public static string ColorDefaultFormatToColor(string str) => ColorFormatToColor(DefaultToUsedChar(str));

        /// <summary>
        /// Converts a format string with tokens into a colorized representation using configured colors and color names.
        /// </summary>
        /// <param name="str">Input format string already mapped to configured tokens.</param>
        /// <returns>Colorized string representation.</returns>
        public static string ColorFormatToColor(string str)
        {
            string Converter(Match m)
            {
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

        /// <summary>
        /// simple container used by TokenParser and GetBasicTokenParser
        /// </summary>
        public struct TokenOrdering(List<TokenKey> first, List<TokenKey> second, List<TokenKey> captures)
        {
            public List<TokenKey> FirstLayer = first;
            public List<TokenKey> SecondLayer = second;
            public List<TokenKey> Captures = captures;
        }

        public enum TokenUsage
        {
            Never, Dependent, Always
        }
        public struct TokenInfo(char token, TokenUsage usage)
        {
            public char Token = token;
            public TokenUsage Usage = usage;
            public List<char> AssignedCapture = [];
            public List<char> AssignedGroup = [];
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
            /// Unwraps and sorts tokens for parsing.
            /// </summary>
            /// <param name="tokens">The tokens to unwrap.</param>
            /// <param name="makeNewReference">Whether to create a new reference for the token values.</param>
            /// <param name="formatted">The formatted string.</param>
            /// <returns>A TokenParser instance with unwrapped tokens.</returns>
            public static TokenParser UnrapTokens(Dictionary<TokenKey, string> tokens, bool makeNewReference = true, string formatted = "")
            {
                var top = new Dictionary<char, List<int>>();
                var bot = new Dictionary<char, List<int>>();
                var relations = new Dictionary<TokenKey, List<TokenKey>>();

                var keys = tokens.Keys.ToList();
                keys.Sort((a, b) => a.Priority - b.Priority);

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

                if (hasLastTop)
                    relations[lastTop] = new List<TokenKey>(children);

                var valuesCopy = makeNewReference ? new Dictionary<TokenKey, string>(tokens) : tokens;

                return new TokenParser(top, bot, relations, valuesCopy) { Formatted = formatted ?? string.Empty };
            }

            /// <summary>
            /// Returns a copy of the token values currently stored in the parser.
            /// </summary>
            public Dictionary<TokenKey, string> RerapTokens()
            {
                return new Dictionary<TokenKey, string>(tokenValues);
            }

            /// <summary>
            /// Gets the internal reference to the token value map used by the parser.
            /// </summary>
            public Dictionary<TokenKey, string> GetReference()
            {
                return tokenValues;
            }

            /// <summary>
            /// Produces the ordering lists (first layer, second layer, capture tokens) and sorts them for formatting.
            /// </summary>
            /// <returns>TokenOrdering struct containing ordered lists for formatting.</returns>
            public TokenOrdering AnalyzeTokens()
            {
                var first = new List<TokenKey>();
                var second = new List<TokenKey>();
                var captures = new List<TokenKey>();

                foreach (var val in tokenValues.Keys)
                {
                    if (char.IsDigit(val.Symbol))
                    {
                        captures.Add(val);
                        first.Add(val);
                        continue;
                    }

                    if (val.Priority < FORMAT_SPLIT)
                    {
                        first.Add(val);
                        second.Add(val);
                    }
                    else
                    {
                        second.Add(new TokenKey(val.Symbol, val.Priority - FORMAT_SPLIT));
                    }
                }

                // sort same as before
                second.Sort((a, b) => a.Priority - b.Priority);
                first.Sort((a, b) => a.Priority - b.Priority);

                return new TokenOrdering(first, second, captures);
            }

#pragma warning disable IDE0018

            /// <summary>
            /// Rebuilds capture tokens by resolving escaped child tokens using the tokenValues map.
            /// </summary>
            /// <param name="runtimeTokenValues">Dictionary of token values at runtime.</param>
            /// <param name="captureChars">List of capture token keys to rebuild.</param>
            public void RebuildCaptures(Dictionary<TokenKey, string> runtimeTokenValues, List<TokenKey> captureChars)
            {
                foreach (TokenKey captureKey in captureChars)
                {
                    string newVal = "";
                    if (!runtimeTokenValues.TryGetValue(captureKey, out string toParse)) continue;
                    int priorityCount = captureKey.Priority;
                    if (string.IsNullOrEmpty(toParse)) { runtimeTokenValues[captureKey] = ""; continue; }

                    for (int j = 0; j < toParse.Length; j++)
                    {
                        if (toParse[j] == ESCAPE_CHAR)
                        {
                            // next char is the child token's symbol
                            j++;
                            if (j >= toParse.Length) break;
                            char temp = toParse[j];
                            string toTry;
                            // search for the child by incrementing priorityCount and adding FORMAT_SPLIT
                            // this matches original behaviour where child priorities are sequential after the capture's priority
                            int searchPriority = priorityCount;
                            while (true)
                            {
                                searchPriority++;
                                var searchKey = new TokenKey(temp, searchPriority + FORMAT_SPLIT);
                                if (runtimeTokenValues.TryGetValue(searchKey, out toTry))
                                {
                                    newVal += toTry;
                                    break;
                                }
                                // safety: avoid infinite loop - if not found and searchPriority leaps too far, bail
                                if (searchPriority - priorityCount > 300) // arbitrary large guard
                                    break;
                            }
                        }
                        else
                            newVal += toParse[j];
                    }

                    runtimeTokenValues[captureKey] = newVal;
                }
            }

#pragma warning restore IDE0018

            /// <summary>
            /// Makes a child token constant by inlining a provided value into parent tokens and removing the child.
            /// </summary>
            /// <param name="token">Child token character to inline.</param>
            /// <param name="value">Replacement text to inline into parents where placeholders exist.</param>
            /// <returns>True if the operation succeeded, otherwise false.</returns>
            public bool MakeTokenConstant(char token, string value = "")
            {
                if (Formatted == default) return false;

                foreach (var kv in topLevelTokens)
                {
                    char topSymbol = kv.Key;
                    var priorities = kv.Value;

                    int removedPriority = -1;

                    foreach (int topPriority in priorities)
                    {
                        var rootKey = new TokenKey(topSymbol, topPriority);
                        if (!tokenRelations.ContainsKey(rootKey)) continue;

                        var relationsList = tokenRelations[rootKey];
                        int idx = relationsList.FindIndex(tk => tk.Symbol == token);
                        if (idx == -1) continue;

                        int childPriority = relationsList[idx].Priority;

                        if (char.IsDigit(topSymbol))
                        {
                            string newChildValue = Regex.Replace(tokenValues[new TokenKey(token, childPriority)], "\\{\\d+\\}", value);

                            var parentKey = new TokenKey(topSymbol, topPriority);
                            string parentValue = tokenValues[parentKey];

                            if (parentValue.Contains(string.Format("{0}{1}", ESCAPE_CHAR, token)))
                            {
                                tokenValues[parentKey] = parentValue.Replace(string.Format("{0}{1}", ESCAPE_CHAR, token), newChildValue);
                            }
                            else
                            {
                                string repStr = tokenValues[new TokenKey(token, childPriority)];
                                int relationIndex = relationsList.FindIndex(tk => tokenValues[tk] == repStr);
                                if (relationIndex == -1)
                                    throw new ArgumentException("The token given has been parsed incorrectly somehow. There is a bug in the code somewhere.");

                                tokenValues[relationsList[relationIndex]] = Regex.Replace(tokenValues[relationsList[relationIndex]], "\\{\\d+\\}", value);
                            }
                        }
                        else
                        {
                            tokenValues[new TokenKey(topSymbol, topPriority)] =
                                Regex.Replace(tokenValues[new TokenKey(topSymbol, topPriority)], "\\{\\d+\\}", value);
                        }

                        tokenValues.Remove(new TokenKey(token, childPriority));
                        removedPriority = childPriority;
                        break;
                    }

                    if (removedPriority != -1)
                    {
                        topLevelTokens[topSymbol].Remove(removedPriority);

                        int comp = (removedPriority > FORMAT_SPLIT) ? removedPriority - FORMAT_SPLIT : removedPriority;

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

                bottomLevelTokens.Remove(token);

                return true;
            }

            /// <summary>
            /// Sets the text for all tokens matching the given character in the parser's token map.
            /// </summary>
            /// <param name="c">Token character to modify.</param>
            /// <param name="text">Replacement text; leaving empty clears for top-level entries.</param>
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

            /// <summary>
            /// Surrounds the token values for all tokens matching the given character with the provided pre/post text.
            /// </summary>
            /// <param name="c">Character token to update.</param>
            /// <param name="preText">Text to prepend to each matching token value.</param>
            /// <param name="postText">Text to append to each matching token value.</param>
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

            /// <summary>
            /// Produces a diagnostic string representation of the internal parser state for debugging.
            /// </summary>
            /// <returns>String containing detailed parser diagnostics.</returns>
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