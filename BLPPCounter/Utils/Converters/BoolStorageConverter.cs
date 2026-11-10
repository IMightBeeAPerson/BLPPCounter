using System.Linq;
using BLPPCounter.Utils.Special_Utils;
using IPA.Config.Data;
using IPA.Config.Stores;

namespace BLPPCounter.Utils.Converters
{
    public class BoolStorageConverter : ValueConverter<BoolStorage>
    {
        public override BoolStorage FromValue(Value? value, object parent)
        {
            if (value is List arr)
            {
                int currentLen = (int)((arr.Last() as Integer)?.Value ?? 0);
                arr.RemoveAt(arr.Count - 1);
                return new BoolStorage(currentLen, [.. arr.Select(token => (ulong)((token as Integer)?.Value ?? 0))]);
            }
            return new BoolStorage();
        }

        public override Value ToValue(BoolStorage? obj, object parent)
        {
            if (obj is not null)
                return Value.From(
                    obj.GetVals().Append((ulong)obj.CurrentLength).Select(current => new Integer((long)current))
                );
            return Value.From("");
        }
    }
}
