using System;
using System.Collections.Generic;
using System.Text;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal sealed class MainCounterFormattingService : IMainCounterFormattingService
    {
        public const string CounterName = "Main";

        private readonly object _sync = new();
        private readonly PluginConfig _config;
        private readonly Tokens _token;
        private readonly HelpfulFormatter _formatter;
        private readonly Formatter.Factory _formatFactory;
        private readonly TokenParser _tokenParser;

        private readonly FormatWrapper _displayWrapper;
        private readonly FormatWrapper _targetWrapper;
        private readonly FormatWrapper _percentNeededWrapper;

        private Formatter? _displayFormatter;
        private Formatter? _targetFormatter;
        private Formatter? _percentNeededFormatter;

        private Printer? _displayPrinter;
        private Printer? _targetPrinter;
        private Printer? _percentNeededPrinter;

        private readonly FormatRelation[] _relations;

        public MainCounterFormattingService(
            PluginConfig config,
            Tokens token,
            HelpfulFormatter formatter,
            Formatter.Factory formatFactory,
            TokenParser tokenParser
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _token = token ?? throw new ArgumentNullException(nameof(token));

            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

            _formatFactory = formatFactory ?? throw new ArgumentNullException(nameof(formatFactory));

            _tokenParser = tokenParser ?? throw new ArgumentNullException(nameof(tokenParser));

            DisplayAlias = new Dictionary<string, char>
            {
                { "PP", 'x' },
                { "FCPP", 'y' },
                { "Mistake Color", 'z' },
                { "Mistakes", 'e' },
                { "Label", 'l' },
            };

            TargetAlias = new Dictionary<string, char> { { "Target", 't' }, { "Mods", 'm' } };

            PercentNeededAlias = new Dictionary<string, char>
            {
                { "Color", 'c' },
                { "Accuracy", 'a' },
                { "Tech PP", 'x' },
                { "Acc PP", 'y' },
                { "Pass PP", 'z' },
                { "PP", 'p' },
            };

            _displayWrapper = new FormatWrapper(
                (typeof(bool), (char)1),
                (typeof(bool), (char)2),
                (typeof(float), 'x'),
                (typeof(string), 'z'),
                (typeof(string), 'l'),
                (typeof(float), 'y'),
                (typeof(int), 'e')
            );

            _targetWrapper = new FormatWrapper((typeof(string), 't'), (typeof(string), 'm'));

            _percentNeededWrapper = new FormatWrapper(
                (typeof(string), 'c'),
                (typeof(float), 'a'),
                (typeof(float), 'x'),
                (typeof(float), 'y'),
                (typeof(float), 'z'),
                (typeof(float), 'p')
            );

            _relations = [CreateDisplayRelation(), CreateTargetRelation(), CreatePercentNeededRelation()];
        }

        public Dictionary<string, char> DisplayAlias { get; }

        public Dictionary<string, char> TargetAlias { get; }

        public Dictionary<string, char> PercentNeededAlias { get; }

        public IReadOnlyList<FormatRelation> Relations => _relations;

        public bool DisplayUsable
        {
            get
            {
                lock (_sync)
                {
                    return _displayFormatter is not null && _displayPrinter is not null;
                }
            }
        }

        public bool TargetUsable
        {
            get
            {
                lock (_sync)
                {
                    return _targetFormatter is not null && _targetPrinter is not null;
                }
            }
        }

        public bool PercentNeededUsable
        {
            get
            {
                lock (_sync)
                {
                    return _percentNeededFormatter is not null && _percentNeededPrinter is not null;
                }
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
                    case "Main Format":
                        RebuildDisplay();
                        break;

                    case "Target Format":
                        RebuildTarget();
                        break;

                    case "Percent Needed Format":
                        RebuildPercentNeeded();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(relation),
                            relation.GetKey,
                            "This service does not own the relation."
                        );
                }
            }
        }

        public void RebuildAll()
        {
            lock (_sync)
            {
                RebuildDisplay();
                RebuildTarget();
                RebuildPercentNeeded();
            }
        }

        public string FormatTarget(string name, string modifiers)
        {
            lock (_sync)
            {
                EnsureTargetFormatter();

                _targetWrapper.SetValues(('t', name), ('m', modifiers));

                return _targetPrinter!.Print();
            }
        }

        public string FormatPercentNeeded(
            string color,
            float accuracy,
            float passPp,
            float accuracyPp,
            float technologyPp,
            float totalPp
        )
        {
            lock (_sync)
            {
                EnsurePercentNeededFormatter();

                _percentNeededWrapper.SetValues(
                    ('c', color),
                    ('a', accuracy),
                    ('x', technologyPp),
                    ('y', accuracyPp),
                    ('z', passPp),
                    ('p', totalPp)
                );

                return _percentNeededPrinter!.Print();
            }
        }

        public void AppendPpText(
            bool displayFc,
            StringBuilder output,
            PPHandler ppValues,
            int mistakes,
            int displayRatingCount,
            IReadOnlyList<string> labels
        )
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (ppValues is null)
            {
                throw new ArgumentNullException(nameof(ppValues));
            }

            if (labels is null)
            {
                throw new ArgumentNullException(nameof(labels));
            }

            lock (_sync)
            {
                EnsureDisplayFormatter();

                string mistakeColor = $"<color={(mistakes == 0 ? "#999" : "red")}>";

                if (_config.SplitPPVals && displayRatingCount > 1)
                {
                    int count = Math.Min(displayRatingCount, labels.Count);

                    for (int index = 0; index < count; index++)
                    {
                        output.AppendLine(
                            FormatDisplay(
                                displayFc,
                                _config.ExtraInfo && index == count - 1,
                                ppValues[0, index],
                                ppValues[1, index],
                                mistakeColor,
                                mistakes,
                                labels[index]
                            )
                        );
                    }

                    return;
                }

                int selectedIndex = Math.Max(0, displayRatingCount - 1);

                string label = labels.Count == 0 ? "" : labels[Math.Min(selectedIndex, labels.Count - 1)];

                output.AppendLine(
                    FormatDisplay(
                        displayFc,
                        _config.ExtraInfo,
                        ppValues[0, selectedIndex],
                        ppValues[1, selectedIndex],
                        mistakeColor,
                        mistakes,
                        label
                    )
                );
            }
        }

        private string FormatDisplay(
            bool fullCombo,
            bool totalPp,
            float pp,
            float fullComboPp,
            string mistakeColor,
            int mistakes,
            string label
        )
        {
            _displayWrapper.SetValues(
                ((char)1, fullCombo),
                ((char)2, totalPp),
                ('x', pp),
                ('y', fullComboPp),
                ('z', mistakeColor),
                ('e', mistakes),
                ('l', label)
            );

            return _displayPrinter!.Print();
        }

        private void RebuildDisplay()
        {
            _displayFormatter = SetupDisplayFormatter(
                _config.FormatSettings.DefaultTextFormat,
                _displayWrapper,
                DisplayAlias
            );

            _displayPrinter = _displayFormatter.GetOutput();
        }

        private void RebuildTarget()
        {
            _targetFormatter = SetupTargetFormatter(
                _config.MessageSettings.TargetingMessage,
                _targetWrapper,
                TargetAlias
            );

            _targetPrinter = _targetFormatter.GetOutput();
        }

        private void RebuildPercentNeeded()
        {
            _percentNeededFormatter = SetupPercentNeededFormatter(
                _config.MessageSettings.PercentNeededMessage,
                _percentNeededWrapper,
                PercentNeededAlias
            );

            _percentNeededPrinter = _percentNeededFormatter.GetOutput();
        }

        private void EnsureDisplayFormatter()
        {
            if (_displayPrinter is null)
                RebuildDisplay();
        }

        private void EnsureTargetFormatter()
        {
            if (_targetPrinter is null)
                RebuildTarget();
        }

        private void EnsurePercentNeededFormatter()
        {
            if (_percentNeededPrinter is null)
                RebuildPercentNeeded();
        }

        private Formatter SetupDisplayFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = _formatFactory.Create(_tokenParser.ParseTokens(format, aliases), values);

            formatter.SurroundToken('z', "$", "</color>");

            formatter.PromiseValueForTokens();
            return formatter;
        }

        private Formatter SetupTargetFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = _formatFactory.Create(_tokenParser.ParseTokens(format, aliases), values);

            formatter.PromiseValueForTokens();
            return formatter;
        }

        private Formatter SetupPercentNeededFormatter(
            string format,
            FormatWrapper values,
            Dictionary<string, char> aliases
        )
        {
            Formatter formatter = _formatFactory.Create(_tokenParser.ParseTokens(format, aliases), values);

            formatter.SurroundToken('c', "$", "</color>");

            formatter.PromiseValueForTokens();
            return formatter;
        }

        private FormatRelation CreateDisplayRelation()
        {
            return new FormatRelation(
                "Main Format",
                CounterName,
                _config.FormatSettings.DefaultTextFormat,
                value => _config.FormatSettings.DefaultTextFormat = value,
                DisplayAlias,
                new Dictionary<char, string>
                {
                    { 'x', "The unmodified PP number" },
                    { 'y', "The unmodified PP number if the map was FC'ed" },
                    { 'z', "The mistake-count color" },
                    { 'e', "The amount of mistakes" },
                    { 'l', "The PP label" },
                },
                FormatRelation.FormatDisplayer(SetupDisplayFormatter, DisplayAlias),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { (char)1, true },
                        { (char)2, true },
                        { 'x', 543.21f },
                        { 'y', 654.32f },
                        { 'z', "red" },
                        { 'e', 2 },
                        { 'l', " PP" },
                    }
                ),
                _token.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 'z', 0 } },
                [FormatRelation.CreateFunc("<color={0}>{0}", "<color={0}>")],
                new Dictionary<char, IEnumerable<(string, object)>>
                {
                    { 'x', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'y', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                },
                [((char)1, "Has a miss"), ((char)2, "Is bottom of text")]
            );
        }

        private FormatRelation CreateTargetRelation()
        {
            return new FormatRelation(
                "Target Format",
                CounterName,
                _config.MessageSettings.TargetingMessage,
                value => _config.MessageSettings.TargetingMessage = value,
                TargetAlias,
                new Dictionary<char, string>
                {
                    { 't', "The targeted player's name" },
                    { 'm', "The targeted player's modifiers" },
                },
                FormatRelation.FormatDisplayer(SetupTargetFormatter, TargetAlias),
                new FormatWrapper(new Dictionary<char, object> { { 't', "Person" }, { 'm', "SF" } }),
                _token.GLOBAL_PARAM_AMOUNT,
                null,
                null,
                null
            );
        }

        private FormatRelation CreatePercentNeededRelation()
        {
            return new FormatRelation(
                "Percent Needed Format",
                CounterName,
                _config.MessageSettings.PercentNeededMessage,
                value => _config.MessageSettings.PercentNeededMessage = value,
                PercentNeededAlias,
                new Dictionary<char, string>
                {
                    { 'c', "Colors the enclosing group" },
                    { 'a', "The accuracy needed" },
                    { 'x', "The tech PP needed" },
                    { 'y', "The accuracy PP needed" },
                    { 'z', "The pass PP needed" },
                    { 'p', "The total PP needed" },
                },
                FormatRelation.FormatDisplayer(SetupPercentNeededFormatter, PercentNeededAlias),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { 'c', new Func<object>(() => "green") },
                        { 'a', 95.85f },
                        { 'x', 114.14f },
                        { 'y', 321.23f },
                        { 'z', 69.42f },
                        { 'p', 543.21f },
                    }
                ),
                _token.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 'c', 0 }, { 'a', 1 } },
                [
                    FormatRelation.CreateFuncWithWrapper("<color={0}>{0}", "<color={0}>"),
                    FormatRelation.CreateFunc("{0}%", "{0}"),
                ],
                new Dictionary<char, IEnumerable<(string, object)>>
                {
                    { 'a', new (string, object)[] { ("MinVal", 0), ("MaxVal", 100), ("IncrementVal", 1.5f) } },
                    { 'x', new (string, object)[] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'y', new (string, object)[] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'z', new (string, object)[] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'p', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                },
                null,
                [('c', ValueListInfo.ValueType.Color)]
            );
        }
    }
}
