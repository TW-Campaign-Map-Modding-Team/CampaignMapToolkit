using System;
using System.Collections.Generic;
using SharpDX;

namespace CAIME.Painters
{
    public class BrushPainter : AbstractViewportPainter
    {
        private class HexSnapshot
        {
            public Swatch   Swatch;
            public int      HexIndex;
        }

        private class BrushPaintStateSnapshot : PaintStateSnapshot
        {
            private class HexSnapshotEqualityComparer : IEqualityComparer<HexSnapshot>
            {
                public bool Equals(HexSnapshot x, HexSnapshot y)
                {
                    return x.HexIndex == y.HexIndex;
                }

                public int GetHashCode(HexSnapshot obj)
                {
                    return obj.HexIndex.GetHashCode();
                }
            }

            public HashSet<HexSnapshot> SnapshotData { get; private set; }

            public override int RecordCount => SnapshotData.Count;

            public BrushPaintStateSnapshot()
            {
                SnapshotData = new HashSet<HexSnapshot>(new HexSnapshotEqualityComparer());
            }

            public void AddData(HexSnapshot data)
            {
                SnapshotData.Add(data);
            }
        }

        public BrushPainter(ViewportViewModel vvm, EditorViewModel evm) : base(vvm, evm)
        {
            customBrushSize = true;
        }

        public override bool PaintStart(System.Windows.Point mousePos)
        {
            if (base.PaintStart(mousePos) == false)
            {
                return false;
            }

            return true;
        }

        public override bool Paint(PaintData data)
        {
            if (base.Paint(data) == false)
            {
                return true;
            }

            var tb = data.BrushSize - 1;
            var hex = data.HitHex;

            // Offset -> cube conversion depends on each hex's own column parity, so it is not
            // translation invariant and cannot be applied to a coordinate delta. Both the centre
            // and the candidate are converted, which is what Hex.GetDistance does.
            var centreCube = Hex.OffsetOddQ_ToCube(hex.Q, hex.R);

            // Set color of area surrounding a center hex, based on what the brush size it
            for (int col = hex.Q - tb; col <= tb + hex.Q; col++)
            {
                for (int row = hex.R - tb; row <= tb + hex.R; row++)
                {
                    int distance = Hex.CubeDistance(centreCube, Hex.OffsetOddQ_ToCube(col, row));
            
                    // only paint hexes that are within the brushSize (radius)
                    if (distance <= tb)
                    {
                        int newIndex = HexGridUtility.IndexFromCoords(row, col, (int)project.MapHexFile.MapWidth);

                        // if outside the bounds of map, do not paint
                        if (newIndex >= 0 &&
                            (row >= 0 && row < project.MapHexFile.MapHeight) &&
                            (col >= 0 && col < project.MapHexFile.MapWidth) &&
                            CanPaintHex(data.Swatch, newIndex))
                        {
                            FillSnapshotStates(data.Layer, data.Swatch, newIndex);
                            ApplyValue(data.Layer, data.Swatch, newIndex, data.CanDisplay);
                        }
                    }
                }
            }

            return true;
        }
        
        public override bool PaintEnd(System.Windows.Point mousePos)
        {
            if (base.PaintEnd(mousePos) == false)
            {
                return false;
            }

            CreateRestoreAction("Hexes paint");

            lastHex = null;
            return true;
        }

        private void ApplyValue(Layer layer, Swatch swatch, int hexIndex, bool canDisplay)
        {
            if (swatch == null)
            {
                return;
            }

            var hex = project.MapHexFile.HexData[hexIndex];
            swatch.Apply(hex);
            SetColour(layer, swatch, hexIndex, canDisplay);
        }

        private void SetColour(Layer layer, Swatch swatch, int hexIndex, bool canDisplay)
        {
            layer.Colours[hexIndex] = swatch.Colour;

            if (canDisplay)
            {
                editorVM.RefreshHex(hexIndex);
            }
        }

        public override void RestoreState(StateSnapshot snapshot)
        {
            if (snapshot is BrushPaintStateSnapshot)
            {
                var paintSnapshot = snapshot as BrushPaintStateSnapshot;
                var layer = paintSnapshot.Layer;

                foreach (var data in paintSnapshot.SnapshotData)
                {
                    var canDisplay = layer.IsVisible && editorVM.CanDisplay(layer, data.HexIndex);
                    this.ApplyValue(layer, data.Swatch, data.HexIndex, canDisplay);
                }

                LoggerViewModel.Log("Hexes paint action undone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("NormalViewportPainter.RestoreState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        public override void ApplyState(StateSnapshot snapshot)
        {
            if (snapshot is BrushPaintStateSnapshot)
            {
                var paintSnapshot = snapshot as BrushPaintStateSnapshot;
                var layer = paintSnapshot.Layer;

                foreach (var data in paintSnapshot.SnapshotData)
                {
                    var canDisplay = layer.IsVisible && editorVM.CanDisplay(layer, data.HexIndex);
                    this.ApplyValue(layer, data.Swatch, data.HexIndex, canDisplay);
                }

                LoggerViewModel.Log("Hexes paint action redone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("NormalViewportPainter.ApplyState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        protected override void InitialiseSnapshotStates()
        {
            if (newState == null || oldState == null)
            {
                newState = new BrushPaintStateSnapshot();
                oldState = new BrushPaintStateSnapshot();
            }
        }

        private void FillSnapshotStates(Layer layer, Swatch newSwatch, int hexIndex)
        {
            // Look the old swatch up by the hex's model value, not its colour - several
            // swatches share a colour, which made colour-based undo restore the wrong one.
            var hex         = project.MapHexFile.HexData[hexIndex];
            var oldSwatch   = editorVM.GetSwatchForHex(layer.Type, hex);

            if (oldSwatch == null)
            {
                oldSwatch = EraserToolCommand.CLEAR_SWATCHES[layer.Type];
            }

            var oldChange = new HexSnapshot()
            {
                HexIndex    = hexIndex,
                Swatch      = oldSwatch,
            };

            (oldState as BrushPaintStateSnapshot).AddData(oldChange);

            var newChange = new HexSnapshot()
            {
                HexIndex    = hexIndex,
                Swatch      = newSwatch,
            };

            (newState as BrushPaintStateSnapshot).AddData(newChange);
        }
    }
}
