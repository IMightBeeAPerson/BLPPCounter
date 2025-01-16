using BLPPCounter.Utils;
using System;
using System.CodeDom.Compiler;
using System.CodeDom;
using System.IO;
using System.Text.RegularExpressions;
using static GameplayModifiers;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage;
using TMPro;
using UnityEngine.TextCore;
using Newtonsoft.Json.Linq;
namespace BLPPCounter.Helpfuls
{
    public static class HelpfulMisc
    {
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
        public static string GetModifierShortname(SongSpeed mod)
        {
            switch (mod)
            {
                case SongSpeed.SuperFast: return "sf";
                case SongSpeed.Faster: return "fs";
                case SongSpeed.Slower: return "ss";
                default: return "";
            }
        }
        public static SongSpeed GetModifierFromShortname(string mod)
        {
            switch (mod)
            {
                case "sf": return SongSpeed.SuperFast;
                case "fs": return SongSpeed.Faster;
                case "ss": return SongSpeed.Slower;
                default: return SongSpeed.Normal;
            }
        }
        public static string AddModifier(string name, SongSpeed modifier) => 
            modifier == SongSpeed.Normal ? name : GetModifierShortname(modifier) + char.ToUpper(name[0]) + name.Substring(1);
        public static string AddModifier(string name, string modifierName) =>
            modifierName.Equals("") ? name : modifierName + char.ToUpper(name[0]) + name.Substring(1);
        public static (float accRating, float passRating, float techRating) GetRatings(JToken diffData, SongSpeed speed, float modMult = 1.0f)
        {
            if (speed != SongSpeed.Normal) diffData = diffData["modifiersRating"];
            return (
                (float)diffData[AddModifier("accRating", speed)] * modMult,
                (float)diffData[AddModifier("passRating", speed)] * modMult,
                (float)diffData[AddModifier("techRating", speed)] * modMult
                );
        }
        public static (float accRating, float passRating, float techRating, float starRating) GetRatingsAndStar(JToken diffData, SongSpeed speed, float modMult = 1.0f)
        {
            (float accRating, float passRating, float techRating) = GetRatings(diffData, speed, modMult);
            if (speed != SongSpeed.Normal) diffData = diffData["modifiersRating"];
            return (accRating, passRating, techRating, (float)diffData[AddModifier("stars", speed)] * modMult);
        }
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
            if (values.Length > 32) throw new ArgumentException("Cannot convert more than 32 bools to a 32 bit number.");
            int outp = 0;
            for (int i = 0; i < values.Length; i++)
                outp |= (values[i] ? 1 : 0) << i;
            return outp;
        }
        public static void ConvertInt16ToBools(bool[] toLoad, short toConvert)
        {
            int count = 0;
            while (toConvert != 0)
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
            while (toConvert != 0)
            {
                if (toLoad.Length > count)
                    toLoad[count++] = toConvert % 2 == 1;
                else break;
                toConvert >>= 1;
                if (count == 1 && toConvert < 0) { toConvert *= -1; toConvert |= 1 << 30; } //manually shift signed bit over bc unsigned shifting isn't allowed in this version 0.0
            }
        }
        public static K GetKeyFromDictionary<K, V>(Dictionary<K, V> dict, V val) => 
            dict.ContainsValue(val) ? dict.First(kvp => kvp.Value.Equals(val)).Key : default;
        public static string GetKeyFromDictionary<V>(Dictionary<string, V> dict, V val) =>
            GetKeyFromDictionary<string, V>(dict, val) ?? val.ToString();
        public static PropertyInfo[] GetAllPropertiesUsingAttribute(Type theClass, Type theAttribute, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public) =>
            theClass.GetProperties(bindingFlags).Where(p => Attribute.IsDefined(p, theAttribute)).ToArray();
        public static FieldInfo[] GetAllFieldsUsingAttribute(Type theClass, Type theAttribute, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public) =>
            theClass.GetFields(bindingFlags).Where(p => Attribute.IsDefined(p, theAttribute)).ToArray();
        public static MemberInfo[] GetAllVariablesUsingAttribute(Type theClass, Type theAttribute, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public) =>
            GetAllPropertiesUsingAttribute(theClass, theAttribute, bindingFlags).Cast<MemberInfo>()
            .Union(GetAllFieldsUsingAttribute(theClass, theAttribute, bindingFlags)).ToArray();
        public static bool IsNumber(Type t)
        {
            TypeCode tc = Type.GetTypeCode(t);
            return tc > TypeCode.Char && tc < TypeCode.DateTime;
        }
        public static bool IsNumber(object o) => IsNumber(o.GetType());
        public static string SplitByUppercase(string s) => Regex.Replace(s, "(?!^)[A-Z][^A-Z]*", " $&");
        public static string ConvertColorToHex(Color c) => $"#{ToRgba(c):X8}";
        public static string ConvertColorToMarkup(Color c) => $"<color={ConvertColorToHex(c)}>";
        public static int ArgbToRgba(int argb) => (argb << 8) + (int)((uint)argb >> 24); //can't use triple shift syntax, so best I can do is casting :(
        public static int RgbaToArgb(int rgba) => (int)((uint)rgba >> 8) + (rgba << 24);
        public static int ToRgba(Color c) => ArgbToRgba(c.ToArgb());
        public static UnityEngine.Color TextToColor(string text)
        {
            if (text[0] == '#')
            {
                if (text.Length - 1 < 6)
                {
                    int len = text.Length;
                    for (int i = 1; i < len; i++)
                        text += $"{text[i]}{text[i]}";
                    text = text.Substring(len);
                }
                else text = text.Substring(1);
                return ConvertColor(Color.FromArgb(RgbaToArgb(int.Parse(text, System.Globalization.NumberStyles.HexNumber))));
            }
            if (text.Contains('"')) text = text.Replace("\"", "");
            return (UnityEngine.Color)(typeof(UnityEngine.Color).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(pi => pi.PropertyType == typeof(UnityEngine.Color))
                .FirstOrDefault(pi => pi.Name.Equals(text))?.GetValue(null, null) ?? default(UnityEngine.Color));
        }
        public static UnityEngine.Color ConvertColor(System.Drawing.Color color) =>
            new UnityEngine.Color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        public static System.Drawing.Color ConvertColor(UnityEngine.Color color) =>
            System.Drawing.Color.FromArgb((int)Math.Round(color.a * 0xFF), (int)Math.Round(color.r * 0xFF), (int)Math.Round(color.g * 0xFF), (int)Math.Round(color.b * 0xFF));
        public static BSMLParserParams AddToComponent(BSMLResourceViewController brvc, UnityEngine.GameObject container) =>
            BSMLParser.Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), brvc.ResourceName), container, brvc);
        public static void SetupTable(TextMeshProUGUI table, int maxWidth, string[][] values, int spaces, params string[] names)
        {
            string space = new string(' ', spaces);
            float[] maxLengths = new float[names.Length];
            string[] rows = new string[values.Length + 2];
            string format = $"|{space}{{0}}";
            for (int i = 1, c = 1; i < names.Length; i++)
                format += $"<space={{{c++}}}px>{space}|{space}{{{c++}}}";
            for (int i = 0; i < maxLengths.Length; i++)
                maxLengths[i] = Math.Max(values.Aggregate(0.0f, (total, strArr) => Math.Max(total, table.GetPreferredValues(strArr[i]).x)), table.GetPreferredValues(names[i]).x);
            object[] GetFormatVals(string[] row) {
                object[] outArr = new object[row.Length * 2 - 1];
                for (int i = 0, c = 0; i < row.Length; i++, c += 2)
                { 
                    outArr[c] = row[i]; 
                    if (i < row.Length - 1) outArr[c + 1] = maxLengths[i] - table.GetPreferredValues(row[i]).x; 
                }    
                return outArr;
            }
            rows[0] = string.Format(format, GetFormatVals(names)) + '\n';
            for (int i = 0; i < values.Length; i++)
                rows[i + 2] = string.Format(format, GetFormatVals(values[i]));
            int dashCount = maxWidth > 0 ?
                (int)Math.Ceiling(maxWidth / table.GetPreferredValues("-").x) :
                (int)Math.Ceiling(rows.Skip(2).Aggregate(0.0f, (total, str) => Math.Max(total, table.GetPreferredValues(str).x)) / table.GetPreferredValues("-").x);
            rows[1] = new string('-', dashCount);
            table.text = rows.Aggregate((total, str) => total + str + "\n");
        }
        public static void SetupTable(TextMeshProUGUI table, int maxWidth, string[][] values, params string[] names) =>
            SetupTable(table, maxWidth, values, 2, names);
        public static void SetupTable(TextMeshProUGUI table, int maxWidth, IEnumerable<KeyValuePair<string, string>> values, string key, string value, int spaces = 2) =>
            SetupTable(table, maxWidth, values.Select(kvp => new string[2] { kvp.Key, kvp.Value }).ToArray(), spaces, key, value);

        public static IEnumerable<T> GetDuplicates<T, V>(this IEnumerable<T> arr, Func<T, V> valToCompare)
        {
            Dictionary<V, (T, bool)> firstItems = new Dictionary<V, (T, bool)>();
            return arr.Where(item => 
            {
                V val = valToCompare(item);
                if (firstItems.ContainsKey(val))
                {
                    if (!firstItems[val].Item2) firstItems[val] = (firstItems[val].Item1, true);
                    return true;
                }
                firstItems.Add(val, (item, false));
                return false;
            }).Union(firstItems.Values.Where(vals => vals.Item2).Select(vals => vals.Item1));
        }
        public static IEnumerable<T> GetDuplicates<T>(this IEnumerable<T> arr)
        {
            Dictionary<T, bool> set = new Dictionary<T, bool>();
            foreach (T item in arr)
                if (set.ContainsKey(item) && !set[item]) set[item] = true;
                else set.Add(item, false);
            return arr.Where(item => set.ContainsKey(item) && set[item]);
        }
        public static IEnumerable<T> RemoveDuplicates<T, V>(this IEnumerable<T> arr, Func<T, V> valToCompare)
        {
            Dictionary<V, T> singleItems = new Dictionary<V, T>();
            foreach (T item in arr) 
            {
                V val = valToCompare(item);
                if (!singleItems.ContainsKey(val)) singleItems.Add(val, item);
            }
            return singleItems.Values;
        }
        public static IEnumerable<T> RemoveDuplicates<T>(this IEnumerable<T> arr)
        {
            HashSet<T> set = new HashSet<T>();
            return arr.Where(item => { if (set.Contains(item)) return false; else set.Add(item); return true; });
        }
        public static IEnumerable<T> RemoveAll<T>(this IEnumerable<T> arr, IEnumerable<T> other)
        {
            HashSet<T> hash = new HashSet<T>(other);
            return arr.Where(item => hash.Contains(item));
        }
        public static Dictionary<V, K> Swap<K, V>(this Dictionary<K, V> dict) =>
            dict.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        public static void MakeSpacesHaveSpace(this TMP_FontAsset font)
        {//This somehow worked first try. All it does is make the mark rich text actually highlight trailing spaces.
            Glyph g = font.characterLookupTable[' '].glyph;
            GlyphMetrics gm = g.metrics;
            gm.width = gm.horizontalAdvance;
            g.metrics = gm;
        }
        public static string AddSpaces(string str) => Regex.Replace(str, "(?<=[a-z])([A-Z])", " $+");
        public static T[][] RowToColumn<T>(IEnumerable<T> arr, int rowLengths = 0)
        {
            int len = arr.Count();
            return arr.Select(s =>
            {
                T[] newArr = new T[rowLengths > 0 ? rowLengths : len];
                newArr[0] = s;
                return newArr;
            }).ToArray();
        }
    }
}
