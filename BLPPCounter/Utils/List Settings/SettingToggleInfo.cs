using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using System;
using System.Collections.Generic;

namespace BLPPCounter.Utils
{
    internal class SettingToggleInfo
    {
        private bool usable;
        private readonly int id;
        private readonly Action<int, bool> changeSettings;
        [UIValue("Text")]
        public string Text { get; private set; }
        [UIValue("Description")]
        public string Description { get; private set; }
        [UIValue("Type")]
        public string Type { get; private set; }
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

        public SettingToggleInfo(string text, string description, string type, int id, Action<int, bool> changeSettings)
        {
            Text = text; 
            Description = description;
            Type = "<color=\"green\">Type of markup:</color> " + (type.Contains("-") ? type.Split('-')[0] : type);
            this.id = id;
            usable = false;
            this.changeSettings = changeSettings;
        }

        public override string ToString() 
        {
            return $"Text: {Text}, Description: {Description}, Toggle State: {Usable}";
        }
    }
}
