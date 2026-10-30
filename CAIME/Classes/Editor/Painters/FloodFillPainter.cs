using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME.Painters
{
    public class FloodFillPainter : AbstractViewportPainter
    {
        // One record per hex that was changed by the fill.
        private class HexRecord
        {
            public int    HexIndex;
            public Swatch OldSwatch;   // value BEFORE the fill
            public Swatch NewSwatch;   // value AFTER  the fill (the user's chosen swatch)
        }

        private class FloodFillPaintStateSnapshot : PaintStateSnapshot
        {
            // Stores the per-hex before/after swatches collected during a single fill.
            public List<HexRecord> Records { get; } = new List<HexRecord>();

            public override int RecordCount => Records.Count;
        }

        private Func<Hex, Hex, bool> _groundTypeConditionMatch;
        private Func<Hex, Hex, bool> _climateConditionMatch;
        private Func<Hex, Hex, bool> _attritionConditionMatch;
        private Func<Hex, Hex, bool> _regionsConditionMatch;
        private Func<Hex, Hex, bool> _nogoConditionMatch;
        private Func<Hex, Hex, bool> _currentConditionMatch;
        private Func<Hex, Hex, bool> _roadsConditionMatch;
        private Func<Hex, Hex, bool> _tradeRoutesConditionMatch;
        private Func<Hex, Hex, bool> _restrictionsConditionMatch;

        private Layer _sourceLayer;
        private Layer _targetLayer;

        public FloodFillPainter(ViewportViewModel vvm, EditorViewModel evm) : base(vvm, evm)
        {
            _groundTypeConditionMatch   = new Func<Hex, Hex, bool>((hex, nbr) => hex.GroundTypeIndex == nbr.GroundTypeIndex);
            _climateConditionMatch      = new Func<Hex, Hex, bool>((hex, nbr) => hex.ClimateIndex    == nbr.ClimateIndex);
            _attritionConditionMatch    = new Func<Hex, Hex, bool>((hex, nbr) => hex.AttritionIndex  == nbr.AttritionIndex);
            _regionsConditionMatch      = new Func<Hex, Hex, bool>((hex, nbr) => hex.RegionId        == nbr.RegionId);
            _nogoConditionMatch         = new Func<Hex, Hex, bool>((hex, nbr) => hex.IsImpassable    == nbr.IsImpassable);
            _roadsConditionMatch        = new Func<Hex, Hex, bool>((hex, nbr) => hex.IsRoad          == nbr.IsRoad);
            _tradeRoutesConditionMatch  = new Func<Hex, Hex, bool>((hex, nbr) => hex.IsTradeRoute    == nbr.IsTradeRoute);
            _restrictionsConditionMatch = new Func<Hex, Hex, bool>((hex, nbr) => hex.RestrictionLvl  == nbr.RestrictionLvl);
        }

        public bool SetSource(Layer layer)
        {
            switch (layer.Type)
            {
                case LayerType.GroundTypes:
                    _currentConditionMatch = _groundTypeConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Attritions:
                    _currentConditionMatch = _attritionConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Climates:
                    _currentConditionMatch = _climateConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Regions:
                    _currentConditionMatch = _regionsConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Impassable:
                    _currentConditionMatch = _nogoConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Roads:
                    _currentConditionMatch = _roadsConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.TradeRoutes:
                    _currentConditionMatch = _tradeRoutesConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.Restrictions:
                    _currentConditionMatch = _restrictionsConditionMatch;
                    _sourceLayer = layer;
                    return true;
                case LayerType.CampaignMasks:
                    var maskLayer = (CampaignMaskLayer)layer;
                    _currentConditionMatch = (hex, nbr) => maskLayer.IsMasked(hex) == maskLayer.IsMasked(nbr);
                    _sourceLayer = layer;
                    return true;
            }

            _sourceLayer = null;
            return false;
        }

        public override bool Paint(PaintData data)
        {
            if (paintingState == PaintState.Painting)
            {
                // Optimization: Don't allow mouse move painting mode. We only really need to click once.
                return false;
            }

            if (base.Paint(data) == false)
            {
                return false;
            }

            if (_sourceLayer == null)
            {
                MessageBox.Show("The selected Flood Fill source is unsupported. Please, choose another source.", "Flood Fill source layer error.");
                return false;
            }

            if (_sourceLayer.IsVisible == false)
            {
                MessageBox.Show("The selected Flood Fill source layer has to be visible!", "Flood Fill source layer error.");
                return false;
            }

            _targetLayer = data.Layer;
            this.FloodFill(data, _currentConditionMatch);

            return true;
        }

        public override bool PaintEnd(Point mousePos)
        {
            if (base.PaintEnd(mousePos) == false)
            {
                return false;
            }

            if (_targetLayer != null)
            {
                project.ColourTable.SetColours(_targetLayer, true);
                _targetLayer = null;

                CreateRestoreAction("Flood fill paint");

                return true;
            }

            return false;
        }

        private void FloodFill(PaintData data, Func<Hex, Hex, bool> fillConditionMatched)
        {
            var isVisited   = new bool[project.MapHexFile.Capacity];
            var queue       = new Queue<Hex>();
            var start       = data.HitHex;
            var startIndex  = HexGridUtility.IndexFromCoords(start.R, start.Q, (int)project.MapHexFile.MapWidth);

            queue.Enqueue(start);
            isVisited[startIndex] = true;

            // Collect indices in BFS order so we can read old colours before painting.
            var hexIndices = new List<int>();
            hexIndices.Add(startIndex);

            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = project.MapHexFile.GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                        continue;

                    if (isVisited[nbrIndex])
                        continue;

                    var nbr = project.MapHexFile.HexData[nbrIndex];
                    if (fillConditionMatched(hex, nbr))
                    {
                        hexIndices.Add(nbrIndex);
                        queue.Enqueue(nbr);
                        isVisited[nbrIndex] = true;
                    }
                }
            }

            // A beach fill target may include hexes that aren't eligible for beach (e.g. inland
            // hexes swept in via a different source layer's match) - drop those before recording
            // or applying anything, so undo/redo don't need to re-check eligibility either.
            hexIndices.RemoveAll(hexIndex => CanPaintHex(data.Swatch, hexIndex) == false);

            // Snapshot old values BEFORE applying the new swatch so every hex gets
            // its own individual pre-fill swatch recorded correctly.
            FillSnapshotStates(data.Swatch, hexIndices);

            // Apply the new swatch to every hex in the filled region.
            foreach (var hexIndex in hexIndices)
            {
                data.Swatch.Apply(project.MapHexFile.HexData[hexIndex]);
            }
        }

        // -------------------------------------------------------------------------
        // RestoreState – replays the OLD swatch per hex (true undo)
        // -------------------------------------------------------------------------

        public override void RestoreState(StateSnapshot snapshot)
        {
            if (snapshot is FloodFillPaintStateSnapshot paintSnapshot)
            {
                foreach (var record in paintSnapshot.Records)
                {
                    record.OldSwatch.Apply(project.MapHexFile.HexData[record.HexIndex]);
                }

                project.ColourTable.SetColours(paintSnapshot.Layer, true);

                LoggerViewModel.Log("Flood fill paint action undone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("FloodFillViewportPainter.RestoreState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        // -------------------------------------------------------------------------
        // ApplyState – replays the NEW swatch per hex (true redo)
        // -------------------------------------------------------------------------

        public override void ApplyState(StateSnapshot snapshot)
        {
            if (snapshot is FloodFillPaintStateSnapshot paintSnapshot)
            {
                foreach (var record in paintSnapshot.Records)
                {
                    record.NewSwatch.Apply(project.MapHexFile.HexData[record.HexIndex]);
                }

                project.ColourTable.SetColours(paintSnapshot.Layer, true);

                LoggerViewModel.Log("Flood fill paint action redone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("FloodFillViewportPainter.ApplyState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        // -------------------------------------------------------------------------
        // InitialiseSnapshotStates
        //
        // The base class calls this once per paint gesture (before the first Paint
        // call).  We allocate fresh snapshots here; Records are populated later in
        // FillSnapshotStates.
        // -------------------------------------------------------------------------

        protected override void InitialiseSnapshotStates()
        {
            if (newState == null || oldState == null)
            {
                newState = new FloodFillPaintStateSnapshot();
                oldState = new FloodFillPaintStateSnapshot();
            }
        }

        // -------------------------------------------------------------------------
        // FillSnapshotStates
        //
        // Called BEFORE the swatch is applied so the layer colour array still holds
        // the pre-fill value for every hex.  Records a (hexIndex, oldSwatch,
        // newSwatch) entry per hex so undo/redo can operate independently on each.
        // -------------------------------------------------------------------------

        private void FillSnapshotStates(Swatch newSwatch, List<int> hexIndices)
        {
            if (hexIndices.Count == 0)
                return;

            var curLayer        = editorVM.GetActiveLayer();
            var oldSnapshot     = (FloodFillPaintStateSnapshot)oldState;
            var newSnapshot     = (FloodFillPaintStateSnapshot)newState;

            foreach (var hexIndex in hexIndices)
            {
                var hex         = project.MapHexFile.HexData[hexIndex];
                var oldSwatch   = editorVM.GetSwatchForHex(curLayer.Type, hex)
                                  ?? EraserToolCommand.ClearSwatchFor(curLayer);

                var record = new HexRecord
                {
                    HexIndex  = hexIndex,
                    OldSwatch = oldSwatch,
                    NewSwatch = newSwatch,
                };

                oldSnapshot.Records.Add(record);
                newSnapshot.Records.Add(record);
            }
        }
    }
}
