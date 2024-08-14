
namespace PleaseWork.Settings.FormatSettings
{
    public class MessageSettings
    {
        public virtual string CustomClanMessage { get; set; } = "Get [c&a]% for [c&p] PP!";
        public virtual string MapCapturedMessage { get; set; } = "<color=\"green\">Map Was Captured!</color>";
        public virtual string MapUncapturableMessage { get; set; } = "<color=\"red\">Map Uncapturable</color>";
        public virtual string MapUnrankedMessage { get; set; } = "<color=#999999>Map is not ranked</color>";
        public virtual string LoadFailedMessage { get; set; } = "<color=\"red\">Load Failed</color>";
        public virtual string TargetingMessage { get; set; } = "Targeting <color=\"red\">&t</color>";
    }
}
