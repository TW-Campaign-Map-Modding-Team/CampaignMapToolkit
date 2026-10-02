using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaktionslogik für PreferencesWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : Window
    {
        private readonly PreferencesViewModel viewModel;

        public PreferencesWindow()
        {
            InitializeComponent();
            viewModel = PreferencesViewModel.Instance;
            DataContext = viewModel;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            bool success = viewModel.OnApply();
            if (success)
            {
                Close();
                Owner.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.OnCancel();
            Close();
            Owner.Focus();
        }

        //private void AutoDetect_Click(object sender, RoutedEventArgs e)
        //{
        //    viewModel.OnAutoDetect();
        //}

        private void BrowseAssKit_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    viewModel.AssKitPath = dialog.FileName;
                };
            }
        }

        private void BaseGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewModel.AssKitPath = viewModel.GetAssKitPath((GameTemplate)viewModel.SelectedGameIndex);
        }

        private void BrowseRpfm_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                // Validate immediately so an invalid installation is never assigned to the setting.
                if (CAIME.Rpfm.RpfmService.ValidateInstallation(dialog.FileName, out var error))
                {
                    viewModel.RpfmPath = dialog.FileName;
                }
                else
                {
                    MessageBox.Show(
                        $"The selected folder is not a valid RPFM installation.\n\n{error}",
                        "Invalid RPFM path");
                }
            }
        }
    }
}
