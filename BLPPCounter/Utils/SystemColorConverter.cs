using IPA.Config.Data;
using IPA.Config.Stores;
using System.Drawing;

namespace BLPPCounter.Utils
{
    public class SystemColorConverter : ValueConverter<Color>
    {
        public override Color FromValue(Value value, object parent) =>
            value is Text t ? Color.FromArgb(int.Parse(t.Value.Substring(1), System.Globalization.NumberStyles.HexNumber)) : default;
        public override Value ToValue(Color obj, object parent) => new Text($"#{obj.ToArgb():X8}");
    }
}
