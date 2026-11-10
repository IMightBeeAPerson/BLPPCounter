using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.TokenParser.FormatTypes;

namespace BLPPCounter.Utils.TokenParser
{
    internal class TokenParser
    {
        private readonly HelpfulFormatter formatter;
        private readonly Tokens token;

        private Dictionary<string, char>? aliasConverter = null;

        public TokenParser(HelpfulFormatter formatter, Tokens token)
        {
            this.formatter = formatter;
            this.token = token;
        }

        internal bool ParseTokens(
            string toParse,
            out string errorMessage,
            out Chunk[] tokens,
            Dictionary<string, char>? aliasConverter = null
        )
        {
            try
            {
                tokens = ParseTokens(toParse, aliasConverter);
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                tokens = [];
                errorMessage = e.Message;
                return false;
            }
        }

        internal Chunk[] ParseTokens(string toParse, Dictionary<string, char>? aliasConverter = null)
        {
            //Plugin.Log.Info("Parsing format: " + toParse);
            this.aliasConverter = aliasConverter;
            List<Chunk> outp = [];
            char[] strChars = toParse.ToCharArray();
            int index = 0,
                lastIndex = -1;
            while (index < strChars.Length)
            {
                if (index == lastIndex)
                    throw new FormatException(
                        "Formatter got stuck in loop, most likely due to trailing group brackets."
                    );
                lastIndex = index;
                if (token.Closer(toParse[0]))
                    throw new FormatException("There are unbalanced brackets inside of this format.");
                DoLoop(outp, strChars, ref index);
            }
            return [.. outp];
        }

        private string ReadUntil(char[] arr, ref int index, char stopAt)
        {
            if (index >= arr.Length)
                return "";
            int count = 0;
            while (index + count < arr.Length && arr[index + count] != stopAt)
                count++;
            string outp = new(arr, index, count);
            index += count;
            return outp;
        }

        private void DoLoop(
            List<Chunk> outp,
            char[] arr,
            ref int index,
            char groupSymbol = '\0',
            bool parsingRich = false,
            char expectedCloser = '\0'
        )
        {
            int start = index;
            string chunk = ReadChunk(arr, ref index);

            if (chunk.Length > 0)
                outp.Add(new Chunk(chunk));

            Chunk? special = ReadSpecial(arr, ref index, groupSymbol, parsingRich, expectedCloser);
            if (special is not null)
                outp.Add(special);

            if (index == start)
                throw new FormatException($"Parser failed to advance at index {index}.");
        }

        private string ReadChunk(char[] arr, ref int index)
        {
            if (index >= arr.Length || token.SPECIAL_TOKENS.Contains(arr[index]))
                return "";
            int count = 0;
            while (index + count < arr.Length && !token.SPECIAL_TOKENS.Contains(arr[index + count]))
                count++;
            string outp = new(arr, index, count);
            index += count;
            return outp;
        }

        private Chunk? ReadSpecial(char[] arr, ref int index, char groupSymbol, bool parsingRich, char expectedCloser)
        {
            if (index >= arr.Length)
                return null;
            char special = arr[index];
            if (special == expectedCloser)
                return null;
            if (special == token.RICH_SHORT)
                return ParseRichText(arr, ref index, groupSymbol, parsingRich);
            index++;
            char symbol = '\0';
            Parameter? parameter = null;
            if (token.UsesSymbol(special))
            {
                symbol = arr[index];
                if (symbol == token.ALIAS)
                    symbol = ReadAlias(arr, ref index);
                else
                    index++;
                if (index < arr.Length && arr[index] == token.PARAM_OPEN)
                    parameter = ParseParameters(symbol, arr, ref index);
            }
            if (token.UsesContent(special))
            {
                List<Chunk> content = [];
                char closer = token.GetCloser(special);
                while (index < arr.Length && arr[index] != closer)
                    DoLoop(content, arr, ref index, symbol, expectedCloser: closer);
                if (index >= arr.Length || arr[index] != closer)
                    throw new FormatException("There are unbalanced brackets inside of this format.");
                index++;
                Chunk outp = token.ToChunk(special, symbol, content)!;
                return parameter is null ? outp : new GroupParameter(symbol, parameter, (Group)outp);
            }
            if (parameter is not null)
                return parameter;
            if (special == token.INSERT_SELF)
                return groupSymbol == '\0' ? new Chunk($"{token.INSERT_SELF}") : token.ToChunk(special, groupSymbol);
            return token.ToChunk(special, symbol);
        }

        private Parameter ParseParameters(char symbol, char[] arr, ref int index)
        {
            if (arr[index] != token.PARAM_OPEN)
                throw new FormatException("Parameters must start with a parameter open token.");
            index++;
            int start = index;
            string raw = ReadUntil(arr, ref index, token.PARAM_CLOSE);

            Token ResolveAlias(string alias)
            {
                int index = 0;
                return new Token(ReadAlias(alias.ToCharArray(), ref index));
            }

            Chunk[] outp =
            [
                .. raw.Split(token.DELIMITER)
                    .Select(str =>
                        str.Length > 1
                            ? str[0] == token.ALIAS
                                ? ResolveAlias(str)
                                : new Chunk(str)
                            : new Token(str[0])
                    ),
            ];

            if (index >= arr.Length)
                throw new FormatException($"Unclosed parameter list starting at index {start} - {index}.");
            index++;
            return new Parameter(symbol, outp);
        }

        private char ReadAlias(char[] arr, ref int index)
        {
            if (arr[index] != token.ALIAS)
                throw new FormatException("Alias must start with an alias token.");
            if (index >= arr.Length)
                throw new FormatException($"Alias indicator at end of format (index {index}).");
            index++;
            string alias = ReadChunk(arr, ref index);
            index++;

            if (aliasConverter is null)
                throw new ArgumentException("No alias dictionary was provided while aliases were used in the format.");

            if (!aliasConverter.TryGetValue(alias, out char outp))
                if (!token.GlobalAliases.TryGetValue(alias, out outp))
                    throw new KeyNotFoundException(
                        $"The alias '{alias}' was not found in the provided alias dictionary nor the global alias dictionary.\n{aliasConverter.Print()}"
                    );
            return outp;
        }

        private RichText? ParseRichText(char[] arr, ref int index, char groupSymbol, bool parsingRich)
        {
            if (parsingRich)
                return null;
            string[] hold;
            List<Chunk> content = [];
            index++;
            while (index < arr.Length && arr[index] != token.RICH_SHORT)
                DoLoop(content, arr, ref index, groupSymbol, true);
            index++;
            hold = Chunk.Combine(content).Split(token.DELIMITER);
            content.Clear();
            if (hold[0].Length == 1)
                hold[0] = token.LookupRichKey(hold[0][0]);
            while (index < arr.Length && arr[index] != token.RICH_SHORT)
                DoLoop(content, arr, ref index, groupSymbol, true);
            index++;
            return new RichText(hold[0], hold[1], content);
        }
    }
}
