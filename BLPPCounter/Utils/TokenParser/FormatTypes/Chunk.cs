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
    internal class Parameter(char symbol, params Chunk[] parameters) : Token(symbol)
    {
        public IReadOnlyList<Chunk> Parameters => parameters;
    }
}
