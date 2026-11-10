using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Serializable_Classes;
using BLPPCounter.Utils.TokenParser;
using Zenject;
using static BLPPCounter.Helpfuls.HelpfulMisc;

namespace BLPPCounter.Helpfuls
{
    /// <summary>
    /// Provides utilities for parsing and formatting strings with custom tokens and aliases.
    /// </summary>
    public class HelpfulFormatter : IInitializable
    {
        public const int FORMAT_SPLIT = 100;

        [Inject]
        private readonly PluginConfig pc = null!;

        [Inject]
        private readonly Tokens token = null!;

        public int GRAD_VARIANCE => pc.ColorGradMaxDiff;
        public HashSet<char> SPECIAL_CHARS { get; private set; } = null!;
        public HashSet<char>? ALL_SPECIAL_CHARS { get; private set; }

        //difference is SPECIAL_CHARS only contains characters that modifies the format by itself, unlike chars like OPEN_PARAM which are normal unless used after an escaped token.
        public Dictionary<string, string> RICH_SHORTHANDS => pc.TokenSettings.RichShorthands;
        public string NUMBER_TOSTRING_FORMAT { get; internal set; } = null!;

        internal string RegexAllSpecialChars { get; private set; } = null!;
        internal string RegexSpecialChars { get; private set; } = null!;

        public void Initialize()
        {
            string hold = "";
            for (int i = 0; i < pc.DecimalPrecision; i++)
                hold += "#";

            NUMBER_TOSTRING_FORMAT =
                pc.DecimalPrecision > 0
                    ? pc.FormatSettings.NumberFormat.Replace("#", "#." + hold)
                    : pc.FormatSettings.NumberFormat;

            SPECIAL_CHARS =
            [
                token.ESCAPE_CHAR,
                token.RICH_SHORT,
                token.GROUP_OPEN,
                token.GROUP_CLOSE,
                token.CAPTURE_OPEN,
                token.CAPTURE_CLOSE,
            ];
            ALL_SPECIAL_CHARS =
            [
                token.ESCAPE_CHAR,
                token.RICH_SHORT,
                token.DELIMITER,
                token.GROUP_OPEN,
                token.GROUP_CLOSE,
                token.INSERT_SELF,
                token.CAPTURE_OPEN,
                token.CAPTURE_CLOSE,
                token.PARAM_OPEN,
                token.PARAM_CLOSE,
                token.ALIAS,
            ];
            RegexAllSpecialChars = "[" + string.Join("", ALL_SPECIAL_CHARS).Replace("]", "\\]") + "]";
            RegexSpecialChars = "[" + string.Join("", SPECIAL_CHARS).Replace("]", "\\]") + "]";
        }

        /// <summary>
        /// Converts a number to a color string based on its value.
        /// </summary>
        /// <param name="num">The number to convert.</param>
        /// <returns>A color string representing the number.</returns>
        public static string NumberToColor(float num)
        {
            return num > 0 ? "<color=green>"
                : num == 0 ? "<color=yellow>"
                : "<color=red>";
        }

        /// <summary>
        /// Converts a number to a gradient color string.
        /// </summary>
        /// <param name="variance">The variance for the gradient.</param>
        /// <param name="num">The number to convert.</param>
        /// <returns>A gradient color string representing the number.</returns>
        public string NumberToGradient(float variance, float num)
        {
            return NumberToGradient(variance, num, pc);
        }

        internal static string NumberToGradient(float variance, float num, PluginConfig pc)
        {
            // Handle zero case based on blending settings
            if (num == 0)
            {
                if (pc.ColorGradBlending)
                {
                    if (!pc.BlendMiddleColor)
                    {
                        num = variance / 2f;
                    }
                }
                else
                {
                    return ConvertColorToMarkup(pc.ColorGradZero);
                }
            }

            bool isNegative = num < 0;

            // Calculate normalized value "toConvert"
            float ratio;
            if (isNegative && !pc.ColorGradBlending)
            {
                ratio = 1.0f + (num / variance);
            }
            else
            {
                ratio = num / variance;
            }

            float adjustedRatio = Math.Abs(ratio);
            float flipAdjustment = pc.BlendMiddleColor ? 0 : pc.ColorGradFlipPercent;
            float clampedValue = Math.Min(Math.Max(adjustedRatio, pc.ColorGradMinDark) + flipAdjustment, 1f);

            // Handle blending logic
            if (pc.ColorGradBlending)
            {
                if (pc.BlendMiddleColor)
                {
                    if (isNegative)
                    {
                        return ConvertColorToMarkup(Blend(pc.ColorGradMin, pc.ColorGradZero, clampedValue));
                    }
                    else
                    {
                        return ConvertColorToMarkup(Blend(pc.ColorGradZero, pc.ColorGradMax, 1.0f - clampedValue));
                    }
                }
                else
                {
                    return ConvertColorToMarkup(
                        Blend(pc.ColorGradMin, pc.ColorGradMax, isNegative ? clampedValue : 1.0f - clampedValue)
                    );
                }
            }

            // No blending: apply color multiplication
            return isNegative
                ? ConvertColorToMarkup(Multiply(pc.ColorGradMin, clampedValue))
                : ConvertColorToMarkup(Multiply(pc.ColorGradMax, clampedValue));
        }

        public string NumberToGradient(float num)
        {
            return NumberToGradient(GRAD_VARIANCE, num);
        }

        internal static string NumberToGradient(float num, PluginConfig pc)
        {
            return NumberToGradient(pc.ColorGradMaxDiff, num, pc);
        }

        /// <summary>
        /// Returns a color string for a weighted rank value based on configuration.
        /// </summary>
        /// <param name="rank">The rank value to evaluate.</param>
        /// <returns>Color markup string for the selected weighted rank color.</returns>
        public string GetWeightedRankColor(uint rank)
        {
            int c = -1;
            ColorMatch[] arr = [.. pc.FormatSettings.WeightedRankColors];
            while (arr[++c].Rank < rank && c + 1 < arr.Length)
                ;
            return "<color=#" + arr[c].Color + ">";
        }

        public string GetWeightedRankColor(int rank)
        {
            return GetWeightedRankColor((uint)rank);
        }

        /// <summary>
        /// Converts a string of default formatting characters to the currently configured used characters.
        /// </summary>
        /// <param name="str">Input string containing legacy default characters.</param>
        /// <returns>String with default characters replaced by configured tokens.</returns>
        public string DefaultToUsedChar(string str)
        {
            return Regex.Replace(str, "[&*,[\\]$<>()']", m => "" + DefaultToUsedChar(m.Value[0]));
        }

        /// <summary>
        /// Converts a single default formatting character to the currently configured character token.
        /// </summary>
        /// <param name="c">The default character to convert.</param>
        /// <returns>The configured token character equivalent.</returns>
        public char DefaultToUsedChar(char c)
        {
            return c switch
            {
                '&' => token.ESCAPE_CHAR,
                '*' => token.RICH_SHORT,
                ',' => token.DELIMITER,
                '[' => token.GROUP_OPEN,
                ']' => token.GROUP_CLOSE,
                '$' => token.INSERT_SELF,
                '<' => token.CAPTURE_OPEN,
                '>' => token.CAPTURE_CLOSE,
                '(' => token.PARAM_OPEN,
                ')' => token.PARAM_CLOSE,
                '\'' => token.ALIAS,
                _ => c,
            };
        }

        /// <summary>
        /// Returns a colorized representation of a special formatting character using configured colors.
        /// </summary>
        /// <param name="c">Special character to colorize.</param>
        /// <returns>A string containing color markup and the character.</returns>
        /// <exception cref="ArgumentException">Thrown if the provided character is not recognized as special.</exception>
        public string ColorSpecialChar(char c)
        {
            return c switch
            {
                char v when v == token.ESCAPE_CHAR => $"{ConvertColorToMarkup(pc.EscapeCharacterColor)}{v}",
                char v when v == token.RICH_SHORT => $"{ConvertColorToMarkup(pc.ShorthandColor)}{v}",
                char v when v == token.DELIMITER => $"{ConvertColorToMarkup(pc.DelimeterColor)}{v}",
                char v when v == token.GROUP_OPEN || v == token.GROUP_CLOSE =>
                    $"{ConvertColorToMarkup(pc.GroupColor)}{v}",
                char v when v == token.INSERT_SELF => $"{ConvertColorToMarkup(pc.GroupReplaceColor)}{v}",
                char v when v == token.CAPTURE_OPEN || v == token.CAPTURE_CLOSE =>
                    $"{ConvertColorToMarkup(pc.CaptureColor)}{v}",
                char v when v == token.PARAM_OPEN || v == token.PARAM_CLOSE =>
                    $"{ConvertColorToMarkup(pc.ParamColor)}{v}",
                char v when v == token.ALIAS => $"{ConvertColorToMarkup(pc.AliasQuoteColor)}{v}",
                _ => throw new ArgumentException("Character given is not special"),
            };
        }

        /// <summary>
        /// Converts a default-format string into a colorized format string using configured token characters.
        /// </summary>
        /// <param name="str">Input format string using default characters.</param>
        /// <returns>Colorized format string with configured tokens replaced by colored tokens where applicable.</returns>
        public string ColorDefaultFormatToColor(string str)
        {
            return ColorFormatToColor(DefaultToUsedChar(str));
        }

        /// <summary>
        /// Converts a format string with tokens into a colorized representation using configured colors and color names.
        /// </summary>
        /// <param name="str">Input format string already mapped to configured tokens.</param>
        /// <returns>Colorized string representation.</returns>
        public string ColorFormatToColor(string str)
        {
            string Converter(Match m)
            {
                string name = m.Groups.OfType<Group>().First(g => g.Success && !char.IsDigit(g.Name[0])).Name;
                return name switch
                {
                    "Special" => ColorSpecialChar(m.Value[0]),
                    "Color" => ConvertColorToMarkup(pc.GetColorFromName(m.Value[1..])),
                    "Replace" => "{" + m.Value + "}",
                    _ => m.Value,
                };
            }
            return Regex.Replace(
                str,
                "(?<Special>" + RegexAllSpecialChars + ")|c(?<Color>[A-Z][a-z]+)|(?<Replace>\\d)",
                Converter
            );
        }
    }
}
