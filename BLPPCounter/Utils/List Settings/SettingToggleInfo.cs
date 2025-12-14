using BeatSaberMarkupLanguage.Attributes;
using System;

namespace BLPPCounter.Utils
{
    internal class SettingToggleInfo(string text, string description, string type, int id, Action<int, bool> changeSettings)
    {
        private bool usable = false;
        private readonly int id = id;
        private readonly Action<int, bool> changeSettings = changeSettings;
        [UIValue("Text")]
        public string Text { get; private set; } = text;
        [UIValue("Description")]
        public string Description { get; private set; } = description;
        [UIValue("Type")]
        public string Type { get; private set; } = "<color=\"green\">Type of markup:</color> " + (type.Contains("-") ? type.Split('-')[0] : type);
        [UIValue("Usable")]
        public bool Usable 
        { 
            get => usable;
            set
            {
                changeSettings(id, value);
                usable = value;
            } 
        }

        public override string ToString() 
        {
            return $"Text: {Text}, Description: {Description}, Toggle State: {Usable}";
        }
    }
}
