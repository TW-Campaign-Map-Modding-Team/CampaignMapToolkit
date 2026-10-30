using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private async void BrowseRpfm_Click(object sender, RoutedEventArgs e)
        {
            string folder;
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                folder = dialog.FileName;
            }

            // Validate immediately so an invalid installation is never assigned to the setting.
            // Asking RPFM's server takes seconds, which the window waits out without freezing.
            string error = null;
            bool valid;

            IsEnabled               = false;
            Mouse.OverrideCursor    = Cursors.Wait;
            try
            {
                valid = await Task.Run(() => viewModel.ValidateRpfmPath(folder, out error));
            }
            finally
            {
                Mouse.OverrideCursor    = null;
                IsEnabled               = true;
            }

            if (valid)
            {
                viewModel.RpfmPath = folder;
            }
            else
            {
                MessageBox.Show(
                    $"The selected folder is not a valid RPFM installation.\n\n{error}\n\n" +
                    "Pick an RPFM 5.3 or later folder, or switch the database source back to Assembly Kit.",
                    "Invalid RPFM path");
            }
        }
    }
}
