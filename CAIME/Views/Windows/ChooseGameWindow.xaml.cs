using System.Windows;
using CAIME.ViewModels;

namespace CAIME.Windows
{
    public partial class ChooseGameWindow : Window
    {
        private readonly ChooseGameViewModel _viewModel;

        public ChooseGameWindow(ChooseGameViewModel viewModel)
        {
            InitializeComponent();

            _viewModel  = viewModel;
            DataContext = _viewModel;
        }

        /// <summary>The game picked, or null when the window was cancelled.</summary>
        public static GameTemplate? Ask(Window owner, string message, GameTemplate[] games)
        {
            var window = new ChooseGameWindow(new ChooseGameViewModel(message, games)) { Owner = owner };
            return window.ShowDialog() == true ? window._viewModel.SelectedGame : (GameTemplate?)null;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
