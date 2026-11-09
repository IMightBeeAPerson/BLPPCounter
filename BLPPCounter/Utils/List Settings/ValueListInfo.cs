using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Helpfuls;
using BLPPCounter.Helpfuls.FormatHelpers;
using UnityEngine;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class ValueListInfo : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, MemberInfo> UiValues = HelpfulMisc
            .GetAllVariablesUsingAttribute(
                typeof(ValueListInfo),
                typeof(UIValue),
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            .ToDictionary(member => member.Name);

        private readonly Type _actualType;
        private readonly char _token;
        private readonly Action _updatePreview;

        private readonly Func<object, bool, object>? _valueFormatter;

        private object _rawValue;
        private ValueType _valueType = ValueType.Inferred;

        private bool _isInteger;
        private float _minimum;
        private float _maximum;
        private float _increment;

        internal ValueListInfo(
            object givenValue,
            char token,
            string name,
            bool hasWrapper,
            Func<object, bool, object>? valueFormatter,
            IEnumerable<(string, object)>? extraParameters,
            ValueType valueType,
            Action updatePreview
        )
        {
            _rawValue = givenValue ?? throw new ArgumentNullException(nameof(givenValue));

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _token = token;
            ValueName = name;
            HasWrapper = hasWrapper;
            _valueFormatter = valueFormatter;

            _updatePreview = updatePreview ?? throw new ArgumentNullException(nameof(updatePreview));

            _actualType = givenValue.GetType();

            _valueType = valueType == ValueType.Inferred ? InferValueType(_actualType) : valueType;

            SetDefaultEditorParameters();

            if (extraParameters is not null)
                ApplyExtraParameters(extraParameters);

            if (_valueType == ValueType.Color)
            {
                GivenValueColor = HelpfulMisc.TextToColor((string)_rawValue);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal bool HasWrapper { get; }

        [UIValue(nameof(ShowToggle))]
        private bool ShowToggle => _valueType == ValueType.Toggle;

        [UIValue(nameof(ShowTextBox))]
        private bool ShowTextBox => _valueType == ValueType.Text;

        [UIValue(nameof(ShowIncrement))]
        private bool ShowIncrement => _valueType == ValueType.Increment;

        [UIValue(nameof(ShowColor))]
        private bool ShowColor => _valueType == ValueType.Color;

        [UIValue(nameof(ValueName))]
        private string ValueName { get; }

        [UIValue(nameof(GivenValueBool))]
        private bool GivenValueBool
        {
            get => _rawValue is bool value && value;
            set
            {
                if (ShowToggle)
                    SetValue(value);
            }
        }

        [UIValue(nameof(GivenValueString))]
        private string GivenValueString
        {
            get => _rawValue as string ?? _rawValue.ToString() ?? "";
            set
            {
                if (ShowTextBox)
                    SetValue(value);
            }
        }

        [UIValue(nameof(GivenValueNumber))]
        private float GivenValueNumber
        {
            get
            {
                if (!ShowIncrement)
                    return 0f;

                return Convert.ToSingle(_rawValue);
            }
            set
            {
                if (!ShowIncrement)
                    return;

                object converted = Convert.ChangeType(value, _actualType);

                SetValue(converted);
            }
        }

        [UIValue(nameof(GivenValueColor))]
        private Color GivenValueColor
        {
            get
            {
                if (!ShowColor || _rawValue is not string value || value.Length < 2)
                {
                    return default;
                }

                return HelpfulMisc.ConvertColor(
                    System.Drawing.Color.FromArgb(
                        HelpfulMisc.RgbaToArgb(int.Parse(value[1..], System.Globalization.NumberStyles.HexNumber))
                    )
                );
            }
            set
            {
                if (!ShowColor)
                    return;

                SetValue(HelpfulMisc.ConvertColorToHex(HelpfulMisc.ConvertColor(value)));
            }
        }

        [UIValue(nameof(IsInteger))]
        private bool IsInteger => _isInteger;

        [UIValue(nameof(MinVal))]
        private float MinVal => _minimum;

        [UIValue(nameof(MaxVal))]
        private float MaxVal => _maximum;

        [UIValue(nameof(IncrementVal))]
        private float IncrementVal => _increment;

        private object GivenValue => HasWrapper ? new Func<object>(() => _rawValue) : _rawValue;

        private object FormattedGivenValue => _valueFormatter?.Invoke(GivenValue, false) ?? GivenValue;

        [UIAction(nameof(Formatterer))]
        private string Formatterer(object input)
        {
            return $"<align=\"center\">" + (_valueFormatter?.Invoke(input, true) ?? input.ToString());
        }

        internal object GetValue(bool formatted)
        {
            return formatted ? FormattedGivenValue : GivenValue;
        }

        internal char Token => _token;

        public override string ToString()
        {
            object formatted = _valueFormatter?.Invoke(GivenValue, false) ?? "null";

            string rawText = HasWrapper ? $"() => {_rawValue}" : _rawValue.ToString() ?? "";

            string formattedText =
                HasWrapper && formatted is Func<object> wrapper
                    ? $"() => {wrapper.Invoke()}"
                    : formatted.ToString() ?? "";

            return $"Raw: {rawText} || "
                + $"Formatted: {formattedText} || "
                + $"Value formatted: "
                + $"{Formatterer(_rawValue)}";
        }

        internal static FormatWrapper GetNewTestValues(
            IEnumerable<ValueListInfo> values,
            bool formatted = true,
            FormatWrapper? destination = null
        )
        {
            if (values is null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (destination is null)
            {
                Dictionary<char, object> output = [];

                foreach (ValueListInfo value in values)
                {
                    output[value.Token] = value.GetValue(formatted);
                }

                return new FormatWrapper(output);
            }

            foreach (ValueListInfo value in values)
            {
                destination[value.Token] = value.GetValue(formatted);
            }

            return destination;
        }

        private void SetValue(object value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(_rawValue, value))
                return;

            _rawValue = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            _updatePreview();
        }

        private void SetDefaultEditorParameters()
        {
            _isInteger = HelpfulMisc.IsIntType(_actualType);

            _minimum = 0f;
            _maximum = 10f;
            _increment = 1f;
        }

        private void ApplyExtraParameters(IEnumerable<(string, object)> extraParameters)
        {
            foreach ((string name, object value) in extraParameters)
            {
                if (!UiValues.TryGetValue(name, out MemberInfo? member))
                {
                    continue;
                }

                switch (member.Name)
                {
                    case nameof(IsInteger):
                        _isInteger = Convert.ToBoolean(value);
                        break;

                    case nameof(MinVal):
                        _minimum = Convert.ToSingle(value);
                        break;

                    case nameof(MaxVal):
                        _maximum = Convert.ToSingle(value);
                        break;

                    case nameof(IncrementVal):
                        _increment = Convert.ToSingle(value);
                        break;
                }
            }
        }

        private static ValueType InferValueType(Type type)
        {
            if (type == typeof(bool))
                return ValueType.Toggle;

            if (HelpfulMisc.IsNumber(type))
                return ValueType.Increment;

            return ValueType.Text;
        }

        internal enum ValueType
        {
            Inferred,
            Text,
            Increment,
            Toggle,
            Color,
        }
    }
}
