using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Where a settlement slot's buildings stand (<see cref="MapDataSlot.Positions"/>), worked out
    /// from the slot's hexes.
    /// </summary>
    public static class MapDataSlotPositions
    {
        /// <summary>
        /// Pharaoh, worked out from phar_main (all 181 settlements): a position is a hex where a
        /// building three hexes big - the hex, the hex north of it and the hex south-east of it -
        /// stands on the slot's open ground: hexes that are not beach and touch no other region. In
        /// a seven-hex slot that is its centre and its south-west hex, centre first. Where no
        /// building fits, as in a port town's four-hex slot, the position is the first open hex,
        /// column by column.
        /// </summary>
        public static void PlacePharaoh(MapDataModel model, MapDataSlotSettlement[] settlements)
        {
            foreach (var settlement in settlements.Where(settlement => settlement != null))
            {
                foreach (var slot in settlement.Slots)
                {
                    PlacePharaoh(model, slot);
                }
            }
        }

        private static void PlacePharaoh(MapDataModel model, MapDataSlot slot)
        {
            var slotHexes   = new HashSet<int>(slot.Hexes);
            var open        = new HashSet<int>(slot.Hexes.Where(hexIndex => IsOpen(model, hexIndex)));

            // Slot hexes are listed column by column; the builder lists the buildings the other way.
            var anchors = slot.Hexes
                .Where(hexIndex => open.Contains(hexIndex)
                                && open.Contains(North(model.Grid, hexIndex))
                                && open.Contains(SouthEast(model.Grid, hexIndex)))
                .Reverse()
                .ToList();

            if (anchors.Count == 0 && open.Count > 0)
            {
                anchors.Add(slot.Hexes.First(open.Contains));
            }

            slot.Positions.Clear();
            slot.Positions.AddRange(anchors);
        }

        private static bool IsOpen(MapDataModel model, int hexIndex)
        {
            var hex = model.Map.HexData[hexIndex];
            if (hex.IsBeach)
            {
                return false;
            }

            for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
            {
                int neighbour = model.Grid.Neighbour(hexIndex, n);
                if (neighbour >= 0 && model.Map.HexData[neighbour].RegionId != hex.RegionId)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The next row up, or -1 off the map.</summary>
        private static int North(MapDataGrid grid, int hexIndex)
        {
            int row = grid.Row(hexIndex) + 1;
            return row < grid.Height ? row * grid.Width + grid.Column(hexIndex) : -1;
        }

        /// <summary>The next column's lower neighbour, or -1 off the map.</summary>
        private static int SouthEast(MapDataGrid grid, int hexIndex)
        {
            int column  = grid.Column(hexIndex) + 1;
            int row     = MapDataGeometry.IsOddColumn(column - 1) ? grid.Row(hexIndex) : grid.Row(hexIndex) - 1;
            return column < grid.Width && row >= 0 ? row * grid.Width + column : -1;
        }
    }
}
