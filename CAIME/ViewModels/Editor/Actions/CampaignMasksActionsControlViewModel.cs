using System;
using System.Collections.ObjectModel;
using System.Windows;
using CAIME.Windows;

namespace CAIME.Controls
{
    /// <summary>
    /// Chooses which campaign mask the campaign masks layer shows and paints, and adds new masks.
    /// </summary>
    public class CampaignMasksActionsControlViewModel : ActionsControlViewModel
    {
        private const string UNNAMED_MASK_DISPLAY_NAME = "(no mask)";

        private CampaignMaskLayer _layer;

        public ObservableCollection<string> MaskNames { get; } = new ObservableCollection<string>();

        private int selectedMaskIndex = -1;
        public int SelectedMaskIndex
        {
            get
            {
                return selectedMaskIndex;
            }
            set
            {
                if (selectedMaskIndex == value)
                {
                    return;
                }

                selectedMaskIndex = value;
                OnPropertyChanged(nameof(SelectedMaskIndex));

                ShowSelectedMask();
                OnMaskShown?.Invoke(this, EventArgs.Empty);
            }
        }

        private string newMaskName;
        public string NewMaskName
        {
            get
            {
                return newMaskName;
            }
            set
            {
                newMaskName = value;
                OnPropertyChanged(nameof(NewMaskName));
            }
        }

        public bool IsShownMaskReadOnly => _layer != null && _layer.IsPaintable == false;

        /// <summary>Raised when the layer starts showing another mask, so it can be recoloured.</summary>
        public event EventHandler OnMaskShown;

        /// <summary>
        /// Shows the opened map's first mask on its masks layer, or clears the list for a game without
        /// one. <see cref="OnMaskShown"/> is not raised: the layers are coloured once the map loads.
        /// </summary>
        public void Attach(CampaignMaskLayer layer)
        {
            _layer = layer;

            MaskNames.Clear();
            if (_layer != null)
            {
                foreach (var mask in _project.MapHexFile.CampaignMasks)
                {
                    MaskNames.Add(DisplayName(mask));
                }
            }

            selectedMaskIndex = MaskNames.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(SelectedMaskIndex));

            ShowSelectedMask();
        }

        public void ShowAddMaskWindow()
        {
            NewMaskName = string.Empty;

            var addMaskWindow = new AddCampaignMaskWindow(this)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            addMaskWindow.ShowDialog();
        }

        /// <summary>Adds an empty mask named <see cref="NewMaskName"/> and shows it.</summary>
        public bool AddMask()
        {
            var mask = _project.MapHexEditor.CreateCampaignMask(NewMaskName?.Trim());
            if (mask == null)
            {
                return false;
            }

            MaskNames.Add(DisplayName(mask));
            SelectedMaskIndex = MaskNames.Count - 1;

            LoggerViewModel.Log($"Added campaign mask {mask.Name}.", LogLevel.Info);
            return true;
        }

        private void ShowSelectedMask()
        {
            if (_layer != null)
            {
                var masks = _project.MapHexFile.CampaignMasks;
                var mask  = selectedMaskIndex >= 0 && selectedMaskIndex < masks.Count ? masks[selectedMaskIndex] : null;

                _layer.ShowMask(_project.MapHexFile, mask);
            }

            OnPropertyChanged(nameof(IsShownMaskReadOnly));
        }

        private static string DisplayName(CampaignMask mask)
        {
            return mask.Name.Length == 0 ? UNNAMED_MASK_DISPLAY_NAME : mask.Name;
        }
    }
}
