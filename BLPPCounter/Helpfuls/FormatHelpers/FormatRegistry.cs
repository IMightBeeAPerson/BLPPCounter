using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Serializable_Classes;
using Zenject;

namespace BLPPCounter.Helpfuls.FormatHelpers
{
    internal sealed class FormatRegistry : IFormatRegistry, IInitializable
    {
        private readonly PluginConfig _config;
        private readonly IReadOnlyList<IFormatRelationContributor> _contributors;

        private readonly Dictionary<(string FormatName, string CounterName), FormatRelation> _relations = [];

        private readonly Dictionary<(string FormatName, string CounterName), IFormatRelationContributor> _owners = [];

        private string[] _counterNames = [];

        public FormatRegistry(PluginConfig config, List<IFormatRelationContributor> contributors)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _contributors = contributors ?? throw new ArgumentNullException(nameof(contributors));
        }

        public event Action? FormatsRebuilt;

        public IReadOnlyDictionary<(string FormatName, string CounterName), FormatRelation> Relations => _relations;

        public IReadOnlyList<string> CounterNames => _counterNames;

        public void Initialize()
        {
            _relations.Clear();
            _owners.Clear();

            foreach (IFormatRelationContributor contributor in _contributors)
            {
                foreach (FormatRelation relation in contributor.Relations)
                {
                    (string FormatName, string CounterName) key = relation.GetKey;

                    if (_relations.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            $"The format relation "
                                + $"\"{key.FormatName}\" for counter "
                                + $"\"{key.CounterName}\" was registered "
                                + "more than once."
                        );
                    }

                    _relations.Add(key, relation);

                    _owners.Add(key, contributor);
                }
            }

            _counterNames =
            [
                .. _relations.Values.Select(relation => relation.CounterName).Distinct(StringComparer.Ordinal),
            ];

            ReapplyAliases();
            RebuildAll();
        }

        public FormatRelation Get(string formatName, string counterName)
        {
            if (TryGet(formatName, counterName, out FormatRelation? relation))
            {
                return relation;
            }

            throw new KeyNotFoundException(
                $"No format named \"{formatName}\" " + $"is registered for counter " + $"\"{counterName}\"."
            );
        }

        public bool TryGet(string formatName, string counterName, [NotNullWhen(true)] out FormatRelation? relation)
        {
            return _relations.TryGetValue((formatName, counterName), out relation);
        }

        public IReadOnlyList<FormatRelation> GetForCounter(string counterName)
        {
            return
            [
                .. _relations.Values.Where(relation =>
                    string.Equals(relation.CounterName, counterName, StringComparison.Ordinal)
                ),
            ];
        }

        public void Rebuild(FormatRelation relation)
        {
            if (relation is null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            if (!_owners.TryGetValue(relation.GetKey, out IFormatRelationContributor? owner))
            {
                throw new KeyNotFoundException($"Format relation " + $"\"{relation.GetKey}\" is not registered.");
            }

            owner.Rebuild(relation);
            NotifyFormatsRebuilt();
        }

        public void RebuildAll()
        {
            foreach (IFormatRelationContributor contributor in _contributors)
            {
                contributor.RebuildAll();
            }

            NotifyFormatsRebuilt();
        }

        public void ReapplyAliases()
        {
            foreach (FormatRelation relation in _relations.Values)
            {
                CustomAlias.RemoveAliases(_config.TokenSettings.TokenAliases, relation);

                CustomAlias.ApplyAliases(_config.TokenSettings.TokenAliases, relation);
            }
        }

        private void NotifyFormatsRebuilt()
        {
            Action? handler = FormatsRebuilt;

            if (handler is null)
                return;

            foreach (Action subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    Plugin.Log.Error("A format-registry subscriber failed:\n" + exception);
                }
            }
        }
    }
}
