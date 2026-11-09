using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace BLPPCounter.Utils.Misc_Classes
{
    public static class Guard
    {
        public static void ThrowIfNull<T>(
            [NotNull] T? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null
        )
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
