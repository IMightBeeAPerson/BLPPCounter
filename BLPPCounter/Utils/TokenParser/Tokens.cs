using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.TokenParser.FormatTypes;
using System;
using System.Collections.Generic;

namespace BLPPCounter.Utils.TokenParser
{
    internal static class Tokens
    {
#nullable enable
        private static PluginConfig PC => PluginConfig.Instance;
        public static char ESCAPE_CHAR => PC.TokenSettings.EscapeCharacter;
        public static char RICH_SHORT => PC.TokenSettings.RichTextShorthand;
        public static char DELIMITER => PC.TokenSettings.Delimiter;
        public static char GROUP_OPEN => PC.TokenSettings.GroupBracketOpen;
        public static char INSERT_SELF => PC.TokenSettings.GroupInsertSelf;
        public static char GROUP_CLOSE => PC.TokenSettings.GroupBracketClose;
        public static char CAPTURE_OPEN => PC.TokenSettings.CaptureBracketOpen;
        public static char CAPTURE_CLOSE => PC.TokenSettings.CaptureBracketClose;
        public static char PARAM_OPEN => PC.TokenSettings.EscapeCharParamBracketOpen;
        public static char PARAM_CLOSE => PC.TokenSettings.EscapeCharParamBracketClose;
        public static char ALIAS => PC.TokenSettings.NicknameIndicator;

        public static HashSet<char> SPECIAL_TOKENS => [ESCAPE_CHAR, GROUP_OPEN, GROUP_CLOSE, CAPTURE_OPEN, CAPTURE_CLOSE, INSERT_SELF, RICH_SHORT, ALIAS];

        public static Chunk? ToChunk(this char c, char symbol = '\0', IEnumerable<Chunk>? chunks = null)
        {
            string content = Chunk.Combine(chunks);
            return c switch
            {
                char v when v == ESCAPE_CHAR || v == INSERT_SELF => SPECIAL_TOKENS.Contains(symbol) ? new Chunk(symbol + "") : new Token(symbol),
                char v when v == GROUP_OPEN || v == RICH_SHORT => new Group(symbol, chunks!),
                char v when v == CAPTURE_OPEN => new Capture(symbol, chunks!),
                _ => new Chunk(content)
            };
        }
        public static bool UsesSymbol(this char c) => c == ESCAPE_CHAR || c == GROUP_OPEN || c == CAPTURE_OPEN;
        public static bool UsesContent(this char c) => c == GROUP_OPEN || c == CAPTURE_OPEN || c == RICH_SHORT || c == ALIAS;
        public static bool Closer(this char c) => c == GROUP_CLOSE || c == CAPTURE_CLOSE || c == PARAM_CLOSE;
        public static char GetCloser(this char c) => c switch
        {
            char v when v == GROUP_OPEN => GROUP_CLOSE,
            char v when v == CAPTURE_OPEN => CAPTURE_CLOSE,
            char v when v == PARAM_OPEN => PARAM_CLOSE,
            _ => '\0'
        };
        internal static string LookupRichKey(char c) => c switch
        {
            'c' => "color",
            _ => ""
        };
        internal static string? TryParseParameter(Parameter p, FormatWrapper vals) => p.GetValue()[0] switch
        {
            's' => Convert.ToDecimal(vals[p.Parameters[0].GetValue()[0]]) == 1 ? "" : "s",
            _ => null,
        };
    }
}
