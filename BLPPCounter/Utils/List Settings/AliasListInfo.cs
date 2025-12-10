using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.Serializable_Classes;
using System;
using System.Collections.Generic;

namespace BLPPCounter.Utils.List_Settings
{
    internal class AliasListInfo
    {
        internal static Action<AliasListInfo> RemoveSelf;
        public CustomAlias Alias { get; private set; }
        [UIValue(nameof(OldAliasName))] private string OldAliasName => Alias.OldAlias;
        [UIValue(nameof(Counter))] private string Counter => "Counter: " + Alias.CounterName;
        [UIValue(nameof(Format))] private string Format => "Format: " + Alias.FormatName;
        [UIValue(nameof(TokenConversion))] private string TokenConversion => OldAliasName + " -> " + Alias.AliasName;
        [UIValue(nameof(Token))] private string Token => "Token <color=green>" + Alias.AliasCharacter;

        internal AliasListInfo(CustomAlias alias)
        {
            Alias = alias;
        }

        [UIAction(nameof(RemoveAlias))]
        private void RemoveAlias() => RemoveSelf?.Invoke(this);

        internal void Apply(FormatRelation fr) => CustomAlias.ApplyAliases([Alias], fr);
        internal void Apply(Dictionary<(string, string), FormatRelation> frs) => CustomAlias.ApplyAliases([Alias], frs);
        internal void Unapply(FormatRelation fr) => CustomAlias.RemoveAliases([Alias], fr);
        internal void Unapply(Dictionary<(string, string), FormatRelation> frs) => CustomAlias.RemoveAliases([Alias], frs);
        public override string ToString() => Alias.ToString();
    }
}
