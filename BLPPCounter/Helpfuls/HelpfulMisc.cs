using IPA.Config.Data;
using BLPPCounter.Utils;
using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.CodeDom;
using System.IO;
using System.Text.RegularExpressions;
namespace BLPPCounter.Helpfuls
{
    public static class HelpfulMisc
    {
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
        public static string AddModifier(string name, string modifierName) =>
            modifierName.Equals("") ? name : modifierName + name.Substring(0, 1).ToUpper() + name.Substring(1);
        public static string ToLiteral(string input)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                    string outp = writer.ToString();
                    return Regex.Replace(outp.Substring(1, outp.Length - 2), "\"\\s*\\+\\s*\"", "");
                }
            }
        }
        public static short ConvertBoolsToInt16(bool[] values)
        {
            if (values.Length > 16) throw new ArgumentException("Cannot convert more than 16 bools to a 16 bit number.");
            short outp = 0;
            for (int i = 0; i < values.Length; i++)
                outp |= (short)((values[i] ? 1 : 0) << i);
            return outp;
        }
        public static int ConvertBoolsToInt32(bool[] values)
        {
            if (values.Length > 32) throw new ArgumentException("Cannot convert more than 16 bools to a 16 bit number.");
            int outp = 0;
            for (int i = 0; i < values.Length; i++)
                outp |= (values[i] ? 1 : 0) << i;
            return outp;
        }
        public static void ConvertInt16ToBools(bool[] toLoad, short toConvert)
        {
            int count = 0;
            while (toConvert > 0)
            {
                if (toLoad.Length > count)
                    toLoad[count++] = toConvert % 2 == 1;
                else break;
                toConvert >>= 1;
                if (count == 1 && toConvert < 0) { toConvert *= -1; toConvert |= 1 << 14; } //manually shift signed bit over bc unsigned shifting isn't allowed in this version 0.0
            }
        }
        public static void ConvertInt32ToBools(bool[] toLoad, int toConvert)
        {
            int count = 0;
            while (toConvert > 0)
            {
                if (toLoad.Length > count)
                    toLoad[count++] = toConvert % 2 == 1;
                else break;
                toConvert >>= 1;
                if (count == 1 && toConvert < 0) { toConvert *= -1; toConvert |= 1 << 30; } //manually shift signed bit over bc unsigned shifting isn't allowed in this version 0.0
            }
        }
    }
}
