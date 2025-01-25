using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using System;
using System.ComponentModel;
using System.Reflection;

namespace BLPPCounter.Utils.List_Settings
{
    internal class ColorListInfo: INotifyPropertyChanged
    {//Intentionally not importing color so that it stays clear which color struct I am using.
#pragma warning disable IDE0044
        private static PluginConfig PC => PluginConfig.Instance;
        internal static Action UpdateSaveButton;

        [UIComponent(nameof(ColorBox))] private ColorSetting ColorBox;
        [UIValue(nameof(ColorValue))] private UnityEngine.Color ColorValue
        {
            get => _ColorValue;
            set { _ColorValue = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorValue))); }
        }
        private UnityEngine.Color _ColorValue;
        [UIValue(nameof(ColorName))] private string ColorName;
        [UIValue(nameof(AlphaValue))] private int AlphaValue
        {
            get => (int)Math.Round(ColorValue.a * 255.0f);
            set 
            { 
                _ColorValue.a = value / 255.0f; ColorBox.CurrentColor = _ColorValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AlphaValue)));
            }
        }
        public bool HasChanges => ColorValue != CurrentRefVal;
        private readonly PropertyInfo pcVarRef;
        private UnityEngine.Color CurrentRefVal;

        public event PropertyChangedEventHandler PropertyChanged;

        internal ColorListInfo(PropertyInfo colorRef)
        {
            pcVarRef = colorRef;
            ColorName = HelpfulMisc.SplitByUppercase(colorRef.Name);
            _ColorValue = HelpfulMisc.ConvertColor((System.Drawing.Color)colorRef.GetValue(PC));
            CurrentRefVal = _ColorValue;
            PropertyChanged += (a, b) => UpdateSaveButton.Invoke(); 
        }

        internal void UpdateReferenceValue()
        {
            if (CurrentRefVal == ColorValue) return;
            pcVarRef.SetValue(PC, HelpfulMisc.ConvertColor(_ColorValue));
            CurrentRefVal = _ColorValue;
        }
    }
}
