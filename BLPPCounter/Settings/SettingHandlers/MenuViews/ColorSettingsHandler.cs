using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.List_Settings;
using UnityEngine.UI;
using Zenject;

namespace BLPPCounter.Settings.SettingHandlers.MenuViews
{
    public sealed class ColorSettingsHandler : BSMLResourceViewController
    {
        private readonly List<object> _colorValues = [];

        private PluginConfig _config = null!;
        private ColorListInfo.Factory _colorFactory = null!;
        private MenuSettingsHandler _menuSettingsHandler = null!;

        private bool _loaded;

        public override string ResourceName => "BLPPCounter.Settings.BSML.MenuComponents.ColorSettings.bsml";

        [UIComponent(nameof(ColorEditor))]
        private CustomCellListTableData ColorEditor = null!;

        [UIComponent(nameof(ColorSaveButton))]
        private Button ColorSaveButton = null!;

        [UIValue(nameof(ColorValues))]
        private List<object> ColorValues => _colorValues;

        [Inject]
        private void Construct(
            PluginConfig config,
            ColorListInfo.Factory colorFactory,
            MenuSettingsHandler menuSettingsHandler
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _colorFactory = colorFactory ?? throw new ArgumentNullException(nameof(colorFactory));

            _menuSettingsHandler = menuSettingsHandler ?? throw new ArgumentNullException(nameof(menuSettingsHandler));
        }

        [UIAction("#back")]
        private void GoBack()
        {
            ResetUnsavedColors();
            _menuSettingsHandler.GoBack();
        }

        internal void InitColorList()
        {
            if (_loaded)
            {
                UpdateSaveButton();
                return;
            }

            _loaded = true;
            _colorValues.Clear();

            _colorValues.AddRange(
                _config.ColorInfos.Select(property => (object)_colorFactory.Create(property, UpdateSaveButton))
            );

            ReloadColorTable();
            UpdateSaveButton();
        }

        [UIAction(nameof(SaveColors))]
        private void SaveColors()
        {
            foreach (ColorListInfo color in _colorValues.OfType<ColorListInfo>())
            {
                color.Save();
            }

            UpdateSaveButton();
        }

        private void ResetUnsavedColors()
        {
            foreach (ColorListInfo color in _colorValues.OfType<ColorListInfo>())
            {
                color.Reset();
            }

            if (_loaded)
                ReloadColorTable();

            UpdateSaveButton();
        }

        private void UpdateSaveButton()
        {
            if (ColorSaveButton is null)
                return;

            ColorSaveButton.interactable = _colorValues.OfType<ColorListInfo>().Any(color => color.HasChanges);
        }

        private void ReloadColorTable()
        {
            if (ColorEditor is null)
                return;

#if NEW_VERSION
            ColorEditor.Data = _colorValues;
            ColorEditor.TableView.ReloadData();
#else
            ColorEditor.data = _colorValues.Count > 0 ? _colorValues : [string.Empty];

            ColorEditor.tableView.ReloadData();
#endif
        }
    }
}
