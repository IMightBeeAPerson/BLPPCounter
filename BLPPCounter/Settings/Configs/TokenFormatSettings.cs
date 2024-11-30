using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using BLPPCounter.Utils;
using System.Collections.Generic;

namespace BLPPCounter.Settings.Configs
{
    public class TokenFormatSettings
    {
        public virtual char EscapeCharacter { get; set; } = '&';
        public virtual char RichTextShorthand { get; set; } = '*';
        public virtual char Delimiter { get; set; } = ',';
        public virtual char GroupInsertSelf { get; set; } = '$';
        public virtual char GroupBracketOpen { get; set; } = '[';
        public virtual char GroupBracketClose { get; set; } = ']';
        public virtual char CaptureBracketOpen { get; set; } = '<';
        public virtual char CaptureBracketClose { get; set; } = '>';
        public virtual char EscapeCharParamBracketOpen { get; set; } = '(';
        public virtual char EscapeCharParamBracketClose { get; set; } = ')';
        public virtual char NicknameIndicator { get; set; } = '\'';

        [UseConverter(typeof(DictionaryConverter<string>))]
        public virtual Dictionary<string, string> RichShorthands { get; set; } = new Dictionary<string, string>()
        {
            {"c", "color" }
        };
        [UseConverter(typeof(ListConverter<CustomAlias>))]
        public virtual List<CustomAlias> TokenAliases { get; set; } = new List<CustomAlias>();
    }
}
