using System;
using System.Reflection;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Safety;
using UnityEngine;
using Zenject;

namespace BLPPCounter.Utils.List_Settings
{
    internal sealed class ColorListInfo : SafeNotifyPropertyChanged
    {
        private readonly PluginConfig _config;
        private readonly PropertyInfo _property;
        private readonly Action _changed;

        private Color _colorValue;
        private Color _savedValue;

        internal ColorListInfo(PluginConfig config, PropertyInfo property, Action changed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _property = property ?? throw new ArgumentNullException(nameof(property));

            _changed = changed ?? throw new ArgumentNullException(nameof(changed));

            if (_property.PropertyType != typeof(System.Drawing.Color))
            {
                throw new ArgumentException(
                    $"{_property.Name} is not a " + $"{nameof(System.Drawing.Color)} property.",
                    nameof(property)
                );
            }

            if (!_property.CanRead || !_property.CanWrite)
            {
                throw new ArgumentException($"{_property.Name} must be readable and writable.", nameof(property));
            }

            System.Drawing.Color configuredColor =
                (System.Drawing.Color?)_property.GetValue(_config)
                ?? throw new InvalidOperationException($"{_property.Name} returned null.");

            _colorValue = HelpfulMisc.ConvertColor(configuredColor);

            _savedValue = _colorValue;
        }

        [UIComponent(nameof(ColorBox))]
        private ColorSetting ColorBox = null!;

        [UIValue(nameof(ColorName))]
        private string ColorName => HelpfulMisc.SplitByUppercase(_property.Name);

        [UIValue(nameof(ColorValue))]
        private Color ColorValue
        {
            get => _colorValue;
            set
            {
                if (_colorValue == value)
                    return;

                _colorValue = value;

                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(AlphaValue));

                _changed();
            }
        }

        [UIValue(nameof(AlphaValue))]
        private int AlphaValue
        {
            get => Mathf.RoundToInt(_colorValue.a * 255f);
            set
            {
                float alpha = Mathf.Clamp(value, 0, 255) / 255f;

                if (Mathf.Approximately(_colorValue.a, alpha))
                {
                    return;
                }

                _colorValue.a = alpha;

                ColorBox?.CurrentColor = _colorValue;

                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(ColorValue));

                _changed();
            }
        }

        internal bool HasChanges => _colorValue != _savedValue;

        internal void Save()
        {
            if (!HasChanges)
                return;

            _property.SetValue(_config, HelpfulMisc.ConvertColor(_colorValue));

            _savedValue = _colorValue;
            _changed();
        }

        internal void Reset()
        {
            if (!HasChanges)
                return;

            _colorValue = _savedValue;

            ColorBox?.CurrentColor = _colorValue;

            NotifyPropertyChanged(nameof(ColorValue));

            NotifyPropertyChanged(nameof(AlphaValue));

            _changed();
        }

        internal sealed class Factory : PlaceholderFactory<PropertyInfo, Action, ColorListInfo> { }
    }
}
