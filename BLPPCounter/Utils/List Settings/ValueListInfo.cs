using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BLPPCounter.Utils.List_Settings
{
    internal class ValueListInfo : INotifyPropertyChanged
    {
        //IDE find and replace pattern
        // \)\s*\n?\s*{\s*\n?\s*([^;]+;)\s*\n?\s*}
        //) => $+
#pragma warning disable IDE0044, CS0414, CS0649
        #region Static Variables
        internal static Action UpdatePreview;
        #endregion
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly Type ActualClass;
        private Func<object, bool, object> ValFormatter;
        private object _GivenValue;
        private char GivenToken;
        private ValueType ValType = ValueType.Inferred;
        private object GivenValue {
            get => HasWrapper ? new Func<object>(() => _GivenValue) : _GivenValue;
            set { _GivenValue = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GivenValue))); }
        }
        private object FormattedGivenValue => ValFormatter?.Invoke(GivenValue, false) ?? GivenValue;
        public bool HasWrapper;
        #endregion
        #region UI Variables
        private static readonly Dictionary<string, MemberInfo> UIVals = 
            HelpfulMisc.GetAllVariablesUsingAttribute(typeof(ValueListInfo), typeof(UIValue), BindingFlags.Instance | BindingFlags.NonPublic).ToDictionary(mi => mi.Name);
        
        [UIValue(nameof(ShowToggle))] private bool ShowToggle => ValType == ValueType.Toggle;
        [UIValue(nameof(ShowTextBox))] private bool ShowTextBox => ValType == ValueType.Text;
        [UIValue(nameof(ShowIncrement))] private bool ShowIncrement => ValType == ValueType.Increment;
        [UIValue(nameof(ShowColor))] private bool ShowColor => ValType == ValueType.Color;

        [UIValue(nameof(ValueName))] private string ValueName;
        [UIValue(nameof(GivenValueBool))] private bool GivenValueBool
        {
            get => _GivenValue is bool outp && outp;
            set { if (ShowToggle) GivenValue = value; }
        }
        [UIValue(nameof(GivenValueString))] private string GivenValueString
        {
            get => _GivenValue is string outp ? outp : _GivenValue.ToString();
            set { if (ShowTextBox) GivenValue = value; }
        }
        [UIValue(nameof(GivenValueNumber))] private float GivenValueNumber
        {
            get => IsInteger ? _GivenValue is int i ? i : default : _GivenValue is float outp ? outp : default;
            set { if (ShowIncrement) GivenValue = value.GetType() == ActualClass ? value : Convert.ChangeType(value, ActualClass); }
        }
        [UIValue(nameof(GivenValueColor))] private Color GivenValueColor
        {//I hate UnityEngine.Color, wayyyy too many steps to convert to it.
            get => ShowColor ? HelpfulMisc.ConvertColor(System.Drawing.Color.FromArgb(HelpfulMisc.RgbaToArgb(int.Parse(((string)_GivenValue).Substring(1), System.Globalization.NumberStyles.HexNumber)))) : default;
            set { if (ShowColor) GivenValue = HelpfulMisc.ConvertColorToHex(HelpfulMisc.ConvertColor(value)); }
        }
        [UIValue(nameof(IsInteger))] private bool IsInteger;
        [UIValue(nameof(MinVal))] private float MinVal;
        [UIValue(nameof(MaxVal))] private float MaxVal;
        [UIValue(nameof(IncrementVal))] private float IncrementVal;
        #endregion
        #region Init
        internal ValueListInfo(object givenValue, char token, string name, bool hasWrapper, Func<object, bool, object> valFormat,
            IEnumerable<(string, object)> extraParams, ValueType valType = ValueType.Inferred)
        {
            HasWrapper = hasWrapper;
            _GivenValue = givenValue;
            GivenToken = token;
            ValueName = name;
            ValFormatter = valFormat;
            //if (valFormat == null) Plugin.Log.Info($"{name} has no formatter!");
            ActualClass = givenValue.GetType();
            if (valType == ValueType.Inferred) switch (ActualClass)
                {
                    case Type v when v == typeof(bool): ValType = ValueType.Toggle; break;
                    case Type v when HelpfulMisc.IsNumber(v): ValType = ValueType.Increment; break;
                    default: ValType = ValueType.Text; break;
                }
            else ValType = valType;
            if (ValType == ValueType.Color) GivenValueColor = HelpfulMisc.TextToColor((string)givenValue);
            if (extraParams != null) foreach ((string, object) newVal in extraParams)
                {
                    if (UIVals.TryGetValue(newVal.Item1, out MemberInfo mi))
                    {
                        if (mi is FieldInfo fi) fi.SetValue(this, newVal.Item2);
                        else if (mi is PropertyInfo pi) pi.SetValue(this, newVal.Item2);
                        else continue;
                    }
                }
            else
            {
                IsInteger = true;
                MinVal = 0;
                MaxVal = 10;
                IncrementVal = 1;
            }
            PropertyChanged += OnPropertyChanged;
        }
        #endregion
        #region UI Functions
        [UIAction(nameof(Formatterer))] 
        private string Formatterer(object input) => $"<align=\"center\">{ValFormatter?.Invoke(input, true) ?? input.ToString()}";
        #endregion
        #region Functions
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs args) => UpdatePreview?.Invoke();
        public override string ToString() => (HasWrapper ?
            $"Raw: () => {_GivenValue} || Formatted: () => {(ValFormatter?.Invoke(GivenValue, false) as Func<object>)?.Invoke() ?? "null"}" :
            $"Raw: {_GivenValue} || Formatted: {ValFormatter?.Invoke(GivenValue, false) ?? "null"}") + 
            " || Val Formatted: " + Formatterer(_GivenValue);
        #endregion
        #region Static Functions
        internal static FormatWrapper GetNewTestVals(IEnumerable<ValueListInfo> arr, bool formatted = true, FormatWrapper oldVals = null)
        {
            if (oldVals is null)
            {
                Dictionary<char, object> outp = new Dictionary<char, object>();
                foreach (ValueListInfo val in arr)
                    outp[val.GivenToken] = formatted ? val.FormattedGivenValue : val.GivenValue;
                return new FormatWrapper(outp);
            }
            foreach (ValueListInfo val in arr)
                oldVals[val.GivenToken] = formatted ? val.FormattedGivenValue : val.GivenValue;
            return oldVals;
        }
        #endregion
        #region Internal Class(es)
        internal enum ValueType
        {
            Inferred, Text, Increment, Toggle, Color
        }
        #endregion
    }
}
