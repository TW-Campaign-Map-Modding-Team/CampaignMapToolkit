using System;
using System.Windows;
using CAIME.Controls;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for AddCampaignMaskWindow.xaml
    /// </summary>
    public partial class AddCampaignMaskWindow : Window
    {
        private readonly CampaignMasksActionsControlViewModel _viewModel;

        public AddCampaignMaskWindow(CampaignMasksActionsControlViewModel viewModel)
        {
            InitializeComponent();

            _viewModel  = viewModel;
            DataContext = viewModel;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.AddMask())
            {
                this.Close();
            }
            else
            {
                MessageBox.Show(this, "A new mask needs a name, in ASCII characters, that no other mask has.", "Add campaign mask");
            }
        }
    }
}
