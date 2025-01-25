using BLPPCounter.Utils;
using System.Collections.Generic;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;

namespace BLPPCounter.Settings.Configs
{
    public class TextFormatSettings
    {
        public virtual string NumberFormat { get; set; } = "▲#;▼#;0";
        public virtual string DefaultTextFormat { get; set; } = "&'PP'<1 / &'FCPP'> &'Label'<2['Mistakes'\n*c,red*$* mistake&'Dynamic s'('Mistakes')]>";
        public virtual string ClanTextFormat { get; set; } = "['PP'$ ]&[['Color'&'PP Difference']&]<1 / ['FCPP'$ ]&[['FC Color'&'FCPP Difference']&] >&'Label'<2\n&'Message'['Target'\n$]>";
        public virtual string WeightedTextFormat { get; set; } = "&'PP Difference'['PP' ($)]<1 / &'FCPP Difference'['FCPP' ($)]><3 ['Rank Color'#&'Rank']> &'Label'<2['Mistakes'\n*c,red*$* mistake&'Dynamic s'('Mistakes')]['Message'\n$]>";
        public virtual string RelativeTextFormat { get; set; } = "['Color'&'PP Difference']['PP' ($)]<1 || ['FC Color'&'FCPP Difference']['FCPP' ($)]> &'Label'<2\n['Color'&'Accuracy']% to beat['Target'\n$]>";
        public virtual string RankTextFormat { get; set; } = "&'PP'<1 / &'FCPP'> &'Label'<2\n['Rank Color'#&'Rank']><3\n*c,green*&'PP Difference'* PP to rank up!><4\n*c,green*&'PP Difference'*['Rank Color' PP ahead of 1st!]>";
        [UseConverter(typeof(ListConverter<ColorMatch>))]
        public virtual List<ColorMatch> WeightedRankColors { get; set; } = new List<ColorMatch>()
        {
            new ColorMatch(1, "FFD700"),
			new ColorMatch(2, "C0C0C0"),
			new ColorMatch(3, "CD7F32"),
            new ColorMatch(10,"A020F0"),
            new ColorMatch(15,"AAAA00"),
            new ColorMatch(20,"999999")
        };

    }
}
