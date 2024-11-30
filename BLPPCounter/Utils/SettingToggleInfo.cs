using BeatSaberMarkupLanguage.Attributes;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using System.Collections.Generic;

namespace BLPPCounter.Utils
{
    internal class SettingToggleInfo
    {
        [UIValue("Text")]
        public string Text { get; private set; }
        [UIValue("Description")]
        public string Description { get; private set; }
        [UIValue("Type")]
        public string Type { get; private set; }
        [UIValue("Usable")]
        public bool Usable 
        { get => usable;
            set
            {
                if (StoredData.Length > id && StoredData[id] != value)
                {
                    StoredData[id] = value;
                    PluginConfig.Instance.SimpleMenuConfig ^= 1 << id;
                }
                usable = value;
            } 
        }
        private static bool[] StoredData => MenuSettingsHandler.Instance.SettingsToSave;
        private bool usable;
        private readonly int id;

        public SettingToggleInfo(string text, string description, string type, int id)
        {
            Text = text; 
            Description = description;
            Type = "<color=\"green\">Type of markup:</color> " + (type.Contains('-') ? type.Split('-')[0] : type);
            this.id = id;
            usable = false;
        }

        public override string ToString() 
        {
            return $"Text: {Text}, Description: {Description}, Toggle State: {Usable}";
        }
    }
}
