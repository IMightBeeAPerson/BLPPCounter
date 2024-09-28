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
        public static Dictionary<string, string> RICH_SHORTHANDS => PC.TokenSettings.RichShorthands;
        public static readonly string NUMBER_TOSTRING_FORMAT;

        static HelpfulFormatter()
        {
            var hold = "";
            for (int i = 0; i < PC.DecimalPrecision; i++) hold += "#";
            NUMBER_TOSTRING_FORMAT = PC.DecimalPrecision > 0 ? PC.FormatSettings.NumberFormat.Replace("#","#." + hold) : PC.FormatSettings.NumberFormat;
        }

        public static (string, Dictionary<(char, int), string>, Dictionary<int, char>) ParseCounterFormat(string format)
        {
            Dictionary<(char, int), string> tokens = new Dictionary<(char, int), string>();
            Dictionary<int, char> priority = new Dictionary<int, char>();
            string formatted = "";
            int repIndex = 0, forRepIndex = 0, sortIndex = 0;
            bool capture = false;
            string captureStr = "", richVal = "";
            int ssIndex = -1;
            char num = (char)0;
            for (int i = 0; i < format.Length; i++)//[p$ ]&[[c&x]&]<1 / [o$ ]&[[f&y]&] >&l
            {
                if (!IsSpecialChar(format[i]) || (format[i] == ESCAPE_CHAR && IsSpecialChar(format[i + 1])))
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
            }
            if (capture)
                throw new FormatException($"Invalid capture format, must close capture bracket.\nSyntax: {CAPTURE_OPEN}<number> ... {CAPTURE_CLOSE}");
            return (formatted, tokens, priority);
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
        public static Func<Func<Dictionary<char, object>, string>> GetBasicTokenParser(
            string format,
            Func<TokenParser, string> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings)
        {
            Dictionary<(char, int), string> tokens;
            Dictionary<int, char> priority;
            string formatted;
            try
            {
                (formatted, tokens, priority) = ParseCounterFormat(format);
            }
            catch (Exception e)
            {
                Plugin.Log.Error("Formatting failed! " + e.Message);
                Plugin.Log.Error("Formatting: " + format);
                return null;
            }
            /*Plugin.Log.Info("---------------");
            foreach (var token in tokens)
                Plugin.Log.Info(HelpfulMisc.ToLiteral($"{token.Key} || {token.Value}"));
            Plugin.Log.Info("Formatted || " +  formatted);//*/
            return () =>
            {
                var thing = TokenParser.UnrapTokens(tokens, false, formatted);
                string newFormatted = settings.Invoke(thing);
                //Plugin.Log.Info(thing.ToString());
                List<(char, int)> first = new List<(char, int)>();
                List<(char, int)> second = new List<(char, int)>();
                List<(char, int)> captureChars = new List<(char, int)>();
                foreach ((char, int) val in tokens.Keys)
                {
                    if (char.IsDigit(val.Item1)) { captureChars.Add(val); first.Add(val); continue; }
                    if (val.Item2 < FORMAT_SPLIT) { first.Add(val); second.Add(val); }
                    else second.Add((val.Item1, val.Item2 - FORMAT_SPLIT));
                }
                second.Sort((a, b) => a.Item2 - b.Item2);
                first.Sort((a, b) => a.Item2 - b.Item2);
                return (vals) =>
                {
                    Dictionary<(char, int), string> tokensCopy = new Dictionary<(char, int), string>(tokens);
                    varSettings.Invoke(tokens, tokensCopy, priority, vals);
                    foreach ((char, int) val in captureChars)
                    {
                        string newVal = "", toParse = tokensCopy[val];
                        int priorityCount = val.Item2;
                        if (toParse.Length == 0) continue;
                        for (int j = 0; j < toParse.Length; j++)
                            if (toParse[j] == ESCAPE_CHAR)
                            {
                                string toTry = null;
                                char temp = toParse[++j];
                                while (toTry == null) tokensCopy.TryGetValue((temp, ++priorityCount + FORMAT_SPLIT), out toTry);
                                newVal += toTry;
                            }
                            else newVal += toParse[j];
                        tokensCopy[val] = newVal;
                    }
                    object[] firstArr = new object[first.Count];
                    int i = 0;
                    foreach ((char, int) val in first) firstArr[i++] = tokensCopy[val];
                    object[] secondArr = new object[second.Count];
                    i = 0;
                    foreach ((char, int) val in second) secondArr[i++] = vals[val.Item1];
                    return string.Format(string.Format(newFormatted, firstArr), secondArr);
                };
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
            List<(char, int, string)> toModify = new List<(char, int, string)>();
            foreach (var item in tokens.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2, preText + tokens[item] + postText));
            foreach (var item in toModify) tokens[(item.Item1, item.Item2)] = item.Item3;
        }
        /*public static string TurnTokenConstant(string format, Dictionary<(char, int), string> tokens, char c)
        {
            int priority = -1;
            (char, int) container = default;
            List<(char, int)> sortedKeys = tokens.Keys.ToList();
            sortedKeys.Sort((a, b) => a.Item2 - b.Item2);
            foreach (var item in sortedKeys)
            {
                //Plugin.Log.Info(item.Item2 + ": " + item.Item1);
                if (item.Item1 == c)
                {
                    priority = item.Item2;
                    if (priority <= FORMAT_SPLIT || container != default) break;
                }
                else if (tokens[item].Contains($"{ESCAPE_CHAR}{c}"))
                {
                    container = item;
                    if (priority != -1) break;
                }
            }
            if (priority > FORMAT_SPLIT)
            {
                //Plugin.Log.Info($"Priority: {priority} || Container: ({container.Item1}, {container.Item2})");
                tokens[container] = tokens[container].Replace($"{ESCAPE_CHAR}{c}", tokens[(c, priority)]);
                tokens.Remove((c, priority));
                return format;
            }
            return format;
        }//*/
        public static bool IsSpecialChar(char c) => c == ESCAPE_CHAR || c == RICH_SHORT || c == GROUP_OPEN || c == GROUP_CLOSE || c == CAPTURE_OPEN || c == CAPTURE_CLOSE;
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
                foreach (var key in topLevelTokens.Keys)
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
                            string newValue = Regex.Replace(tokenValues[(token, tokenPriority)], "\\{\\d\\}", value);
                            Plugin.Log.Info(newValue);
                            tokenValues[(key, priority)] = tokenValues[(key, priority)].Replace($"{ESCAPE_CHAR}{token}", newValue);
                        } else
                            tokenValues[(key, priority)] = Regex.Replace(tokenValues[(key, priority)], "\\{\\d\\}", value);
                        tokenValues.Remove((token, tokenPriority));
                        toRemove = tokenPriority;
                        break;
                    }
                    if (toRemove != -1) topLevelTokens[key].Remove(toRemove);
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
