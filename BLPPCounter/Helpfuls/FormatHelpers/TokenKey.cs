using System;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    public readonly struct TokenKey : IEquatable<TokenKey>
    {
        public readonly char Symbol;
        public readonly int Priority;

        public TokenKey(char symbol, int priority)
        {
            Symbol = symbol;
            Priority = priority;
        }

        public override int GetHashCode()
        {
            unchecked { return (Symbol * 397) ^ Priority; }
        }

        public override bool Equals(object obj)
        {
            return obj is TokenKey key && Equals(key);
        }

        public bool Equals(TokenKey other)
        {
            return Symbol == other.Symbol && Priority == other.Priority;
        }

        public override string ToString()
        {
            return $"({Symbol}, {Priority})";
        }
    }

}
