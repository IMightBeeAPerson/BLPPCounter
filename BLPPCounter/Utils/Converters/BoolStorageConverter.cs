using BLPPCounter.Utils.Special_Utils;
using IPA.Config.Data;
using IPA.Config.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Converters
{
    public class BoolStorageConverter : ValueConverter<BoolStorage> 
    {
        public override BoolStorage FromValue(Value value, object parent)
        {
            if (value is List arr)
            {
                int currentLen = (int)(arr.Last() as Integer).Value;
                arr.RemoveAt(arr.Count - 1);
                return new BoolStorage(currentLen, arr.Select(token => (ulong)(token as Integer).Value).ToArray());
            }
            return new BoolStorage();
        }
        public override Value ToValue(BoolStorage obj, object parent) =>
            Value.From(obj.GetVals().Append((ulong)obj.CurrentLength).Select(current => new Integer((long)current)));
    }
}
