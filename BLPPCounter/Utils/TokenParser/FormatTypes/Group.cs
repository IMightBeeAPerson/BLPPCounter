using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.TokenParser.FormatTypes
{
    internal class Group(char symbol, IEnumerable<Chunk> chunks) : Chunk(null)
    {
        public char Symbol = symbol;
        public List<Chunk> Chunks = [.. chunks];

        public void AddChunk(Chunk c) => Chunks.Add(c);

        public override string GetValue() => Combine(Chunks);
        public override bool Equals(object obj)
        {
            if (obj is null || obj is not Group other || other.Symbol != Symbol || other.Chunks.Count != Chunks.Count)
                return false;
            for (int i = 0; i < Chunks.Count; i++)
                if (!Chunks.ElementAt(i).Equals(other.Chunks.ElementAt(i)))
                    return false;
            return true;
        }
        public override int GetHashCode() => Chunks.GetHashCode() + Symbol;
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
