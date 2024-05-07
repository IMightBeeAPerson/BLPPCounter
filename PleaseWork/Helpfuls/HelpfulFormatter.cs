using PleaseWork.Settings;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PleaseWork.Helpfuls
{
    public static class HelpfulFormatter
    {
        public static readonly int FORMAT_SPLIT = 100;
        public static char DISPLAY_ESCAPE_CHAR => PluginConfig.Instance.TokenSettings.EscapeCharacter;
        public static char DISPLAY_BRACKET_OPEN => PluginConfig.Instance.TokenSettings.GroupBracketOpen;
        public static char DISPLAY_INSERT_SELF => PluginConfig.Instance.TokenSettings.GroupInsertSelf;
        public static char DISPLAY_BRACKET_CLOSE => PluginConfig.Instance.TokenSettings.GroupBracketClose;
        public static string NUMBER_TOSTRING_FORMAT => PluginConfig.Instance.FormatSettings.NumberFormat;

        public static (string, Dictionary<(char, int), string>, Dictionary<int, char>) ParseCounterFormat(string format)
        {
            Dictionary<(char, int), string> tokens = new Dictionary<(char, int), string>();
            Dictionary<int, char> priority = new Dictionary<int, char>();
            string formatted = "";
            int repIndex = 0, forRepIndex = 0, sortIndex = 0;
            bool capture = false;
            string captureStr = "";
            int ssIndex = -1;
            char num = (char)0;
            for (int i = 0; i < format.Length; i++)//[p$ ]&[[c&x]&]&1 / [o$ ]&[[f&y]&] &1&l
            {
                if (!IsSpecialChar(format[i]) || (format[i] == DISPLAY_ESCAPE_CHAR && IsSpecialChar(format[i + 1])))
                {
                    if (format[i] == DISPLAY_ESCAPE_CHAR) i++;
                    if (capture)
                    { captureStr += format[i]; continue; }
                    else
                    { formatted += format[i]; continue; }
                }
                if (!capture) formatted += $"{{{forRepIndex++}}}";
                if (format[i] == DISPLAY_BRACKET_OPEN)
                {
                    string bracket = "";
                    char symbol = format[++i];
                    int index = repIndex++, sIndex = sortIndex++;
                    while (format[++i] != DISPLAY_BRACKET_CLOSE && i < format.Length)
                    {
                        if (format[i] == DISPLAY_INSERT_SELF) { bracket += $"{{{index}}}"; continue; }
                        if (format[i] == DISPLAY_ESCAPE_CHAR)
                        {
                            tokens[(format[++i], FORMAT_SPLIT + sortIndex)] = $"{{{repIndex}}}";
                            priority[FORMAT_SPLIT + sortIndex++] = format[i];
                            bracket += $"{{{repIndex++}}}";
                            continue;
                        }
                        else bracket += format[i];
                    }
                    if (sortIndex == sIndex) sortIndex++;
                    if (repIndex == index) repIndex++;
                    if (capture)
                    {
                        captureStr += $"{DISPLAY_ESCAPE_CHAR}{symbol}";
                        sIndex += FORMAT_SPLIT;
                    }
                    priority[sIndex] = symbol;
                    tokens[(symbol, sIndex)] = bracket;

                    continue;
                }
                if (char.IsDigit(format[++i]))
                {
                    if (!capture)
                    {
                        capture = true;
                        captureStr = "";
                        ssIndex = sortIndex++;
                        num = format[i];
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
                if (capture)
                {
                    captureStr += $"{DISPLAY_ESCAPE_CHAR}{format[i]}";
                    tempIndex += FORMAT_SPLIT;
                }
                tokens[(format[i], tempIndex)] = $"{{{repIndex++}}}";
                priority[tempIndex] = format[i];
            }
            return (formatted, tokens, priority);
        }
        public static Func<Dictionary<char, object>, string> GetBasicTokenParser(string format,
            Action<Dictionary<(char, int), string>> settings,
            Action<Dictionary<(char, int), string>, Dictionary<(char, int), string>, Dictionary<int, char>, Dictionary<char, object>> varSettings)
        {
            Dictionary<(char, int), string> tokens;
            Dictionary<int, char> priority;
            string formatted;
            (formatted, tokens, priority) = ParseCounterFormat(format);
            /*Plugin.Log.Info("---------------");
            foreach (var token in tokens)
                Plugin.Log.Info($"{token.Key} || {token.Value}");//*/
            settings.Invoke(tokens);
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
                        if (toParse[j] == DISPLAY_ESCAPE_CHAR)
                            newVal += tokensCopy[(toParse[++j], ++priorityCount + FORMAT_SPLIT)];
                        else newVal += toParse[j];
                    tokensCopy[val] = newVal;
                }
                object[] firstArr = new object[first.Count];
                int i = 0;
                foreach ((char, int) val in first) firstArr[i++] = tokensCopy[val];
                object[] secondArr = new object[second.Count];
                i = 0;
                foreach ((char, int) val in second) secondArr[i++] = vals[val.Item1];
                return string.Format(string.Format(formatted, firstArr), secondArr);
            };
        }
        public static void SetText(Dictionary<(char, int), string> tokens, char c, string text = "") 
        { 
            List<(char, int)> toModify = new List<(char, int)>();
            foreach (var item in tokens.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2));
            foreach (var item in toModify) tokens[(item.Item1, item.Item2)] = text;
        }
        public static void SurroundText(Dictionary<(char, int), string> tokens, char c, string preText, string postText) 
        {
            List<(char, int, string)> toModify = new List<(char, int, string)>();
            foreach (var item in tokens.Keys) if (item.Item1 == c) toModify.Add((c, item.Item2, preText + tokens[item] + postText));
            foreach (var item in toModify) tokens[(item.Item1, item.Item2)] = item.Item3;
        }
        public static bool IsSpecialChar(char c) => c == DISPLAY_ESCAPE_CHAR || c == DISPLAY_BRACKET_OPEN || c == DISPLAY_BRACKET_CLOSE;
        public static string NumberToColor(float num) => num > 0 ? "<color=\"green\">" : num == 0 ? "<color=\"yellow\">" : "<color=\"red\">";
        public static string NumberToGradient(float variance, float num)
        {
            bool neg = num < 0;
            num = Mathf.Min(variance, Mathf.Abs(num));
            int toConvert = (int)Math.Abs(Math.Round((1.0f - num / variance) * 255.0f));
            toConvert = Math.Max(toConvert, 128);
            return neg ? $"<color=#{toConvert:X2}0000>" :
                $"<color=#00{toConvert:X2}00>";
        }
    }
}
