using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace BLPPCounter.Utils.List_Settings
{
    internal class ValueListInfo : INotifyPropertyChanged
    {
#pragma warning disable IDE0044
        #region Variables
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly Type ActualClass;
        private IncrementInfo incrementInfo = default;
        private Func<object, bool, string> ValFormatter;
        private object _GivenValue;
        private char GivenToken;
        private object GivenValue {
            get => HasWrapper ? new Func<object>(() => _GivenValue) : _GivenValue;
            set { _GivenValue = value; PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(GivenValue))); }
        }
        private object FormattedGivenValue { get { 
                object outp = ValFormatter?.Invoke(_GivenValue, false) ?? _GivenValue;
                return HasWrapper ? new Func<object>(() => outp) : outp; 
            } }
        public bool HasWrapper;
        #endregion
        #region UI Variables
        private static readonly Dictionary<string, MemberInfo> UIVals = 
            HelpfulMisc.GetAllVariablesUsingAttribute(typeof(ValueListInfo), typeof(UIValue), BindingFlags.Instance | BindingFlags.NonPublic).ToDictionary(mi => mi.Name);
        
        [UIValue(nameof(ShowToggle))] private bool ShowToggle = false;
        [UIValue(nameof(ShowTextBox))] private bool ShowTextBox = false;
        [UIValue(nameof(ShowIncrement))] private bool ShowIncrement = false;

        [UIValue(nameof(ValueName))] private string ValueName;
        [UIValue(nameof(GivenValueBool))] private bool GivenValueBool
        {
            get => GivenValue is bool outp ? outp : default;
            set { if (ActualClass == typeof(bool)) GivenValue = value; }
        }
        [UIValue(nameof(GivenValueString))] private string GivenValueString
        {
            get => GivenValue is string outp ? outp : GivenValue.ToString();
            set { if (ActualClass == typeof(string)) GivenValue = value; }
        }
        [UIValue(nameof(GivenValueNumber))] private float GivenValueNumber
        {
            get => GivenValue is float outp ? outp : default;
            set { if (HelpfulMisc.IsNumber(ActualClass)) GivenValue = value; }
        }


        [UIValue(nameof(IsInteger))] private bool IsInteger { get => incrementInfo.IsInteger; set => incrementInfo.IsInteger = value; }
        [UIValue(nameof(MinVal))] private float MinVal { get => incrementInfo.MinVal; set => incrementInfo.MinVal = value; }
        [UIValue(nameof(MaxVal))] private float MaxVal { get => incrementInfo.MaxVal; set => incrementInfo.MaxVal = value; }
        [UIValue(nameof(IncrementVal))] private float IncrementVal { get => incrementInfo.IncrementVal; set => incrementInfo.IncrementVal = value; }
        #endregion
        #region UI Components
        [UIComponent(nameof(TextBox))] private StringSetting TextBox;
        [UIComponent(nameof(Increment))] private IncrementSetting Increment;

        #endregion
        #region Init
        internal ValueListInfo(object givenValue, char token, string name, bool hasWrapper = false, Func<object, bool, string> valFormat = null,
            IEnumerable<(string, object)> extraParams = null)
        {
            HasWrapper = hasWrapper;
            _GivenValue = givenValue;
            GivenToken = token;
            ValueName = name;
            ValFormatter = valFormat;
            ActualClass = givenValue.GetType();
            Plugin.Log.Info(ActualClass.ToString());
            switch (ActualClass)
            {
                case Type v when v == typeof(bool): ShowToggle = true; break;
                case Type v when HelpfulMisc.IsNumber(v): ShowIncrement = true; break;
                default: ShowTextBox = true; break;
            }
            if (extraParams != null) foreach ((string, object) newVal in extraParams)
                {
                    if (UIVals.TryGetValue(newVal.Item1, out MemberInfo mi))
                    {
                        if (mi is FieldInfo fi) fi.SetValue(this, newVal.Item2);
                        else if (mi is PropertyInfo pi) pi.SetValue(this, newVal.Item2);
                        else continue;
                    }
                }
            if (incrementInfo.Equals(default))
                incrementInfo = new IncrementInfo(true, 0, 10, 1);
            PropertyChanged += OnPropertyChanged;
        }
        #endregion
        #region UI Functions
        [UIAction(nameof(Formatterer))] 
        private string Formatterer(object input) => $"<align=\"center\">{ValFormatter?.Invoke(input, true) ?? input.ToString()}";
        #endregion
        #region Functions
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {

        }
        #endregion
        #region Static Functions
        internal static Dictionary<char, object> GetNewTestVals(IEnumerable<ValueListInfo> arr, Dictionary<char, object> oldVals = null)
        {
            Dictionary<char, object> outp = oldVals ?? new Dictionary<char, object>();
            foreach (ValueListInfo val in arr)
                outp[val.GivenToken] = val.FormattedGivenValue;
            return outp;
        }
        #endregion
        #region Inner Classes & Structs
        private struct IncrementInfo
        {
            public bool IsInteger;
            public float MinVal;
            public float MaxVal;
            public float IncrementVal;
            
            public IncrementInfo(bool isInteger, float minVal, float maxVal, float incrementVal)
            {
                IsInteger = isInteger;
                MinVal = minVal;
                MaxVal = maxVal;
                IncrementVal = incrementVal;
            }
        }
        #endregion
    }
}
