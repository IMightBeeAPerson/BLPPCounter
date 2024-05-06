using IPA.Config.Data;
using PleaseWork.Utils;
using System;
using System.Collections.Generic;
namespace PleaseWork.Helpfuls
{
    public static class HelpfulMisc
    {
        public static readonly int FORMAT_SPLIT = 100;
        public static Modifier SpeedToModifier(double speed) => speed > 1.0 ? speed >= 1.5 ? Modifier.SuperFastSong : Modifier.FasterSong :
            speed != 1.0 ? Modifier.SlowerSong : Modifier.None;
        public static string PPTypeToRating(PPType type)
        {
            switch (type)
            {
                case PPType.Acc: return "accRating";
                case PPType.Tech: return "techRating";
                case PPType.Pass: return "passRating";
                case PPType.Star: return "stars";
                default: return "";
            }
        }
        public static string GetModifierShortname(Modifier mod)
        {
            switch (mod)
            {
                case Modifier.SuperFastSong: return "sf";
                case Modifier.FasterSong: return "fs";
                case Modifier.SlowerSong: return "ss";
                default: return "";
            }
        }
        public static string AddModifier(string name, Modifier modifier) => 
            modifier == Modifier.None ? name : GetModifierShortname(modifier) + name.Substring(0, 1).ToUpper() + name.Substring(1);
        public static (string, Dictionary<char, (string, int)>) ParseCounterFormat(string format)
        {
            Dictionary<char, (string, int)> outp = new Dictionary<char, (string, int)>();
            string formatted = "";
            int repIndex = 0, forRepIndex = 0, sortIndex = 0;
            bool capture = false;
            string captureStr = "";
            int ssIndex = -1;
            char num = (char)0;
            for (int i=0; i<format.Length; i++)//&:p$ &:&:c[&x] &:&:u / &o&:&:f [&y&:] &:&l
            {
                if (capture)
                {
                    if (format[i] != '&') { captureStr += format[i]; continue; }
                }
                else
                {
                    if (format[i] != '&') { formatted += format[i]; continue; }
                    formatted += $"{{{forRepIndex++}}}";
                }
                if (format[++i] == ':')
                {
                    string bracket = "";
                    char symbol = format[++i];
                    int index = repIndex++, sIndex = sortIndex++;
                    while ((format[++i] != '&' || format[i + 1] != ':') && i < format.Length)
                    {
                        if (format[i] == '$') { bracket += $"{{{index}}}"; i++; }
                        if (format[i] == '&' && format[i + 1] != ':')
                        { 
                            outp[format[++i]] = ($"{{{repIndex}}}", FORMAT_SPLIT + sortIndex++);
                            bracket += $"{{{repIndex++}}}";
                            continue; 
                        }
                        else bracket += format[i];
                    }
                    if (sortIndex == sIndex) sortIndex++;
                    if (repIndex == index) repIndex++;
                    outp[symbol] = (bracket, capture ? FORMAT_SPLIT + sIndex : sIndex);
                    if (capture) captureStr += $"&{symbol}";
                    i++;
                    continue;
                }
                if (char.IsDigit(format[i]))
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
                        outp[num] = (captureStr, ssIndex);
                        continue;
                    }
                }
                outp[format[i]] = ($"{{{repIndex++}}}", capture ? FORMAT_SPLIT + sortIndex++ : sortIndex++);
                if (capture) captureStr += $"&{format[i]}";
            }
            return (formatted, outp);
        }
        public static Func<Dictionary<char, object>, string> GetBasicTokenParser(string format,
            Action<Dictionary<char, (string, int)>> settings, 
            Action<Dictionary<char, (string, int)>, Dictionary<char, (string, int)>, Dictionary<char, object>> varSettings)
        {
            Dictionary<char, (string, int)> tokens;
            string formatted;
            (formatted, tokens) = ParseCounterFormat(format);
            settings.Invoke(tokens);
            List<(int, char)> first = new List<(int, char)>();
            List<(int, char)> second = new List<(int, char)>();
            List<char> captureChars = new List<char>();
            foreach (char c in tokens.Keys)
            {
                if (char.IsDigit(c)) { captureChars.Add(c); first.Add((tokens[c].Item2, c)); continue; }
                if (tokens[c].Item2 < FORMAT_SPLIT) { var hold = (tokens[c].Item2, c); first.Add(hold); second.Add(hold); }
                else second.Add((tokens[c].Item2 - FORMAT_SPLIT, c));
            }
            second.Sort((a, b) => a.Item1 - b.Item1);
            first.Sort((a, b) => a.Item1 - b.Item1);
            return (vals) =>
            {
                Dictionary<char, (string, int)> tokensCopy = new Dictionary<char, (string, int)>(tokens);
                varSettings.Invoke(tokens, tokensCopy, vals);
                foreach (char ch in captureChars)
                {
                    string newVal = "", toParse = tokensCopy[ch].Item1;
                    if (toParse.Length == 0) continue;
                    for (int j = 0; j < toParse.Length; j++)
                        if (toParse[j] == '&')
                            newVal += tokensCopy[toParse[++j]].Item1;
                        else newVal += toParse[j];
                    tokensCopy[ch] = (newVal, tokensCopy[ch].Item2);
                }
                object[] firstArr = new object[first.Count];
                int i = 0;
                foreach ((int, char) val in first) firstArr[i++] = tokensCopy[val.Item2].Item1;
                object[] secondArr = new object[second.Count];
                i = 0;
                foreach ((int, char) val in second) secondArr[i++] = vals[val.Item2];
                return string.Format(string.Format(formatted, firstArr), secondArr);
            };
        }
        public static string NumberToColor(float num) => num > 0 ? "<color=\"green\">+" : num == 0 ? "<color=\"yellow\">" : "<color=\"red\">";
    }
}
