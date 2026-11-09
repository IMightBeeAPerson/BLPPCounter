using System;
using System.Collections.Generic;
using BLPPCounter.Counters;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal sealed class ClanCounterFormattingService : IClanCounterFormattingService
    {
        private const string MainFormatName = "Main Format";

        private const string WeightedFormatName = "Weighted Format";

        private const string MessageFormatName = "Custom Message Format";

        private readonly object _sync = new();
        private readonly PluginConfig _config;

        private readonly FormatWrapper _mainWrapper;
        private readonly FormatWrapper _weightedWrapper;
        private readonly FormatWrapper _messageWrapper;

        private readonly FormatRelation[] _relations;

        private Printer? _mainPrinter;
        private Printer? _weightedPrinter;
        private Printer? _messagePrinter;

        public ClanCounterFormattingService(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            MainAliases = new Dictionary<string, char>
            {
                { "PP", 'p' },
                { "PP Difference", 'x' },
                { "FCPP", 'o' },
                { "FCPP Difference", 'y' },
                { "Label", 'l' },
                { "Mistakes", 'e' },
                { "Target", 't' },
                { "Message", 'm' },
            };

            WeightedAliases = new Dictionary<string, char>
            {
                { "Mistakes", 'e' },
                { "Rank Color", 'c' },
                { "Rank", 'r' },
                { "Weighted PP", 'x' },
                { "PP", 'p' },
                { "Label", 'l' },
                { "Weighted FCPP", 'y' },
                { "FCPP", 'o' },
                { "Message", 'm' },
            };

            MessageAliases = new Dictionary<string, char>
            {
                { "Color", 'c' },
                { "Accuracy", 'a' },
                { "Tech PP", 'x' },
                { "Acc PP", 'y' },
                { "Pass PP", 'z' },
                { "PP", 'p' },
                { "Target", 't' },
            };

            _mainWrapper = CreateMainWrapper();
            _weightedWrapper = CreateWeightedWrapper();

            _messageWrapper = CreateMessageWrapper();

            _relations = [CreateMainRelation(), CreateWeightedRelation(), CreateMessageRelation()];
        }

        public IReadOnlyList<FormatRelation> Relations => _relations;

        public Dictionary<string, char> MainAliases { get; }

        public Dictionary<string, char> WeightedAliases { get; }

        public Dictionary<string, char> MessageAliases { get; }

        public bool UsesMainPpValues
        {
            get
            {
                lock (_sync)
                {
                    EnsureMainFormatter();

                    return _mainPrinter!.UsedKeys.ContainsAny('x', 'p', 'y', 'o');
                }
            }
        }

        public bool UsesWeightedPpValues
        {
            get
            {
                lock (_sync)
                {
                    EnsureWeightedFormatter();

                    return _weightedPrinter!.UsedKeys.ContainsAny('x', 'p', 'y', 'o');
                }
            }
        }

        public string FormatMain(
            bool displayFullCombo,
            bool displayExtraInformation,
            int mistakes,
            float ppDifference,
            float pp,
            float fullComboPpDifference,
            float fullComboPp,
            string label,
            string target,
            string message
        )
        {
            lock (_sync)
            {
                EnsureMainFormatter();

                _mainWrapper.SetValues(
                    ((char)1, displayFullCombo),
                    ((char)2, displayExtraInformation),
                    ('e', mistakes),
                    ('x', ppDifference),
                    ('p', pp),
                    ('y', fullComboPpDifference),
                    ('o', fullComboPp),
                    ('l', label),
                    ('t', target),
                    ('m', message)
                );

                return _mainPrinter!.Print();
            }
        }

        public string FormatWeighted(
            bool displayFullCombo,
            bool displayExtraInformation,
            bool showRank,
            int mistakes,
            string rankColor,
            int rank,
            float weightedPp,
            float pp,
            float weightedFullComboPp,
            float fullComboPp,
            string label,
            string message
        )
        {
            lock (_sync)
            {
                EnsureWeightedFormatter();

                _weightedWrapper.SetValues(
                    ((char)1, displayFullCombo),
                    ((char)2, displayExtraInformation),
                    ((char)3, showRank),
                    ('e', mistakes),
                    ('c', rankColor),
                    ('r', rank),
                    ('x', weightedPp),
                    ('p', pp),
                    ('y', weightedFullComboPp),
                    ('o', fullComboPp),
                    ('l', label),
                    ('m', message)
                );

                return _weightedPrinter!.Print();
            }
        }

        public string FormatMessage(
            string color,
            float accuracy,
            float accuracyPp,
            float passPp,
            float technologyPp,
            float totalPp,
            string target
        )
        {
            lock (_sync)
            {
                EnsureMessageFormatter();

                _messageWrapper.SetValues(
                    ('c', color),
                    ('a', accuracy),
                    ('y', accuracyPp),
                    ('z', passPp),
                    ('x', technologyPp),
                    ('p', totalPp),
                    ('t', target)
                );

                return _messagePrinter!.Print();
            }
        }

        public void Rebuild(FormatRelation relation)
        {
            if (relation is null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            lock (_sync)
            {
                switch (relation.GetKey.FormatName)
                {
                    case MainFormatName:
                        RebuildMainFormatter();
                        break;

                    case WeightedFormatName:
                        RebuildWeightedFormatter();
                        break;

                    case MessageFormatName:
                        RebuildMessageFormatter();
                        break;

                    default:
                        throw new ArgumentException(
                            "This format relation is not " + "owned by the clan formatter.",
                            nameof(relation)
                        );
                }
            }
        }

        public void RebuildAll()
        {
            lock (_sync)
            {
                RebuildMainFormatter();
                RebuildWeightedFormatter();
                RebuildMessageFormatter();
            }
        }

        private void EnsureMainFormatter()
        {
            if (_mainPrinter is null)
                RebuildMainFormatter();
        }

        private void EnsureWeightedFormatter()
        {
            if (_weightedPrinter is null)
                RebuildWeightedFormatter();
        }

        private void EnsureMessageFormatter()
        {
            if (_messagePrinter is null)
                RebuildMessageFormatter();
        }

        private void RebuildMainFormatter()
        {
            Formatter formatter = SetupMainFormatter(_config.FormatSettings.ClanTextFormat, _mainWrapper, MainAliases);

            _mainPrinter = formatter.GetOutput();
        }

        private void RebuildWeightedFormatter()
        {
            Formatter formatter = SetupWeightedFormatter(
                _config.FormatSettings.WeightedTextFormat,
                _weightedWrapper,
                WeightedAliases
            );

            _weightedPrinter = formatter.GetOutput();
        }

        private void RebuildMessageFormatter()
        {
            Formatter formatter = SetupMessageFormatter(
                _config.MessageSettings.ClanMessage,
                _messageWrapper,
                MessageAliases
            );

            _messagePrinter = formatter.GetOutput();
        }

        private Formatter SetupMainFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = new(TokenParser.ParseTokens(format, aliases), values);

            if (!_config.ShowLbl)
            {
                formatter.SetTokenToConstantValue('l');
            }

            if (!_config.ShowEnemy || _config.Target == Utils.Misc_Classes.Targeter.NO_TARGET)
            {
                formatter.SetTokenToConstantValue('t');
            }

            formatter.FlagTokensForToString('x', 'y');

            formatter.PromiseValueForAllTokens();

            return formatter;
        }

        private Formatter SetupWeightedFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = new(TokenParser.ParseTokens(format, aliases), values);

            if (!_config.ShowLbl)
            {
                formatter.SetTokenToConstantValue('l');
            }

            formatter.PromiseValueForAllTokens();

            return formatter;
        }

        private Formatter SetupMessageFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = new(TokenParser.ParseTokens(format, aliases), values);

            if (!_config.ShowEnemy || _config.Target == Utils.Misc_Classes.Targeter.NO_TARGET)
            {
                formatter.SetTokenToConstantValue('t');
            }

            formatter.SurroundTokens("$", "</color>", 'c');

            formatter.PromiseValueForAllTokens();

            return formatter;
        }

        private FormatRelation CreateMainRelation()
        {
            return new FormatRelation(
                MainFormatName,
                ClanCounter.DisplayName,
                _config.FormatSettings.ClanTextFormat,
                value => _config.FormatSettings.ClanTextFormat = value,
                MainAliases,
                new Dictionary<char, string>
                {
                    { 'p', "The current unmodified PP" },
                    { 'x', "The PP difference from the amount " + "needed to capture the map" },
                    { 'o', "The full-combo unmodified PP" },
                    { 'y', "The full-combo PP difference" },
                    { 'l', "The PP label" },
                    { 'e', "The current mistake count" },
                    { 't', "The formatted target name" },
                    { 'm', "The capture requirement message" },
                },
                FormatRelation.FormatDisplayer(SetupMainFormatter, MainAliases),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { (char)1, true },
                        { (char)2, true },
                        { 'p', 543.21f },
                        { 'x', -69.42f },
                        { 'o', 654.32f },
                        { 'y', 42.69f },
                        { 'l', " PP" },
                        { 'e', 1 },
                        { 't', "Targeting <color=red>" + "Person</color>" },
                        { 'm', "Get 95% for 543.21 PP!" },
                    }
                ),
                Tokens.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 't', 0 }, { 'm', 1 } },
                [
                    FormatRelation.CreateFunc("Targeting <color=red>{0}</color>", "{0}"),
                    FormatRelation.CreateFunc("{0}", "{0}"),
                ],
                CreatePpParameters(),
                [((char)1, "Has a miss"), ((char)2, "Is bottom of text")]
            );
        }

        private FormatRelation CreateWeightedRelation()
        {
            Dictionary<char, IEnumerable<(string, object)>> parameters = CreatePpParameters();

            parameters['c'] = CreateIntegerParameters(1, 100, 1);

            parameters['r'] = CreateIntegerParameters(1, 100, 1);

            return new FormatRelation(
                WeightedFormatName,
                ClanCounter.DisplayName,
                _config.FormatSettings.WeightedTextFormat,
                value => _config.FormatSettings.WeightedTextFormat = value,
                WeightedAliases,
                new Dictionary<char, string>
                {
                    { 'e', "The current mistake count" },
                    { 'c', "The color corresponding to the " + "player's clan rank" },
                    { 'r', "The player's projected clan rank" },
                    { 'x', "The weighted current PP" },
                    { 'p', "The unweighted current PP" },
                    { 'l', "The PP label" },
                    { 'y', "The weighted full-combo PP" },
                    { 'o', "The unweighted full-combo PP" },
                    { 'm', "The reason normal capture mode " + "cannot be displayed" },
                },
                FormatRelation.FormatDisplayer(SetupWeightedFormatter, WeightedAliases),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { (char)1, true },
                        { (char)2, true },
                        { (char)3, true },
                        { 'e', 1 },
                        { 'c', HelpfulFormatter.GetWeightedRankColor(3) },
                        { 'r', 3 },
                        { 'x', 473.21f },
                        { 'p', 543.21f },
                        { 'l', " PP" },
                        { 'y', 521.69f },
                        { 'o', 654.32f },
                        { 'm', "<color=red>Load Failed</color>" },
                    }
                ),
                Tokens.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 'c', 0 }, { 'r', 1 } },
                [
                    FormatRelation.CreateFuncWithWrapper<int>(
                        rank => HelpfulFormatter.GetWeightedRankColor(rank) + rank,
                        rank => () => HelpfulFormatter.GetWeightedRankColor(rank)
                    ),
                    FormatRelation.CreateFunc("#{0}", "{0}"),
                ],
                parameters,
                [((char)1, "Has a miss"), ((char)2, "Is bottom of text"), ((char)3, "Show rank information")]
            );
        }

        private FormatRelation CreateMessageRelation()
        {
            return new FormatRelation(
                MessageFormatName,
                ClanCounter.DisplayName,
                _config.MessageSettings.ClanMessage,
                value => _config.MessageSettings.ClanMessage = value,
                MessageAliases,
                new Dictionary<char, string>
                {
                    { 'c', "Colors the enclosing group" },
                    { 'a', "The accuracy needed to capture " + "the map" },
                    { 'x', "The needed technology PP" },
                    { 'y', "The needed accuracy PP" },
                    { 'z', "The needed pass PP" },
                    { 'p', "The total PP needed" },
                    { 't', "The formatted target name" },
                },
                FormatRelation.FormatDisplayer(SetupMessageFormatter, MessageAliases),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { 'c', new Func<object>(() => "#0F0") },
                        { 'a', 95.85f },
                        { 'x', 114.14f },
                        { 'y', 321.23f },
                        { 'z', 69.42f },
                        { 'p', 543.21f },
                        { 't', "Targeting <color=red>" + "Person</color>" },
                    }
                ),
                Tokens.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int>
                {
                    { 'c', 0 },
                    { 'a', 1 },
                    { 't', 2 },
                },
                [
                    FormatRelation.CreateFuncWithWrapper("<color={0}>{0}", "<color={0}>"),
                    FormatRelation.CreateFunc("{0}%", "{0}"),
                    FormatRelation.CreateFunc("Targeting <color=red>{0}</color>", "{0}"),
                ],
                new Dictionary<char, IEnumerable<(string, object)>>
                {
                    { 'a', CreateDecimalParameters(0, 100, 1.5f) },
                    { 'x', CreateDecimalParameters(10, 1000, 10) },
                    { 'y', CreateDecimalParameters(10, 1000, 10) },
                    { 'z', CreateDecimalParameters(10, 1000, 10) },
                    { 'p', CreateDecimalParameters(100, 1000, 10) },
                },
                null,
                [('c', ValueListInfo.ValueType.Color)]
            );
        }

        private static Dictionary<char, IEnumerable<(string, object)>> CreatePpParameters()
        {
            return new Dictionary<char, IEnumerable<(string, object)>>
            {
                { 'p', CreateDecimalParameters(100, 1000, 10) },
                { 'o', CreateDecimalParameters(100, 1000, 10) },
                { 'x', CreateDecimalParameters(-100, 100, 5) },
                { 'y', CreateDecimalParameters(-100, 100, 5) },
            };
        }

        private static IEnumerable<(string, object)> CreateDecimalParameters(
            float minimum,
            float maximum,
            float increment
        )
        {
            return new (string, object)[] { ("MinVal", minimum), ("MaxVal", maximum), ("IncrementVal", increment) };
        }

        private static IEnumerable<(string, object)> CreateIntegerParameters(int minimum, int maximum, int increment)
        {
            return new (string, object)[]
            {
                ("IsInteger", true),
                ("MinVal", minimum),
                ("MaxVal", maximum),
                ("IncrementVal", increment),
            };
        }

        private static FormatWrapper CreateMainWrapper()
        {
            return new FormatWrapper(
                (typeof(bool), (char)1),
                (typeof(bool), (char)2),
                (typeof(int), 'e'),
                (typeof(float), 'x'),
                (typeof(float), 'p'),
                (typeof(string), 'l'),
                (typeof(float), 'y'),
                (typeof(float), 'o'),
                (typeof(string), 't'),
                (typeof(string), 'm')
            );
        }

        private static FormatWrapper CreateWeightedWrapper()
        {
            return new FormatWrapper(
                (typeof(bool), (char)1),
                (typeof(bool), (char)2),
                (typeof(bool), (char)3),
                (typeof(int), 'e'),
                (typeof(string), 'c'),
                (typeof(int), 'r'),
                (typeof(float), 'x'),
                (typeof(float), 'p'),
                (typeof(string), 'l'),
                (typeof(float), 'y'),
                (typeof(float), 'o'),
                (typeof(string), 'm')
            );
        }

        private static FormatWrapper CreateMessageWrapper()
        {
            return new FormatWrapper(
                (typeof(string), 'c'),
                (typeof(float), 'a'),
                (typeof(float), 'x'),
                (typeof(float), 'y'),
                (typeof(float), 'z'),
                (typeof(float), 'p'),
                (typeof(string), 't')
            );
        }
    }
}
