using BLPPCounter.Utils.TokenParser.FormatTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.TokenParser
{
    internal static class TokenParser
    {
#nullable enable
        private static Dictionary<string, char>? aliasConverter = null;
        internal static Chunk[] ParseTokens(string toParse, Dictionary<string, char>? aliasConverter = null)
        {
            //Plugin.Log.Info("Parsing format: " + toParse);
            TokenParser.aliasConverter = aliasConverter;
            List<Chunk> outp = [];
            char[] strChars = toParse.ToCharArray();
            int index = 0, lastIndex = -1;
            while (index < strChars.Length)
            {
                if (index == lastIndex) throw new FormatException("Formatter got stuck in loop, most likely due to trailing group brackets.");
                lastIndex = index;
                if (toParse[0].Closer()) throw new FormatException("There are unbalanced brackets inside of this format.");
                DoLoop(outp, strChars, ref index);
            }
            return [.. outp];
        }
        private static string ReadUntil(char[] arr, ref int index, char stopAt)
        {
            if (index >= arr.Length) return "";
            int count = 0;
            while (index + count < arr.Length && arr[index + count] != stopAt)
                count++;
            string outp = new(arr, index, count);
            index += count;
            return outp;
        }
        private static void DoLoop(List<Chunk> outp, char[] arr, ref int index, char groupSymbol = '\0', bool parsingRich = false, char expectedCloser = '\0')
        {
            int start = index;
            string chunk = ReadChunk(arr, ref index);

            if (chunk.Length > 0)
                outp.Add(new Chunk(chunk));

            Chunk? special = ReadSpecial(arr, ref index, groupSymbol, parsingRich, expectedCloser);
            if (special is not null) outp.Add(special);

            if (index == start)
                throw new FormatException($"Parser failed to advance at index {index}.");
        }
        private static string ReadChunk(char[] arr, ref int index)
        {
            if (index >= arr.Length || Tokens.SPECIAL_TOKENS.Contains(arr[index])) return "";
            int count = 0;
            while (index + count < arr.Length && !Tokens.SPECIAL_TOKENS.Contains(arr[index + count]))
                count++;
            string outp = new(arr, index, count);
            index += count;
            return outp;
        }
        private static Chunk? ReadSpecial(char[] arr, ref int index, char groupSymbol, bool parsingRich, char expectedCloser)
        {
            char special = arr[index];
            if (index >= arr.Length || special == expectedCloser) return null;
            if (special == Tokens.RICH_SHORT) return ParseRichText(arr, ref index, groupSymbol, parsingRich);
            index++;
            char symbol = '\0';
            if (special.UsesSymbol())
            {
                symbol = arr[index];
                if (symbol == Tokens.ALIAS)
                    symbol = ReadAlias(arr, ref index);
                else index++;
            }
            if (special.UsesContent())
            {
                List<Chunk> content = [];
                char closer = special.GetCloser();
                while (index < arr.Length && arr[index] != closer)
                    DoLoop(content, arr, ref index, symbol, expectedCloser: closer);
                if (index >= arr.Length || arr[index] != closer)
                    throw new FormatException("There are unbalanced brackets inside of this format.");
                index++;
                return special.ToChunk(symbol, content);
            }
            return special switch
            {
                var c when c == Tokens.INSERT_SELF => groupSymbol == '\0' ? new Chunk($"{Tokens.INSERT_SELF}") : special.ToChunk(groupSymbol),
                var c when c == Tokens.ESCAPE_CHAR && index < arr.Length && arr[index] == Tokens.PARAM_OPEN => ParseParameters(symbol, arr, ref index),
                _ => special.ToChunk(symbol),
            };
        }
        private static Parameter ParseParameters(char symbol, char[] arr, ref int index)
        {
            if (arr[index] != Tokens.PARAM_OPEN) throw new FormatException("Parameters must start with a parameter open token.");
            index++;
            int start = index;
            string raw = ReadUntil(arr, ref index, Tokens.PARAM_CLOSE);
            Token[] outp = [.. raw.Split(Tokens.DELIMITER).Select(str => new Token(str[0]))];
            if (index >= arr.Length)
                throw new FormatException($"Unclosed parameter list starting at index {start} - {index}.");
            index++;
            return new Parameter(symbol, outp);
        }
        private static char ReadAlias(char[] arr, ref int index)
        {
            if (arr[index] != Tokens.ALIAS) throw new FormatException("Alias must start with an alias token.");
            if (index >= arr.Length)
                throw new FormatException($"Alias indicator at end of format (index {index}).");
            index++;
            string alias = ReadChunk(arr, ref index);
            index++;
            if (aliasConverter is null || !aliasConverter.TryGetValue(alias, out char value))
                throw new KeyNotFoundException($"The alias '{alias}' was not found in the provided alias dictionary.");
            return value;
        }
        private static RichText? ParseRichText(char[] arr, ref int index, char groupSymbol, bool parsingRich)
        {
            if (parsingRich) return null;
            parsingRich = true;
            string[] hold;
            List<Chunk> content = [];
            try
            {
                index++;
                while (index < arr.Length && arr[index] != Tokens.RICH_SHORT)
                    DoLoop(content, arr, ref index, groupSymbol, parsingRich);
                index++;
                hold = Chunk.Combine(content).Split(Tokens.DELIMITER);
                content.Clear();
                if (hold[0].Length == 1)
                    hold[0] = Tokens.LookupRichKey(hold[0][0]);
                while (index < arr.Length && arr[index] != Tokens.RICH_SHORT)
                    DoLoop(content, arr, ref index, groupSymbol, parsingRich);
                index++;
            } finally
            {
                parsingRich = false;
            }
            return new RichText(hold[0], hold[1], content);
        }
    }
}
