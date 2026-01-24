using BLPPCounter.Helpfuls;
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
        private static readonly Dictionary<char, ParamInfo> ParamParsers = [];

        private static PluginConfig PC => PluginConfig.Instance;
        public static readonly char ESCAPE_CHAR = PC.TokenSettings.EscapeCharacter;
        public static readonly char RICH_SHORT = PC.TokenSettings.RichTextShorthand;
        public static readonly char DELIMITER = PC.TokenSettings.Delimiter;
        public static readonly char GROUP_OPEN = PC.TokenSettings.GroupBracketOpen;
        public static readonly char INSERT_SELF = PC.TokenSettings.GroupInsertSelf;
        public static readonly char GROUP_CLOSE = PC.TokenSettings.GroupBracketClose;
        public static readonly char CAPTURE_OPEN = PC.TokenSettings.CaptureBracketOpen;
        public static readonly char CAPTURE_CLOSE = PC.TokenSettings.CaptureBracketClose;
        public static readonly char PARAM_OPEN = PC.TokenSettings.EscapeCharParamBracketOpen;
        public static readonly char PARAM_CLOSE = PC.TokenSettings.EscapeCharParamBracketClose;
        public static readonly char ALIAS = PC.TokenSettings.NicknameIndicator;

        public static readonly HashSet<char> SPECIAL_TOKENS = [ESCAPE_CHAR, GROUP_OPEN, GROUP_CLOSE, CAPTURE_OPEN, CAPTURE_CLOSE, INSERT_SELF, RICH_SHORT, ALIAS];
        public static readonly Dictionary<string, char> GlobalAliases = new() {
            { "Dynamic s", 's' },
            { "Hide" , 'h' },
            { "Gradient", 'g' }
        };

        static Tokens()
        {
            RegisterParamParser('s', 1, p =>
            {
                char key = p.Parameters[0].GetValue()[0];
                return vals => vals.GetValueAsNumber<int>(key) == 1 ? "" : "s";
            });
            RegisterParamParser('h', 1, 2, p =>
            {
                char key = p.Parameters[0].GetValue()[0];
                int threshold = p.Parameters.Count > 1 ? int.Parse(p.Parameters[1].GetValue()) : 0;
                return vals => vals.GetValueAsNumber<decimal>(key) > threshold ? $"{vals[key]}" : "";
            });
            RegisterParamParser('g', 1, p =>
            {
                char key = p.Parameters[0].GetValue()[0];
                return vals => PC.UseGrad ? HelpfulFormatter.NumberToGradient(vals.GetValueAsNumber<float>(key)) : HelpfulFormatter.NumberToColor(vals.GetValueAsNumber<float>(key));
            });
        }

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
        internal static string LookupRichKey(char c) => PC.TokenSettings.RichShorthands.TryGetValue(c.ToString(), out string? val) ? val : c + "";
        /*internal static string? TryParseParameter(Parameter p, FormatWrapper vals)
        {
            if (ParamParsers.TryGetValue(p.GetValue()[0], out ParamInfo info))
            {
                if (info.Count != p.Parameters.Count)
                    throw new FormatException($"Parameter '{info.Name}' expected {info.Count} arguments, but got {p.Parameters.Count}.");
                return info.Parser(p, vals);
            }
            return null;
        }*/
        internal static void RegisterParamParser(char name, int minParams, int maxParams, Func<Parameter, Func<FormatWrapper, string>> parser) =>
            ParamParsers[name] = new ParamInfo(name, minParams, maxParams, parser);
        internal static void RegisterParamParser(char name, int count, Func<Parameter, Func<FormatWrapper, string>> parser) =>
            ParamParsers[name] = new ParamInfo(name, count, parser);
        internal static Func<FormatWrapper, string> PreparseParameter(Parameter p)
        {
            if (ParamParsers.TryGetValue(p.GetValue()[0], out ParamInfo info))
            {
                int pLen = p.Parameters.Count;
                if (pLen < info.MinParams || pLen > info.MaxParams)
                    throw new FormatException($"Parameter '{info.Name}' expected between {info.MinParams} and {info.MaxParams} arguments, but got {pLen}.");
                return info.Parser(p);
            }
            throw new KeyNotFoundException($"No parser registered for parameter '{p.GetValue()[0]}'.");
        }

        public readonly struct ParamInfo(char name, int minParams, int maxParams, Func<Parameter, Func<FormatWrapper, string>> parser)
        {
            public readonly char Name = name;
            public readonly int MinParams = minParams;
            public readonly int MaxParams = maxParams;
            public readonly Func<Parameter, Func<FormatWrapper, string>> Parser = parser;

            public ParamInfo(char name, int count, Func<Parameter, Func<FormatWrapper, string>> parser) : this(name, count, count, parser) { }

            public override int GetHashCode() => Name.GetHashCode();
        }
    }
}
