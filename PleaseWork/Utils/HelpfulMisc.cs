using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PleaseWork.Utils
{
    public static class HelpfulMisc
    {
        public static Modifier SpeedToModifier(double speed) => speed > 1.0 ? speed >= 1.5 ? Modifier.SuperFastSong : Modifier.FasterSong :
            speed != 1.0 ? Modifier.SlowerSong : Modifier.None;
        public static string PPTypeToRating(PPType type)
        {
            switch (type)
            {
                case PPType.Acc: return "accRating";
                case PPType.Tech: return "techRating";
                case PPType.Pass: return "passRating";
                case PPType.Star: return "stars";
                default: return "";
            }
        }
        public static string GetModifierShortname(Modifier mod)
        {
            switch (mod)
            {
                case Modifier.SuperFastSong: return "sf";
                case Modifier.FasterSong: return "fs";
                case Modifier.SlowerSong: return "ss";
                default: return "";
            }
        }
        public static string AddModifier(string name, Modifier modifier) => 
            modifier == Modifier.None ? name : GetModifierShortname(modifier) + name.Substring(0, 1).ToUpper() + name.Substring(1);
    }
}
