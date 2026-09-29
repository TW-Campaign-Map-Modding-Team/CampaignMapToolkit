using System;
using System.Windows;

namespace CAIME
{
    public interface IAppState
    {
        void UpdateView(AppStateContext context);
    }

    public class UninitialisedState : IAppState
    {
        public void UpdateView(AppStateContext context)
        {
            context.IsSwatchesExpanded              = false;
            context.IsActionsExpanded               = false;
            context.IsMinimapExpanded               = false;
            context.IsLayersExpanded                = false;
            context.IsLayerOpacityExpanded          = false;
            context.IsToolbarEnabled                = false;
            context.IsSidebarEnabled                = false;
            context.IsViewportEnabled               = false;
            context.IsExportBordersEnabled          = false;
            context.IsExportSVGBordersEnabled       = false;
            context.IsExportSVGRoadsEnabled          = false;
            context.IsProcessTradeRoutesEnabled     = false;
            context.IsExportPathfindingEnabled      = false;
            context.IsGenerateLookupEnabled         = false;
            context.IsProcessMapDataEnabled         = false;
            context.IsProcessDynResEnabled          = false;
            context.IsLoggerItemEnabled             = true;
            context.IsLoadItemEnabled               = true;
            context.IsReloadItemEnabled             = false;
            context.IsSaveItemEnabled               = false;
            context.IsCloseItemEnabled              = false;
            context.IsUndoItemEnabled               = false;
            context.IsRedoItemEnabled               = false;
            context.IsResizeEnabled                 = false;
            context.IsRenameMapEnabled              = false;
            context.IsBrushSizeSliderEnabled        = false;
            context.IsBackImgSliderEnabled          = false;
            context.IsGroundTypesValidatorEnabled   = false;
            context.IsClimatesValidatorEnabled      = false;
            context.IsAttritionsValidatorEnabled    = false;
            context.IsRegionsValidatorEnabled       = false;
            context.IsTownSlotsValidatorEnabled     = false;
            context.IsRoadsValidatorEnabled         = false;
            context.IsRiversValidatorEnabled        = false;
            context.IsBridgesValidatorEnabled       = false;
            context.IsBeachesValidatorEnabled       = false;
            context.IsImpassableValidatorEnabled    = false;
            context.IsSprawlValidatorEnabled        = false;
            context.IsShaderResolutionCorrectorEnabled = false;
            context.IsMapDataEditorEnabled          = false;
            context.BrushSizeSliderVisibility       = Visibility.Collapsed;
            context.FloodFillSourceVisibility       = Visibility.Collapsed;
        }
    }

    public class InitialisedState : IAppState
    {
        public void UpdateView(AppStateContext context)
        {
            context.IsLayersExpanded                = true;
            context.IsLayerOpacityExpanded          = true;
            context.IsSwatchesExpanded              = true;
            context.IsActionsExpanded               = true;
            context.IsMinimapExpanded               = true;
            context.IsToolbarEnabled                = true;
            context.IsSidebarEnabled                = true;
            context.IsViewportEnabled               = true;
            context.IsProcessMapDataEnabled         = true;
            context.IsProcessDynResEnabled          = true;
            context.IsExportBordersEnabled          = true;
            context.IsExportSVGBordersEnabled       = true;
            context.IsExportSVGRoadsEnabled          = true;
            context.IsProcessTradeRoutesEnabled     = true;
            context.IsExportPathfindingEnabled      = true;
            context.IsGenerateLookupEnabled         = true;
            context.IsLoadItemEnabled               = true;
            context.IsReloadItemEnabled             = true;
            context.IsSaveItemEnabled               = true;
            context.IsCloseItemEnabled              = true;
            context.IsResizeEnabled                 = true;
            context.IsRenameMapEnabled              = true;
            context.BrushSizeSliderVisibility       = Visibility.Visible;
            context.IsGroundTypesValidatorEnabled   = true;
            context.IsClimatesValidatorEnabled      = true;
            context.IsAttritionsValidatorEnabled    = true;
            context.IsRegionsValidatorEnabled       = true;
            context.IsTownSlotsValidatorEnabled     = true;
            context.IsRoadsValidatorEnabled         = true;
            context.IsRiversValidatorEnabled        = true;
            context.IsBridgesValidatorEnabled       = true;
            context.IsBeachesValidatorEnabled       = true;
            context.IsImpassableValidatorEnabled    = true;
            context.IsSprawlValidatorEnabled        = true;

            LoggerViewModel.Log("Editor has been initialised", LogLevel.Info);
        }
    }

    public class AppStateContext : ObservableObject
    {
        public static readonly AppStateContext Instance     = new AppStateContext();
        public static readonly IAppState InitialisedState   = new InitialisedState();
        public static readonly IAppState UninitialisedState = new UninitialisedState();

        private IAppState currentState;

        #region Sidebar properties
        private bool isBackImgSliderEnabled;
        public bool IsBackImgSliderEnabled
        {
            get
            {
                return isBackImgSliderEnabled;
            }
            set
            {
                isBackImgSliderEnabled = value;
                OnPropertyChanged(nameof(IsBackImgSliderEnabled));
            }
        }
        private bool isBrushSizeSliderEnabled;
        public bool IsBrushSizeSliderEnabled
        {
            get
            {
                return isBrushSizeSliderEnabled;
            }
            set
            {
                isBrushSizeSliderEnabled = value;
                OnPropertyChanged(nameof(IsBrushSizeSliderEnabled));
            }
        }
        private bool isSwatchesExpanded;
        public bool IsSwatchesExpanded
        {
            get
            {
                return isSwatchesExpanded;
            }
            set
            {
                isSwatchesExpanded = value;
                OnPropertyChanged(nameof(IsSwatchesExpanded));
            }
        }
        private bool isActionsExpanded;
        public bool IsActionsExpanded
        {
            get
            {
                return isActionsExpanded;
            }
            set
            {
                isActionsExpanded = value;
                OnPropertyChanged(nameof(IsActionsExpanded));
            }
        }
        private bool isMinimapExpanded;
        public bool IsMinimapExpanded
        {
            get
            {
                return isMinimapExpanded;
            }
            set
            {
                isMinimapExpanded = value;
                OnPropertyChanged(nameof(IsMinimapExpanded));
            }
        }
        private bool isLayersExpanded;
        public bool IsLayersExpanded
        {
            get
            {
                return isLayersExpanded;
            }
            set
            {
                isLayersExpanded = value;
                OnPropertyChanged(nameof(IsLayersExpanded));
            }
        }
        private bool isLayerOpacityExpanded;
        public bool IsLayerOpacityExpanded
        {
            get
            {
                return isLayerOpacityExpanded;
            }
            set
            {
                isLayerOpacityExpanded = value;
                OnPropertyChanged(nameof(IsLayerOpacityExpanded));
            }
        }
        #endregion

        #region Editor properties
        private bool isToolbarEnabled;
        public bool IsToolbarEnabled
        {
            get
            {
                return isToolbarEnabled;
            }
            set
            {
                isToolbarEnabled = value;
                OnPropertyChanged(nameof(IsToolbarEnabled));
            }
        }
        private bool isSidebarEnabled;
        public bool IsSidebarEnabled
        {
            get
            {
                return isSidebarEnabled;
            }
            set
            {
                isSidebarEnabled = value;
                OnPropertyChanged(nameof(IsSidebarEnabled));
            }
        }
        private bool isViewportEnabled;
        public bool IsViewportEnabled
        {
            get
            {
                return isViewportEnabled;
            }
            set
            {
                isViewportEnabled = value;
                OnPropertyChanged(nameof(IsViewportEnabled));
            }
        }

        private Visibility brushSizeSliderVisibility;
        public Visibility BrushSizeSliderVisibility
        {
            get => brushSizeSliderVisibility;
            set
            {
                brushSizeSliderVisibility = value;
                OnPropertyChanged(nameof(BrushSizeSliderVisibility));
            }
        }

        private Visibility floodFillSourceVisibility;
        public Visibility FloodFillSourceVisibility
        {
            get
            {
                return floodFillSourceVisibility;
            }
            set
            {
                floodFillSourceVisibility = value;
                OnPropertyChanged(nameof(FloodFillSourceVisibility));
            }
        }
        #endregion

        #region Menu properties
        private bool isExportBordersEnabled;
        public bool IsExportBordersEnabled
        {
            get
            {
                return isExportBordersEnabled;
            }
            set
            {
                isExportBordersEnabled = value;
                OnPropertyChanged(nameof(IsExportBordersEnabled));
            }
        }

        private bool isExportSVGBordersEnabled;
        public bool IsExportSVGBordersEnabled
        {
            get
            {
                return isExportSVGBordersEnabled;
            }
            set
            {
                isExportSVGBordersEnabled = value;
                OnPropertyChanged(nameof(IsExportSVGBordersEnabled));
            }
        }

        private bool isExportSVGRoadsEnabled;
        public bool IsExportSVGRoadsEnabled
        {
            get
            {
                return isExportSVGRoadsEnabled;
            }
            set
            {
                isExportSVGRoadsEnabled = value;
                OnPropertyChanged(nameof(IsExportSVGRoadsEnabled));
            }
        }

        private bool isProcessTradeRoutesEnabled;
        public bool IsProcessTradeRoutesEnabled
        {
            get
            {
                return isProcessTradeRoutesEnabled;
            }
            set
            {
                isProcessTradeRoutesEnabled = value;
                OnPropertyChanged(nameof(IsProcessTradeRoutesEnabled));
            }
        }
        private bool isExportPathfindingEnabled;
        public bool IsExportPathfindingEnabled
        {
            get
            {
                return isExportPathfindingEnabled;
            }
            set
            {
                isExportPathfindingEnabled = value;
                OnPropertyChanged(nameof(IsExportPathfindingEnabled));
            }
        }
        private bool isGenerateLookupEnabled;
        public bool IsGenerateLookupEnabled
        {
            get
            {
                return isGenerateLookupEnabled;
            }
            set
            {
                isGenerateLookupEnabled = value;
                OnPropertyChanged(nameof(IsGenerateLookupEnabled));
            }
        }

        private bool isLoggerItemEnabled;
        public bool IsLoggerItemEnabled
        {
            get
            {
                return isLoggerItemEnabled;
            }
            set
            {
                isLoggerItemEnabled = value;
                OnPropertyChanged(nameof(IsLoggerItemEnabled));
            }
        }
        private bool isProcessDynResEnabled;
        public bool IsProcessDynResEnabled
        {
            get
            {
                return isProcessDynResEnabled;
            }
            set
            {
                isProcessDynResEnabled = value;
                OnPropertyChanged(nameof(IsProcessDynResEnabled));
            }
        }
        private bool isProcessMapDataEnabled;
        public bool IsProcessMapDataEnabled
        {
            get
            {
                return isProcessMapDataEnabled;
            }
            set
            {
                isProcessMapDataEnabled = value;
                OnPropertyChanged(nameof(IsProcessMapDataEnabled));
            }
        }
        private bool isLoadItemEnabled;
        public bool IsLoadItemEnabled
        {
            get
            {
                return isLoadItemEnabled;
            }
            set
            {
                isLoadItemEnabled = value;
                OnPropertyChanged(nameof(IsLoadItemEnabled));
            }
        }
        private bool isReloadItemEnabled;
        public bool IsReloadItemEnabled
        {
            get
            {
                return isReloadItemEnabled;
            }
            set
            {
                isReloadItemEnabled = value;
                OnPropertyChanged(nameof(IsReloadItemEnabled));
            }
        }
        private bool isSaveItemEnabled;
        public bool IsSaveItemEnabled
        {
            get
            {
                return isSaveItemEnabled;
            }
            set
            {
                isSaveItemEnabled = value;
                OnPropertyChanged(nameof(IsSaveItemEnabled));
            }
        }
        private bool isCloseItemEnabled;
        public bool IsCloseItemEnabled
        {
            get
            {
                return isCloseItemEnabled;
            }
            set
            {
                isCloseItemEnabled = value;
                OnPropertyChanged(nameof(IsCloseItemEnabled));
            }
        }
        private bool isUndoItemEnabled;
        public bool IsUndoItemEnabled
        {
            get
            {
                return isUndoItemEnabled;
            }
            set
            {
                isUndoItemEnabled = value;
                OnPropertyChanged(nameof(IsUndoItemEnabled));
            }
        }
        private bool isRedoItemEnabled;
        public bool IsRedoItemEnabled
        {
            get
            {
                return isRedoItemEnabled;
            }
            set
            {
                isRedoItemEnabled = value;
                OnPropertyChanged(nameof(IsRedoItemEnabled));
            }
        }
        private bool isResizeEnabled;
        public bool IsResizeEnabled
        {
            get
            {
                return isResizeEnabled;
            }
            set
            {
                isResizeEnabled = value;
                OnPropertyChanged(nameof(IsResizeEnabled));
            }
        }
        private bool isRenameMapEnabled;
        public bool IsRenameMapEnabled
        {
            get
            {
                return isRenameMapEnabled;
            }
            set
            {
                isRenameMapEnabled = value;
                OnPropertyChanged(nameof(IsRenameMapEnabled));
            }
        }
        private bool isGroundTypesValidatorEnabled;
        public bool IsGroundTypesValidatorEnabled
        {
            get
            {
                return isGroundTypesValidatorEnabled;
            }
            set
            {
                isGroundTypesValidatorEnabled = value;
                OnPropertyChanged(nameof(IsGroundTypesValidatorEnabled));
            }
        }
        private bool isClimatesValidatorEnabled;
        public bool IsClimatesValidatorEnabled
        {
            get
            {
                return isClimatesValidatorEnabled;
            }
            set
            {
                isClimatesValidatorEnabled = value;
                OnPropertyChanged(nameof(IsClimatesValidatorEnabled));
            }
        }
        private bool isAttritionsValidatorEnabled;
        public bool IsAttritionsValidatorEnabled
        {
            get
            {
                return isAttritionsValidatorEnabled;
            }
            set
            {
                isAttritionsValidatorEnabled = value;
                OnPropertyChanged(nameof(IsAttritionsValidatorEnabled));
            }
        }
        private bool isRegionsValidatorEnabled;
        public bool IsRegionsValidatorEnabled
        {
            get
            {
                return isRegionsValidatorEnabled;
            }
            set
            {
                isRegionsValidatorEnabled = value;
                OnPropertyChanged(nameof(IsRegionsValidatorEnabled));
            }
        }
        private bool isTownSlotsValidatorEnabled;
        public bool IsTownSlotsValidatorEnabled
        {
            get
            {
                return isTownSlotsValidatorEnabled;
            }
            set
            {
                isTownSlotsValidatorEnabled = value;
                OnPropertyChanged(nameof(IsTownSlotsValidatorEnabled));
            }
        }
        private bool isRoadsValidatorEnabled;
        public bool IsRoadsValidatorEnabled
        {
            get
            {
                return isRoadsValidatorEnabled;
            }
            set
            {
                isRoadsValidatorEnabled = value;
                OnPropertyChanged(nameof(IsRoadsValidatorEnabled));
            }
        }
        private bool isRiversValidatorEnabled;
        public bool IsRiversValidatorEnabled
        {
            get
            {
                return isRiversValidatorEnabled;
            }
            set
            {
                isRiversValidatorEnabled = value;
                OnPropertyChanged(nameof(IsRiversValidatorEnabled));
            }
        }
        private bool isBeachesValidatorEnabled;
        public bool IsBeachesValidatorEnabled
        {
            get
            {
                return isBeachesValidatorEnabled;
            }
            set
            {
                isBeachesValidatorEnabled = value;
                OnPropertyChanged(nameof(IsBeachesValidatorEnabled));
            }
        }
        private bool isBridgesValidatorEnabled;
        public bool IsBridgesValidatorEnabled
        {
            get
            {
                return isBridgesValidatorEnabled;
            }
            set
            {
                isBridgesValidatorEnabled = value;
                OnPropertyChanged(nameof(IsBridgesValidatorEnabled));
            }
        }
        private bool isImpassableValidatorEnabled;
        public bool IsImpassableValidatorEnabled
        {
            get
            {
                return isImpassableValidatorEnabled;
            }
            set
            {
                isImpassableValidatorEnabled = value;
                OnPropertyChanged(nameof(IsImpassableValidatorEnabled));
            }
        }
        private bool isSprawlValidatorEnabled;
        public bool IsSprawlValidatorEnabled
        {
            get
            {
                return isSprawlValidatorEnabled;
            }
            set
            {
                isSprawlValidatorEnabled = value;
                OnPropertyChanged(nameof(IsSprawlValidatorEnabled));
            }
        }

        // Not set by InitialisedState: unlike the other menu flags, this one also depends on the
        // open project's game (Three Kingdoms only) - see EditorViewModel.PostLoadProject.
        private bool isShaderResolutionCorrectorEnabled;
        public bool IsShaderResolutionCorrectorEnabled
        {
            get
            {
                return isShaderResolutionCorrectorEnabled;
            }
            set
            {
                isShaderResolutionCorrectorEnabled = value;
                OnPropertyChanged(nameof(IsShaderResolutionCorrectorEnabled));
            }
        }

        // Not set by InitialisedState, same reasoning as IsShaderResolutionCorrectorEnabled above:
        // the Map Data Editor only supports the games whose map_data.esf still duplicates settlement
        // slot entries (Rome 2, Attila, Thrones of Britannia - see MapDataEditorViewModel's version
        // check and Docs/user-guide-tools-menu.md). Set based on the open project's game in
        // EditorViewModel.PostLoadProject.
        private bool isMapDataEditorEnabled;
        public bool IsMapDataEditorEnabled
        {
            get
            {
                return isMapDataEditorEnabled;
            }
            set
            {
                isMapDataEditorEnabled = value;
                OnPropertyChanged(nameof(IsMapDataEditorEnabled));
            }
        }
        #endregion

        private AppStateContext()
        {
            SetState(new UninitialisedState());
        }

        public void SetState(IAppState state)
        {
            currentState = state;
            currentState.UpdateView(this);
        }

        public void OnToolChanged(ToolType tool)
        {
            IsBrushSizeSliderEnabled = tool == ToolType.Brush || tool == ToolType.Eraser;
            FloodFillSourceVisibility = tool == ToolType.FloodFill ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetLayerActionsControlExpanded(bool expanded)
        {
            IsActionsExpanded = expanded;
        }

        public void SetBackImageSlider(bool enabled)
        {
            IsBackImgSliderEnabled = enabled;
        }
    }
}
