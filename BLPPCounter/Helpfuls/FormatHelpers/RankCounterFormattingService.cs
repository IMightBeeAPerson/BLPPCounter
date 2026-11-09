using System;
using System.Collections.Generic;
using BLPPCounter.Counters;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal sealed class RankCounterFormattingService : IRankCounterFormattingService
    {
        private const string FormatName = "Main Format";

        private readonly object _sync = new();
        private readonly PluginConfig _config;
        private readonly FormatWrapper _wrapper;
        private readonly FormatRelation[] _relations;

        private Formatter? _formatter;
        private Printer? _printer;

        public RankCounterFormattingService(PluginConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Aliases = new Dictionary<string, char>
            {
                { "PP", 'x' },
                { "FCPP", 'y' },
                { "Rank", 'r' },
                { "Next Player", 'n' },
                { "PP Difference", 'd' },
                { "Acc Difference", 'p' },
                { "Label", 'l' },
                { "Rank Color", 'c' },
            };

            _wrapper = CreateWrapper();

            _relations = [CreateRelation()];
        }

        public IReadOnlyList<FormatRelation> Relations => _relations;

        public Dictionary<string, char> Aliases { get; }

        public bool UsesPpValues
        {
            get
            {
                lock (_sync)
                {
                    EnsureFormatter();

                    return _printer!.UsedKeys.ContainsAny('x', 'y', 'd');
                }
            }
        }

        public string Format(
            bool displayFullCombo,
            bool displayExtraInformation,
            bool isFirstPlace,
            float pp,
            float fullComboPp,
            int rank,
            string nextPlayerName,
            float ppDifference,
            float accuracyDifference,
            string rankColor,
            string label
        )
        {
            lock (_sync)
            {
                EnsureFormatter();

                _wrapper.SetValues(
                    ((char)1, displayFullCombo),
                    ((char)2, displayExtraInformation),
                    ((char)3, !isFirstPlace && displayExtraInformation),
                    ((char)4, isFirstPlace && displayExtraInformation),
                    ('x', pp),
                    ('y', fullComboPp),
                    ('r', rank),
                    ('n', nextPlayerName),
                    ('d', ppDifference),
                    ('p', accuracyDifference),
                    ('c', rankColor),
                    ('l', label)
                );

                return _printer!.Print();
            }
        }

        public void Rebuild(FormatRelation relation)
        {
            if (relation is null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            if (
                !string.Equals(relation.GetKey.FormatName, FormatName, StringComparison.Ordinal)
                || !string.Equals(relation.GetKey.CounterName, RankCounter.DisplayName, StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    "The relation is not owned by the " + "rank-counter formatting service.",
                    nameof(relation)
                );
            }

            lock (_sync)
                RebuildFormatter();
        }

        public void RebuildAll()
        {
            lock (_sync)
                RebuildFormatter();
        }

        private void EnsureFormatter()
        {
            if (_printer is null)
                RebuildFormatter();
        }

        private void RebuildFormatter()
        {
            _formatter = SetupFormatter(_config.FormatSettings.RankTextFormat, _wrapper, Aliases);

            _printer = _formatter.GetOutput();
        }

        private Formatter SetupFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = new(TokenParser.ParseTokens(format, aliases), values);

            if (!_config.ShowLbl)
            {
                formatter.SetTokenToConstantValue('l');
            }

            formatter.SurroundToken('c', "$", "</color>");

            formatter.PromiseValueForAllTokens();

            return formatter;
        }

        private FormatRelation CreateRelation()
        {
            return new FormatRelation(
                FormatName,
                RankCounter.DisplayName,
                _config.FormatSettings.RankTextFormat,
                value => _config.FormatSettings.RankTextFormat = value,
                Aliases,
                new Dictionary<char, string>
                {
                    { 'x', "The current PP value" },
                    { 'y', "The PP value if the map were full-comboed" },
                    { 'r', "The projected leaderboard rank" },
                    { 'n', "The player directly ahead on the leaderboard" },
                    { 'd', "The PP difference needed to gain one rank" },
                    { 'p', "The accuracy difference to the next player" },
                    { 'l', "The PP label" },
                    { 'c', "The configured color for the projected rank" },
                },
                FormatRelation.FormatDisplayer(SetupFormatter, Aliases),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { (char)1, true },
                        { (char)2, true },
                        { (char)3, true },
                        { (char)4, true },
                        { 'x', 543.21f },
                        { 'y', 654.32f },
                        { 'r', 3 },
                        { 'n', "Person" },
                        { 'd', 1.1f },
                        { 'p', 0.1f },
                        { 'l', "PP" },
                        { 'c', 3 },
                    }
                ),
                Tokens.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 'r', 0 }, { 'c', 1 } },
                [
                    FormatRelation.CreateFunc("#{0}", "{0}"),
                    FormatRelation.CreateFunc<int>(
                        rank => HelpfulFormatter.GetWeightedRankColor(rank) + '#' + rank,
                        HelpfulFormatter.GetWeightedRankColor
                    ),
                ],
                new Dictionary<char, IEnumerable<(string, object)>>
                {
                    { 'x', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'y', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    {
                        'r',
                        new (string, object)[]
                        {
                            ("IsInteger", true),
                            ("MinVal", 1),
                            ("MaxVal", 100),
                            ("IncrementVal", 1),
                        }
                    },
                    { 'd', new (string, object)[] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 1.5f) } },
                    { 'p', new (string, object)[] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 0.5f) } },
                    {
                        'c',
                        new (string, object)[]
                        {
                            ("IsInteger", true),
                            ("MinVal", 1),
                            ("MaxVal", 100),
                            ("IncrementVal", 1),
                        }
                    },
                }
            );
        }

        private static FormatWrapper CreateWrapper()
        {
            return new FormatWrapper(
                (typeof(bool), (char)1),
                (typeof(bool), (char)2),
                (typeof(bool), (char)3),
                (typeof(bool), (char)4),
                (typeof(float), 'x'),
                (typeof(float), 'y'),
                (typeof(int), 'r'),
                (typeof(string), 'n'),
                (typeof(float), 'd'),
                (typeof(float), 'p'),
                (typeof(string), 'c'),
                (typeof(string), 'l')
            );
        }
    }
}
