using BLPPCounter.Utils;
using System.Text.RegularExpressions;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulRegex
    {
        public static readonly Regex JsonFloatValueGrabber = new(@"^\s*""(.+?)"": *(-?\d(?:\.\d+)?).*$", RegexOptions.Multiline);
        public static readonly Regex ConvertMenuRegex = new(@"<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['""])(?!~)(.*?)\2[^>]*?(?:(?<=hover-hint) *= *(['""])(.*?)\4[^>]*)?\/>(?=[^<]*?$)", RegexOptions.Multiline);
        public static readonly Regex LoadElementsRegex = new(@"(?<=\s)<\/?([A-z\-]+)[^>]*>(?=[^<]*?$)(?!\z)", RegexOptions.Multiline);
        internal static readonly Regex CollectiveFormatRegex = FormatListInfo.GetRegexForAllChunks(); // \G(?:(?<Insert_Group_Value>\$)|(?<Group_Open>(?<Alias>\['[^']+')|(?<Token>\[[^']))|(?<Regular_Text>[^$&*[\]<>]+)|(?<Escaped_Character>&[&*[\]<>])|(?<Escaped_Token>(?<Token>&.|&'[^']+')\((?<Params>[^\)]+)\)|(?<Token>&'[^']+'|&.))|(?<Capture_Open><\d+)|(?<Capture_Close>>)|(?<Group_Close>])|(?<Rich_Text_Open>\*(?<Key>[^,\*]+),(?<Value>[^\*]+)\*|<(?<Key>[^=]+)=(?<Value>[^>]+)>)|(?<Rich_Text_Close>\*|<[^>]+>))
        public static readonly Regex MarkTagFinder = new("</?mark[^>]*>"); //Simple regex that finds any <mark> tag.
    }
}
