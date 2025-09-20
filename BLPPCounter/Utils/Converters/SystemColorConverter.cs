using BLPPCounter.Helpfuls;
using IPA.Config.Data;
using IPA.Config.Stores;
using System.Drawing;

namespace BLPPCounter.Utils.Converters
{
    public class SystemColorConverter : ValueConverter<Color>
    {
        public override Color FromValue(Value value, object parent) =>
            value is Text t ? Color.FromArgb(HelpfulMisc.RgbaToArgb(int.Parse(t.Value.Substring(1), System.Globalization.NumberStyles.HexNumber))) : default;
        public override Value ToValue(Color obj, object parent) => new Text($"#{HelpfulMisc.ToRgba(obj):X8}");
    }
}
