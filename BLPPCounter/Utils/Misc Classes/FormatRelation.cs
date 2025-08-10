using BLPPCounter.Utils.List_Settings;
using IPA.Utilities;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils
{
    internal class FormatRelation
    {
        public readonly string Name;
        public readonly string CounterName;
        public string Format { get => _Format; set { _Format = value; FormatSetter.Invoke(value); } }
        private string _Format;
        private readonly Dictionary<char, ValueListInfo.ValueType> ValTypes;
        private readonly Action<string> FormatSetter;
        public readonly Dictionary<string, char> Alias;
        public readonly Dictionary<char, string> Descriptions;
        private readonly Func<char, int> ParamAmounts;
        private readonly Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> GetFormat;
        public readonly Dictionary<char, object> TestValues;
        private readonly Dictionary<char, IEnumerable<(string, object)>> TestValueParams;
        private readonly Dictionary<char, int> TestValueFormatIndex;
        private readonly Func<object, bool, object>[] TestValueFormats;
        private readonly Dictionary<char, string> TokenToName;
        public (string, string) GetKey => (Name, CounterName);
        public string GetName(char token) => TokenToName.TryGetValue(token, out string name) ? name : null;
        public ValueListInfo.ValueType GetValueType(char token) =>
            (ValTypes?.TryGetValue(token, out var outp) ?? false) ? outp : ValueListInfo.ValueType.Inferred;
        public int GetParamAmount(char token) => ParamAmounts(token);

        public FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<char, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts, Dictionary<char, int> testValueFormatIndex,
            Func<object, bool, object>[] testValueFormats, Dictionary<char, IEnumerable<(string, object)>> testValueParams,
            IEnumerable<KeyValuePair<char, string>> extraNames = null, IEnumerable<KeyValuePair<char, ValueListInfo.ValueType>> valTypes = null)
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
            if (valTypes != null) ValTypes = new Dictionary<char, ValueListInfo.ValueType>(valTypes);
            var hold = alias.Select(kvp => new KeyValuePair<char, string>(kvp.Value, kvp.Key));
            if (extraNames != null) hold = hold.Union(extraNames);
            TokenToName = new Dictionary<char, string>(hold);
        }
        public FormatRelation(string name, string counterName, string format, Action<string> formatSetter, Dictionary<string, char> alias,
            Dictionary<char, string> descriptions, Func<string, (Func<Func<Dictionary<char, object>, string>>, string)> getFormat,
            Dictionary<char, object> testValues, Func<char, int> paramAmounts, Dictionary<char, int> testValueFormatIndex,
            Func<object, bool, object>[] testValueFormats, Dictionary<char, IEnumerable<(string, object)>> testValueParams,
            IEnumerable<(char, string)> extraNames, IEnumerable<(char, ValueListInfo.ValueType)> valTypes = null) :
            this(name, counterName, format, formatSetter, alias, descriptions, getFormat, testValues, paramAmounts, testValueFormatIndex, testValueFormats,
                testValueParams, extraNames?.Select(a => new KeyValuePair<char, string>(a.Item1, a.Item2)), 
                valTypes?.Select(a => new KeyValuePair<char, ValueListInfo.ValueType>(a.Item1, a.Item2)))
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
        public Dictionary<char, object> GetFormattedTestVals(bool toDisplay)
        {
            Dictionary<char, object> outp = new Dictionary<char, object>(TestValues);
            if (TestValueFormats == null) return outp;
            foreach (char token in TestValueFormatIndex.Keys)
                outp[token] = TestValueFormats[TestValueFormatIndex[token]].Invoke(outp[token], toDisplay);
            return outp;
        }
        public IEnumerable<(string, object)> GetExtraTestParams(char token) =>
            (TestValueParams?.TryGetValue(token, out var result) ?? false) ? result : default;
        public Func<object, bool, object> GetTestValFormatter(char token) =>
            (TestValueFormatIndex?.TryGetValue(token, out var result) ?? false) ? TestValueFormats[result] : default;
        public static Func<object, bool, object> CreateFunc<T>(Func<T, string> displayFormat, Func<T, string> formatFormat) =>
            (obj, isDisplay) => 
            obj is T outp ? //Condition #1
            isDisplay ? //Condition #2
            displayFormat(outp) : //#1: True, #2: True
            formatFormat(outp) :  //#1: True, #2: False
            obj.ToString(); //#1: False
        public static Func<object, bool, object> CreateFunc<T>(Func<T, string> format) =>
            (obj, _) => 
            obj is T outp ? //Condition #1
            format(outp) : //#1: True
            obj.ToString(); //#1: False
        public static Func<object, bool, object> CreateFunc(string displayFormat, string formatFormat) =>
            (obj, isDisplay) =>
            isDisplay ? //Condition #1
            string.Format(displayFormat, obj) : //#1: True
            string.Format(formatFormat, obj); //#1: False
        public static Func<object, bool, object> CreateFunc(string format) => (obj, _) => string.Format(format, obj);
        public static Func<object, bool, object> CreateFuncWithWrapper<T>(Func<T, string> displayFormat, Func<T, Func<object>> formatFormat) =>
            (wrapper, isDisplay) => {
                object obj = wrapper is Func<object> f ? f.Invoke() : wrapper;
                return obj is T outp ? //Condition #1
                isDisplay ? //Condition #2
                displayFormat(outp) as object : //#1: True, #2: True
                formatFormat(outp) : //#1: True, #2: False
                obj.ToString(); //#1: False
            };
        public static Func<object, bool, object> CreateFuncWithWrapper(string displayFormat, string formatFormat) =>
            (wrapper, isDisplay) =>
            {
                object obj = wrapper is Func<object> f ? f.Invoke() : wrapper; //Unwraps the wrapper
                return isDisplay ? //Return based off of this is to display or to format
                string.Format(displayFormat, obj) as object : //return this if it is to display
                new Func<object>(() => string.Format(formatFormat, obj)); //return this if it is to format
            };
            
        public static Func<object, bool, object> CreateFuncWithWrapper(string format) => CreateFuncWithWrapper(format, format);
        public override bool Equals(object obj) => obj is FormatRelation fr && Equals(fr);
        public bool Equals(FormatRelation fr) => !(fr is null) && fr.CounterName.Equals(CounterName) && fr.Name.Equals(Name);
        public override int GetHashCode() => base.GetHashCode();
        public static bool operator ==(FormatRelation left, FormatRelation right) => !(left is null) && left.Equals(right);
        public static bool operator !=(FormatRelation left, FormatRelation right) => left is null || !left.Equals(right);
    }
}
