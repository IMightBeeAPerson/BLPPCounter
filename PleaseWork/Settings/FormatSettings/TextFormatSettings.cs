namespace PleaseWork.Settings.FormatSettings
{
    public class TextFormatSettings
    {
        public virtual string NumberFormat { get; set; } = "▲#;▼#;0";
        public virtual string DefaultTextFormat { get; set; } = "&x&1 / &y&1 &l";
        public virtual string ClanTextFormat { get; set; } = "[p$ ]&[[c&x]&]&1 / [o$ ]&[[f&y]&] &1&l";
        public virtual string WeightedTextFormat { get; set; } = "&x[p ($)]&1 / &y[o ($)]&1 &l";
        public virtual string RelativeTextFormat { get; set; } = "[c&x][p ($)]&1 || [f&y][o ($)]&1 &l";
    }
}
