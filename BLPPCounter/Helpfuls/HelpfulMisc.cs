using BeatLeader.Models;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using IPA.Config.Data;
using Newtonsoft.Json.Linq;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using static GameplayModifiers;
namespace BLPPCounter.Helpfuls
{
    public static class HelpfulMisc
    {
        /// <summary>
        /// Returns a number based off the song speed, in the correct number, with slower being 0 and super fast being 3.
        /// </summary>
        /// <param name="ss">The song speed to convert to a number</param>
        /// <returns>The ordered number for given song speed</returns>
        public static int OrderSongSpeedCorrectly(SongSpeed ss)
        {
            switch (ss)
            {
                case SongSpeed.Slower: return 0;
                case SongSpeed.Normal: return 1;
                case SongSpeed.Faster: return 2;
                case SongSpeed.SuperFast: return 3;
                default: return -1;
            }
        }
        public static SongSpeed OrderSongSpeedCorrectly(int ss)
        {
            switch (ss)
            {
                case 0: return SongSpeed.Slower;
                case 1: return SongSpeed.Normal;
                case 2: return SongSpeed.Faster;
                case 3: return SongSpeed.SuperFast;
                default: return default;
            }
        }
        public static string PPTypeToRating(PPType type)
        {
            switch (type)
            {
                case PPType.Acc: return "accRating";
                case PPType.Tech: return "techRating";
                case PPType.Pass: return "passRating";
                case PPType.Star: return TheCounter.Leaderboard == Leaderboards.Accsaber ? "complexity" : "stars";
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
            if (!(diffData["difficulty"] is null)) diffData = diffData["difficulty"];
            if (speed != SongSpeed.Normal) diffData = diffData["modifiersRating"];
            return (
                (float)diffData[AddModifier("accRating", speed)] * modMult,
                (float)diffData[AddModifier("passRating", speed)] * modMult,
                (float)diffData[AddModifier("techRating", speed)] * modMult
                );
        }
        public static (float accRating, float passRating, float techRating, float starRating) GetRatingsAndStar(JToken diffData, SongSpeed speed, float modMult = 1.0f)
        {
            float accRating, passRating, techRating;
            try
            {
                (accRating, passRating, techRating) = GetRatings(diffData, speed, modMult);
            } catch (Exception)
            {
                return (0, 0, 0, 0);
            }
            if (!(diffData["difficulty"] is null)) diffData = diffData["difficulty"];
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
        public static long ConvertBoolsToInt64(bool[] values)
        {
            if (values.Length > 64) throw new ArgumentException("Cannot convert more than 64 bools to a 64 bit number.");
            long outp = 0;
            for (int i = 0; i < values.Length; i++)
                outp |= (values[i] ? 1L : 0L) << i;
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
        public static void ConvertInt64ToBools(bool[] toLoad, long toConvert)
        {
            int count = 0;
            while (toConvert != 0)
            {
                if (toLoad.Length > count)
                    toLoad[count++] = toConvert % 2 == 1;
                else break;
                toConvert >>= 1;
                if (count == 1 && toConvert < 0) { toConvert *= -1; toConvert |= 1L << 62; } //manually shift signed bit over bc unsigned shifting isn't allowed in this version 0.0
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
            if (t is null) return false;
            TypeCode tc = Type.GetTypeCode(t);
            return tc > TypeCode.Char && tc < TypeCode.DateTime;
        }
        public static bool IsNumber(object o) => IsNumber(o?.GetType());
        public static string SplitByUppercase(string s) => Regex.Replace(s, "(?!^)[A-Z][^A-Z]*", " $&");
        public static string ConvertColorToHex(System.Drawing.Color c) => $"#{ToRgba(c):X8}";
        public static string ConvertColorToHex(UnityEngine.Color c) => $"#{ToRgba(c):X8}";
        public static string ConvertColorToMarkup(System.Drawing.Color c) => $"<color={ConvertColorToHex(c)}>";
        public static int ArgbToRgba(int argb) => (argb << 8) + (int)((uint)argb >> 24); //can't use triple shift syntax, so best I can do is casting :(
        public static int RgbaToArgb(int rgba) => (int)((uint)rgba >> 8) + (rgba << 24);
        public static int ToRgba(System.Drawing.Color c) => ArgbToRgba(c.ToArgb());
        public static int ToRgba(UnityEngine.Color c) => ((int)Math.Round(c.r * 0xFF) << 24) + ((int)Math.Round(c.g * 0xFF) << 16) + ((int)Math.Round(c.b * 0xFF) << 8) + (int)Math.Round(c.a * 0xFF);
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
                return ConvertColor(System.Drawing.Color.FromArgb(RgbaToArgb(int.Parse(text, System.Globalization.NumberStyles.HexNumber))));
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
        public static System.Drawing.Color Multiply(System.Drawing.Color a, float b) =>
            System.Drawing.Color.FromArgb((int)Math.Round(a.A * b), (int)Math.Round(a.R * b), (int)Math.Round(a.G * b), (int)Math.Round(a.B * b));
        public static System.Drawing.Color Blend(System.Drawing.Color a, System.Drawing.Color b, float aWeight = 0.5f, float bWeight = 0.5f)
        {
            aWeight *= 2f;
            bWeight *= 2f;
            float newA = a.A * aWeight + b.A * bWeight,
                newR = a.R * aWeight + b.R * bWeight,
                newG = a.G * aWeight + b.G * bWeight,
                newB = a.B * aWeight + b.B * bWeight;
            float maxMult = Math.Min(255f / Math.Max(Math.Max(newA, Math.Max(newR, Math.Max(newG, newB))), 0.1f), 1f);
            return System.Drawing.Color.FromArgb((int)Math.Round(newA * maxMult), (int)Math.Round(newR * maxMult), (int)Math.Round(newG * maxMult), (int)Math.Round(newB * maxMult));
        }
        public static BSMLParserParams AddToComponent(BSMLResourceViewController brvc, GameObject container) =>
#if NEW_VERSION
            BSMLParser.Instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), brvc.ResourceName), container, brvc); // 1.37.0 and above
#else
            BSMLParser.instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), brvc.ResourceName), container, brvc); // 1.34.2 and below
#endif
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
        /// <summary>
        /// Adds a spaces for words that are combined by putting spaces between lowercase and capital letters 
        /// </summary>
        /// <param name="str">The word to add spaces to</param>
        /// <returns>The words with spaces</returns>
        public static string AddSpaces(string str) => Regex.Replace(str, "(?<=[a-z])([A-Z])", " $+");
        public static T[][] RowToColumn<T>(this IEnumerable<T> arr, int rowLengths = 0)
        {
            int len = arr.Count();
            return arr.Select(s =>
            {
                T[] newArr = new T[rowLengths > 0 ? rowLengths : len];
                newArr[0] = s;
                return newArr;
            }).ToArray();
        }
        public static string Print<T>(this T[][] matrix)
        {
            string outp = "";
            foreach (T[] arr in matrix)
                outp += arr.Aggregate("", (total, current) => total + " | " + current).Substring(1) + " |\n";
            return outp;
        }
        public static string Print<K, V>(this Dictionary<K, V> dict, string delimiter = ",\n")
        {
            string outp = "";
            foreach (var val in dict)
                outp += $"[{val.Key}, {val.Value}]{delimiter}";
            return outp;
        }
        public static string Print<K, V>(this Dictionary<K, V> dict, Func<K, string> keyToString, Func<V, string> valToString, string delimiter = ",\n")
        {
            string outp = "";
            foreach (var val in dict)
                outp += $"[{keyToString(val.Key)}, {valToString(val.Value)}]{delimiter}";
            return outp;
        }
        public static string Print<T>(this IEnumerable<T> arr)
        {
            if (arr.Count() == 0) return "[]";
            string outp = "";
            foreach (T item in arr)
                outp += ", " + item;
            return $"[{outp.Substring(2)}]";
        }
        public static string Print(this IEnumerable<string> arr)
        {
            if (arr.Count() == 0) return "[]";
            string outp = "";
            foreach (string item in arr)
                outp += ", \"" + item + '"';
            return $"[{outp.Substring(2)}]";
        }
        public static string Print<T>(this IEnumerable<T> arr, Func<T, string> valToString)
        {
            if (arr.Count() == 0) return "[]";
            string outp = "";
            foreach (T item in arr)
                outp += ", " + valToString(item);
            return $"[{outp.Substring(2)}]";
        }
        //TODO, DOESN'T WORK RN
        /*public static float GetLengthOfText(this TextMeshProUGUI guiText, string text, Dictionary<uint, TMP_Character> givenLookupTable = null)
        {
            float outp = 0;
            bool hasGivenLookupTable = !(givenLookupTable is null);
            Dictionary<uint, TMP_Character> lookupTable = hasGivenLookupTable ? givenLookupTable : guiText.font.characterLookupTable;
            for (int i = 0; i < text.Length; i++)
            {
                if (!lookupTable.TryGetValue(text[i], out TMP_Character c))
                {
                    List<TMP_FontAsset> backups = guiText.font.fallbackFontAssetTable;
                    foreach (TMP_FontAsset asset in backups)
                        if (asset.characterLookupTable.TryGetValue(text[i], out c))
                            break;
                    if (hasGivenLookupTable) givenLookupTable.Add(text[i], c);
                }
                outp += c.glyph.metrics.width;
            }
            Plugin.Log.Info($"Given: {text} || Mine: {outp} || Unity's: {guiText.GetPreferredValues(text).x}");
            return outp;
        }*/
        public static int CompareValues<T>(JToken item1, JToken item2, string value, Func<string, T> caster, Func<T, T, int> comparer) =>
            comparer(caster(item1[value].ToString()), caster(item2[value].ToString()));
        public static int CompareValues<T>(JToken item1, JToken item2, string value, Func<T, T, int> comparer) where T : class =>
            comparer(item1[value] as T, item2[value] as T);
        public static int CompareStructValues<T>(JToken item1, JToken item2, string value, Func<T, T, int> comparer) where T : struct =>
           comparer((T)Convert.ChangeType(item1[value], typeof(T)), (T)Convert.ChangeType(item2[value], typeof(T)));
        public static void UpdateListSetting(this ListSetting menu, List<string> newValues)//https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments
        {
#if NEW_VERSION
            menu.Values = newValues; // 1.37.0 and above
#else
            menu.values = newValues.Cast<object>().ToList(); // 1.34.2 and below
#endif
            if (!newValues.Any(str => str.Equals((string)menu.Value))) menu.Value = newValues[0];
            else menu.Value = menu.Value; //seems stupid but calls the update method.
            menu.ApplyValue(); //Update the actual value
        }
        /// <summary>
        /// Checks if status given by BeatLeader's api is usable. Current usable statuses are listed below:
        /// <list type="bullet">
        ///     <item>
        ///     <term>Unranked</term>
        ///     <description>Map is unranked. The leaderboard <b>does not</b> show pp. Only used if <see cref="PluginConfig.UseUnranked"/> is enabled. (<paramref name="status"/> = 0)</description>
        ///     </item>
        ///     <item>
        ///     <term>Nominated</term>
        ///     <description>Map is nominated, 2 steps from being ranked. The leaderboard <b>does not</b> show pp. (<paramref name="status"/> = 1)</description>
        ///     </item>
        ///     <item>
        ///     <term>Qualified</term>
        ///     <description>Map is qualified, 1 step from being ranked. The leaderboard shows pp. (<paramref name="status"/> = 2)</description>
        ///     </item>
        ///     <item>
        ///     <term>Ranked</term>
        ///     <description>Map is ranked. The leaderboard shows pp. (<paramref name="status"/> = 3)</description>
        ///     </item>
        ///     <item>
        ///     <term>Event</term>
        ///     <description>Map is part of event. The leaderboard shows pp. (<paramref name="status"/> = 6)</description>
        ///     </item>
        /// </list>
        /// </summary>
        /// <param name="status">The status value returned from BeatLeader's api.</param>
        /// <returns>Whether or not the api will provide enough info to calculate pp.</returns>
        public static bool StatusIsUsable(int status) => (status > 0 && status <= 3) || status == 6 || (PluginConfig.Instance.UseUnranked && status == 0);
        /// <summary>
        /// Checks if a given map selection is usable. If it is using a score saber map or an acc saber map, then it is always usable. 
        /// Otherwise, it checks the beat leader status against the list below:
        /// <list type="bullet">
        ///     <item>
        ///     <term>Unranked</term>
        ///     <description>Map is unranked. The leaderboard <b>does not</b> show pp. Only used if <see cref="PluginConfig.UseUnranked"/> is enabled. (status = 0)</description>
        ///     </item>
        ///     <item>
        ///     <term>Nominated</term>
        ///     <description>Map is nominated, 2 steps from being ranked. The leaderboard <b>does not</b> show pp. (status = 1)</description>
        ///     </item>
        ///     <item>
        ///     <term>Qualified</term>
        ///     <description>Map is qualified, 1 step from being ranked. The leaderboard shows pp. (status = 2)</description>
        ///     </item>
        ///     <item>
        ///     <term>Ranked</term>
        ///     <description>Map is ranked. The leaderboard shows pp. (status = 3)</description>
        ///     </item>
        ///     <item>
        ///     <term>Event</term>
        ///     <description>Map is part of event. The leaderboard shows pp. (status = 6)</description>
        ///     </item>
        /// </list>
        /// </summary>
        /// <param name="ms">The map selection to check.</param>
        /// <returns>Whether or not the map selection has a usable status.</returns>
        public static bool StatusIsUsable(MapSelection ms) => ms.Mode.Equals(Utils.Map.SS_MODE_NAME) || ms.Mode.Equals(Utils.Map.AP_MODE_NAME) || StatusIsUsable((int)ms.MapData.Item2["status"]);
        /// <summary>
        /// Create a square matrix (a matrix with all arrays inside it being the same size).
        /// </summary>
        /// <typeparam name="T">The type of matrix.</typeparam>
        /// <param name="rows">How many rows to have (amount of arrays).</param>
        /// <param name="columns">How many columns to have (the lengths of arrays).</param>
        /// <returns>An empty matrix with all arrays inside of it initialized.</returns>
        public static T[][] CreateSquareMatrix<T>(int rows, int columns)
        {
            T[][] outp = new T[rows][];
            for (int i = 0; i < rows; i++)
                outp[i] = new T[columns];
            return outp;
        }
        /// <summary>
        /// Checks if a given matrix is a square (all of its arrays are the same length).
        /// </summary>
        /// <typeparam name="T">The type of the matrix</typeparam>
        /// <param name="matrix">The matrix to validate</param>
        /// <returns>Whether or not the matrix is a square.</returns>
        public static bool ValidateMatrixAsSquare<T>(T[][] matrix)
        {
            int len = matrix[0].Length;
            foreach (T[] arr in matrix)
                if (arr.Length != len) return false;
            return true;
        }
        /// <summary>
        /// Changes the song speed of the gameplay modifier panel to the given song speed.
        /// </summary>
        /// <param name="gmpc">the gameplay modifier panel</param>
        /// <param name="speed">the new speed to set it to</param>
        public static void ChangeSongSpeed(this GameplayModifiersPanelController gmpc, SongSpeed speed)
        {
            if (gmpc.gameplayModifiers.songSpeed == speed) return; //If the speed is already set to this, no work need to be done.
            gmpc.SetData(gmpc.gameplayModifiers.CopyWith(songSpeed: speed)); //Copy the current mods while only changing the speed, then set it to be correct.
            (gmpc as IRefreshable).Refresh(); //Refresh the display so that the new modifier is shown correctly.
        }
        public static void ChangeMinValue(this SliderSetting ss, float newVal) => ChangeValue(ss, newVal, true);
        public static void ChangeMaxValue(this SliderSetting ss, float newVal) => ChangeValue(ss, newVal, false);
        private static void ChangeValue(SliderSetting ss, float newVal, bool isMinVal)
        {
#if NEW_VERSION
            float currentVal = ss.Slider.value;
            if (isMinVal) ss.Slider.minValue = newVal; else ss.Slider.maxValue = newVal;
            ss.Slider.numberOfSteps = (int)Math.Round((ss.Slider.maxValue - ss.Slider.minValue) / ss.Increments) + 1;
            ss.Slider.value = currentVal; // 1.37.0 and above
#else
            float currentVal = ss.slider.value;
            if (isMinVal) ss.slider.minValue = newVal; else ss.slider.maxValue = newVal;
            ss.slider.numberOfSteps = (int)Math.Round((ss.slider.maxValue - ss.slider.minValue) / ss.increments) + 1;
            ss.slider.value = currentVal; //1.34.2 and below
#endif
        }
        /// <summary>
        /// Ties 2 <see cref="SliderSetting"/>s together so that the value of the <paramref name="max"/> <see cref="SliderSetting"/> never goes below
        /// the value of the <paramref name="min"/> <see cref="SliderSetting"/>, and the value of the <paramref name="min"/> <see cref="SliderSetting"/>
        /// never goes above the value of the <paramref name="max"/> <see cref="SliderSetting"/>. Should a <see cref="SliderSetting"/> break this rule, 
        /// instead of preventing it, this function will simply set the <see cref="SliderSetting"/> to match the value of the other.
        /// </summary>
        /// <param name="min">The <see cref="SliderSetting"/> that sets the minimum of the other.</param>
        /// <param name="max">The <see cref="SliderSetting"/> that sets the maximum of the other.</param>
        public static void CoupleMinMaxSliders(SliderSetting min, SliderSetting max)
        {
#if NEW_VERSION
            min.Slider.valueDidChangeEvent += (slider, newVal) => { if (max.Value < newVal) min.Value = max.Value; };
            max.Slider.valueDidChangeEvent += (slider, newVal) => { if (min.Value > newVal) max.Value = min.Value; }; // 1.37.0 and above
#else
            min.slider.valueDidChangeEvent += (slider, newVal) => { if (max.Value < newVal) min.Value = max.Value; };
            max.slider.valueDidChangeEvent += (slider, newVal) => { if (min.Value > newVal) max.Value = min.Value; }; //1.34.2 and below
#endif
        }
        public static void PrintVars(params (string varName, object value)[] vars)
        {
            Plugin.Log.Info(vars.Aggregate("", (total, current) => total + $", {current.varName} = {current.value}").Substring(2));
        }
        /// <summary>
        /// Sets the increment for the given <see cref="SliderSetting"/>s to the given number.
        /// </summary>
        /// <param name="incrementNum">The number to set the increment to.</param>
        /// <param name="toSet">The <see cref="SliderSetting"/>s to set.</param>
        public static void SetIncrements(int incrementNum, params SliderSetting[] toSet)
        {
            foreach (SliderSetting s in toSet)
            {
#if NEW_VERSION
                s.Increments = incrementNum;
                s.Slider.numberOfSteps = (int)Math.Round((s.Slider.maxValue - s.Slider.minValue) / incrementNum) + 1;
#else
                s.increments = incrementNum;
                s.slider.numberOfSteps = (int)Math.Round((s.slider.maxValue - s.slider.minValue) / incrementNum) + 1;
#endif
            }
        }
        /// <summary>
        /// Takes any given <see cref="ITuple"/> and converts it into an <see cref="IEnumerable{T}">IEnumerable&lt;object&gt;</see>.
        /// </summary>
        /// <param name="tuple">The <see cref="ITuple"/> to convert.</param>
        /// <returns>Outputs an <see cref="IEnumerable{T}">IEnumerable&lt;object&gt;</see> that contains the tuple objects in order.</returns>
        public static IEnumerable<object> TupleToEnumerable(ITuple tuple) =>
            Enumerable.Range(0, tuple.Length).Select(i => tuple[i]?.ToString());
        /// <summary>
        /// Checks if a given <see cref="IEnumerable"/> is filled with only default values. 
        /// </summary>
        /// <param name="vals">The <see cref="IEnumerable"/> to check.</param>
        /// <returns>If there is at least one value that is not the default value in <paramref name="vals"/>, this will return false.
        /// Otherwise, it returns true.</returns>
        public static bool FilledWithDefaults<T>(this IEnumerable<T> vals)
        {
            foreach (T val in vals)
                if (!val.Equals(default(T))) return false;
            return true;
        }
        /// <summary>
        /// Attempts to move into a <see cref="JToken">JToken</see>.
        /// </summary>
        /// <param name="data">Parent <see cref="JToken">JToken</see>.</param>
        /// <param name="name">The key to attempt to move into.</param>
        /// <returns>Either the child <see cref="JToken">JToken</see> or if it is null the parent <see cref="JToken">JToken</see>.</returns>
        public static JToken TryEnter(this JToken data, string name) => data[name] ?? data;
        /// <summary>
        /// Inserts a value at <paramref name="insertIndex"/>, sifting all values at and below <paramref name="insertIndex"/> down.
        /// This will remove the value at the last index and return it. This action does not change the size of <paramref name="arr"/>.
        /// </summary>
        /// <param name="arr">The array to preform this action on. The array must be of length 2 or greater.</param>
        /// <param name="insertIndex">The index to insert the value at. Must be within the bounds of <paramref name="arr"/>.</param>
        /// <param name="value">The value to be inserted.</param>
        /// <returns>The value at the last index in <paramref name="arr"/>.</returns>
        public static T SiftDown<T>(T[] arr, int insertIndex, T value)
        {
            if (arr.Length <= insertIndex || insertIndex < 0 || arr.Length < 2) return default;
            T outp = arr[arr.Length - 1];
            for (int i = arr.Length - 1; i > insertIndex; i--)
                arr[i] = arr[i - 1];
            arr[insertIndex] = value;
            return outp;
        }
        /// <summary>
        /// Inserts a value at <paramref name="insertIndex"/>, sifting all values at and below <paramref name="insertIndex"/> down while also multiplying by <paramref name="mult"/>.
        /// This will remove the value at the last index and return it. This action does not change the size of <paramref name="arr"/>.
        /// </summary>
        /// <param name="arr">The array to preform this action on. The array must be of length 2 or greater.</param>
        /// <param name="insertIndex">The index to insert the value at. Must be within the bounds of <paramref name="arr"/>.</param>
        /// <param name="value">The value to be inserted.</param>
        /// <param name="mult">The mutliplier to apply to all values that get sifted down.</param>
        /// <returns>The value at the last index in <paramref name="arr"/></returns>
        public static float SiftDown(float[] arr, int insertIndex, float value, float mult)
        {
            if (arr.Length <= insertIndex || insertIndex < 0 || arr.Length < 2) return default;
            float outp = arr[arr.Length - 1];
            for (int i = arr.Length - 1; i > insertIndex; i--)
                arr[i] = arr[i - 1] * mult;
            arr[insertIndex] = value;
            return outp;
        }
        /// <summary>
        /// Inserts a value at <paramref name="insertIndex"/>, sifting all values at and below <paramref name="insertIndex"/> down until
        /// <paramref name="endIndex"/> is reached. Once <paramref name="endIndex"/> is reached, the value there will be replaced, with the original
        /// value being returned. This action does not change the size of <paramref name="arr"/>.
        /// </summary>
        /// <param name="arr">The array to preform this action on. The array must be of length 2 or greater.</param>
        /// <param name="insertIndex">The index to insert the value at. Must be within the bounds of the array.</param>
        /// <param name="endIndex">The index of the value that will be removed and returned. This value must be smaller than <paramref name="insertIndex"/>.
        /// If this value is greater than the length of <paramref name="arr"/>, it will be set to the last index. No values after this index will be touched.</param>
        /// <param name="value">The value to be inserted.</param>
        /// <returns>The value at <paramref name="endIndex"/> in <paramref name="arr"/>.</returns>
        public static T SiftDown<T>(T[] arr, int insertIndex, int endIndex, T value)
        {
            if (arr.Length - 1 <= insertIndex || insertIndex < 0 || endIndex < insertIndex || arr.Length < 2) return default;
            T outp;
            if (endIndex == insertIndex)
            {
                outp = arr[insertIndex];
                arr[insertIndex] = value;
                return outp;
            }
            if (endIndex >= arr.Length) endIndex = arr.Length - 1;
            outp = arr[endIndex];
            for (int i = endIndex; i > insertIndex; i--)
                arr[i] = arr[i - 1];
            arr[insertIndex] = value;
            return outp;
        }
        /// <summary>
        /// Inserts a value at <paramref name="insertIndex"/>, sifting all values at and below <paramref name="insertIndex"/> down until
        /// <paramref name="endIndex"/> is reached, while also applying <paramref name="mult"/>. Once <paramref name="endIndex"/> is reached, the value there will be replaced,
        /// with the original value being returned. This action does not change the size of <paramref name="arr"/>.
        /// </summary>
        /// <param name="arr">The array to preform this action on. The array must be of length 2 or greater.</param>
        /// <param name="insertIndex">The index to insert the value at. Must be within the bounds of the array.</param>
        /// <param name="endIndex">The index of the value that will be removed and returned. This value must be smaller than <paramref name="insertIndex"/>.
        /// If this value is greater than the length of <paramref name="arr"/>, it will be set to the last index. No values after this index will be touched.</param>
        /// <param name="value">The value to be inserted.</param>
        /// <param name="mult">The mutliplier to apply to all values that get sifted down.</param>
        /// <returns>The value at <paramref name="endIndex"/> in <paramref name="arr"/>.</returns>
        public static float SiftDown(float[] arr, int insertIndex, int endIndex, float value, float mult)
        {
            if (arr.Length - 1 <= insertIndex || insertIndex < 0 || endIndex < insertIndex || arr.Length < 2) return default;
            float outp;
            if (endIndex == insertIndex)
            {
                outp = arr[insertIndex];
                arr[insertIndex] = value;
                return outp;
            }
            if (endIndex >= arr.Length) endIndex = arr.Length - 1;
            outp = arr[endIndex];
            for (int i = endIndex; i > insertIndex; i--)
                arr[i] = arr[i - 1] * mult;
            arr[insertIndex] = value;
            return outp;
        }
        /// <summary>
        /// Given a sorted array (smallest to largest) <paramref name="arr"/> and a <paramref name="value"/>, find where this value is to be inserted.
        /// </summary>
        /// <typeparam name="T">A type that can be compared using <see cref="IComparable{T}"/></typeparam>
        /// <param name="arr">The sorted array to check (is not modified).</param>
        /// <param name="value">the value to check for insertion.</param>
        /// <returns>The index where the value should be inserted.</returns>
        public static int FindInsertValue<T>(T[] arr, T value) where T : IComparable<T>
        {
            if (arr is null || arr.Length == 0 || (value?.Equals(default(T)) ?? false)) return -1;
            int index = Array.BinarySearch(arr, value);
            if (index >= 0) return index;
            return -index - 1;
        }
        /// <summary>
        /// Standard binary serach, but assumes <paramref name="arr"/> is sorted from greatest to least instead of least to greatest.
        /// The same thing as passing a least to greatest array into <seealso cref="Array.BinarySearch(Array, object)"/>.
        /// </summary>
        /// <param name="arr">The array to preform the search on.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Either the index of <paramref name="value"/> will be returned, or the index of the highest value without going over.
        /// It will return the arr length if the given value is less than all of the other values.</returns>
        public static int ReverseBinarySearch<T>(T[] arr, T value) where T : IComparable<T>
        {
            int low = 0;
            int high = arr.Length - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                int cmp = arr[mid].CompareTo(value);

                if (cmp == 0)
                    return mid;

                if (cmp > 0)
                {
                    // mid value > value, move right (since descending)
                    low = mid + 1;
                }
                else
                {
                    // mid value < value, move left
                    high = mid - 1;
                }
            }

            return low; // first index where value <= arr[i], or arr.Length if value is smallest
        }
        /// <summary>
        /// Compresses <see cref="Enum"/>s into a series of numbers.
        /// </summary>
        /// <typeparam name="T">The type of an <see cref="Enum"/>.</typeparam>
        /// <param name="arr">The array of <see cref="Enum"/>s to compress.</param>
        /// <returns>An array of <see cref="ulong"/>s. There will be an extra number at the end of <paramref name="arr"/> if there are extra bits left over. You can either
        /// leave it there or store it seperately as a <see cref="byte"/>.</returns>
        public static ulong[] CompressEnums<T>(T[] arr) where T : Enum
        {
            const int LongBits = 64;

            if (arr is null || arr.Length == 0)
                return Array.Empty<ulong>();

            // Determine bits per enum
            ulong max = 0;
            foreach (T item in Enum.GetValues(typeof(T)))
                max = Math.Max(Convert.ToUInt64(item), max);
            if (max == 0) return new ulong[] { 0 }; // Single-value enum

            int bitsPerEnum = (int)Math.Ceiling(Math.Log(max + 1, 2));

            // Total bits needed
            int totalBits = arr.Length * bitsPerEnum;
            int numUlongs = (totalBits + LongBits - 1) / LongBits;

            ulong[] result = new ulong[numUlongs + 1]; // +1 for potential extra bits length
            int bitPos = 0;
            int ulongIndex = 0;

            foreach (T item in arr)
            {
                ulong value = Convert.ToUInt64(item);
                int bitsLeft = LongBits - bitPos;

                if (bitsLeft >= bitsPerEnum)
                {
                    result[ulongIndex] |= value << bitPos;
                    bitPos += bitsPerEnum;
                }
                else
                {
                    int bitsInFirst = bitsLeft;
                    int bitsInSecond = bitsPerEnum - bitsInFirst;

                    ulong firstPart = value & ((1UL << bitsInFirst) - 1);
                    ulong secondPart = value >> bitsInFirst;

                    result[ulongIndex] |= firstPart << bitPos;
                    ulongIndex++;
                    result[ulongIndex] = secondPart;
                    bitPos = bitsInSecond;
                }

                if (bitPos == LongBits)
                {
                    ulongIndex++;
                    bitPos = 0;
                }
            }

            // Store the number of bits used in the last ulong (if not full)
            result[result.Length - 1] = bitPos > 0 ? (ulong)bitPos : 0;

            return result;
        }

        /// <summary>
        /// Given an array of <see cref="ulong"/>s from the method <see cref="CompressEnums{T}(T[])"/> and the type of <see cref="Enum"/>, converts the data
        /// back to an <see cref="Enum"/> array.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="Enum"/> to convert back to.</typeparam>
        /// <param name="arr">The array of <see cref="ulong"/>s.</param>
        /// <param name="extraLength">The optional byte from <paramref name="arr"/>. If it is still in the array leave this blank.</param>
        /// <returns>The array of <see cref="Enum"/>s, in the order they were originally stored.</returns>
        public static T[] UncompressEnums<T>(ulong[] arr, byte extraLength = 65) where T : Enum
        {
            const int LongBits = 64;

            // Determine if the last ulong is an "extra" partial byte
            bool ignoreLast = false;
            if (extraLength > LongBits) // default magic value means "not provided"
            {
                extraLength = (byte)arr[arr.Length - 1];
                ignoreLast = true;
            }

            // Find how many bits are needed to store each enum
            ulong max = 0;
            foreach (T item in Enum.GetValues(typeof(T)))
                max = Math.Max(Convert.ToUInt64(item), max);
            int bitsPerEnum = max == 0 ? 1 : (int)Math.Ceiling(Math.Log(max + 1, 2));

            // Total number of usable bits in the array
            int totalBits = (arr.Length - (ignoreLast ? 1 : 0)) * LongBits;
            if (ignoreLast && extraLength > 0)
                totalBits -= LongBits - extraLength;

            int numEnums = totalBits / bitsPerEnum;

            T[] result = new T[numEnums];

            int bitPos = 0;     // Bit position inside the current ulong
            int ulongIndex = 0; // Index of the current ulong in arr

            for (int i = 0; i < numEnums; i++)
            {
                ulong value;
                int bitsLeft = LongBits - bitPos;

                if (bitsLeft >= bitsPerEnum)
                {
                    // Case 1: Enum fits entirely in current ulong
                    value = (arr[ulongIndex] >> bitPos) & ((1UL << bitsPerEnum) - 1);
                    bitPos += bitsPerEnum;
                }
                else
                {
                    // Case 2: Enum spans current and next ulong
                    ulong part1 = arr[ulongIndex] >> bitPos; // take remaining bits from current ulong
                    ulong part2 = 0;
                    if (ulongIndex + 1 < arr.Length)
                        part2 = arr[ulongIndex + 1] & ((1UL << (bitsPerEnum - bitsLeft)) - 1); // take remaining from next ulong
                    value = part1 | (part2 << bitsLeft);
                    bitPos = bitsPerEnum - bitsLeft;
                    ulongIndex++;
                }

                // Move to next ulong if we consumed all bits in the current one
                if (bitPos >= LongBits)
                {
                    bitPos -= LongBits;
                    ulongIndex++;
                }

                result[i] = (T)Enum.ToObject(typeof(T), value);
            }
            return result;
        }
        public static T[] RemoveElement<T>(this T[] arr, int index)
        {
            List<T> hold = arr.ToList();
            hold.RemoveAt(index);
            return hold.ToArray();
        }
        public static T[] InsertElement<T>(this T[] arr, int index, T value)
        {
            if (index >= arr.Length)
                return arr.Append(value).ToArray();
            List<T> hold = arr.ToList();
            hold.Insert(index, value);
            return hold.ToArray();
        }
        public static string ClampString(this string str, int maxLength)
        {
            if (str.Length < maxLength) return str;
            return str.Substring(0, maxLength) + "...";
        }
        public static string ToCapName(Leaderboards leaderboard)
        {
            switch (leaderboard)
            {
                case Leaderboards.Beatleader:
                    return "BeatLeader";
                case Leaderboards.Scoresaber:
                    return "ScoreSaber";
                case Leaderboards.Accsaber:
                    return "AccSaber";
                default:
                    return "";
            }
        }
        public static float GetLineHeight(this TMP_TextInfo textInfo, int line = 0)
        {
            TMP_LineInfo lineInfo = textInfo.lineInfo[line];
            return Mathf.Abs(lineInfo.ascender - lineInfo.descender);
        }
        /// <summary>
        /// Calculates the final displayed accuracy ratio (0.0f – 1.0f) for a completed level.
        /// This matches the accuracy shown on the in-game results screen and ignores modifiers,
        /// since modifiers only affect the final leaderboard score, not the accuracy percentage.
        /// </summary>
        /// <param name="results">
        /// The <see cref="LevelCompletionResults"/> instance produced after finishing a level.
        /// The field <c>modifiedScore</c> is used as the numerator in the accuracy calculation.
        /// </param>
        /// <param name="transitionData">
        /// The <see cref="StandardLevelScenesTransitionSetupDataSO"/> for the played level.
        /// This provides access to the <see cref="IDifficultyBeatmap"/> and its
        /// <see cref="BeatmapData"/>, which is used to determine the total number of scorable notes
        /// (excluding bombs and walls).
        /// </param>
        /// <returns>
        /// A <see cref="float"/> between <c>0.0f</c> and <c>1.0f</c>, representing the player’s
        /// final accuracy. For example, a return value of <c>0.9735f</c> corresponds to 97.35%.
        /// If no valid data is available, this method returns <c>0.0f</c>.
        /// </returns>
        public static float GetAcc(StandardLevelScenesTransitionSetupDataSO transitionData, LevelCompletionResults results)
        {
            if (results is null || transitionData is null)
                return 0f;
#if NEW_VERSION
            if (results.invalidated || results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
#else
            if (results.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
#endif
            {
                Plugin.Log.Warn($"Level was invalidated or failed, not saving score.");
                return 0f;
            }
            if (results.energy <= 0.0f)
            {
                Plugin.Log.Warn($"Level was failed with No Fail enabled, not saving score.");
                return 0f;
            }

            // Grab the selected difficulty beatmap
#if !NEW_VERSION
            IDifficultyBeatmap beatmap = transitionData.difficultyBeatmap;
            if (beatmap is null)
                return 0f;
#endif

            // Get beatmap data (spawning objects like notes, bombs, walls)
#if NEW_VERSION
            transitionData.beatmapLevel.beatmapBasicData.TryGetValue((transitionData.beatmapKey.beatmapCharacteristic, transitionData.beatmapKey.difficulty), out BeatmapBasicData beatmapData);
#else
            IBeatmapDataBasicInfo beatmapData = beatmap.GetBeatmapDataBasicInfoAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif

            // Count only scorable notes (bombs/walls are excluded)
#if NEW_VERSION
            int totalNotes = beatmapData?.notesCount ?? -1;
#else
            int totalNotes = beatmapData?.cuttableNotesCount ?? -1;
#endif
            if (totalNotes <= 0)
            {
                Plugin.Log.Warn($"totalNotes is not set properly! (totalNotes = {totalNotes})");
                totalNotes = results.goodCutsCount + results.badCutsCount + results.missedCount;
                Plugin.Log.Warn($"Using backup! Setting the totalNotes to {totalNotes}.");
                if (totalNotes <= 0)
                {
                    Plugin.Log.Warn("Still failed. Defaulting to zero accuracy.");
                    return 0f;
                }
            }

            // Use unmodified score (before multipliers) for accuracy
            int rawScore = results.multipliedScore;
            Plugin.Log.Info($"multipliedScore: {results.multipliedScore} || modifiedScore: {results.modifiedScore}");

            // Max possible score for this beatmap
            int maxScore = HelpfulMath.MaxScoreForNotes(totalNotes);

            return (float)rawScore / maxScore;
        }
        public static string FormatTime(this TimeSpan time)
        {
            if (time.TotalMinutes < 1)
            {
                int seconds = (int)Math.Round(time.TotalSeconds);
                return $"{seconds} second{(seconds == 1 ? "" : "s")} ago";
            }
            if (time.TotalHours < 1)
            {
                int minutes = (int)Math.Round(time.TotalMinutes);
                return $"{minutes} minute{(minutes == 1 ? "" : "s")} ago";
            }
            int hours = (int)Math.Round(time.TotalHours);
            return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
        }

            /*float[] ConvertArr(double[] arr)
            {
                float[] outp = new float[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    outp[i] = (float)arr[i];
                return outp;
            }*/
    }
}
