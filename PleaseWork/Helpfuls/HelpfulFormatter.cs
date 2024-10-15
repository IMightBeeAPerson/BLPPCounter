using BeatmapEditor3D;
using ModestTree;
using PleaseWork.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using static BeatSaberMarkupLanguage.Components.KEYBOARD;

namespace PleaseWork.Helpfuls
{
    public static class HelpfulFormatter
    {
        private static PluginConfig PC => PluginConfig.Instance;

        public static readonly int FORMAT_SPLIT = 100;
        public static int GRAD_VARIANCE => PC.GradVal;
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
        public static readonly HashSet<char> SPECIAL_CHARS;
        public static Dictionary<string, string> RICH_SHORTHANDS => PC.TokenSettings.RichShorthands;
        public static readonly string NUMBER_TOSTRING_FORMAT;

        static HelpfulFormatter()
        {
            var hold = "";
            for (int i = 0; i < PC.DecimalPrecision; i++) hold += "#";
            NUMBER_TOSTRING_FORMAT = PC.DecimalPrecision > 0 ? PC.FormatSettings.NumberFormat.Replace("#","#." + hold) : PC.FormatSettings.NumberFormat;
            SPECIAL_CHARS = new HashSet<char>() { ESCAPE_CHAR, RICH_SHORT, DELIMITER, GROUP_OPEN, GROUP_CLOSE, INSERT_SELF, CAPTURE_OPEN, CAPTURE_CLOSE };
        }

        public static (string, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<(char, int), string[]>) ParseCounterFormat(string format)
        {
            Dictionary<(char, int), string> tokens = new Dictionary<(char, int), string>();
            Dictionary<(char, int), string[]> extraArgs = new Dictionary<(char, int), string[]>();
            Dictionary<int, char> priority = new Dictionary<int, char>();
            string formatted = "";
            int repIndex = 0, forRepIndex = 0, sortIndex = 0;
            bool capture = false;
            string captureStr = "", richVal = "";
            int ssIndex = -1;
            char num = (char)0;
            for (int i = 0; i < format.Length; i++)//[p$ ]&[[c&x]&]<1 / [o$ ]&[[f&y]&] >&l<2\n&m[t\n$]>
            {
                if (!IsSpecialChar(format[i]) || (format[i] == ESCAPE_CHAR && (IsSpecialChar(format[i + 1]) || format[i + 1] == PARAM_OPEN)))
                {
                    if (format[i] == ESCAPE_CHAR) i++;
                    if (capture)
                    { captureStr += format[i]; continue; }
                    else
                    { formatted += format[i]; continue; }
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
                    if (!char.IsLetter(symbol)) 
                        throw new FormatException($"Invalid group format, must define what letter group corresponds to.\nSyntax: {GROUP_OPEN}<letter> ... {GROUP_CLOSE}");
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
       
        public static Func<Func<Dictionary<char, object>, string>> GetBasicTokenParser(
            string format,
            Action<TokenParser> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings)
            => GetBasicTokenParser(format, settings, varSettings, null, null);
        public static Func<Func<Dictionary<char, object>, string>> GetBasicTokenParser(
            string format,
            Action<TokenParser> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings,
            Func<char, string[], bool> confirmFormat,
            Func<char, string[], Dictionary<char, object>, string> implementArgs)
        {
            Dictionary<(char, int), string> tokens;
            Dictionary<(char, int), string[]> extraArgs;
            Dictionary<int, char> priority;
            string formatted;
            confirmFormat = GetParentConfirmFormat(confirmFormat);
            implementArgs = GetParentImplementArgs(implementArgs);
            try
            {
                (formatted, tokens, priority, extraArgs) = ParseCounterFormat(format);
                foreach (var val in extraArgs.Keys)
                {
                    if (!confirmFormat.Invoke(val.Item1, extraArgs[val]))
                        throw new FormatException($"Invalid extra parameter format, one of three things happened, too many arguments, too few arguments, or a non reference where a reference should be.\nThis is for the char '{val.Item1}'");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Formatting failed! " + e.Message);
                Plugin.Log.Error("Formatting: " + format);
                return null;
            }
            /*Plugin.Log.Info("---------------");
                    foreach (var token in tokens)
                        Plugin.Log.Info($"{token.Key} || " + HelpfulMisc.ToLiteral(token.Value));
                    Plugin.Log.Info("Formatted || " + formatted);//*/
            return () =>
            {
                var thing = TokenParser.UnrapTokens(tokens, true, formatted);
                settings.Invoke(thing);
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
                        Plugin.Log.Info($"{token.Key} || " + HelpfulMisc.ToLiteral(token.Value));
                    Plugin.Log.Info("Formatted || " + formatted);//*/
                    Dictionary<(char, int), string> tokensCopy2 = new Dictionary<(char, int), string>(tokensCopy1);
                    varSettings.Invoke(tokensCopy1, tokensCopy2, priority, vals);
                    foreach (var val in extraArgs.Keys)
                        vals.TryAdd(val.Item1, implementArgs.Invoke(val.Item1, extraArgs[val], vals));
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
                                while (toTry == null) tokensCopy2.TryGetValue((temp, ++priorityCount + FORMAT_SPLIT), out toTry);
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
        private static Func<char, string[], bool> GetParentConfirmFormat(Func<char, string[], bool> child)
        {
            return (paramChar, values) =>
            {
                if (child != null && child.Invoke(paramChar, values)) return true;
                switch (paramChar)
                {
                    case 's':
                        return values.Length == 1 && IsReferenceChar(values[0]);
                    default: return false;
                }
            };
        }
        private static Func<char, string[], Dictionary<char, object>, string> GetParentImplementArgs(Func<char, string[], Dictionary<char, object>, string> child) =>
            (paramChar, values, vals) =>
            {
                if (child != null) {
                    string outp = child.Invoke(paramChar, values, vals);
                    if (outp.Length != 0) return outp;
                }
                switch (paramChar)
                {
                    case 's':
                        bool isOne = decimal.Parse(vals[values[0][0]] + "") == 1;
                        return isOne ? "" : "s";
                    default: return "";
                }
            };
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
            List<(char, int, string)> toModify = new List<(char, int, string)>();
            foreach (var item in tokens.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2, preText + tokens[item] + postText));
            foreach (var item in toModify) tokens[(item.Item1, item.Item2)] = item.Item3;
        }
        public static bool IsReferenceChar(string s) => s.Length == 1 && char.IsLetter(s[0]) && !IsSpecialChar(s[0]);
        public static bool IsSpecialChar(char c) => SPECIAL_CHARS.Contains(c);
        public static string NumberToColor(float num) => num > 0 ? "<color=\"green\">" : num == 0 ? "<color=\"yellow\">" : "<color=\"red\">";
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
            bool neg = num < 0;
            num = Mathf.Min(variance, Mathf.Abs(num));
            if (num == 0) return "<color=#FFFF00>";
            int toConvert = (int)Math.Abs(Math.Round((neg ? 1.0f - num / variance : num / variance) * 255.0f));
            toConvert = Math.Max(toConvert, 128);
            return neg ? $"<color=#{toConvert:X2}0000>" :
                $"<color=#00{toConvert:X2}00>";
        }
        public static string NumberToGradient(float num) => NumberToGradient(GRAD_VARIANCE, num);
        public static string GetWeightedRankColor(int rank)
        {
            int c = -1;
            var arr = PluginConfig.Instance.FormatSettings.WeightedRankColors.ToArray();
            while (arr[++c].Rank < rank && c + 1 < arr.Length) ;
            return "<color=#" + arr[c].Color + ">";
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
                this.Formatted = "";
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
                    outp += $"\n{token.Key} || {HelpfulMisc.ToLiteral(token.Value)}";
                return outp + "\n----------------------------";
            }
        }
    }
}
