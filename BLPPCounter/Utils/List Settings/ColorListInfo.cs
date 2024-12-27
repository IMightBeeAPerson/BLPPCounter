using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using IPA.Config.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            _ColorValue = ConvertColor((System.Drawing.Color)colorRef.GetValue(PC));
            CurrentRefVal = _ColorValue;
            PropertyChanged += (a, b) => UpdateSaveButton.Invoke(); 
        }

        internal void UpdateReferenceValue()
        {
            if (CurrentRefVal == ColorValue) return;
            pcVarRef.SetValue(PC, ConvertColor(_ColorValue));
            CurrentRefVal = _ColorValue;
        }
        private static UnityEngine.Color ConvertColor(System.Drawing.Color color) =>
            new UnityEngine.Color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        private static System.Drawing.Color ConvertColor(UnityEngine.Color color) =>
            System.Drawing.Color.FromArgb((int)Math.Round(color.a * 255), (int)Math.Round(color.r * 255), (int)Math.Round(color.g * 255), (int)Math.Round(color.b * 255));
    }
}
