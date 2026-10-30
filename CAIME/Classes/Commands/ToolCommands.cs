using System;
using System.Collections.Generic;
using System.Windows.Input;
using CAIME.Painters;
using CAIME.Tools;

namespace CAIME
{
    public abstract class ViewportCommand : ICommand
    {
        public bool DidExecute;

        public ViewportTool Tool { get; set; }

        public event EventHandler CanExecuteChanged = (sender, e) => {};

        public ViewportCommand()
        {
            DidExecute = false;
        }

        public virtual bool CanExecute(object parameter)
        {
            return Tool != null && Tool.IsActive;
        }

        public virtual void Execute(object parameter)
        {
            DidExecute = false;
        }

        public virtual void Prepare(System.Windows.Point mousePos)
        {
            var tool = Tool as ViewportPaintTool;
            if (tool != null)
            {
                tool.Painter.PaintStart(mousePos);
            }
        }

        public virtual void Finish(System.Windows.Point mousePos)
        {
            var tool = Tool as ViewportPaintTool;
            if (tool != null)
            {
                tool.Painter.PaintEnd(mousePos);
            }

            DidExecute = false;
        }
    }

    public class ZoomToolCommand : ViewportCommand
    {
        private readonly ViewportViewModel _viewportVM;

        public ZoomToolCommand(ViewportViewModel viewportVM)
        {
            _viewportVM = viewportVM;
        }

        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var tool = Tool as ZoomTool;
            if (tool != null)
            {
                _viewportVM.Zoom(tool.Delta);
            }
        }
    }

    public class BrushToolCommand : ViewportCommand
    {
        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var tool = Tool as PaintTool;
            if (tool != null)
            {
                var hex         = (parameter as ViewportToolParameters).Hex;
                var index       = (parameter as ViewportToolParameters).HexIndex;
                var layer       = (parameter as ViewportToolParameters).Layer;
                var swatch      = (parameter as ViewportToolParameters).Swatch;
                var canDisplay  = (parameter as ViewportToolParameters).CanDisplay;
                var brushSize   = tool.GetBrushSize();

                var paintData = new PaintData()
                {
                    Layer       = layer,
                    HitHexIndex = index,
                    HitHex      = hex,
                    Swatch      = swatch,
                    CanDisplay  = canDisplay,
                    BrushSize   = brushSize
                };

                tool.Painter.Paint(paintData);
            }
        }
    }

    public class EraserToolCommand : ViewportCommand
    {
        public static readonly Dictionary<LayerType, Swatch> CLEAR_SWATCHES = CreateClearSwatches();

        private static Dictionary<LayerType, Swatch> CreateClearSwatches()
        {
            return new Dictionary<LayerType, Swatch>
            {
                [LayerType.AreasOfInterest] = new AreaOfInterestSwatch(null, Hex.INVALID_AREA_OF_INT_INDEX, ColourTable.Zero),
                [LayerType.Restrictions]    = new RestrictionSwatch(0),
                [LayerType.GroundTypes]     = new GroundSwatch(null, Hex.INVALID_GROUND_TYPE_INDEX, ColourTable.Zero),
                [LayerType.Climates]        = new ClimateSwatch(null, Hex.INVALID_CLIMATE_INDEX, ColourTable.Zero),
                [LayerType.Attritions]      = new AttritionSwatch(null, Hex.INVALID_ATTRITION_INDEX, ColourTable.Zero),
                [LayerType.Regions]         = new RegionSwatch(null, Hex.INVALID_REGION_INDEX, ColourTable.Zero),
                [LayerType.RegionBorders]   = new RegionBorderSwatch(false),
                [LayerType.Beaches]         = new BeachSwatch(false),
                [LayerType.Rivers]          = new RiverSwatch(false),
                [LayerType.Bridges]         = new BridgeSwatch(false),
                [LayerType.TownSprawl]      = new TownSprawlSwatch(false),
                [LayerType.TownSlots]       = new TownSlotSwatch(Hex.INVALID_SLOT_INDEX),
                [LayerType.Roads]           = new RoadSwatch(false),
                [LayerType.TradeRoutes]     = new TradeRouteSwatch(false),
                [LayerType.Impassable]      = new NogoSwatch(false)
            };
        }

        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var tool = Tool as EraserTool;
            if (tool != null)
            {
                var hex         = (parameter as ViewportToolParameters).Hex;
                var index       = (parameter as ViewportToolParameters).HexIndex;
                var layer       = (parameter as ViewportToolParameters).Layer;
                var canDisplay  = (parameter as ViewportToolParameters).CanDisplay;
                var brushSize   = tool.GetBrushSize();
                var swatch      = CLEAR_SWATCHES[layer.Type];

                var paintData = new PaintData()
                {
                    Layer       = layer,
                    HitHexIndex = index,
                    HitHex      = hex,
                    Swatch      = swatch,
                    CanDisplay  = canDisplay,
                    BrushSize   = brushSize
                };

                tool.Painter.Paint(paintData);
            }
        }
    }

    public class FloodFillCommand : ViewportCommand
    {
        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var tool = Tool as FloodFillTool;
            if (tool != null)
            {
                var hex         = (parameter as ViewportToolParameters).Hex;
                var index       = (parameter as ViewportToolParameters).HexIndex;
                var layer       = (parameter as ViewportToolParameters).Layer;
                var swatch      = (parameter as ViewportToolParameters).Swatch;
                var canDisplay  = (parameter as ViewportToolParameters).CanDisplay;
                
                var paintData = new PaintData()
                {
                    Layer       = layer,
                    HitHexIndex = index,
                    HitHex      = hex,
                    Swatch      = swatch,
                    CanDisplay  = canDisplay,
                    BrushSize   = 0
                };

                tool.Painter.Paint(paintData);
            }
        }
    }

    public class LineToolCommand : ViewportCommand
    {
        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var tool = Tool as LineTool;
            if (tool != null)
            {
                var hex         = (parameter as ViewportToolParameters).Hex;
                var index       = (parameter as ViewportToolParameters).HexIndex;
                var layer       = (parameter as ViewportToolParameters).Layer;
                var swatch      = (parameter as ViewportToolParameters).Swatch;
                var canDisplay  = (parameter as ViewportToolParameters).CanDisplay;
                
                var paintData = new PaintData()
                {
                    Layer       = layer,
                    HitHexIndex = index,
                    HitHex      = hex,
                    Swatch      = swatch,
                    CanDisplay  = canDisplay,
                    BrushSize   = 1
                };

                tool.Painter.Paint(paintData);
            }
        }
    }

    public class ColorPickerToolCommand : ViewportCommand
    {
        private readonly SidebarViewModel sidebarVM;

        public ColorPickerToolCommand(SidebarViewModel sidebarVM)
        {
            this.sidebarVM = sidebarVM;
        }

        public override void Execute(object parameter)
        {
            base.Execute(parameter);

            var hex         = (parameter as ViewportToolParameters).Hex;
            var hexIndex    = (parameter as ViewportToolParameters).HexIndex;
            var layer       = (parameter as ViewportToolParameters).Layer;

            var swatchesVM  = sidebarVM.SwatchesVM;
            var swatches    = swatchesVM.Swatches[layer.Type];

            int swatchIndex;
            if (layer.Type == LayerType.Regions) //regions are weird because colors can be reused
            {
                var regionId = hex.RegionId;

                swatchIndex = swatches.FindIndex(swatch => ((RegionSwatch)swatch).RegionIndex == regionId);
            }
            else
            {
                var color = layer.Colours[hexIndex];
                swatchIndex = swatches.FindIndex(swatch => swatch.Colour == color);
            }

            swatchesVM.SetActiveSwatches(layer.Type, swatchIndex);
        }
    }
}
