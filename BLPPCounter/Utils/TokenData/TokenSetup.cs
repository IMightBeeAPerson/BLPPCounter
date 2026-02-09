using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BLPPCounter.Utils.TokenData
{
    internal class TokenSetup(string formatName, string counterName, int controlParamCount, PropertyInfo format, Func<string, FormatWrapper, Dictionary<string, char>, Formatter> formatCreator)
    {
        private readonly Dictionary<string, char> aliases = [];
        private readonly Dictionary<char, string> descriptions = [], customNames = [];
        private readonly Dictionary<char, object> tokenValues = [];
        private readonly Dictionary<char, IEnumerable<(string, object)>> tokenTestParameters = [];
        private readonly Dictionary<char, int> tokenFormatterId = [];

        private readonly List<Func<object, bool, object>> formatters = [];
        private readonly List<(Type, char)> tokenTypes = [];

        private readonly string formatName = formatName;
        private readonly string counterName = counterName;
        private readonly int controlParamCount = controlParamCount;
        private readonly PropertyInfo format = format;
        private readonly Func<string, FormatWrapper, Dictionary<string, char>, Formatter> formatCreator = formatCreator;

        public void RegisterToken<T>(char token, string alias, string description, T defaultValue)
        {
            tokenTypes.Add((typeof(T), token));
            aliases[alias] = token;
            descriptions[token] = description;
            tokenValues[token] = defaultValue;
        }
        public void RegisterTokenTestParameters(char token, IEnumerable<(string, object)> parameters) => tokenTestParameters[token] = parameters;
        public void AddFormatting(Func<object, bool, object> formatter, params char[] tokens)
        {
            foreach (char token in tokens)
                tokenFormatterId[token] = formatters.Count;

            formatters.Add(formatter);
        }
        public void SetCustomName(char token, string name) => customNames[token] = name;
        public TokenHandler GetHandler()
        {
            FormatRelation relation = new(
                formatName,
                counterName,
                (string)format.GetValue(PluginConfig.Instance.FormatSettings),
                str => format.SetValue(PluginConfig.Instance.FormatSettings, str),
                aliases,
                descriptions,
                FormatRelation.FormatDisplayer(formatCreator, aliases),
                new(tokenValues),
                Tokens.GLOBAL_PARAM_AMOUNT,
                tokenFormatterId,
                [.. formatters],
                tokenTestParameters,
                customNames
            );

            (Type, char)[] extras = new (Type, char)[controlParamCount];
            for (int i = 0; i < controlParamCount; i++)
                extras[i] = (typeof(bool), (char)(i + 1));

            return new(relation, new([.. tokenTypes.Union(extras)]), formatCreator);
        }
    }
}
