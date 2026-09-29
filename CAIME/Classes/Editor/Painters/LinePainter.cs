using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME.Painters
{
    public class LinePainter : AbstractViewportPainter
    {
        private class HexSnapshot
        {
            public Swatch   Swatch;
            public int      HexIndex;
        }

        private class LinePaintStateSnapshot : PaintStateSnapshot
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

            public LinePaintStateSnapshot()
            {
                SnapshotData = new HashSet<HexSnapshot>(new HexSnapshotEqualityComparer());
            }

            public void AddData(HexSnapshot data)
            {
                SnapshotData.Add(data);
            }
        }

        private class RestoreInfo
        {
            public int HexIndex;
            public int LayerColour;
        }

        private static int END_POINT_COLOUR = -13521318;
        private static int MID_POINT_COLOUR = -1382640;

        private Hex srcHex;
        private List<int> path;
        private Dictionary<int, RestoreInfo> restoreInfo;

        /// <summary>
        /// Layer the preview sentinel colours were written to. Reset/commit must target this
        /// layer, not whatever layer is active when the gesture ends.
        /// </summary>
        private Layer previewLayer;

        public LinePainter(ViewportViewModel vvm, EditorViewModel evm) : base(vvm, evm)
        {
            srcHex      = null;
            path        = new List<int>();
            restoreInfo = new Dictionary<int, RestoreInfo>();
        }

        public override bool PaintStart(Point mousePos)
        {
            if (base.PaintStart(mousePos) == false)
            {
                return false;
            }

            srcHex = viewportVM.FindHitHex(mousePos, project.MapHexFile);
            return true;
        }

        public override bool Paint(PaintData data)
        {
            if (base.Paint(data) == false)
            {
                return false;
            }

            if (srcHex == null)
            {
                return false;
            }

            if (lastHex == null)
            {
                return false;
            }

            if (lastHex.GetDistance(srcHex) < 1)
            {
                return false;
            }

            Reset();
            previewLayer = data.Layer;
            AddToPath(srcHex);
            FindPath(srcHex, lastHex);
            AddToPath(lastHex);
            UpdatePathColours();

            return true;
        }

        public override bool PaintEnd(Point mousePos)
        {
            // If the active layer changed mid-gesture, the active swatch belongs to the new
            // layer - committing would corrupt the previewed layer, so cancel the line instead.
            if (lastHex != null && previewLayer != null && editorVM.GetActiveLayer() == previewLayer &&
                CanCreateLine(srcHex, lastHex) && path.Count > 0)
            {
                SetLineHexes();
            }
            else
            {
                Reset();
            }

            CreateRestoreAction("Line paint");
            srcHex = null;
            previewLayer = null;
            return base.PaintEnd(mousePos);
        }

        public override void OnDeactivated()
        {
            // Switching tools mid-gesture must not leave preview sentinel colours in the
            // layer's colour data.
            Reset();
            srcHex = null;
            previewLayer = null;

            base.OnDeactivated();
        }

        public override void Shutdown()
        {
            restoreInfo.Clear();
            path.Clear();
            srcHex = null;
            previewLayer = null;

            base.Shutdown();
        }

        private void FindPath(Hex src, Hex dst)
        {
            // Convert offset coordinates to cube coordinates
            int cube_q1 = src.Q;
            int cube_r1 = src.R - (src.Q - (src.Q & 1)) / 2;
            int cube_s1 = -cube_q1 - cube_r1;

            int cube_q2 = dst.Q;
            int cube_r2 = dst.R - (dst.Q - (dst.Q & 1)) / 2;
            int cube_s2 = -cube_q2 - cube_r2;

            // Calculate the number of steps needed for interpolation
            int numSteps = Math.Max(Math.Abs(cube_q2 - cube_q1), Math.Max(Math.Abs(cube_r2 - cube_r1), Math.Abs(cube_s2 - cube_s1)));

            // Calculate the step size for interpolation
            float stepSize = 1.0f / numSteps;

            float Lerp(int a, int b, float t)
            {
                return a + (b - a) * t;
            }

            // Interpolate between the two points and add each interpolated hexagon to the list
            for (int i = 1; i <= numSteps - 1; i++)
            {
                float t = i * stepSize;
                float q_pos = Lerp(cube_q1, cube_q2, t);
                float r_pos = Lerp(cube_r1, cube_r2, t);
                float s_pos = Lerp(cube_s1, cube_s2, t);

                //This part is a special type of rounding
                int q = (int)Math.Round(q_pos);
                int r = (int)Math.Round(r_pos);
                int s = (int)Math.Round(s_pos);

                var q_diff = Math.Abs(q - q_pos);
                var r_diff = Math.Abs(r - r_pos);
                var s_diff = Math.Abs(s - s_pos);

                if (q_diff > r_diff && q_diff > s_diff)
                {
                    q = -(r + s);
                }
                else if (r_diff > s_diff)
                {
                    r = -(q + s);
                }
                else
                {
                    s = -(q + r);
                }
                int col = q;
                int row = r + (q - (q & 1)) / 2;

                //Add hexagon to list
                path.Add(HexGridUtility.IndexFromCoords(row, col, (int)project.MapHexFile.MapWidth));
            }
        }

        private void AddToPath(Hex hex)
        {
            int hexIndex = HexGridUtility.IndexFromCoords(hex.R, hex.Q, (int)project.MapHexFile.MapWidth);
            if (path.IndexOf(hexIndex) == -1)
            {
                path.Add(hexIndex);
            }
        }

        private void UpdatePathColours()
        {
            var srcIndex = path[0];
            var dstIndex = path[path.Count-1];
            var curLayer = previewLayer;

            for (int i = 1; i < path.Count-1; ++i)
            {
                var hexIndex  = path[i];
                int midColour = MID_POINT_COLOUR;

                SetCellColour(curLayer, hexIndex, midColour);
            }

            SetCellColour(curLayer, srcIndex, END_POINT_COLOUR);
            SetCellColour(curLayer, dstIndex, END_POINT_COLOUR);
        }

        private void SetLineHexes()
        {
            var startIndex  = path[0];
            var finishIndex = path[path.Count - 1];
            var curLayer    = previewLayer;
            var curSwatch   = editorVM.GetActiveSwatch();

            var midHexes = new int[path.Count - 2];
            for (int i = 0; i < midHexes.Length; ++i)
            {
                midHexes[i] = path[i + 1];
            }

            Reset();

            if (CanPaintHex(curSwatch, startIndex))
            {
                FillSnapshotStates(curLayer, curSwatch, startIndex);
                ApplyValue(curLayer, curSwatch, startIndex, CanDisplayOn(curLayer, startIndex));
            }

            if (CanPaintHex(curSwatch, finishIndex))
            {
                FillSnapshotStates(curLayer, curSwatch, finishIndex);
                ApplyValue(curLayer, curSwatch, finishIndex, CanDisplayOn(curLayer, finishIndex));
            }

            for (int i = 0; i < midHexes.Length; ++i)
            {
                if (CanPaintHex(curSwatch, midHexes[i]))
                {
                    FillSnapshotStates(curLayer, curSwatch, midHexes[i]);
                    ApplyValue(curLayer, curSwatch, midHexes[i], CanDisplayOn(curLayer, midHexes[i]));
                }
            }
        }

        private bool CanDisplayOn(Layer layer, int hexIndex)
        {
            return layer.IsVisible && editorVM.CanDisplay(layer, hexIndex);
        }

        private void ApplyValue(Layer layer, Swatch swatch, int hexIndex, bool canDisplay)
        {
            var hex = project.MapHexFile.GetHex(hexIndex);
            swatch.Apply(hex);
            layer.Colours[hexIndex] = swatch.Colour;

            if (canDisplay)
            {
                editorVM.RefreshHex(hexIndex);
            }
        }

        private void SetCellColour(Layer curLayer, int cellIndex, int newColour)
        {
            AddRestoreInfo(cellIndex);

            curLayer.Colours[cellIndex] = newColour;
            viewportVM.UpdateCellColour(newColour, cellIndex, (int)project.MapHexFile.MapWidth, (int)project.MapHexFile.MapHeight);
        }

        private bool CanCreateLine(Hex src, Hex dst)
        {
            // Could put other stuff in here, per layer
            // Like maybe you can't draw a road through impassable terrain
            // Meh

            return src != dst;
        }

        private void Reset()
        {
            if (path.Count == 0)
            {
                return;
            }

            // The preview was painted on previewLayer - restore that layer, regardless of
            // which layer is active now. Without a project the layer data is gone anyway.
            if (previewLayer == null || project == null)
            {
                restoreInfo.Clear();
                path.Clear();
                return;
            }

            for (int i = 0; i < path.Count; ++i)
            {
                int hexIndex = path[i];
                var info = restoreInfo[hexIndex];

                previewLayer.Colours[info.HexIndex] = info.LayerColour;
                editorVM.RefreshHex(info.HexIndex);
            }

            restoreInfo.Clear();
            path.Clear();
        }

        private void AddRestoreInfo(int cellIndex)
        {
            if (restoreInfo.ContainsKey(cellIndex) == false)
            {
                restoreInfo[cellIndex]  = new RestoreInfo
                {
                    HexIndex    = cellIndex,
                    LayerColour = previewLayer.Colours[cellIndex],
                };
            }
        }

        public override void RestoreState(StateSnapshot snapshot)
        {
            if (snapshot is LinePaintStateSnapshot)
            {
                var paintSnapshot = snapshot as LinePaintStateSnapshot;
                var layer = paintSnapshot.Layer;

                foreach (var snapshotData in paintSnapshot.SnapshotData)
                {
                    var canDisplay = layer.IsVisible && editorVM.CanDisplay(layer, snapshotData.HexIndex);
                    this.ApplyValue(layer, snapshotData.Swatch, snapshotData.HexIndex, canDisplay);
                }

                LoggerViewModel.Log("Line paint action undone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("LineToolViewportPainter.RestoreState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        public override void ApplyState(StateSnapshot snapshot)
        {
            if (snapshot is LinePaintStateSnapshot)
            {
                var paintSnapshot = snapshot as LinePaintStateSnapshot;
                var layer = paintSnapshot.Layer;

                foreach (var snapshotData in paintSnapshot.SnapshotData)
                {
                    var canDisplay = layer.IsVisible && editorVM.CanDisplay(layer, snapshotData.HexIndex);
                    this.ApplyValue(layer, snapshotData.Swatch, snapshotData.HexIndex, canDisplay);
                }

                LoggerViewModel.Log("Line paint action redone.", LogLevel.Info);
            }
            else
            {
#if DEBUG
                LoggerViewModel.Log("LineToolViewportPainter.ApplyState() - unknown snapshot passed!", LogLevel.Warning);
#endif
            }
        }

        protected override void InitialiseSnapshotStates()
        {
            if (newState == null || oldState == null)
            {
                newState = new LinePaintStateSnapshot();
                oldState = new LinePaintStateSnapshot();
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

            (oldState as LinePaintStateSnapshot).AddData(oldChange);

            var newChange = new HexSnapshot()
            {
                HexIndex    = hexIndex,
                Swatch      = newSwatch,
            };

            (newState as LinePaintStateSnapshot).AddData(newChange);
        }
    }
}
