using BeatSaberMarkupLanguage.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BLPPCounter.Utils
{
    public class SimpleMenuInfo
    {
        #region General Vars
        [UIValue(nameof(Text))]
        public string Text { get; private set; } = "";
        [UIValue(nameof(HoverHint))]
        public string HoverHint { get; private set; } = "";
        [UIValue(nameof(Align))]
        public string Align { get; private set; } = "Left";
        [UIValue(nameof(Id))]
        public string Id { get; private set; } = "";
        #endregion
        #region Increment Vars
        [UIValue(nameof(IntegerOnly))]
        public bool IntegerOnly { get; private set; } = true;
        [UIValue(nameof(MinAmount))]
        public float MinAmount { get; private set; } = 0;
        [UIValue(nameof(MaxAmount))]
        public float MaxAmount { get; private set; } = 100;
        [UIValue(nameof(Increment))] 
        public float Increment { get; private set; } = 1;
        #endregion
        #region Button Vars
        private Action OnClickAction;
        [UIAction(nameof(OnClick))]
        private void OnClick() => OnClickAction?.Invoke();
        #endregion
        #region List Vars
        private PropertyInfo OptionVal;
        [UIValue(nameof(Options))]
        public List<object> Options => OptionVal?.GetValue(SettingObj) as List<object> ?? new List<object>();
        #endregion
        #region Show Events
        [UIValue(nameof(ShowEvent0))] public bool ShowEvent0 { get; private set; } = false;
        [UIValue(nameof(ShowEvent1))] public bool ShowEvent1 { get; private set; } = false;
        [UIValue(nameof(ShowEvent2))] public bool ShowEvent2 { get; private set; } = false;
        [UIValue(nameof(ShowEvent3))] public bool ShowEvent3 { get; private set; } = false;
        [UIValue(nameof(ShowEvent4))] public bool ShowEvent4 { get; private set; } = false;
        [UIValue(nameof(ShowEvent5))] public bool ShowEvent5 { get; private set; } = false;
        #endregion
        #region Setting Vals
        private readonly PropertyInfo SettingRef;
        private readonly object SettingObj;
        [UIValue(nameof(SettingValBool))]
        public bool SettingValBool
        {
            get { if (SettingRef?.GetValue(SettingObj) is bool outp) return outp; return default; }
            set { if (SettingRef != null && SettingRef.PropertyType == typeof(bool)) SettingRef.SetValue(SettingObj, value); }
        }
        [UIValue(nameof(SettingValInt))]
        public int SettingValInt
        {
            get { if (SettingRef?.GetValue(SettingObj) is int outp) return outp; return default; }
            set { if (SettingRef != null && SettingRef.PropertyType == typeof(int)) SettingRef.SetValue(SettingObj, value); }
        }
        [UIValue(nameof(SettingValString))]
        public string SettingValString
        {
            get { if (SettingRef?.GetValue(SettingObj) is string outp) return outp; return default; }
            set { if (SettingRef != null && SettingRef.PropertyType == typeof(string)) SettingRef.SetValue(SettingObj, value); }
        }
        #endregion
        public static SimpleMenuInfo InitUsingSettingsType(string settingType, string valueName, object valueContainer) => 
            valueContainer != null ? new SimpleMenuInfo(settingType, GetVarFromName(valueName, valueContainer), valueContainer) : new SimpleMenuInfo(settingType);
        private static PropertyInfo GetVarFromName(string name, object container) => container.GetType().GetProperties().First(a => a.Name.Equals(name));

        public SimpleMenuInfo(string settingType) => HandleShowEvent(settingType); 
        public SimpleMenuInfo(string settingType, object settingObj) : this(settingType) => SettingObj = settingObj;
        public SimpleMenuInfo(string settingType, PropertyInfo settingRef, object settingObj)
        {
            HandleShowEvent(settingType);
            SettingRef = settingRef;
            SettingObj = settingObj;
        }
        private void HandleShowEvent(string settingType)
        {
            switch (settingType.ToLower())
            {
                case "checkbox-setting": ShowEvent0 = true; break;
                case "text": ShowEvent1 = true; break;
                case "increment-setting": ShowEvent2 = true; break;
                case "button": ShowEvent3 = true; break;
                case "list-setting": ShowEvent4 = true; break;
                case "dropdown-list-setting": ShowEvent5 = true; break;
            }
        }
        public void SetAttribute(UsableAttributes type, string val)
        {
            switch (type)
            {
                case UsableAttributes.text: Text = val; break;
                case UsableAttributes.hover_hint: HoverHint = val; break;
                case UsableAttributes.align: Align = val; break;
                case UsableAttributes.id: Id = val; break;
                case UsableAttributes.min: MinAmount = float.Parse(val); break;
                case UsableAttributes.max: MaxAmount = float.Parse(val); break;
                case UsableAttributes.increment: Increment = float.Parse(val); break;
                case UsableAttributes.integer_only: IntegerOnly = bool.Parse(val); break;
                case UsableAttributes.on_click: var hold = GetUIAction(val); OnClickAction = () => hold.Invoke(SettingObj, null); break;
                case UsableAttributes.options: OptionVal = SettingObj?.GetType().GetProperties().First(p => p.Name.Equals(val)); break;
            }
        }
        private MethodInfo GetUIAction(string name) => SettingObj?.GetType().GetMethods().First(m => m.Name.Equals(name));
        public static bool UsableSetting(string settingType) => 
            new List<string>() { "checkbox-setting", "text", "increment-setting", "button", "list-setting", "dropdown-list-setting" }.Any(a => a.Equals(settingType));
        public override string ToString()
        {
            return $"Text: {Text}, hover-hint: {HoverHint}";
        }
        public enum UsableAttributes
        {
            text, hover_hint, align, id, min, max, increment, integer_only, on_click, options
        }
    }
}
