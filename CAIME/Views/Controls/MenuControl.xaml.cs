using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CAIME.ViewModels;
using CAIME.Views.Windows;
using CAIME.Windows;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for Menu.xaml
    /// </summary>
    public partial class MenuControl : UserControl
    {
        private MenuViewModel _viewModel;
        private ProjectManager _projectManager;

        public MenuControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        public void SetViewModel(MenuViewModel viewModel, ProjectManager projectManager)
        {
            _viewModel = viewModel;
            _projectManager = projectManager;
            DataContext = viewModel;
        }

        /// <summary>
        /// Create new project click handler
        /// </summary>
        private void Create_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CreateNewMap(Window.GetWindow(this));
        }

        /// <summary>
        /// Load project click handler
        /// </summary>
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Open();
        }

        /// <summary>
        /// Reload project click handler
        /// </summary>
        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Reload();
        }

        /// <summary>
        /// Save project click handler
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Save();
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveAs();
        }

        /// <summary>
        /// Resize current project
        /// </summary>
        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Resize();
        }

        /// <summary>
        /// Rename campaign map
        /// </summary>
        private void RenameMap_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RenameCampaignMap();
        }

        /// <summary>
        /// Export processed pathfinding data (.ppd)
        /// </summary>
        private void ExportPathfinding_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportPathfindingData();
        }

        /// <summary>
        /// Export processed borders data (.pbd)
        /// </summary>
        private void ExportBorders_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportBordersData();
        }

        /// <summary>
        /// Export svg borders (.svg)
        /// </summary>
        private void ExportSVGBorders_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportSVGBorders();
        }

        /// <summary>
        /// Export svg roads (.svg)
        /// </summary>
        private void ExportSVGRoads_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportSVGRoads();
        }

        /// <summary>
        /// Export processed trade routes data (.ptd)
        /// </summary>
        private void ExportTradeRoutes_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportTradeRoutesData();
        }

        /// <summary>
        /// Close current project
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Close();
        }

        /// <summary>
        /// Exit tool click handler
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Exit();
        }

        /// <summary>
        /// Opens <see cref="LoggerWindow"/>
        /// </summary>
        private void LoggerTool_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowLogger();
        }

        private void MapDataEditor_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowMapDataEditor();
        }

        private void ShaderResolutionCorrector_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowShaderResolutionCorrector();
        }

        private void ImportLayerData_Click(object sender, RoutedEventArgs e)
        {
            var project = _viewModel.EditorViewModel.ProjectManager.Project;
            var layers = _viewModel.EditorViewModel.SidebarViewModel.LayersVM.GetLayers();

            var importLayerWindow = new ImportLayerWindow(project, layers)
            {
                Owner = Window.GetWindow(this),
            };

            importLayerWindow.Show();
        }

        /// <summary>
        /// Opens border editor
        /// </summary>
        private void BorderEditor_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowBorderEditor();
        }

        private void ProcessMapData_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ProcessMapData();
        }

        private void ProcessDynResources_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ProcessDynamicResources();
        }

        private void GenerateLookupMinimap_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.GenerateLookupMinimap();
        }

        private void GenerateBaselineTilemap_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.GenerateBaselineTilemap();
        }

        private void ExportLayerToImage_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportLayersToImage();
        }
        
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var win = TextViewerWindow.FromInlines(
                AppWindowAbout.Caption,
                AppWindowAbout.ContentLoader(AppInfo.Version),
                AppWindowAbout.WindowTitle);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void License_Click(object sender, RoutedEventArgs e)
        {
            var win = TextViewerWindow.FromFile(
                AppWindowLicense.Caption,
                AppWindowLicense.FilePath,
                AppWindowLicense.WindowTitle);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void Eula_Click(object sender, RoutedEventArgs e)
        {
            var win = TextViewerWindow.FromFile(
                AppWindowEula.Caption,
                AppWindowEula.FilePath,
                AppWindowEula.WindowTitle);
            win.WithAcceptedLabel();
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void Credits_Click(object sender, RoutedEventArgs e)
        {
            var win = TextViewerWindow.FromFile(
                AppWindowCredits.Caption,
                AppWindowCredits.FilePath,
                AppWindowCredits.WindowTitle);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void Guides_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://tw-campaign-map-modding-team.github.io/CampaignMapToolkit/");
        }

        private void DiscordSupport_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/YN5ndunme6");
        }

        /// <summary>
        /// User preferences click handler
        /// </summary>
        private void Preferences_Click(object sender, RoutedEventArgs e)
        {
            // An open project's game always wins as the default "Base game" - even if it differs from
            // whatever was last browsed to manually in a previous Preferences session.
            if (_projectManager?.Project != null)
            {
                PreferencesViewModel.Instance.SelectedGameIndex = (int)_projectManager.Project.Game;
            }

            var prefWindow = new Windows.PreferencesWindow()
            {
                Owner = Window.GetWindow(this)
            };

            prefWindow.ShowDialog();
        }

        /// <summary>
        /// Opens the RPFM mod window for the currently open project.
        /// </summary>
        private void PackFile_Click(object sender, RoutedEventArgs e)
        {
            if (_projectManager?.Project == null)
            {
                MessageBox.Show("Open a project first to set its RPFM mod.", "No project open");
                return;
            }

            var packWindow = new Windows.PackFileWindow(_projectManager.Project.ProjectPath)
            {
                Owner = Window.GetWindow(this)
            };

            packWindow.ShowDialog();
        }

        private void ValidateGroundTypes_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateGroundTypes();
        }

        private void ValidateClimates_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateClimates();
        }

        private void ValidateAttritions_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateAttritions();
        }

        private void ValidateRegions_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateRegions();
        }

        private void ValidateRoads_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateRoads();
        }

        private void ValidateTownSlots_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateTownSlots();
        }

        private void ValidateRivers_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateRivers();
        }

        private void ValidateBeaches_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateBeaches();
        }

        private void ValidateBridges_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateBridges();
        }

        private void ValidateImpassable_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateImpassable();
        }

        private void ValidateSprawl_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateSprawl();
        }
    }
}
