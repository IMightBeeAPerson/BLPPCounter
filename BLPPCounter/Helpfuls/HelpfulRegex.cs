using System.Text.RegularExpressions;

namespace BLPPCounter.Helpfuls
{
    public static class HelpfulRegex
    {
        public static readonly Regex JsonFloatValueGrabber = new(
            @"^\s*""(.+?)"": *(-?\d(?:\.\d+)?).*$",
            RegexOptions.Multiline
        );
        public static readonly Regex ConvertMenuRegex = new(
            @"<([^ ]+-setting|text|button)[^>]*(?<=text) *= *(['""])(?!~)(.*?)\2[^>]*?(?:(?<=hover-hint) *= *(['""])(.*?)\4[^>]*)?\/>(?=[^<]*?$)",
            RegexOptions.Multiline
        );
        public static readonly Regex LoadElementsRegex = new(
            @"(?<=\s)<\/?([A-z\-]+)[^>]*>(?=[^<]*?$)(?!\z)",
            RegexOptions.Multiline
        );
        public static readonly Regex MarkTagFinder = new("</?mark[^>]*>"); //Simple regex that finds any <mark> tag.
    }
}
