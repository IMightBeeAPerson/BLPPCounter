using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.TokenParser.FormatTypes
{
#nullable enable
    internal class Chunk(string? value)
    {
        private readonly string? Value = value;

        public virtual string GetValue() { return Value ?? ""; }
        public static string Combine(IEnumerable<Chunk>? arr) => arr?.Aggregate("", (total, current) => total + current) ?? "";

        public sealed override string ToString() { return GetValue(); }
        public override bool Equals(object? obj)
        {
            if (obj is null || obj is not Chunk other)
                return false;
            return other.GetValue().Equals(GetValue());
        }
        public override int GetHashCode() => GetValue().GetHashCode();
    }

    internal class Token(char symbol) : Chunk(symbol.ToString()) { }
    internal class Parameter(char symbol, params Token[] parameters) : Token(symbol)
    {
        public IReadOnlyList<Token> Parameters => parameters;
    }
    internal class Capture(char symbol, IEnumerable<Chunk> chunks) : Group((char)(symbol - '0'), chunks) { }
    internal class RichText(string richKey, string richVal, IEnumerable<Chunk> chunks) : Group('\0', chunks)
    {
        public string RichKey = richKey;
        public string RichVal = richVal;

        public override string GetValue()
        {
            return $"<{RichKey}={RichVal}>{base.GetValue()}</{RichKey}>";
        }
        public string GetStart() => $"<{RichKey}={RichVal}>";
        public string GetEnd() => $"</{RichKey}>";
    }
}
