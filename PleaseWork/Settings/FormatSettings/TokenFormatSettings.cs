namespace PleaseWork.Settings.FormatSettings
{
    public class TokenFormatSettings
    {
        public virtual char EscapeCharacter { get; set; } = '&';
        public virtual char GroupBracketOpen { get; set; } = '[';
        public virtual char GroupBracketClose { get; set; } = ']';
        public virtual char GroupInsertSelf { get; set; } = '$';
    }
}
