using System;
using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Utils.Serializable_Classes;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class AliasListInfo
    {
        internal AliasListInfo(CustomAlias alias)
        {
            if (alias.Equals(default))
                throw new ArgumentNullException(nameof(alias));

            Alias = alias;
        }

        internal event Action<AliasListInfo>? RemoveRequested;

        public CustomAlias Alias { get; }

        [UIValue(nameof(OldAliasName))]
        private string OldAliasName => Alias.OldAlias;

        [UIValue(nameof(Counter))]
        private string Counter => $"Counter: {Alias.CounterName}";

        [UIValue(nameof(Format))]
        private string Format => $"Format: {Alias.FormatName}";

        [UIValue(nameof(TokenConversion))]
        private string TokenConversion => $"{OldAliasName} -> {Alias.AliasName}";

        [UIValue(nameof(Token))]
        private string Token => $"Token <color=green>{Alias.AliasCharacter}";

        [UIAction(nameof(RemoveAlias))]
        private void RemoveAlias()
        {
            RemoveRequested?.Invoke(this);
        }

        internal void Apply(FormatRelation relation)
        {
            if (relation is null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            CustomAlias.ApplyAliases([Alias], relation);
        }

        internal void Unapply(FormatRelation relation)
        {
            if (relation is null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            CustomAlias.RemoveAliases([Alias], relation);
        }

        public override string ToString()
        {
            return Alias.ToString();
        }
    }
}
