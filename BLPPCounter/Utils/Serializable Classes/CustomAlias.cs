using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using ModestTree;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.Serializable_Classes
{
    public struct CustomAlias
    {
        [JsonProperty(nameof(CounterName), Required = Required.DisallowNull)]
        public string CounterName;
        [JsonProperty(nameof(FormatName), Required = Required.DisallowNull)]
        public string FormatName;
        [JsonProperty(nameof(AliasCharacter), Required = Required.DisallowNull)]
        public char AliasCharacter;
        [JsonProperty(nameof(AliasName), Required = Required.DisallowNull)]
        public string AliasName;
        [JsonProperty(nameof(OldAlias), Required = Required.DisallowNull)]
        public string OldAlias;

        public CustomAlias(string counterName, string formatName, char aliasCharacter, string aliasName, string oldAliasName)
        {
            if (!TheCounter.DisplayNameToCounter.ContainsKey(counterName)) throw new ArgumentException("The counter name given doesn't exist.");
            CounterName = counterName;
            FormatName = formatName;
            AliasCharacter = aliasCharacter;
            AliasName = aliasName;
            OldAlias = oldAliasName;
        }

        private static void ChangeAliases(IEnumerable<CustomAlias> aliases, Dictionary<string, char> aliasConverter, string counterName, bool toRemove)
        {
            if (counterName.Length == 0) return;
            aliases = aliases.Where(a => counterName.Equals(a.CounterName) || a.CounterName.IsEmpty()).RemoveDuplicates(ca => ca.AliasCharacter);
            //if (aliases.Count() > 0) Plugin.Log.Info("Before: " + string.Join(", ", aliasConverter));
            foreach (var alias in aliases) 
            {
                if (toRemove ^ aliasConverter.ContainsKey(alias.AliasName)) continue;
                string hold = aliasConverter.FirstOrDefault(a => a.Value == alias.AliasCharacter).Key;
                if (!aliasConverter.ContainsKey(hold)) throw new ArgumentException("The given key isn't present in the given aliasConverter.");
                aliasConverter[toRemove ? alias.OldAlias : alias.AliasName] = aliasConverter[hold];
                aliasConverter.Remove(hold);
            }
            //if (aliases.Count() > 0) Plugin.Log.Info("After: " + string.Join(", ", aliasConverter));
        }
        public static void ApplyAliases(IEnumerable<CustomAlias> aliases, Dictionary<string, char> aliasConverter, string counterName) =>
            ChangeAliases(aliases, aliasConverter, counterName, false);
        internal static void ApplyAliases(IEnumerable<CustomAlias> aliases, Dictionary<(string, string), FormatRelation> relations) =>
            ApplyAliases(aliases, relations.Values);
        internal static void ApplyAliases(IEnumerable<CustomAlias> aliases, IEnumerable<FormatRelation> relations)
        { foreach (FormatRelation fr in relations) ApplyAliases(aliases, fr); }
        internal static void ApplyAliases(IEnumerable<CustomAlias> aliases, FormatRelation relation)
        {
            IEnumerable<CustomAlias> appliedAliases = aliases.Where(ca => (ca.FormatName, ca.CounterName).Equals(relation.GetKey));
            if (appliedAliases.Any()) ApplyAliases(appliedAliases, relation.Alias, relation.CounterName);
        }
        public static void RemoveAliases(IEnumerable<CustomAlias> aliases, Dictionary<string, char> aliasConverter, string counterName) =>
            ChangeAliases(aliases, aliasConverter, counterName, true);
        internal static void RemoveAliases(IEnumerable<CustomAlias> aliases, Dictionary<(string, string), FormatRelation> relations)
        {
            foreach ((string, string) key in relations.Keys)
            {
                IEnumerable<CustomAlias> appliedAliases = aliases.Where(ca => (ca.FormatName, ca.CounterName).Equals(key));
                if (appliedAliases.Any()) RemoveAliases(appliedAliases, relations[key]);
            }
        }
        internal static void RemoveAliases(IEnumerable<CustomAlias> aliases, IEnumerable<FormatRelation> relations) =>
            RemoveAliases(aliases, relations.ToDictionary(fr => fr.GetKey));
        internal static void RemoveAliases(IEnumerable<CustomAlias> aliases, FormatRelation relation) =>
            RemoveAliases(aliases, relation.Alias, relation.CounterName);


        public override readonly string ToString() => $"For {CounterName} counter, replace the default alias for the variable '{AliasCharacter}' to be \"{AliasName}\"";
    }
}
