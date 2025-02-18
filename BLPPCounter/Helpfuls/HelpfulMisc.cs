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
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Settings.Configs;
using BeatLeader.Models;
namespace BLPPCounter.Helpfuls
{
    public static class HelpfulMisc
    {
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
        public static string ConvertColorToHex(UnityEngine.Color c) => $"#{ToRgba(c):X8}";
        public static string ConvertColorToMarkup(Color c) => $"<color={ConvertColorToHex(c)}>";
        public static int ArgbToRgba(int argb) => (argb << 8) + (int)((uint)argb >> 24); //can't use triple shift syntax, so best I can do is casting :(
        public static int RgbaToArgb(int rgba) => (int)((uint)rgba >> 8) + (rgba << 24);
        public static int ToRgba(Color c) => ArgbToRgba(c.ToArgb());
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
            string outp = "";
            foreach (T item in arr)
                outp += ", " + item;
            return $"[{outp.Substring(2)}]";
        }
        public static string Print<T>(this IEnumerable<T> arr, Func<T, string> valToString)
        {
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
            Plugin.Log.Info("Front end value = " + (string)menu.Value);
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
        /// Checks if a given map selection is usable. If it is using a score saber map, then it is always usable. Otherwise, it checks the beat leader status against the list below:
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
        public static bool StatusIsUsable(MapSelection ms) => ms.Mode.Equals(Map.SS_MODE_NAME) || StatusIsUsable((int)ms.MapData.Item2["status"]);
        public static bool StatusIsUsable(JToken diffData) => StatusIsUsable((int)diffData["status"]);
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
            if ((isMinVal && newVal < currentVal) || (!isMinVal && newVal > currentVal)) ss.Slider.value = currentVal; // 1.37.0 and above
#else
            float currentVal = ss.slider.value;
            if (isMinVal) ss.slider.minValue = newVal; else ss.slider.maxValue = newVal;
            ss.slider.numberOfSteps = (int)Math.Round((ss.slider.maxValue - ss.slider.minValue) / ss.increments) + 1;
            if ((isMinVal && newVal < currentVal) || (!isMinVal && newVal > currentVal)) ss.slider.value = currentVal; //1.34.2 and below
#endif
        }
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
    }
}
