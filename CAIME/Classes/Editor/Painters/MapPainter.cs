using System;
using System.Collections.Generic;

namespace CAIME.Painters
{
    public sealed class MapPainter
    {
        private readonly MapHexFile _map;

        public MapPainter(MapHexFile map)
        {
            _map = map;
        }

        public static bool CanPaintHex(MapHexFile map, Swatch swatch, int hexIndex)
        {
            if (swatch is BeachSwatch beachSwatch && beachSwatch.IsBeach)
            {
                return map.IsEligibleForBeach(map.HexData[hexIndex]);
            }

            return true;
        }

        public List<int> PaintBrush(Hex centre, int brushSize, Swatch swatch)
        {
            var area = HexShapes.BrushArea(centre, brushSize, (int)_map.MapWidth, (int)_map.MapHeight);
            return Apply(swatch, area);
        }

        public List<int> PaintLine(Hex from, Hex to, Swatch swatch)
        {
            var width       = (int)_map.MapWidth;
            var startIndex  = HexGridUtility.GetHexIndex(from, width);
            var finishIndex = HexGridUtility.GetHexIndex(to, width);

            var hexes = new List<int> { startIndex, finishIndex };
            hexes.AddRange(HexShapes.LineBetween(from, to, width));

            return Apply(swatch, hexes);
        }

        public List<int> FloodFill(Hex seed, Swatch swatch, Func<Hex, Hex, bool> match)
        {
            var region = HexShapes.FloodRegion(_map, HexGridUtility.GetHexIndex(seed, (int)_map.MapWidth), match);
            return Apply(swatch, region);
        }

        private List<int> Apply(Swatch swatch, IEnumerable<int> hexIndices)
        {
            var painted = new List<int>();

            foreach (var hexIndex in hexIndices)
            {
                if (CanPaintHex(_map, swatch, hexIndex))
                {
                    swatch.Apply(_map.HexData[hexIndex]);
                    painted.Add(hexIndex);
                }
            }

            if (painted.Count > 0)
            {
                _map.SetDirty();
            }

            return painted;
        }
    }
}
