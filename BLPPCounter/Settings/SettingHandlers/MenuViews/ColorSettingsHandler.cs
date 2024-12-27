using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.List_Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public class ColorSettingsHandler: BSMLResourceViewController
    {
        private static PluginConfig PC => PluginConfig.Instance;
        public static ColorSettingsHandler Instance { get; private set; } = new ColorSettingsHandler();
        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.ColorSettings.bsml";
        private ColorSettingsHandler() { }

        #region Format Color Editor
        private bool loaded = false;
        #region UI Components & UI Objects
        [UIComponent(nameof(ColorEditor))]
        private CustomCellListTableData ColorEditor;
        [UIComponent(nameof(ColorSaveButton))]
        private Button ColorSaveButton;
        #endregion
        #region UI Values
        [UIValue(nameof(ColorValues))]
        private List<object> ColorValues { get; } = new List<object>();
        #endregion
        #region UI Actions & Misc Functions
        [UIAction("#back")] private void GoBack() => MenuSettingsHandler.Instance.GoBack();
        internal void InitColorList()
        {
            if (loaded) return;
            loaded = true;
            ColorListInfo.UpdateSaveButton = () => ColorSaveButton.interactable = ColorValues.Cast<ColorListInfo>().Any(cli => cli.HasChanges);
            ColorValues.Clear();
            ColorValues.AddRange(PC.ColorInfos.Select(pi => new ColorListInfo(pi)));
            ColorEditor.TableView.ReloadData();
            ColorSaveButton.interactable = false;
        }
        [UIAction(nameof(SaveColors))]
        private void SaveColors()
        {
            foreach (ColorListInfo cli in ColorValues.Cast<ColorListInfo>())
                cli.UpdateReferenceValue();
            ColorSaveButton.interactable = false;
        }
        #endregion
        #endregion
    }
}
