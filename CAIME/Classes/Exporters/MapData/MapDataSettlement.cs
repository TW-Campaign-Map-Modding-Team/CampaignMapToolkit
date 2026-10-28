using System.Collections.Generic;

namespace CAIME.Exporters
{
    /// <summary>
    /// A land region's settlement as map_data.esf records it: a hexagon of radius
    /// <see cref="RADIUS"/> around a centre hex, the hexagon's main slot hexes, and, when the
    /// hexagon holds port slot hexes, the port.
    /// </summary>
    public sealed class MapDataSettlement
    {
        public const int RADIUS         = 2;
        public const int FOOTPRINT_SIZE = RADIUS * 2 + 1;

        public int          CentreColumn    { get; private set; }
        public int          CentreRow       { get; private set; }

        /// <summary>Hexagon hexes in the main slot, column by column.</summary>
        public List<int>    MainSlotHexes   { get; } = new List<int>();

        /// <summary>Hexagon hexes in the port slot, column by column, wherever their region.</summary>
        public List<int>    PortSlotHexes   { get; } = new List<int>();

        public bool         HasPort         => PortSlotHexes.Count > 0;

        /// <summary>
        /// Where ships dock: the first hexagon hex at sea that is in either slot (a sea region's
        /// hex can be in the main slot), or -1 when the settlement has no port.
        /// </summary>
        public int          DockHex         => HasPort ? FirstSlotHexAtSea : -1;

        private int         FirstSlotHexAtSea { get; set; } = -1;

        /// <summary>
        /// The <see cref="FOOTPRINT_SIZE"/>-square around the centre, row by row from its lowest
        /// row, one bit per hex (lowest bit first), set for the hexes of the hexagon.
        /// </summary>
        public byte[]       FootprintBits   { get; private set; }

        /// <summary>
        /// The settlement of each region, null for sea regions and for land regions without main
        /// slot hexes.
        /// </summary>
        public static MapDataSettlement[] FindAll(MapHexFile map, MapDataGrid grid, int regionCount)
        {
            int landRegionCount = map.LandRegions.Count;
            var mainSlotHexes   = new List<int>[regionCount];

            for (int hexIndex = 0; hexIndex < grid.HexCount; ++hexIndex)
            {
                var hex = map.HexData[hexIndex];
                if (hex.RegionId < landRegionCount && hex.TownSlotIndex == Hex.MAIN_SLOT_INDEX)
                {
                    (mainSlotHexes[hex.RegionId] ?? (mainSlotHexes[hex.RegionId] = new List<int>())).Add(hexIndex);
                }
            }

            var settlements = new MapDataSettlement[regionCount];
            for (int region = 0; region < regionCount; ++region)
            {
                if (mainSlotHexes[region] != null)
                {
                    var settlement = new MapDataSettlement();
                    settlement.PlaceCentre(grid, mainSlotHexes[region]);
                    settlement.CollectSlots(map, grid);
                    settlement.FootprintBits = FootprintMask(settlement.CentreColumn, settlement.CentreRow);
                    settlements[region] = settlement;
                }
            }

            return settlements;
        }

        /// <summary>
        /// The centre is the hex whose hexagon holds all of the region's main slot hexes - in a
        /// well-formed map exactly one. Otherwise it is the one holding the most, the first column
        /// by column.
        /// </summary>
        private void PlaceCentre(MapDataGrid grid, List<int> mainSlotHexes)
        {
            int minColumn = int.MaxValue, minRow = int.MaxValue, maxColumn = int.MinValue, maxRow = int.MinValue;
            foreach (int hexIndex in mainSlotHexes)
            {
                minColumn   = System.Math.Min(minColumn, grid.Column(hexIndex));
                maxColumn   = System.Math.Max(maxColumn, grid.Column(hexIndex));
                minRow      = System.Math.Min(minRow, grid.Row(hexIndex));
                maxRow      = System.Math.Max(maxRow, grid.Row(hexIndex));
            }

            int bestCovered = -1;
            for (int column = minColumn - RADIUS; column <= maxColumn + RADIUS; ++column)
            {
                for (int row = minRow - RADIUS - 1; row <= maxRow + RADIUS + 1; ++row)
                {
                    int covered = 0;
                    foreach (int hexIndex in mainSlotHexes)
                    {
                        if (MapDataGrid.Distance(column, row, grid.Column(hexIndex), grid.Row(hexIndex)) <= RADIUS)
                        {
                            ++covered;
                        }
                    }

                    if (covered > bestCovered)
                    {
                        bestCovered     = covered;
                        CentreColumn    = column;
                        CentreRow       = row;
                    }
                }
            }
        }

        private void CollectSlots(MapHexFile map, MapDataGrid grid)
        {
            for (int column = CentreColumn - RADIUS; column <= CentreColumn + RADIUS; ++column)
            {
                for (int row = CentreRow - RADIUS - 1; row <= CentreRow + RADIUS + 1; ++row)
                {
                    if (column < 0 || column >= grid.Width || row < 0 || row >= grid.Height
                        || MapDataGrid.Distance(CentreColumn, CentreRow, column, row) > RADIUS)
                    {
                        continue;
                    }

                    int hexIndex    = row * grid.Width + column;
                    var hex         = map.HexData[hexIndex];

                    if (hex.TownSlotIndex == Hex.MAIN_SLOT_INDEX)
                    {
                        MainSlotHexes.Add(hexIndex);
                    }
                    else if (hex.TownSlotIndex == Hex.PORT_SLOT_INDEX)
                    {
                        PortSlotHexes.Add(hexIndex);
                    }
                    else
                    {
                        continue;
                    }

                    if (FirstSlotHexAtSea < 0 && hex.IsSea)
                    {
                        FirstSlotHexAtSea = hexIndex;
                    }
                }
            }
        }

        private static byte[] FootprintMask(int centreColumn, int centreRow)
        {
            var bits = new byte[(FOOTPRINT_SIZE * FOOTPRINT_SIZE + 7) / 8];

            for (int bit = 0; bit < FOOTPRINT_SIZE * FOOTPRINT_SIZE; ++bit)
            {
                int column  = centreColumn - RADIUS + bit % FOOTPRINT_SIZE;
                int row     = centreRow - RADIUS + bit / FOOTPRINT_SIZE;

                if (MapDataGrid.Distance(centreColumn, centreRow, column, row) <= RADIUS)
                {
                    bits[bit / 8] |= (byte)(1 << (bit % 8));
                }
            }

            return bits;
        }
    }
}
