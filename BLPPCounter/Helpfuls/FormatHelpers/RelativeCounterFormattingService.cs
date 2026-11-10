using System;
using System.Collections.Generic;
using BLPPCounter.Counters;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.TokenParser;
using BLPPCounter.Utils.TokenParser.Printers;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal sealed class RelativeCounterFormattingService : IRelativeCounterFormattingService
    {
        private const string FormatName = "Main Format";

        private readonly object _sync = new();
        private readonly PluginConfig _config;
        private readonly Formatter.Factory _formatterFactory;
        private readonly Tokens _token;
        private readonly TokenParser _tokenParser;
        private readonly FormatWrapper _wrapper;
        private readonly FormatRelation[] _relations;

        private Formatter? _formatter;
        private Printer? _printer;

        public RelativeCounterFormattingService(
            PluginConfig config,
            Formatter.Factory formatterFactory,
            Tokens token,
            TokenParser tokenParser
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _formatterFactory = formatterFactory ?? throw new ArgumentNullException(nameof(formatterFactory));

            _token = token ?? throw new ArgumentNullException(nameof(token));

            _tokenParser = tokenParser ?? throw new ArgumentNullException(nameof(tokenParser));

            Aliases = new Dictionary<string, char>
            {
                { "Acc Difference", 'd' },
                { "PP Difference", 'x' },
                { "PP", 'p' },
                { "Label", 'l' },
                { "FCPP Difference", 'y' },
                { "FCPP", 'o' },
                { "Accuracy", 'a' },
                { "Target", 't' },
                { "Mistakes", 'e' },
                { "Mistake Color", 'z' },
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

                    return _printer!.UsedKeys.ContainsAny('x', 'p', 'y', 'o');
                }
            }
        }

        public string Format(
            bool displayFullCombo,
            bool displayExtraInformation,
            int mistakes,
            string mistakeColor,
            float accuracyDifference,
            float ppDifference,
            float pp,
            float fullComboPpDifference,
            float fullComboPp,
            float targetAccuracy,
            string label,
            string target
        )
        {
            lock (_sync)
            {
                EnsureFormatter();

                _wrapper.SetValues(
                    ((char)1, displayFullCombo),
                    ((char)2, displayExtraInformation),
                    ('e', mistakes),
                    ('z', mistakeColor),
                    ('d', accuracyDifference),
                    ('x', ppDifference),
                    ('p', pp),
                    ('y', fullComboPpDifference),
                    ('o', fullComboPp),
                    ('a', targetAccuracy),
                    ('l', label),
                    ('t', target)
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
                || !string.Equals(relation.GetKey.CounterName, RelativeCounter.DisplayName, StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    "The relation is not owned by the " + "relative-counter formatting service.",
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
            _formatter = SetupFormatter(_config.FormatSettings.RelativeTextFormat, _wrapper, Aliases);

            _printer = _formatter.GetOutput();
        }

        private Formatter SetupFormatter(string format, FormatWrapper values, Dictionary<string, char> aliases)
        {
            Formatter formatter = _formatterFactory.Create(_tokenParser.ParseTokens(format, aliases), values);

            if (!_config.ShowLbl)
            {
                formatter.SetTokenToConstantValue('l');
            }

            if (_config.Target == Targeter.NO_TARGET || !_config.ShowEnemy)
            {
                formatter.SetTokenToConstantValue('t');
            }

            formatter.SurroundTokens("$", "</color>", 'z');

            formatter.FlagTokensForToString('x', 'y', 'd');

            formatter.PromiseValueForAllTokens();

            return formatter;
        }

        private FormatRelation CreateRelation()
        {
            return new FormatRelation(
                FormatName,
                RelativeCounter.DisplayName,
                _config.FormatSettings.RelativeTextFormat,
                value => _config.FormatSettings.RelativeTextFormat = value,
                Aliases,
                new Dictionary<char, string>
                {
                    { 'd', "The accuracy difference between the " + "player and target" },
                    { 'x', "The PP difference between the player " + "and target" },
                    { 'p', "The player's current PP" },
                    { 'l', "The PP label" },
                    { 'y', "The full-combo PP difference" },
                    { 'o', "The player's full-combo PP" },
                    { 'a', "The target accuracy" },
                    { 't', "The targeted player's formatted name" },
                    { 'e', "The player's mistake count" },
                    { 'z', "The color representing the mistake " + "difference" },
                },
                FormatRelation.FormatDisplayer(SetupFormatter, Aliases),
                new FormatWrapper(
                    new Dictionary<char, object>
                    {
                        { (char)1, true },
                        { (char)2, true },
                        { 'e', 1 },
                        { 'd', 0.1f },
                        { 'x', -30.5f },
                        { 'p', 543.21f },
                        { 'y', 21.21f },
                        { 'o', 654.32f },
                        { 'a', 99.54f },
                        { 'l', "PP" },
                        { 't', "Targeting <color=red>Person</color>" },
                        { 'z', "yellow" },
                    }
                ),
                _token.GLOBAL_PARAM_AMOUNT,
                new Dictionary<char, int> { { 'a', 0 }, { 't', 1 } },
                [
                    FormatRelation.CreateFunc("{0}%", "{0}"),
                    FormatRelation.CreateFunc("Targeting <color=red>{0}</color>", "{0}"),
                ],
                new Dictionary<char, IEnumerable<(string, object)>>
                {
                    { 'd', new (string, object)[] { ("MinVal", 0), ("MaxVal", 50), ("IncrementVal", 1.5f) } },
                    { 'x', new (string, object)[] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10) } },
                    { 'p', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'y', new (string, object)[] { ("MinVal", -100), ("MaxVal", 100), ("IncrementVal", 10) } },
                    { 'o', new (string, object)[] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10) } },
                    { 'a', new (string, object)[] { ("MinVal", 10), ("MaxVal", 100), ("IncrementVal", 0.5f) } },
                },
                [((char)1, "Has a miss"), ((char)2, "Is bottom of text")]
            );
        }

        private static FormatWrapper CreateWrapper()
        {
            return new FormatWrapper(
                (typeof(bool), (char)1),
                (typeof(bool), (char)2),
                (typeof(int), 'e'),
                (typeof(string), 'z'),
                (typeof(string), 't'),
                (typeof(float), 'd'),
                (typeof(float), 'x'),
                (typeof(float), 'p'),
                (typeof(float), 'y'),
                (typeof(float), 'o'),
                (typeof(float), 'a'),
                (typeof(string), 'l')
            );
        }
    }
}
