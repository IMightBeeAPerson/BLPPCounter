using BLPPCounter.Settings.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static BLPPCounter.Helpfuls.HelpfulMisc;
using static BLPPCounter.Utils.TokenParser.Tokens;

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
        public static readonly HashSet<char> SPECIAL_CHARS, ALL_SPECIAL_CHARS;

        //difference is SPECIAL_CHARS only contains characters that modifies the format by itself, unlike chars like OPEN_PARAM which are normal unless used after an escaped token.
        public static Dictionary<string, string> RICH_SHORTHANDS => PC.TokenSettings.RichShorthands;
        public static string NUMBER_TOSTRING_FORMAT { get; internal set; }

        internal static readonly string RegexAllSpecialChars, RegexSpecialChars;
        static HelpfulFormatter()
        {
            string hold = "";
            for (int i = 0; i < PC.DecimalPrecision; i++) hold += "#";
            NUMBER_TOSTRING_FORMAT = PC.DecimalPrecision > 0 ? PC.FormatSettings.NumberFormat.Replace("#", "#." + hold) : PC.FormatSettings.NumberFormat;
            SPECIAL_CHARS = [ESCAPE_CHAR, RICH_SHORT, GROUP_OPEN, GROUP_CLOSE, CAPTURE_OPEN, CAPTURE_CLOSE];
            ALL_SPECIAL_CHARS = [ESCAPE_CHAR, RICH_SHORT, DELIMITER, GROUP_OPEN, GROUP_CLOSE, INSERT_SELF, CAPTURE_OPEN, CAPTURE_CLOSE, PARAM_OPEN, PARAM_CLOSE, ALIAS];
            RegexAllSpecialChars = "[" + string.Join("", ALL_SPECIAL_CHARS).Replace("]", "\\]") + "]";
            RegexSpecialChars = "[" + string.Join("", SPECIAL_CHARS).Replace("]", "\\]") + "]";
        }


        /// <summary>
        /// Converts a number to a color string based on its value.
        /// </summary>
        /// <param name="num">The number to convert.</param>
        /// <returns>A color string representing the number.</returns>
        public static string NumberToColor(float num) => num > 0 ? "<color=green>" : num == 0 ? "<color=yellow>" : "<color=red>";
        /// <summary>
        /// Converts a number to a gradient color string.
        /// </summary>
        /// <param name="variance">The variance for the gradient.</param>
        /// <param name="num">The number to convert.</param>
        /// <returns>A gradient color string representing the number.</returns>
        public static string NumberToGradient(float variance, float num)
        {
            // Handle zero case based on blending settings
            if (num == 0)
            {
                if (PC.ColorGradBlending)
                {
                    if (!PC.BlendMiddleColor)
                    {
                        num = variance / 2f;
                    }
                }
                else
                {
                    return ConvertColorToMarkup(PC.ColorGradZero);
                }
            }

            bool isNegative = num < 0;

            // Calculate normalized value "toConvert"
            float ratio;
            if (isNegative && !PC.ColorGradBlending)
            {
                ratio = 1.0f + num / variance;
            }
            else
            {
                ratio = num / variance;
            }

            float adjustedRatio = Math.Abs(ratio);
            float flipAdjustment = PC.BlendMiddleColor ? 0 : PC.ColorGradFlipPercent;
            float clampedValue = Math.Min(
                Math.Max(adjustedRatio, PC.ColorGradMinDark) + flipAdjustment,
                1f
            );

            // Handle blending logic
            if (PC.ColorGradBlending)
            {
                if (PC.BlendMiddleColor)
                {
                    if (isNegative)
                    {
                        return ConvertColorToMarkup(Blend(PC.ColorGradMin, PC.ColorGradZero, clampedValue));
                    }
                    else
                    {
                        return ConvertColorToMarkup(Blend(PC.ColorGradZero, PC.ColorGradMax, 1.0f - clampedValue));
                    }
                }
                else
                {
                    return ConvertColorToMarkup(
                        Blend(PC.ColorGradMin, PC.ColorGradMax, isNegative ? clampedValue : 1.0f - clampedValue)
                    );
                }
            }

            // No blending: apply color multiplication
            return isNegative
                ? ConvertColorToMarkup(Multiply(PC.ColorGradMin, clampedValue))
                : ConvertColorToMarkup(Multiply(PC.ColorGradMax, clampedValue));
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
        public static char DefaultToUsedChar(char c) => c switch
        {
            '&' => ESCAPE_CHAR,
            '*' => RICH_SHORT,
            ',' => DELIMITER,
            '[' => GROUP_OPEN,
            ']' => GROUP_CLOSE,
            '$' => INSERT_SELF,
            '<' => CAPTURE_OPEN,
            '>' => CAPTURE_CLOSE,
            '(' => PARAM_OPEN,
            ')' => PARAM_CLOSE,
            '\'' => ALIAS,
            _ => c,
        };

        /// <summary>
        /// Returns a colorized representation of a special formatting character using configured colors.
        /// </summary>
        /// <param name="c">Special character to colorize.</param>
        /// <returns>A string containing color markup and the character.</returns>
        /// <exception cref="ArgumentException">Thrown if the provided character is not recognized as special.</exception>
        public static string ColorSpecialChar(char c) => c switch
        {
            char v when v == ESCAPE_CHAR => $"{ConvertColorToMarkup(PC.EscapeCharacterColor)}{v}",
            char v when v == RICH_SHORT => $"{ConvertColorToMarkup(PC.ShorthandColor)}{v}",
            char v when v == DELIMITER => $"{ConvertColorToMarkup(PC.DelimeterColor)}{v}",
            char v when v == GROUP_OPEN || v == GROUP_CLOSE => $"{ConvertColorToMarkup(PC.GroupColor)}{v}",
            char v when v == INSERT_SELF => $"{ConvertColorToMarkup(PC.GroupReplaceColor)}{v}",
            char v when v == CAPTURE_OPEN || v == CAPTURE_CLOSE => $"{ConvertColorToMarkup(PC.CaptureColor)}{v}",
            char v when v == PARAM_OPEN || v == PARAM_CLOSE => $"{ConvertColorToMarkup(PC.ParamColor)}{v}",
            char v when v == ALIAS => $"{ConvertColorToMarkup(PC.AliasQuoteColor)}{v}",
            _ => throw new ArgumentException("Character given is not special"),
        };

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
            static string Converter(Match m)
            {
#if NEW_VERSION
                string name = m.Groups.First(g => g.Success && !char.IsDigit(g.Name[0])).Name; // 1.37.0 and above
#else
                string name = m.Groups.OfType<Group>().First(g => g.Success && !char.IsDigit(g.Name[0])).Name; //1.34.2 and below
#endif
                return name switch
                {
                    "Special" => ColorSpecialChar(m.Value[0]),
                    "Color" => ConvertColorToMarkup(PluginConfig.Instance.GetColorFromName(m.Value.Substring(1))),
                    "Replace" => "{" + m.Value + "}",
                    _ => m.Value
                };
            }
            return Regex.Replace(str, "(?<Special>" + RegexAllSpecialChars + ")|c(?<Color>[A-Z][a-z]+)|(?<Replace>\\d)", Converter);
        }
    }
}