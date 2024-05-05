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
                        //Plugin.Log.Info(formatted);
                        if (format[i] == '$') { bracket += $"{{{index}}}"; i++; }
                        if (format[i] == '&' && format[i + 1] != ':') { outp[format[++i]] = ($"{{{repIndex}}}", FORMAT_SPLIT + sortIndex++); bracket += $"{{{repIndex++}}}"; continue; }
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
    }
}
