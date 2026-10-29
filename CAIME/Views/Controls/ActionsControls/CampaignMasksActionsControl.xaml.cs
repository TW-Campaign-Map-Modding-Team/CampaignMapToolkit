using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for CampaignMasksActionsControl.xaml
    /// </summary>
    public partial class CampaignMasksActionsControl : UserControl
    {
        public CampaignMasksActionsControlViewModel ViewModel { get; private set; }

        public CampaignMasksActionsControl()
        {
            InitializeComponent();

            ViewModel   = new CampaignMasksActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.CampaignMasks, -1);
        }

        private void AddMask_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowAddMaskWindow();
        }
    }
}
