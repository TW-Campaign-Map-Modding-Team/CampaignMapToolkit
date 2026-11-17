using System;
using System.Windows;
using CAIME.Rpfm;
using Microsoft.Win32;

namespace CAIME.Windows
{
    /// <summary>
    /// Lets the user view and change the project-specific mod the RPFM database source reads this
    /// campaign's own data from - distinct from the RPFM path, which is a setting in Preferences, not
    /// per-project. The mod is kept by .pack file name only; RPFM finds it, and the mods it depends
    /// on, when the project opens. Persisted into caime_metadata.json beside the project's map.hex via
    /// <see cref="MetadataService"/>.
    /// </summary>
    public partial class PackFileWindow : Window
    {
        private readonly string _projectPath;

        public PackFileWindow(string projectPath)
        {
            InitializeComponent();

            _projectPath = projectPath;
            modNameBox.Text = MetadataService.GetModPackName(projectPath) ?? string.Empty;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Pack file (*.pack)|*.pack",
                Title  = "Select the campaign's .pack file",
            };

            if (dialog.ShowDialog() == true)
            {
                modNameBox.Text = MetadataService.NormalizeModPackName(dialog.FileName);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var modPackName = MetadataService.NormalizeModPackName(modNameBox.Text);
                MetadataService.SetModPackName(_projectPath, modPackName);
                LoggerViewModel.Log(modPackName == null
                    ? "RPFM mod cleared from project metadata."
                    : $"RPFM mod saved to project metadata ({modPackName}).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Failed to save the mod to metadata: {ex.Message}", LogLevel.ErrorMessageBox);
                return;
            }

            Close();
            Owner?.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Owner?.Focus();
        }
    }
}
