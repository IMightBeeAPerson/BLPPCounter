using System.Diagnostics.CodeAnalysis;

namespace BLPPCounter.Utils.LangAddons
{
    public static class BetterString
    {
        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of whitespace characters. Guarantees
        /// <paramref name="value"/> to not be null when this function returns false.</summary>
        /// <param name="value">The string to test.</param>
        /// <returns>
        /// true if the value parameter is null or <see cref="string.Empty"/>, or if <paramref name="value"/> consists
        /// exclusively of whitespace characters.
        /// </returns>
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? value)
        {
            if (value is null)
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Indicates whether the specified string is null or an empty string (<see cref="string.Empty"/>). Guarantees
        /// <paramref name="value"/> to not be null when this function returns false.
        /// </summary>
        /// <param name="s">The string to test.</param>
        /// <returns>
        /// true if the value parameter is null or an empty string (<see cref="string.Empty"/>); otherwise, false.
        /// </returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] string? value)
        {
            return value is null || value.Length <= 0;
        }
    }
}
