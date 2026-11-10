using System;
using System.Collections.Generic;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.TokenParser.FormatTypes;
using Zenject;

namespace BLPPCounter.Utils.TokenParser
{
    internal sealed class Tokens : IInitializable
    {
        private readonly Dictionary<char, ParamInfo> ParamParsers = [];

        [Inject]
        private readonly PluginConfig pc = null!;

        public char ESCAPE_CHAR { get; private set; }
        public char RICH_SHORT { get; private set; }
        public char DELIMITER { get; private set; }
        public char GROUP_OPEN { get; private set; }
        public char INSERT_SELF { get; private set; }
        public char GROUP_CLOSE { get; private set; }
        public char CAPTURE_OPEN { get; private set; }
        public char CAPTURE_CLOSE { get; private set; }
        public char PARAM_OPEN { get; private set; }
        public char PARAM_CLOSE { get; private set; }
        public char ALIAS { get; private set; }

        public HashSet<char> SPECIAL_TOKENS { get; private set; } = null!;
        public readonly Dictionary<string, char> GlobalAliases = new()
        {
            { "Dynamic s", 's' },
            { "Hide", 'h' },
            { "Gradient", 'g' },
        };
        public Func<char, int> GLOBAL_PARAM_AMOUNT { get; private set; } = null!;

        public void Initialize()
        {
            ESCAPE_CHAR = pc.TokenSettings.EscapeCharacter;
            RICH_SHORT = pc.TokenSettings.RichTextShorthand;
            DELIMITER = pc.TokenSettings.Delimiter;
            GROUP_OPEN = pc.TokenSettings.GroupBracketOpen;
            INSERT_SELF = pc.TokenSettings.GroupInsertSelf;
            GROUP_CLOSE = pc.TokenSettings.GroupBracketClose;
            CAPTURE_OPEN = pc.TokenSettings.CaptureBracketOpen;
            CAPTURE_CLOSE = pc.TokenSettings.CaptureBracketClose;
            PARAM_OPEN = pc.TokenSettings.EscapeCharParamBracketOpen;
            PARAM_CLOSE = pc.TokenSettings.EscapeCharParamBracketClose;
            ALIAS = pc.TokenSettings.NicknameIndicator;

            SPECIAL_TOKENS =
            [
                ESCAPE_CHAR,
                GROUP_OPEN,
                GROUP_CLOSE,
                CAPTURE_OPEN,
                CAPTURE_CLOSE,
                INSERT_SELF,
                RICH_SHORT,
                ALIAS,
            ];

            GLOBAL_PARAM_AMOUNT = c => ParamParsers[c].MaxParams;

            RegisterParamParser(
                's',
                1,
                p =>
                {
                    char key = p.Parameters[0].GetValue()[0];
                    return vals => vals.GetValueAsNumber<int>(key) == 1 ? "" : "s";
                }
            );
            RegisterParamParser(
                'h',
                1,
                2,
                p =>
                {
                    char key = p.Parameters[0].GetValue()[0];
                    int threshold = p.Parameters.Count > 1 ? int.Parse(p.Parameters[1].GetValue()) : 0;
                    return vals => vals.GetValueAsNumber<decimal>(key) > threshold ? $"{vals[key]}" : "";
                }
            );
            RegisterParamParser(
                'g',
                1,
                p =>
                {
                    char key = p.Parameters[0].GetValue()[0];
                    return vals =>
                        pc.UseGrad
                            ? HelpfulFormatter.NumberToGradient(vals.GetValueAsNumber<float>(key), pc)
                            : HelpfulFormatter.NumberToColor(vals.GetValueAsNumber<float>(key));
                }
            );
        }

        public Chunk? ToChunk(char c, char symbol = '\0', IEnumerable<Chunk>? chunks = null)
        {
            string content = Chunk.Combine(chunks);
            return c switch
            {
                char v when v == ESCAPE_CHAR || v == INSERT_SELF => SPECIAL_TOKENS.Contains(symbol)
                    ? new Chunk(symbol + "")
                    : new Token(symbol),
                char v when v == GROUP_OPEN || v == RICH_SHORT => new Group(symbol, chunks!),
                char v when v == CAPTURE_OPEN => new Capture(symbol, chunks!),
                _ => new Chunk(content),
            };
        }

        public bool UsesSymbol(char c)
        {
            return c == ESCAPE_CHAR || c == GROUP_OPEN || c == CAPTURE_OPEN;
        }

        public bool UsesContent(char c)
        {
            return c == GROUP_OPEN || c == CAPTURE_OPEN || c == RICH_SHORT || c == ALIAS;
        }

        public bool Closer(char c)
        {
            return c == GROUP_CLOSE || c == CAPTURE_CLOSE || c == PARAM_CLOSE;
        }

        public char GetCloser(char c)
        {
            return c switch
            {
                char v when v == GROUP_OPEN => GROUP_CLOSE,
                char v when v == CAPTURE_OPEN => CAPTURE_CLOSE,
                char v when v == PARAM_OPEN => PARAM_CLOSE,
                _ => '\0',
            };
        }

        internal string LookupRichKey(char c)
        {
            return pc.TokenSettings.RichShorthands.TryGetValue(c.ToString(), out string? val) ? val : c + "";
        }

        internal void RegisterParamParser(
            char name,
            int minParams,
            int maxParams,
            Func<Parameter, Func<FormatWrapper, string>> parser
        )
        {
            ParamParsers[name] = new ParamInfo(name, minParams, maxParams, parser);
        }

        internal void RegisterParamParser(char name, int count, Func<Parameter, Func<FormatWrapper, string>> parser)
        {
            ParamParsers[name] = new ParamInfo(name, count, parser);
        }

        internal Func<FormatWrapper, string> PreparseParameter(Parameter p)
        {
            if (ParamParsers.TryGetValue(p.GetValue()[0], out ParamInfo info))
            {
                int pLen = p.Parameters.Count;
                if (pLen < info.MinParams || pLen > info.MaxParams)
                    throw new FormatException(
                        $"Parameter '{info.Name}' expected between {info.MinParams} and {info.MaxParams} arguments, but got {pLen}."
                    );
                return info.Parser(p);
            }
            throw new KeyNotFoundException($"No parser registered for parameter '{p.GetValue()[0]}'.");
        }

        public readonly struct ParamInfo(
            char name,
            int minParams,
            int maxParams,
            Func<Parameter, Func<FormatWrapper, string>> parser
        )
        {
            public readonly char Name = name;
            public readonly int MinParams = minParams;
            public readonly int MaxParams = maxParams;
            public readonly Func<Parameter, Func<FormatWrapper, string>> Parser = parser;

            public ParamInfo(char name, int count, Func<Parameter, Func<FormatWrapper, string>> parser)
                : this(name, count, count, parser) { }

            public override int GetHashCode()
            {
                return Name.GetHashCode();
            }
        }
    }
}
