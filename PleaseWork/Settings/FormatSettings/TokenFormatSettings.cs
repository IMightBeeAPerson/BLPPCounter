using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using System.Collections.Generic;

namespace PleaseWork.Settings.FormatSettings
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
        [UseConverter(typeof(DictionaryConverter<string>))]
        public virtual Dictionary<string, string> RichShorthands { get; set; } = new Dictionary<string, string>()
        {
            {"c", "color" }
        };
        
    }
}
