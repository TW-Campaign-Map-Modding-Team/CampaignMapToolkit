using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CAIME.Validators;
using CAIME.Views.Windows;

namespace CAIME.ViewModels
{
    public class MenuViewModel : BaseViewModel, IHotkeyHandler
    {
        private static readonly Brush SavedBrush   = new SolidColorBrush(Color.FromRgb(0x90, 0xEE, 0x90));   // light green
        private static readonly Brush UnsavedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB2, 0x66));   // light orange

        private readonly ProjectManager _projectManager;
        private readonly DispatcherTimer _saveStateTimer;
        public EditorViewModel EditorViewModel;

        public ICommand MenuUndoCommand { get; private set; }
        public ICommand MenuRedoCommand { get; private set; }

        private string saveStateText;
        public string SaveStateText
        {
            get => saveStateText;
            private set => SetValue(ref saveStateText, value);
        }

        private Brush saveStateBrush;
        public Brush SaveStateBrush
        {
            get => saveStateBrush;
            private set => SetValue(ref saveStateBrush, value);
        }

        public MenuViewModel(ProjectManager projectManager, EditorViewModel editorViewModel)
        {
            _projectManager = projectManager;
            EditorViewModel = editorViewModel;

            MenuUndoCommand = new RelayCommand(ExecuteUndo, null);
            MenuRedoCommand = new RelayCommand(ExecuteRedo, null);

            HotkeyManager.Subscribe(this);

            _saveStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _saveStateTimer.Tick += (sender, e) => RefreshSaveState();

            // Only poll while a project is open - with none there is nothing to refresh.
            _projectManager.OnOpenProject += (sender, e) =>
            {
                RefreshSaveState();
                _saveStateTimer.Start();
            };
            _projectManager.OnCloseProject += (sender, e) =>
            {
                _saveStateTimer.Stop();
                RefreshSaveState();
            };

            RefreshSaveState();
        }

        private void RefreshSaveState()
        {
            if (_projectManager.IsProjectOpen == false)
            {
                SaveStateText = string.Empty;
                return;
            }

            var hasUnsavedChanges = _projectManager.Project.HasUnsavedChanges;
            SaveStateText   = hasUnsavedChanges ? "Modified" : "Saved";
            SaveStateBrush  = hasUnsavedChanges ? UnsavedBrush : SavedBrush;
        }

        public void Close()
        {
            if (EditorViewModel.CanCloseProject())
            {
                _projectManager.CloseProject();
            }
        }

        public ProcessHotkeyResult ProcessHotkeyCombination(Key[] keys)
        {
            var isCtrlPressed = keys.Length > 0 && (keys[0] == Key.LeftCtrl || keys[0] == Key.RightCtrl);

            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.L)
            {
                ShowLogger();
                return ProcessHotkeyResult.Handled;
            }

            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.B
                && AppStateContext.Instance.IsExportBordersEnabled)
            {
                ShowBorderEditor();
                return ProcessHotkeyResult.Handled;
            }

            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.M)
            {
                ShowMapDataEditor();
                return ProcessHotkeyResult.Handled;
            }

            return ProcessHotkeyResult.Passed;
        }

        public void ShowLogger()
        {
            var loggerWindow = new Windows.LoggerWindow()
            {
                Owner = Application.Current.MainWindow
            };
            loggerWindow.Show();
        }

        public void ShowBorderEditor()
        {
            var borderEditorWindow = new BorderEditorWindow(_projectManager.Project)
            {
                Owner = Application.Current.MainWindow
            };
            borderEditorWindow.Show();
        }

        public void ShowMapDataEditor()
        {
            var mapDataEditorWin = new Windows.MapDataEditor(_projectManager.Project)
            {
                Owner = Application.Current.MainWindow
            };
            mapDataEditorWin.Show();
        }

        public void ShowShaderResolutionCorrector()
        {
            var shaderResolutionCorrectorWindow = new ShaderResolutionCorrectorWindow(_projectManager.Project)
            {
                Owner = Application.Current.MainWindow
            };
            shaderResolutionCorrectorWindow.Show();
        }

        public void CreateNewMap(Window owner)
        {
            // Initialise new project window, it's viewModel and assign viewModel to window
            var newProjWindow = new Windows.NewProjectWindow(_projectManager)
            {
                Owner = owner,
            };

            newProjWindow.ShowDialog();
        }

        public void Save()
        {
            EditorViewModel.SaveProject();
        }

        public void SaveAs()
        {
            EditorViewModel.SaveProjectAs();
        }

        public void Open()
        {
            EditorViewModel.OpenProject();
        }

        public void ImportMapData()
        {
            EditorViewModel.ImportMapData();
        }

        public void Reload()
        {
            EditorViewModel.ReloadProject();
        }

        public void Exit()
        {
            EditorViewModel.ExitApplication();
        }

        public void Resize()
        {
            EditorViewModel.ResizeMap();
        }

        public void RenameCampaignMap()
        {
            EditorViewModel.RenameCampaignMap();
        }

        public void ExportPathfindingData()
        {
            _projectManager.ExportProcessedPathfindingData();
        }

        public void ExportBordersData()
        {
            var exportDialog = new BordersExportOptionsWindow(_projectManager);
            exportDialog.Show();
        }

        public void ExportSVGBorders()
        {
            _projectManager.ExportSVGBorders();
        }

        public void ExportSVGRoads()
        {
            _projectManager.ExportSVGRoads();
        }

        public void ExportTradeRoutesData()
        {
            _projectManager.ExportProcessedTradeRoutesData();
        }

        public void ProcessMapData()
        {
            _projectManager.ProcessMapDataEsf();
        }

        public void ProcessDynamicResources()
        {
            _projectManager.ProcessDynamicResourcesEsf();
        }

        public void GenerateLookupMinimap()
        {
            _projectManager.GenerateLookupImage();
        }

        public void GenerateBaselineTilemap()
        {
            _projectManager.GenerateBaselineTilemap();
        }

        public void ExportLayersToImage()
        {
            var window = new ExportLayersWindow();
            window.Owner = Application.Current.MainWindow;
            window.ProjectManager = _projectManager;
            window.Show();
        }

        public void ValidateGroundTypes()
        {
            var isSuccess = GroundTypesValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Ground Types layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Ground Types layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateClimates()
        {
            var isSuccess = ClimatesValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Climates layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Climates layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateRegions()
        {
            var isSuccess = RegionsValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Regions layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Regions layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateRoads()
        {
            var isSuccess = RoadsValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Roads layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Roads layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateTownSlots()
        {
            var isSuccess = TownSlotsValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Town Slots layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Town Slots layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateRivers()
        {
            var isSuccess = RiversValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Rivers layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Rivers layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateBeaches()
        {
            var isSuccess = BeachesValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Beaches layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Beaches layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateBridges()
        {
            var isSuccess = BridgesValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Bridges layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Bridges layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateAttritions()
        {
            var isSuccess = AttritionsValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Attritions layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Attritions layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateImpassable()
        {
            var isSuccess = ImpassableValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Impassable layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Impassable layer failed. See output logs for details.", "Validation completed");
            }
        }

        public void ValidateSprawl()
        {
            var isSuccess = SprawlValidator.Validate(_projectManager.Project);
            if (isSuccess)
            {
                MessageBox.Show("No issues have been found during Town Sprawl layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show("Validating Town Sprawl layer failed. See output logs for details.", "Validation completed");
            }
        }

        private void ExecuteUndo(object _)
        {
            EditorViewModel.UndoRedoManager.ExecuteUndoAction();
        }

        private void ExecuteRedo(object _)
        {
            EditorViewModel.UndoRedoManager.ExecuteRedoAction();
        }
    }
}
