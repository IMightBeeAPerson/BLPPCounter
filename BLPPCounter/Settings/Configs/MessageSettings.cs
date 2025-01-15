
namespace BLPPCounter.Settings.Configs
{
    public class MessageSettings
    {
        public virtual string ClanMessage { get; set; } = "Get [c&a]% for [c&p] PP!";
        public virtual string MapCapturedMessage { get; set; } = "<color=\"green\">Map Was Captured!</color>";
        public virtual string MapUncapturableMessage { get; set; } = "<color=\"red\">Map Uncapturable</color>";
        public virtual string MapUnrankedMessage { get; set; } = "<color=#999999>Map is not ranked</color>";
        public virtual string LoadFailedMessage { get; set; } = "<color=\"red\">Load Failed</color>";
        public virtual string TargetingMessage { get; set; } = "Targeting *c,red*&t*";
        public virtual string PercentNeededMessage { get; set; } = "Aiming for [c&a%]";
        #region UI Messages
        public virtual string RelativeCalcInfo { get; set; } = "To beat your target, *c,red*&t*, you need *c,green*&a*% accuracy[m with the mod(s)\n*c,yellow*$*]";
        #endregion
    }
}
