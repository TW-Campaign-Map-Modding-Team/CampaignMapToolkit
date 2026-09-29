using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CAIME.Controls;
using CAIME.Painters;
using CAIME.Tools;
using CAIME.ViewModels;
using CAIME.Views.Controls.ActionsControls;
using CAIME.Views.Windows;
using CAIME.Windows;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;

namespace CAIME
{
    public class EditorViewModel : BaseViewModel, IDisposable, IHotkeyHandler
    {
        private bool isInit;

        private string selectedFloodFillSource;
        public string SelectedFloodFillSource
        {
            get
            {
                return selectedFloodFillSource;
            }
            set
            {
                selectedFloodFillSource = value;
                OnPropertyChanged(nameof(SelectedFloodFillSource));
            }
        }

        private int brushSize;
        public int BrushSize
        {
            get
            {
                return brushSize;
            }
            set
            {
                brushSize = value;

                if (ToolbarViewModel != null)
                {
                    ToolbarViewModel.ChangeBrushProperties(BrushSize);
                }

                OnPropertyChanged(nameof(BrushSize));
            }
        }

        private readonly PreferencesViewModel   _preferencesViewModel;
        private readonly AutoSavesManager       _autoSavesManager;
        private readonly AutoBackupManager      _autoBackupManager;

        public SidebarViewModel                 SidebarViewModel;
        public ToolbarViewModel                 ToolbarViewModel;
        public ViewportViewModel                ViewportViewModel;

        private float                           cachedHexSpacing;

        public UndoRedoManager                  UndoRedoManager { get; private set; }
        public ProjectManager                   ProjectManager  { get; private set; }

        private HexLayout                       hexLayout;
        private Project                         project;

        private ActionsControl                  actionsControl;
        private BorderActionsControl            borderActionsControl;
        private SprawlActionsControl            sprawlActionsControl;
        private GroundTypeActionsControl        groundTypeActionsControl;
        private ImpassableActionsControl        impassableActionsControl;
        private TradeRoutesActionControl        tradeRoutesActionControl;
        private RoadsActionsControl             roadsActionsControl;
        private RiversActionsControl            riversActionsControl;
        private BridgesActionsControl           bridgesActionsControl;
        private BeachesActionsControl           beachesActionsControl;
        private TownSlotsActionsControl         townSlotsActionsControl;

        private int[]                           gridColours;
        private bool                            isLayersStackRedrawScheduled;
        private LayerOpacityPreview             opacityPreview;
        private ViewportViewModel.CellBatch     opacityPreviewCells;
        private byte                            opacityBeforePreview;
        private bool                            isOpacityPreviewDrawn;
        private System.Timers.Timer             updateMinimapTimer;

        public ObservableCollection<string>     FloodFillLayersSource { get; private set; }

        public EditorViewModel(PreferencesViewModel preferencesViewModel)
        {
            isInit                                                          = false;
            BrushSize                                                       = 1;
            _preferencesViewModel                                           = preferencesViewModel;
            cachedHexSpacing                                                = preferencesViewModel.HexSpacing;

            UndoRedoManager                                                 = new UndoRedoManager();
            ProjectManager                                                  = new ProjectManager();

            actionsControl                                                  = new ActionsControl();
            borderActionsControl                                            = new BorderActionsControl();
            sprawlActionsControl                                            = new SprawlActionsControl();
            groundTypeActionsControl                                        = new GroundTypeActionsControl();
            impassableActionsControl                                        = new ImpassableActionsControl();
            tradeRoutesActionControl                                        = new TradeRoutesActionControl();
            roadsActionsControl                                             = new RoadsActionsControl();
            riversActionsControl                                            = new RiversActionsControl();
            bridgesActionsControl                                           = new BridgesActionsControl();
            beachesActionsControl                                           = new BeachesActionsControl();
            townSlotsActionsControl                                         = new TownSlotsActionsControl();

            FloodFillLayersSource                                           = new ObservableCollection<string>();

            _autoSavesManager                                               = new AutoSavesManager(ProjectManager, _preferencesViewModel);   
            _autoBackupManager                                              = new AutoBackupManager(ProjectManager, _preferencesViewModel);   

            AppDomain.CurrentDomain.UnhandledException                      += LogUnhandledException;
            Application.Current.DispatcherUnhandledException                += LogUnhandledException;

            borderActionsControl.ViewModel.OnUpdateBorderColour             += OnUpdateBorderColour;
            borderActionsControl.ViewModel.OnBorderColoursUpdated           += UpdateColours;

            sprawlActionsControl.ViewModel.OnUpdateSprawlColour             += OnUpdateSprawlColour;
            sprawlActionsControl.ViewModel.OnSprawlColoursUpdated           += UpdateColours;

            groundTypeActionsControl.ViewModel.OnUpdateGroundTypeColour     += OnUpdateGroundTypeColour;
            groundTypeActionsControl.ViewModel.OnGroundTypeColoursUpdated   += UpdateColours;

            impassableActionsControl.ViewModel.OnUpdateImpassableColour     += OnUpdateImpassableColour;
            impassableActionsControl.ViewModel.OnImpassableColoursUpdated   += UpdateColours;

            tradeRoutesActionControl.ViewModel.OnUpdateTradeRouteColour     += OnUpdateTradeRouteColour;
            tradeRoutesActionControl.ViewModel.OnTradeRouteColoursUpdated   += UpdateColours;

            _preferencesViewModel.OnPreferencesChanged                      += OnPreferencesChanged;

            ProjectManager.OnOpenProject                                    += OnProjectOpened;
            ProjectManager.OnCloseProject                                   += OnProjectClosed;
            ProjectManager.OnSaveProject                                    += OnProjectSaved;
            ProjectManager.OnFilePathChanged                                += OnProjectPathChanged;
            ProjectManager.OnMapResized                                     += OnMapResized;

            HotkeyManager.Subscribe(this);
            HotkeyManager.Subscribe(UndoRedoManager);

            LoggerViewModel.Log("Tool session started", LogLevel.Info);
        }

        private static void LogUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            LoggerViewModel.Log(args.Exception.Message, LogLevel.Error);
        }

        private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            LoggerViewModel.Log(args.ExceptionObject.ToString(), LogLevel.Error);
        }

        public void Initialise()
        {
            var akitPathDetector = new AssemblyKitPathAutoDetector();
            akitPathDetector.Scan();

            var zoomTool    = ToolbarViewModel.Tools[(int)ToolType.Zoom]           as ZoomTool;
            var brushTool   = ToolbarViewModel.Tools[(int)ToolType.Brush]          as PaintTool;
            var eraserTool  = ToolbarViewModel.Tools[(int)ToolType.Eraser]         as PaintTool;
            var fillTool    = ToolbarViewModel.Tools[(int)ToolType.FloodFill]      as FloodFillTool;
            var lineTool    = ToolbarViewModel.Tools[(int)ToolType.Line]           as LineTool;
            var pickerTool  = ToolbarViewModel.Tools[(int)ToolType.ColorPicker]    as ColorPickerTool;

            zoomTool.SetCommand(new ZoomToolCommand(ViewportViewModel));
            brushTool.SetCommand(new BrushToolCommand());
            eraserTool.SetCommand(new EraserToolCommand());
            fillTool.SetCommand(new FloodFillCommand());
            lineTool.SetCommand(new LineToolCommand());
            pickerTool.SetCommand(new ColorPickerToolCommand(SidebarViewModel));

            brushTool.Painter   = new BrushPainter(ViewportViewModel, this);
            eraserTool.Painter  = new BrushPainter(ViewportViewModel, this);
            fillTool.Painter    = new FloodFillPainter(ViewportViewModel, this);
            lineTool.Painter    = new LinePainter(ViewportViewModel, this);
        }

        // Subscriptions use method groups (not lambdas) so the matching -= in
        // UnsubscribeFromMapHexEvents actually removes them.
        private void SubscribeToMapHexEvents()
        {
            project.MapHexEditor.OnRegionCreated            += this.OnRegionCreated;
            project.MapHexEditor.OnRegionRenamed            += this.OnRegionRenamed;
            project.MapHexEditor.OnRegionRemoved            += this.OnRegionRemoved;

            project.MapHexEditor.OnGroundTypeCreated        += this.OnGroundTypeCreated;
            project.MapHexEditor.OnGroundTypeRenamed        += this.OnGroundTypeRenamed;
            project.MapHexEditor.OnGroundTypeRemoved        += this.OnGroundTypeRemoved;

            project.MapHexEditor.OnAttritionCreated         += this.OnAttritionCreated;
            project.MapHexEditor.OnAttritionRenamed         += this.OnAttritionRenamed;
            project.MapHexEditor.OnAttritionRemoved         += this.OnAttritionRemoved;

            project.MapHexEditor.OnClimateCreated           += this.OnClimateCreated;
            project.MapHexEditor.OnClimateRenamed           += this.OnClimateRenamed;
            project.MapHexEditor.OnClimateRemoved           += this.OnClimateRemoved;

            project.MapHexEditor.OnAreaOfInterestCreated    += this.OnAreaOfInterestCreated;
            project.MapHexEditor.OnAreaOfInterestRenamed    += this.OnAreaOfInterestRenamed;
            project.MapHexEditor.OnAreaOfInterestRemoved    += this.OnAreaOfInterestRemoved;
        }

        private void UnsubscribeFromMapHexEvents()
        {
            project.MapHexEditor.OnRegionCreated            -= this.OnRegionCreated;
            project.MapHexEditor.OnRegionRenamed            -= this.OnRegionRenamed;
            project.MapHexEditor.OnRegionRemoved            -= this.OnRegionRemoved;

            project.MapHexEditor.OnGroundTypeCreated        -= this.OnGroundTypeCreated;
            project.MapHexEditor.OnGroundTypeRenamed        -= this.OnGroundTypeRenamed;
            project.MapHexEditor.OnGroundTypeRemoved        -= this.OnGroundTypeRemoved;

            project.MapHexEditor.OnAttritionCreated         -= this.OnAttritionCreated;
            project.MapHexEditor.OnAttritionRenamed         -= this.OnAttritionRenamed;
            project.MapHexEditor.OnAttritionRemoved         -= this.OnAttritionRemoved;

            project.MapHexEditor.OnClimateCreated           -= this.OnClimateCreated;
            project.MapHexEditor.OnClimateRenamed           -= this.OnClimateRenamed;
            project.MapHexEditor.OnClimateRemoved           -= this.OnClimateRemoved;

            project.MapHexEditor.OnAreaOfInterestCreated    -= this.OnAreaOfInterestCreated;
            project.MapHexEditor.OnAreaOfInterestRenamed    -= this.OnAreaOfInterestRenamed;
            project.MapHexEditor.OnAreaOfInterestRemoved    -= this.OnAreaOfInterestRemoved;
        }

        #region Property setters
        /// <summary>
        /// Sets <see cref="CAIME.ToolbarViewModel"/> externally.
        /// Assigns commands for known tools.
        /// Subscribes to <see cref="ToolbarViewModel.OnUpdateCellColour"/> event.
        /// Subscribes to <see cref="Tool.ActiveToolChanged"/> event per each tool.
        /// </summary>
        public void SetToolbarVM(ToolbarViewModel viewModel)
        {
            ToolbarViewModel = viewModel;

            foreach (var tool in ToolbarViewModel.Tools)
            {
                tool.ActiveToolChanged += Toolbar_OnActiveToolChanged;
            }
        }

        /// <summary>
        /// Sets <see cref="CAIME.SidebarViewModel"/> externally.
        /// Subscribes to <see cref="Layer.ActiveLayerChanged"/> and <see cref="Layer.VisibilityChanged"/> events per each layer.
        /// Subscribes to <see cref="SidebarViewModel.OnLayersImport"/> and <see cref="SidebarViewModel.OnLayersExport"/> events.
        /// </summary>
        public void SetSidebarVM(SidebarViewModel viewModel)
        {
            SidebarViewModel = viewModel;
            SidebarViewModel.ToolbarVM = ToolbarViewModel;
            SidebarViewModel.SwatchesVM.OnActiveSwatchChanged += (sender, e) => ActiveSwatchChanged(sender, e);
            SidebarViewModel.LayersVM.LayerOrderChanged += LayerOrderChanged;
            SidebarViewModel.LayersVM.OpacityAdjustmentStarted += OpacityAdjustmentStarted;
            SidebarViewModel.LayersVM.OpacityAdjustmentEnded += OpacityAdjustmentEnded;
        }

        /// <summary>
        /// Sets <see cref="CAIME.ViewportViewModel"/> externally.
        /// Subscribes to <see cref="ViewportViewModel.OnExecuteCommand"/> event.
        /// </summary>
        public void SetViewportVM(ViewportViewModel viewModel)
        {
            ViewportViewModel = viewModel;

            ViewportViewModel.OnLeftMouseDown          += Viewport_OnLeftMouseDown;
            ViewportViewModel.OnLeftMouseUp            += Viewport_OnLeftMouseUp;
            ViewportViewModel.OnLeftMouseMove          += Viewport_OnLeftMouseMove;
            ViewportViewModel.OnMouseOver              += Viewport_OnMouseOver;
            ViewportViewModel.Viewport.CameraChanged   += Viewport_CameraChanged;
            ViewportViewModel.Viewport.QueryCursor     += Viewport_QueryCursor;
        }

        private void Viewport_CameraChanged(object sender, RoutedEventArgs e)
        {
            UpdatePaintCursorScale();

            SidebarViewModel?.MinimapVM.UpdateViewFrameFromCamera();
        }

        /// <summary>
        /// Resizes both paint cursors to the current zoom level.
        /// </summary>
        private void UpdatePaintCursorScale()
        {
            if (ViewportViewModel == null || ToolbarViewModel == null)
            {
                return;
            }

            var zoomScale = ViewportViewModel.ZoomScale;

            foreach (var tool in ToolbarViewModel.Tools)
            {
                var brushTool = tool as IBrushTool;
                if (brushTool != null)
                {
                    brushTool.SetViewportZoomScale(zoomScale);
                }
            }
        }

        /// <summary>
        /// Supplies the active tool's cursor. This answers the cursor query rather than
        /// assigning Viewport.Cursor, because Helix's gesture handlers push the Cursor
        /// property onto a stack on mouse down and pop it back on mouse up - anything
        /// written to that property mid-gesture is reverted when the gesture ends.
        /// </summary>
        private void Viewport_QueryCursor(object sender, QueryCursorEventArgs e)
        {
            var tool = ToolbarViewModel?.GetActiveTool() as ViewportTool;
            if (tool == null || tool.Cursor == null)
            {
                return;
            }

            e.Cursor = tool.Cursor;
            e.Handled = true;
        }

        public bool SetFloodFillSource(Layer layer)
        {
            var floodFillTool = ToolbarViewModel.GetTool(ToolType.FloodFill) as FloodFillTool;
            return (floodFillTool.Painter as FloodFillPainter).SetSource(layer);
        }
        #endregion

        public void ResizeMap()
        {
            if (ProjectManager.IsProjectOpen)
            {
                var window = new ResizeWindow(ProjectManager)
                {
                    Owner = Application.Current.MainWindow
                };

                window.Show();
            }
        }

        public void RenameCampaignMap()
        {
            if (ProjectManager.IsProjectOpen)
            {
                var renameMapWindow = new RenameCampaignMapWindow(this.ProjectManager)
                {
                    Owner = Application.Current.MainWindow
                };

                renameMapWindow.Show();
            }
        }

        public void ExitApplication()
        {
            if (CanExit())
            {
                CloseProject();
                Application.Current.Shutdown();
            }
        }

        public void OpenProject()
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "map.hex file (*.hex)|*.hex"
            };

            if (dialog.ShowDialog() == true)
            {
                if (ProjectManager.IsProjectOpen)
                {
                    ProjectManager.CloseProject();
                }

                ProjectManager.Open(dialog.FileName);
            }
        }

        public void ReloadProject()
        {
            if (ProjectManager.IsProjectOpen)
            {
                var projectPath = ProjectManager.Project.FileName;
                ProjectManager.CloseProject();
                ProjectManager.Open(projectPath);

                LoggerViewModel.Log("Project has been successfully reloaded.", LogLevel.Info);
            }
        }

        public void SaveProject(SaveParameters settings = null)
        {
            if (ProjectManager.IsProjectOpen)
            {
                var saveParams = settings == null ? SaveParameters.Default : settings;
                this.ProjectManager.Save(saveParams);
            }
        }

        public void SaveProjectAs()
        {
            if (ProjectManager.IsProjectOpen)
            {
                var dialog = new SaveFileDialog()
                {
                    Filter = "map.hex file (*.hex)|*.hex",
                    Title = "Save a map.hex file",
                };

                dialog.FileName = "map.hex";

                if (dialog.ShowDialog() == true && string.IsNullOrEmpty(dialog.FileName) == false)
                {
                    var savePath = dialog.FileName.Substring(0, dialog.FileName.LastIndexOf('\\') + 1);
                    var saveName = dialog.FileName.Substring(dialog.FileName.LastIndexOf('\\') + 1);
                    saveName = saveName.Substring(0, saveName.LastIndexOf('.'));

                    var saveParams = new SaveParameters()
                    {
                        CustomSavePath = savePath,
                        CustomFileName = saveName,
                        Flags = SaveParameters.SaveFlags.MapHexFile
                    };

                    SaveProject(saveParams);
                }
            }
        }

        private void CloseProject()
        {
            if (ProjectManager.IsProjectOpen)
            {
                ProjectManager.CloseProject();
                LoggerViewModel.Log("Project has been successfully closed.", LogLevel.Info);
            }
        }

        /// <summary>
        /// Constructs hex map grid and populates layers & swatches
        /// </summary>
        private void PostLoadProject()
        {
            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                try
                {
                    GenerateGrid();

                    PostToUi(context, () =>
                    {
                        // Raises Layer and PropertyChanged events and swaps the bound ActiveSwatches
                        // collection, so it belongs on the UI thread.
                        PopulateLayersAndSwatches();

                        // Set active layer and raise <see cref="Layer.ActiveLayerChanged"/> event
                        SidebarViewModel.LayersVM.SetActiveLayer(LayerType.GroundTypes);
                        // Set default visible layer and raise <see cref="Layer.VisibilityChanged"/> event
                        SidebarViewModel.LayersVM.SetVisibleLayer(LayerType.GroundTypes);
                        // Set default active tool and raise <see cref="Tool.ActiveToolChanged"/> event
                        ToolbarViewModel.SetActiveTool(ToolType.Pan);

                        SidebarViewModel.SwatchesVM.ResetIndex();

                        updateMinimapTimer = new System.Timers.Timer()
                        {
                            Interval = TimeSpan.FromSeconds(2.0).TotalMilliseconds,
                        };

                        updateMinimapTimer.Elapsed += UpdateMinimapTimer_Elapsed;
                        updateMinimapTimer.Start();

                        // Change application state to initialised
                        isInit = true;
                        AppStateContext.Instance.SetState(AppStateContext.InitialisedState);

                        // Gated separately from the flags above: only available for Three Kingdoms projects.
                        AppStateContext.Instance.IsShaderResolutionCorrectorEnabled = project.Game == GameTemplate.Three_Kingdoms;

                        // The Map Data Editor only supports games whose map_data.esf still duplicates
                        // settlement slot entries - see MapDataEditorViewModel's version check.
                        AppStateContext.Instance.IsMapDataEditorEnabled =
                            project.Game == GameTemplate.Rome2 ||
                            project.Game == GameTemplate.Attila ||
                            project.Game == GameTemplate.Thrones_Of_Britannia;
                    });
                }
                catch (Exception ex)
                {
                    // Without this the exception is unobserved and the app silently stays
                    // half-initialised: isInit false, state never Initialised, no error shown.
                    ReportPostLoadFailure(context, ex);
                }
            });
        }

        /// <summary>
        /// Runs <paramref name="work"/> on the UI thread. SynchronizationContext.Current can be
        /// null depending on where the load was kicked off from, so the dispatcher is the fallback.
        /// </summary>
        private static void PostToUi(SynchronizationContext context, Action work)
        {
            if (context != null)
            {
                context.Post(o => work(), null);
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(work);
            }
            else
            {
                work();
            }
        }

        private static void ReportPostLoadFailure(SynchronizationContext context, Exception ex)
        {
            PostToUi(context, () =>
                LoggerViewModel.Log($"Failed to finish opening the project: {ex.Message}", LogLevel.ErrorMessageBox));
        }

        private void UnloadProject()
        {
            if (updateMinimapTimer != null)
            {
                updateMinimapTimer.Elapsed -= UpdateMinimapTimer_Elapsed;
                updateMinimapTimer.Stop();
                updateMinimapTimer.Dispose();
                updateMinimapTimer = null;
            }

            isInit = false;
            hexLayout = null;
            gridColours = null;
            this.StopOpacityPreview();

            ViewportViewModel.DestroyGrid();

            this.UnsubscribeFromMapHexEvents();

            if (project?.MapHexFile != null)
            {
                project.MapHexFile.OnLayerChanged -= OnMapHexLayerChanged;
            }

            foreach (var layer in SidebarViewModel.LayersVM.Layers)
            {
                layer.ActiveLayerChanged    -= ActiveLayerChanged;
                layer.VisibilityChanged     -= LayerVisibilityChanged;
                layer.OpacityChanged        -= LayerOpacityChanged;
            }

            UndoRedoManager.Clear();

            _autoSavesManager.Shutdown();
            _autoBackupManager.Shutdown();

            AppStateContext.Instance.SetState(AppStateContext.UninitialisedState);
        }

        private void ResetGrid()
        {
            ViewportViewModel.DestroyGrid();

            var context = SynchronizationContext.Current;
            Task.Run(() =>
            {
                try
                {
                    GenerateGrid();

                    PostToUi(context, () =>
                    {
                        PopulateLayersAndSwatches();
                        UpdateColours(resizeColors: true);
                    });
                }
                catch (Exception ex)
                {
                    ReportPostLoadFailure(context, ex);
                }
            });
        }

        public bool CanExit()
        {
            return ConfirmDiscardUnsavedChanges("Are you sure you want to quit without saving changes?", "Closing application with unsaved changes!");
        }

        public bool CanCloseProject()
        {
            return ConfirmDiscardUnsavedChanges("Are you sure you want to close the project without saving changes?", "Closing project with unsaved changes!");
        }

        private bool ConfirmDiscardUnsavedChanges(string message, string logMessage)
        {
            if (project != null && project.HasUnsavedChanges)
            {
                var result = MessageBox.Show(Application.Current.MainWindow, message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    LoggerViewModel.Log(logMessage, LogLevel.Warning);
                }

                return result != MessageBoxResult.No;
            }

            return true;
        }

        public Layer GetActiveLayer()
        {
            return SidebarViewModel.LayersVM.GetActiveLayer();
        }

        /// <summary>
        /// Determines whether a colour painted on the given layer can be displayed
        /// </summary>
        public bool CanDisplay(Layer layer, int hexIndex)
        {
            return SidebarViewModel.LayersVM.CanDisplay(layer, hexIndex);
        }

        public Tool GetActiveTool()
        {
            return ToolbarViewModel.GetActiveTool();
        }

        public void SetActiveTool(ToolType toolType)
        {
            if (isInit)
            {
                ToolbarViewModel.SetActiveTool(toolType);
            }
        }

        public Swatch GetActiveSwatch()
        {
            return SidebarViewModel.SwatchesVM.ActiveSwatch;
        }

        public Swatch GetSwatchByValue(LayerType layer, int colourValue)
        {
            return SidebarViewModel.SwatchesVM.Swatches[layer].Find(swatch => swatch.Colour == colourValue);
        }

        /// <summary>
        /// Finds the swatch matching the hex's current model value for the given layer.
        /// Colour-based lookups are ambiguous because several swatches share a colour;
        /// this is what undo snapshots must use. Returns null when no swatch matches
        /// (e.g. the hex holds a cleared/invalid value).
        /// </summary>
        public Swatch GetSwatchForHex(LayerType layerType, Hex hex)
        {
            if (SidebarViewModel.SwatchesVM.Swatches.TryGetValue(layerType, out var swatches) == false)
            {
                return null;
            }

            switch (layerType)
            {
                case LayerType.GroundTypes:     return swatches.Find(s => ((GroundSwatch)s).GroundTypeIndex == hex.GroundTypeIndex);
                case LayerType.Attritions:      return swatches.Find(s => ((AttritionSwatch)s).AttritionIndex == hex.AttritionIndex);
                case LayerType.Climates:        return swatches.Find(s => ((ClimateSwatch)s).ClimateIndex == hex.ClimateIndex);
                case LayerType.AreasOfInterest: return swatches.Find(s => ((AreaOfInterestSwatch)s).AreaOfInterestIndex == hex.InterestIndex);
                case LayerType.Regions:         return swatches.Find(s => ((RegionSwatch)s).RegionIndex == hex.RegionId);
                case LayerType.RegionBorders:   return swatches.Find(s => ((RegionBorderSwatch)s).IsBorder == hex.IsBorder);
                case LayerType.Beaches:         return swatches.Find(s => ((BeachSwatch)s).IsBeach == hex.IsBeach);
                case LayerType.Rivers:          return swatches.Find(s => ((RiverSwatch)s).IsRiver == hex.IsRiver);
                case LayerType.Roads:           return swatches.Find(s => ((RoadSwatch)s).IsRoad == hex.IsRoad);
                case LayerType.Bridges:         return swatches.Find(s => ((BridgeSwatch)s).IsBridge == hex.IsBridge);
                case LayerType.TradeRoutes:     return swatches.Find(s => ((TradeRouteSwatch)s).IsTradeRoute == hex.IsTradeRoute);
                case LayerType.Impassable:      return swatches.Find(s => ((NogoSwatch)s).IsImpassable == hex.IsImpassable);
                case LayerType.TownSlots:       return swatches.Find(s => ((TownSlotSwatch)s).SlotIndex == hex.TownSlotIndex);
                case LayerType.TownSprawl:      return swatches.Find(s => ((TownSprawlSwatch)s).Value == hex.IsTownSprawl);
                case LayerType.Restrictions:    return swatches.Find(s => ((RestrictionSwatch)s).RestrictionLevel == hex.RestrictionLvl);
            }

            return null;
        }

        /// <summary>
        /// Create a hex grid with initial data
        /// And create a geometry mesh from this data
        /// </summary>
        private void GenerateGrid()
        {
            float scale = 1.0f - cachedHexSpacing;
            hexLayout = new HexLayout(HexStyle.FlatTop, OffsetType.Odd, hexSize: 10, hexScale: scale);

            // Create grid geometry from hex grid
            ViewportViewModel.ConstructGridMesh((int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight, hexLayout);
        }

        /// <summary>
        /// Import hex data from .raw images
        /// </summary>
        private void PopulateLayersAndSwatches()
        {
            SidebarViewModel.LayersVM.SetColours(ProjectManager.Project.ColourTable);

            // Update swatches set
            SidebarViewModel.SwatchesVM.UpdateSwatchesSet(ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.GroundTypes, 0);
        }

        /// <summary>
        /// Calculates visible grid colours from all visible layers
        /// </summary>
        public void UpdateColours(bool resizeColors)
        {
            if (gridColours == null || resizeColors)
            {
                gridColours = new int[project.MapHexFile.Capacity];
            }

            LayerCompositor.ComposeAll(SidebarViewModel.LayersVM.Layers, gridColours);
            ViewportViewModel.UpdateGridColours(gridColours, (int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight);
        }

        public void RefreshHex(int index)
        {
            var colour = LayerCompositor.ComposeHex(SidebarViewModel.LayersVM.Layers, index);

            gridColours[index] = colour;
            ViewportViewModel.UpdateCellColour(colour, index, (int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight);
        }

        private void OnProjectOpened(object sender, ProjectEventArgs e)
        {
            this.project = e.Project;

            if (project.MapHexFile != null)
            {
                project.MapHexFile.OnLayerChanged += OnMapHexLayerChanged;
            }

            actionsControl.ViewModel.Initialise(e.Project);
            borderActionsControl.ViewModel.Initialise(e.Project);
            sprawlActionsControl.ViewModel.Initialise(e.Project);
            groundTypeActionsControl.ViewModel.Initialise(e.Project);
            impassableActionsControl.ViewModel.Initialise(e.Project);
            tradeRoutesActionControl.ViewModel.Initialise(e.Project);
            roadsActionsControl.ViewModel.Initialise(e.Project);
            riversActionsControl.ViewModel.Initialise(e.Project);
            bridgesActionsControl.ViewModel.Initialise(e.Project);
            beachesActionsControl.ViewModel.Initialise(e.Project);
            townSlotsActionsControl.ViewModel.Initialise(e.Project);

            SidebarViewModel.LayersVM.Initialise(e.Project.Game, _preferencesViewModel.GetLayerOrder(e.Project.Game));

            FloodFillLayersSource.Clear();
            foreach (var layer in SidebarViewModel.LayersVM.Layers)
            {
                FloodFillLayersSource.Add(layer.Name);

                layer.ActiveLayerChanged    += ActiveLayerChanged;
                layer.VisibilityChanged     += LayerVisibilityChanged;
                layer.OpacityChanged        += LayerOpacityChanged;
            }

            this.SubscribeToMapHexEvents();

            SelectedFloodFillSource = SidebarViewModel.LayersVM.GetActiveLayer().Name;

            _autoSavesManager.Initialise();
            _autoBackupManager.Initialise(e.Project.ProjectPath);

            this.PostLoadProject();
        }

        private void OnProjectClosed(object sender, RoutedEventArgs e)
        {
            this.UnloadProject();
        }

        private void OnMapResized(object sender, RoutedEventArgs e)
        {
            UndoRedoManager.Clear();

            this.ResetGrid();
        }

        private void OnProjectSaved(object sender, ProjectEventArgs e)
        {
            // TODO: Handle anything that needs to be done when project was saved
        }

        private void OnProjectPathChanged(object sender, ProjectPathChangedEventArgs e)
        {
            // TODO: Handle anything that needs to be done when project path changed
        }

        private void OnPreferencesChanged(object sender, EventArgs e)
        {
            if (_preferencesViewModel.HexSpacing != cachedHexSpacing)
            {
                cachedHexSpacing = _preferencesViewModel.HexSpacing;
                ResetGrid();
            }
        }

        private void OnMapHexLayerChanged(object sender, LayerChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            var layerType = e.LayerType;
            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetLayer(layerType), true);
        }

        #region Event Handlers
        /// <summary>
        /// Fires when user switches between layers
        /// </summary>
        private void ActiveLayerChanged(object sender, RoutedEventArgs e)
        {
            var layer = sender as Layer;
            var layerType = layer.Type;

            SidebarViewModel.LayersVM.UpdateActiveLayer(layerType);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(layerType, 0);

            switch (layerType)
            {
                case LayerType.Regions:
                case LayerType.Attritions:
                case LayerType.Climates:
                case LayerType.AreasOfInterest:
                    SidebarViewModel.ActionsVM.LayerControl = actionsControl;
                    break;
                case LayerType.GroundTypes:
                    SidebarViewModel.ActionsVM.LayerControl = groundTypeActionsControl;
                    break;
                case LayerType.RegionBorders:
                    SidebarViewModel.ActionsVM.LayerControl = borderActionsControl;
                    break;
                case LayerType.TownSprawl:
                    SidebarViewModel.ActionsVM.LayerControl = sprawlActionsControl;
                    break;
                case LayerType.Impassable:
                    SidebarViewModel.ActionsVM.LayerControl = impassableActionsControl;
                    break;
                case LayerType.TradeRoutes:
                    SidebarViewModel.ActionsVM.LayerControl = tradeRoutesActionControl;
                    break;
                case LayerType.Roads:
                    SidebarViewModel.ActionsVM.LayerControl = roadsActionsControl;
                    break;
                case LayerType.Rivers:
                    SidebarViewModel.ActionsVM.LayerControl = riversActionsControl;
                    break;
                case LayerType.Bridges:
                    SidebarViewModel.ActionsVM.LayerControl = bridgesActionsControl;
                    break;
                case LayerType.Beaches:
                    SidebarViewModel.ActionsVM.LayerControl = beachesActionsControl;
                    break;
                case LayerType.TownSlots:
                    SidebarViewModel.ActionsVM.LayerControl = townSlotsActionsControl;
                    break;
                default:
                    SidebarViewModel.ActionsVM.LayerControl = null;
                    break;
            }

            AppStateContext.Instance.SetLayerActionsControlExpanded(SidebarViewModel.ActionsVM.LayerControl != null);
        }

        /// <summary>
        /// Fires when user switches between tools in toolbar
        /// </summary>
        private void Toolbar_OnActiveToolChanged(object sender, RoutedEventArgs e)
        {
            var newTool = sender as Tool;
            var type = newTool.Type;

            if (type == ToolType.Pan)
            {
                ViewportViewModel.LeftClick = ViewportCommands.Pan;
            }
            else
            if (type == ToolType.Zoom)
            {
                ViewportViewModel.LeftClick = ViewportCommands.Zoom;
            }
            else
            {
                ViewportViewModel.LeftClick = null;
            
                if (newTool is OneTimeTool)
                {
                    (newTool as OneTimeTool).ProcessClick(ViewportViewModel);
                }
            }

            ToolbarViewModel.UpdateActiveTool(newTool);

            if (type == ToolType.FloodFill)
            {
                SelectedFloodFillSource = GetActiveLayer().Name;
            }

            if (newTool is IBrushTool)
            {
                (newTool as IBrushTool).SetBrushSize(BrushSize);
            }

            UpdatePaintCursorScale();

            // The incoming tool usually needs the same diameter as the outgoing one, so the
            // scale update alone would not re-query the cursor until the mouse next moved.
            Mouse.UpdateCursor();

            AppStateContext.Instance.OnToolChanged(type);
        }

        private void ActiveSwatchChanged(object sender, ActiveSwatchChangedEventArgs e)
        {
            var layerType = SidebarViewModel.LayersVM.ActiveLayer;
            if (layerType == LayerType.Regions)
            {
                var swatch = e.ActiveSwatch as RegionSwatch;
                if (swatch == null)
                {
                    LoggerViewModel.Log("EditorViewModel.ActiveSwatchChanged: Potential issue with the current region swatch.", LogLevel.Warning);
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, -1);
                }
                else
                {
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, swatch.RegionIndex);
                }
            }
            else
            if (layerType == LayerType.GroundTypes)
            {
                var swatch = e.ActiveSwatch as GroundSwatch;
                if (swatch == null)
                {
                    LoggerViewModel.Log("EditorViewModel.ActiveSwatchChanged: Potential issue with the current ground type swatch.", LogLevel.Warning);
                    groundTypeActionsControl.ViewModel.ActiveSwatchChanged(layerType, -1);
                }
                else
                {
                    groundTypeActionsControl.ViewModel.ActiveSwatchChanged(layerType, swatch.GroundTypeIndex);
                }
            }
            else
            if (layerType == LayerType.Attritions)
            {
                var swatch = e.ActiveSwatch as AttritionSwatch;
                if (swatch == null)
                {
                    LoggerViewModel.Log("EditorViewModel.ActiveSwatchChanged: Potential issue with the current attrition swatch.", LogLevel.Warning);
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, -1);
                }
                else
                {
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, swatch.AttritionIndex);
                }
            }
            else
            if (layerType == LayerType.Climates)
            {
                var swatch = e.ActiveSwatch as ClimateSwatch;
                if (swatch == null)
                {
                    LoggerViewModel.Log("EditorViewModel.ActiveSwatchChanged: Potential issue with the current climate swatch.", LogLevel.Warning);
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, -1);
                }
                else
                {
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, swatch.ClimateIndex);
                }
            }
            else
            if (layerType == LayerType.AreasOfInterest)
            {
                var swatch = e.ActiveSwatch as AreaOfInterestSwatch;
                if (swatch == null)
                {
                    LoggerViewModel.Log("EditorViewModel.ActiveSwatchChanged: Potential issue with the current area of interest swatch.", LogLevel.Warning);
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, -1);
                }
                else
                {
                    actionsControl.ViewModel.ActiveSwatchChanged(layerType, swatch.AreaOfInterestIndex);
                }
            }
        }

        private void OnRegionCreated(object sender, CreateRegionEvent e)
        {
            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Regions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Regions, e.NewRegionIndex + 0); //This needs to be +0 because the fact you are adding a new swatch counteracts the 0 indexing

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnRegionRenamed(object sender, RenameRegionEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.OldRegionName)
            {
                // Rename region button is tied to the Active Swatch, hence region's old name has to match active swatch name
                LoggerViewModel.Log("Region rename functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Regions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Regions, e.NewRegionIndex + 1);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnRegionRemoved(object sender, RemoveRegionEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.RegionName)
            {
                // Remove region button is tied to the Active Swatch, hence region's old name has to match active swatch name
                LoggerViewModel.Log("Region remove functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Regions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Regions, e.RegionIndex);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnGroundTypeCreated(object sender, CreateGroundTypeEvent e)
        {
            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.GroundTypes, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.GroundTypes, e.NewGroundTypeIndex + 0); //This needs to be +0 because the fact you are adding a new swatch counteracts the 0 indexing

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnGroundTypeRenamed(object sender, RenameGroundTypeEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.OldGroundTypeName)
            {
                LoggerViewModel.Log("Ground type rename functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.GroundTypes, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.GroundTypes, e.NewGroundTypeIndex + 1);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnGroundTypeRemoved(object sender, RemoveGroundTypeEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.GroundTypeName)
            {
                LoggerViewModel.Log("Ground type remove functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.GroundTypes, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.GroundTypes, e.GroundTypeIndex);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAttritionCreated(object sender, CreateAttritionEvent e)
        {
            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Attritions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Attritions, e.NewAttritionIndex + 0); //This needs to be +0 because the fact you are adding a new swatch counteracts the 0 indexing

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAttritionRenamed(object sender, RenameAttritionEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.OldAttritionName)
            {
                LoggerViewModel.Log("Attrition rename functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Attritions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Attritions, e.NewAttritionIndex + 1);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAttritionRemoved(object sender, RemoveAttritionEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.AttritionName)
            {
                LoggerViewModel.Log("Attrition remove functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Attritions, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Attritions, e.AttritionIndex);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnClimateCreated(object sender, CreateClimateEvent e)
        {
            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Climates, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Climates, e.NewClimateIndex + 0); //This needs to be +0 because the fact you are adding a new swatch counteracts the 0 indexing

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnClimateRenamed(object sender, RenameClimateEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.OldClimateName)
            {
                LoggerViewModel.Log("Climate rename functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Climates, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Climates, e.NewClimateIndex + 1);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnClimateRemoved(object sender, RemoveClimateEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.ClimateName)
            {
                LoggerViewModel.Log("Climate remove functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.Climates, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.Climates, e.ClimateIndex);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAreaOfInterestCreated(object sender, CreateAreaOfInterestEvent e)
        {
            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.AreasOfInterest, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.AreasOfInterest, e.NewAreaOfInterestIndex + 0); //This needs to be +0 because the fact you are adding a new swatch counteracts the 0 indexing

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAreaOfInterestRenamed(object sender, RenameAreaOfInterestEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.OldAreaOfInterestName)
            {
                LoggerViewModel.Log("Area of Interest rename functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.AreasOfInterest, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.AreasOfInterest, e.NewAreaOfInterestIndex + 1);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnAreaOfInterestRemoved(object sender, RemoveAreaOfInterestEvent e)
        {
            if (SidebarViewModel.SwatchesVM.ActiveSwatch.Name != e.AreaOfInterestName)
            {
                LoggerViewModel.Log("Area of Interest remove functionality might be broken!!!", LogLevel.Warning);
                return;
            }

            SidebarViewModel.SwatchesVM.UpdateSwatches(LayerType.AreasOfInterest, ProjectManager.Project);
            SidebarViewModel.SwatchesVM.SetActiveSwatches(LayerType.AreasOfInterest, e.AreaOfInterestIndex);

            project.ColourTable.SetColours(SidebarViewModel.LayersVM.GetActiveLayer(), true);
        }

        private void OnUpdateBorderColour(object sender, UpdateBorderColorEventArgs args)
        {
            int colour = ColourTable.GetRegionBorderColour(ProjectManager.Project.MapHexFile.HexData[args.hexIndex].IsBorder);
            SidebarViewModel.LayersVM.GetActiveLayer().Colours[args.hexIndex] = colour;
        }

        private void OnUpdateSprawlColour(object sender, UpdateSprawlColorEventArgs args)
        {
            int colour = ColourTable.GetTownSprawlColour(ProjectManager.Project.MapHexFile.HexData[args.hexIndex].IsTownSprawl);
            SidebarViewModel.LayersVM.GetActiveLayer().Colours[args.hexIndex] = colour;
        }

        private void OnUpdateGroundTypeColour(object sender, UpdateGroundTypeColorEventArgs args)
        {
            int colour = project.ColourTable.GetTerrainColour(ProjectManager.Project.MapHexFile.HexData[args.hexIndex].GroundTypeIndex);
            SidebarViewModel.LayersVM.GetActiveLayer().Colours[args.hexIndex] = colour;
        }

        private void OnUpdateImpassableColour(object sender, UpdateImpassableColorEventArgs args)
        {
            int colour = ColourTable.GetNogoColour(ProjectManager.Project.MapHexFile.HexData[args.hexIndex].IsPassable);
            SidebarViewModel.LayersVM.GetActiveLayer().Colours[args.hexIndex] = colour;
        }

        private void OnUpdateTradeRouteColour(object sender, UpdateTradeRouteColorEventArgs args)
        {
            int colour = ColourTable.GetTradeRouteColour(ProjectManager.Project.MapHexFile.HexData[args.hexIndex].IsTradeRoute);
            SidebarViewModel.LayersVM.GetActiveLayer().Colours[args.hexIndex] = colour;
        }

        /// <summary>
        /// Fires when layer visibility changes
        /// </summary>
        private void LayerVisibilityChanged(object sender, RoutedEventArgs e)
        {
            this.RedrawLayersStack();
        }

        /// <summary>
        /// Fires when user re-arranges the layers stack
        /// </summary>
        private void LayerOrderChanged(object sender, EventArgs e)
        {
            if (isInit)
            {
                this.RedrawLayersStack();
                this.SaveLayerOrder();
            }
        }

        private void LayerOpacityChanged(object sender, EventArgs e)
        {
            if (isInit)
            {
                this.ScheduleLayersStackRedraw();
            }
        }

        private void OpacityAdjustmentStarted(object sender, EventArgs e)
        {
            var layer = SidebarViewModel.LayersVM.ActiveLayerModel;
            if (isInit == false || layer == null)
            {
                return;
            }

            if (isLayersStackRedrawScheduled)
            {
                this.RedrawLayersStack();
            }

            opacityPreview          = LayerOpacityPreview.Prepare(SidebarViewModel.LayersVM.Layers, layer, gridColours.Length);
            opacityPreviewCells     = ViewportViewModel.CreateCellBatch(opacityPreview.AffectedHexes, (int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight);
            opacityBeforePreview    = layer.Opacity;
            isOpacityPreviewDrawn   = false;
        }

        private void OpacityAdjustmentEnded(object sender, EventArgs e)
        {
            if (opacityPreview == null)
            {
                return;
            }

            var layer = opacityPreview.Layer;
            var needsRedraw = isOpacityPreviewDrawn;
            this.StopOpacityPreview();

            if (isInit && (needsRedraw || layer.Opacity != opacityBeforePreview))
            {
                this.ScheduleLayersStackRedraw();
            }
        }
        #endregion

        private void StopOpacityPreview()
        {
            opacityPreview          = null;
            opacityPreviewCells     = null;
            isOpacityPreviewDrawn   = false;
        }

        private void ScheduleLayersStackRedraw()
        {
            if (isLayersStackRedrawScheduled)
            {
                return;
            }

            isLayersStackRedrawScheduled = true;
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                isLayersStackRedrawScheduled = false;
                if (isInit == false)
                {
                    return;
                }

                if (opacityPreview != null)
                {
                    this.DrawOpacityPreview();
                }
                else
                {
                    this.RedrawLayersStack();
                }
            }), DispatcherPriority.Background);
        }

        private void DrawOpacityPreview()
        {
            opacityPreview.Compose(opacityPreview.Layer.Opacity, gridColours);
            ViewportViewModel.UpdateCellColours(opacityPreviewCells, gridColours);
            isOpacityPreviewDrawn = true;
        }

        // The built-in order is stored as no order at all, so a later change to it still reaches
        // users who never re-arranged their layers.
        private void SaveLayerOrder()
        {
            var layersVM = SidebarViewModel.LayersVM;
            _preferencesViewModel.SetLayerOrder(project.Game, layersVM.IsInDefaultOrder() ? null : layersVM.GetLayers());
            _preferencesViewModel.Save();
        }

        private void RedrawLayersStack()
        {
            this.UpdateColours(resizeColors: false);

            SidebarViewModel.LayersVM.UpdateTopLayer();
            SidebarViewModel.MinimapVM.UpdateMinimap((int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight, gridColours);
        }

        /// <summary>
        /// Disposes unmanaged resources from <see cref="CAIME.ViewportViewModel"/>
        /// </summary>
        public void Dispose()
        {
            ViewportViewModel.Dispose();
        }

        public ProcessHotkeyResult ProcessHotkeyCombination(Key[] keys)
        {
            var isCtrlPressed   = keys.Length > 0 ? keys[0] == Key.LeftCtrl  || keys[0] == Key.RightCtrl  : false;
            var isShiftPressed  = keys.Length > 1 ? keys[1] == Key.LeftShift || keys[1] == Key.RightShift : false;

            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.S)
            {
                this.SaveProject();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 3 && isCtrlPressed && isShiftPressed && keys[2] == Key.S)
            {
                this.SaveProjectAs();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.O)
            {
                this.OpenProject();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.R)
            {
                this.ReloadProject();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.X)
            {
                this.CloseProject();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 2 && isCtrlPressed && keys[1] == Key.Q)
            {
                this.ExitApplication();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 3 && isCtrlPressed && isShiftPressed && keys[2] == Key.R)
            {
                this.ResizeMap();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (keys.Length == 3 && isCtrlPressed && isShiftPressed && keys[2] == Key.N)
            {
                this.RenameCampaignMap();
                return ProcessHotkeyResult.Handled;
            }

            if (this.ProcessToolHotkeys(keys) == ProcessHotkeyResult.Handled)
            {
                return ProcessHotkeyResult.Handled;
            }

            return ProcessHotkeyResult.Passed;
        }

        private ProcessHotkeyResult ProcessToolHotkeys(Key[] keys)
        {
            if (ToolbarViewModel.SetActiveToolHotkey(keys))
            {
                return ProcessHotkeyResult.Handled;
            }

            return ProcessHotkeyResult.Passed;
        }
        
        private void Viewport_OnLeftMouseDown(object sender, ViewportMouseEventArgs e)
        {
            var tool = GetActiveTool();
            if (tool is null)
            {
                return;
            }

            var viewportTool = tool as ViewportTool;
            if (viewportTool != null)
            {
                viewportTool.OnLeftMouseDown(e.MousePos);
            }
        }

        private ViewportToolParameters CreateViewportToolParameters(ViewportMouseEventArgs e)
        {
            var hexIndex = e.ViewportViewModel.FindHitHexIndex(e.MousePos);
            if (hexIndex == -1)
            {
                return null;
            }

            var args = new ViewportToolParameters
            {
                HexIndex    = hexIndex,
                Hex         = project.MapHexFile.HexData[hexIndex],
                Layer       = GetActiveLayer(),
                Swatch      = GetActiveSwatch(),
                CanDisplay  = SidebarViewModel.LayersVM.CanDisplay(hexIndex),
            };

            return args;
        }

        private void Viewport_OnLeftMouseMove(object sender, ViewportMouseEventArgs e)
        {
            var tool = GetActiveTool();
            if (tool is null)
            {
                return;
            }

            var viewportTool = tool as ViewportTool;
            if (viewportTool != null)
            {
                var hexIndex = e.ViewportViewModel.FindHitHexIndex(e.MousePos);
                if (hexIndex == -1)
                {
                    return;
                }

                var args = CreateViewportToolParameters(e);
                if (args != null)
                {
                    viewportTool.OnLeftMouseMove(args);
                }
            }
        }

        private void Viewport_OnLeftMouseUp(object sender, ViewportMouseEventArgs e)
        {
            var tool = GetActiveTool();
            if (tool is null)
            {
                return;
            }

            var viewportTool = tool as ViewportTool;
            if (viewportTool != null)
            {
                var viewportCommand = viewportTool.Command as ViewportCommand;
                if (viewportCommand != null && viewportCommand.DidExecute == false)
                {
                    // Workaround fix: single click paint
                    var args = CreateViewportToolParameters(e);
                    if (args != null)
                    {
                        viewportTool.OnLeftMouseMove(args);
                    }
                }

                viewportTool.OnLeftMouseUp(e.MousePos);
            }
        }

        private void Viewport_OnMouseOver(object sender, ViewportMouseEventArgs e)
        {
            PrintDebugInfo(e.MousePos);
        }

        private void PrintDebugInfo(System.Windows.Point mousePos)
        {
            var x = string.Empty;
            var y = string.Empty;

            int index = ViewportViewModel.FindHitHexIndex(mousePos);
            if (index != -1)
            {
                var hex = project.MapHexFile.HexData[index];
                x = hex.Q.ToString();
                y = hex.R.ToString();
            }

            LoggerViewModel.Instance.CoordX = x;
            LoggerViewModel.Instance.CoordY = y;
        }

        private void UpdateMinimapTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SidebarViewModel.MinimapVM.UpdateMinimap((int)project.MapWidth, (int)project.MapHeight, gridColours);
            });
        }
    }
}
