using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.List_Settings
{
    internal class FormatRelation
    {
        public readonly string Name;
        public readonly string CounterName;
        public string Format { get => _Format; set { _Format = value; FormatSetter.Invoke(value); } }
        private string _Format;
        private readonly Action<string> FormatSetter;
        public readonly Dictionary<string, char> Alias;
        public readonly Dictionary<string, string> Descriptions;
        private readonly Func<char, int> ParamAmounts;
        private readonly Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> GetFormat;
        private readonly Dictionary<char, object> TestValues;
        public (string, string) GetKey => (Name, CounterName);
        public int GetParamAmount(char token) => ParamAmounts(token);

        public FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<string, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts)
        {
            Name = name;
            CounterName = counterName;
            _Format = format;
            FormatSetter = formatSetter;
            Alias = alias;
            Descriptions = descriptions;
            ParamAmounts = paramAmounts;
            GetFormat = getFormat;
            TestValues = testValues;
        }

        public string GetQuickFormat(string rawFormat)
        {
            (Func<Func<Dictionary<char, object>, string>>, string) gotFormat = GetFormat.Invoke(rawFormat); //item1 = formatter, item2 = error message
            return gotFormat.Item1?.Invoke().Invoke(TestValues) ?? gotFormat.Item2;
        }
        public string GetQuickFormat() => GetQuickFormat(_Format);
    }
}
