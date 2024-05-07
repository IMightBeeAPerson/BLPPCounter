
namespace PleaseWork.Settings.FormatSettings
{
    public class MessageSettings
    {
        public virtual string CustomClanMessage { get; set; } = "Get <color=\"yellow\">&c</color>% for <color=\"yellow\">&p</color> PP!";
        public virtual string MapCapturedMessage { get; set; } = "<color=\"green\">Map Was Captured!</color>";
        public virtual string MapUncapturableMessage { get; set; } = "<color=\"red\">Map Uncapturable</color>";
    }
}
