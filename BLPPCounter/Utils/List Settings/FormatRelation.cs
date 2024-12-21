using IPA.Utilities;
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
        internal readonly Dictionary<string, char> Alias;
        internal readonly Dictionary<string, string> Descriptions;
        private readonly Func<char, int> ParamAmounts;
        private readonly Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> GetFormat;
        internal readonly Dictionary<char, object> TestValues;
        private readonly Dictionary<char, IEnumerable<(string, object)>> TestValueParams;
        private readonly Dictionary<char, int> TestValueFormatIndex;
        private readonly Func<object, bool, string>[] TestValueFormats;
        private readonly Dictionary<char, string> TokenToName;
        public (string, string) GetKey => (Name, CounterName);
        public string GetName(char token) => TokenToName.TryGetValue(token, out string name) ? name : null;
        public int GetParamAmount(char token) => ParamAmounts(token);

        internal FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<string, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts, Dictionary<char, int> testValueFormatIndex, 
            Func<object, bool, string>[] testValueFormats, Dictionary<char, IEnumerable<(string, object)>> testValueParams,
            IEnumerable<KeyValuePair<char, string>> extraNames = null)
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
            TestValueFormatIndex = testValueFormatIndex;
            TestValueFormats = testValueFormats;
            TestValueParams = testValueParams;
            var hold = alias.Select(kvp => new KeyValuePair<char, string>(kvp.Value, kvp.Key));
            if (extraNames != null) hold.Union(extraNames);
            TokenToName = new Dictionary<char, string>(hold);
        }
        internal FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<string, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts, Dictionary<char, int> testValueFormatIndex,
            Func<object, bool, string>[] testValueFormats, Dictionary<char, IEnumerable<(string, object)>> testValueParams, 
            IEnumerable<(char, string)> extraNames) :
            this(name, counterName, format, formatSetter, alias, descriptions, getFormat, testValues, paramAmounts, testValueFormatIndex, testValueFormats,
                testValueParams, extraNames.Select(a => new KeyValuePair<char, string>(a.Item1, a.Item2)))
        { }
        internal FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<string, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts, Dictionary<char, int> testValueFormatIndex,
            Func<object, bool, string>[] testValueFormats) :
            this(name, counterName, format, formatSetter, alias, descriptions, getFormat, testValues, paramAmounts, testValueFormatIndex, testValueFormats, null)
        { }

        internal FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<string, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts) :
            this(name, counterName, format, formatSetter, alias, descriptions, getFormat, testValues, paramAmounts, null, null, null)
        { }
        private string GetQuickFormat(string rawFormat, bool useFormatsOnTestVals, Dictionary<char, object> givenTestVals = null)
        {
            Dictionary<char, object> testVals = givenTestVals ?? (useFormatsOnTestVals ? GetFormattedTestVals(false) : TestValues);
            (Func<Func<Dictionary<char, object>, string>>, string) gotFormat = GetFormat.Invoke(rawFormat); //item1 = formatter, item2 = error message
            return gotFormat.Item1?.Invoke().Invoke(testVals) ?? gotFormat.Item2;
        }
        public string GetQuickFormat(string rawFormat = default) => GetQuickFormat(rawFormat == default ? _Format : rawFormat, true);
        public string GetQuickFormat(Dictionary<char, object> testVals, string rawFormat = default) => 
            GetQuickFormat(rawFormat == default ? _Format : rawFormat, true, testVals);
        public string GetQuickFormatWithRawTestVals(string rawFormat = default) => GetQuickFormat(rawFormat == default ? _Format : rawFormat, false);
        internal Dictionary<char, object> GetFormattedTestVals(bool toDisplay)
        {
            Dictionary<char, object> outp = new Dictionary<char, object>(TestValues);
            if (TestValueFormats == null) return outp;
            foreach (char token in TestValueFormatIndex.Keys)
                outp[token] = TestValueFormats[TestValueFormatIndex[token]].Invoke(outp[token], toDisplay);
            return outp;
        }
        public IEnumerable<(string, object)> GetExtraTestParams(char token) => 
            (TestValueParams?.TryGetValue(token, out var result) ?? false) ? result : default;
        public Func<object, bool, string> GetTestValFormatter(char token) =>
            (TestValueFormatIndex?.TryGetValue(token, out var result) ?? false) ? TestValueFormats[result] : default;
        internal static Func<object, bool, string> CreateFunc<T>(Func<T, string> displayFormat, Func<T, string> formatFormat) =>
            (obj, isDisplay) => obj is T outp ? isDisplay ? displayFormat(outp) : formatFormat(outp) : obj.ToString();
        internal static Func<object, bool, string> CreateFunc<T>(Func<T, string> format) =>
            (obj, _) => obj is T outp ? format(outp) : obj.ToString();
        internal static Func<object, bool, string> CreateFunc<T>(string displayFormat, string formatFormat) =>
            (obj, isDisplay) => obj is T outp ? isDisplay ? string.Format(displayFormat, outp) : string.Format(formatFormat, outp) : obj.ToString();
        internal static Func<object, bool, string> CreateFunc<T>(string format) =>
            (obj, _) => obj is T outp ? string.Format(format, outp) : obj.ToString();
    }
}
