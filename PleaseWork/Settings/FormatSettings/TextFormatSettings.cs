using PleaseWork.Utils;
using System.Collections.Generic;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;

namespace PleaseWork.Settings.FormatSettings
{
    public class TextFormatSettings
    {
        public virtual string NumberFormat { get; set; } = "▲#;▼#;0";
        public virtual string DefaultTextFormat { get; set; } = "&x<1 / &y> &l";
        public virtual string ClanTextFormat { get; set; } = "[p$ ]&[[c&x]&]<1 / [o$ ]&[[f&y]&] >&l";
        public virtual string WeightedTextFormat { get; set; } = "&x[p ($)]<1 / &y[o ($)]><2 [c#&r]> &l";
        public virtual string RelativeTextFormat { get; set; } = "[c&x][p ($)]<1 || [f&y][o ($)]> &l";
        [UseConverter(typeof(ListConverter<ColorMatch>))]
        public virtual List<ColorMatch> WeightedRankColors { get; set; } = new List<ColorMatch>()
        {
            new ColorMatch(1, "FFD700"),
			new ColorMatch(2, "C0C0C0"),
			new ColorMatch(3, "CD7F32"),
            new ColorMatch(10,"A020F0"),
            new ColorMatch(15,"AAAA00"),
            new ColorMatch(20,"999999")
        };//*/
        /*public virtual List<(int, string)> WeightedRankColors { get; set; } = new List<(int, string)>()
        {
            (1, "#FF0000")
        };//*//*{
			"Rank": 1,
			"Color": "FFD700"
		},
		{
			"Rank": 2,
			"Color": "C0C0C0"
		},
		{
			"Rank": 3,
			"Color": "CD7F32"
		},
		{
			"Rank": 10,
			"Color": "A020F0"
		},
		{
			"Rank": 15,
			"Color": "AAAA00"
		},
		{
			"Rank": 20,
			"Color": "999999"
		}*/

    }
}
