using BeatLeader.Utils;
using ModestTree;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PleaseWork.Utils
{
    public struct CustomAlias
    {
        [JsonProperty(nameof(CounterName), Required = Required.DisallowNull)]
        public string CounterName;
        [JsonProperty(nameof(AliasCharacter), Required = Required.DisallowNull)]
        public string AliasCharacter;
        [JsonProperty(nameof(AliasName), Required = Required.DisallowNull)]
        public string AliasName;

        public CustomAlias(string counterName, string aliasCharacter, string aliasName)
        {
            if (!TheCounter.DisplayNameToCounter.ContainsKey(counterName)) throw new ArgumentException("The counter name given doesn't exist.");
            CounterName = counterName;
            AliasCharacter = aliasCharacter;
            AliasName = aliasName;
        }

        public static void ApplyAliases(IEnumerable<CustomAlias> aliases, Dictionary<string, char> aliasConverter, string counterName)
        {
            if (counterName.IsEmpty()) return;
            aliases = aliases.Where(a => (counterName.Equals(a.CounterName) || a.CounterName.IsEmpty()) && 
            (a.AliasCharacter.Length == 1 ? aliasConverter.Values.Contains(a.AliasCharacter[0]) : aliasConverter.Keys.Contains(a.AliasCharacter)));
            if (aliases.Count() > 0) Plugin.Log.Info("Before: " + string.Join(", ", aliasConverter));
            foreach (var alias in aliases) 
            {
                string hold;
                if (alias.AliasCharacter.Length == 1)
                    hold = aliasConverter.First(a => a.Value == alias.AliasCharacter[0]).Key;
                else
                    hold = aliasConverter.Keys.First(a => a.Equals(alias.AliasCharacter));
                if (!aliasConverter.ContainsKey(hold)) throw new ArgumentException("The given key isn't present in the given aliasConverter.");
                aliasConverter[alias.AliasName] = aliasConverter[hold];
                aliasConverter.Remove(hold);
            }
            if (aliases.Count() > 0) Plugin.Log.Info("After: " + string.Join(", ", aliasConverter));

        }

        public override string ToString() => $"For {CounterName} counter, replace the default alias for the variable '{AliasCharacter}' to be \"{AliasName}\"";
    }
}
