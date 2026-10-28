using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// A land region's settlement as Warhammer III's and Three Kingdoms' map_data.esf record it: a
    /// footprint, a logical and a visual position, its main slot hexes and, when it has one, its
    /// port. The two games' builders find them differently (<see cref="FindAll"/>,
    /// <see cref="FindAllThreeKingdoms"/>).
    /// </summary>
    public sealed class MapDataSettlement
    {
        /// <summary>Warhammer III's settlement radius.</summary>
        public const int RADIUS                 = 2;
        public const int FOOTPRINT_SIZE         = RADIUS * 2 + 1;

        /// <summary>Three Kingdoms' settlements are a hexagon of this many hexes, main slot and port together.</summary>
        private static readonly int[] THREE_KINGDOMS_HEXAGON_SIZES = { 7, 19 };

        /// <summary>Where the settlement stands as far as the game's rules go.</summary>
        public int          LogicalColumn   { get; private set; }
        public int          LogicalRow      { get; private set; }

        /// <summary>Where the settlement is drawn, and where the campaign UI puts it.</summary>
        public int          CentreColumn    { get; private set; }
        public int          CentreRow       { get; private set; }

        /// <summary>Main slot hexes, column by column.</summary>
        public List<int>    MainSlotHexes   { get; } = new List<int>();

        /// <summary>Port slot hexes, column by column, wherever their region.</summary>
        public List<int>    PortSlotHexes   { get; } = new List<int>();

        public bool         HasPort         => PortSlotHexes.Count > 0;

        /// <summary>Where ships dock, or -1.</summary>
        public int          DockHex         { get; private set; } = -1;

        public int          FootprintMinColumn  { get; private set; }
        public int          FootprintMinRow     { get; private set; }
        public int          FootprintMaxColumn  { get; private set; }
        public int          FootprintMaxRow     { get; private set; }

        /// <summary>The footprint rectangle, row by row from its lowest row, one bit per hex (lowest bit first).</summary>
        public byte[]       FootprintBits   { get; private set; }

        /// <summary>
        /// Warhammer III: the settlement is a hexagon of radius <see cref="RADIUS"/> around a
        /// centre hex, holding the region's main slot hexes; the logical and visual positions are
        /// that centre; and when the hexagon holds port slot hexes, ships dock at its first hex at
        /// sea in either slot. Null for sea regions and land regions without main slot hexes.
        /// </summary>
        public static MapDataSettlement[] FindAll(MapHexFile map, MapDataGrid grid, int regionCount)
        {
            var mainSlotHexes   = MainSlotHexesByRegion(map, grid, regionCount);
            var settlements     = new MapDataSettlement[regionCount];

            for (int region = 0; region < regionCount; ++region)
            {
                if (mainSlotHexes[region] != null)
                {
                    var settlement = new MapDataSettlement();
                    settlement.PlaceCentre(grid, mainSlotHexes[region]);
                    settlement.LogicalColumn    = settlement.CentreColumn;
                    settlement.LogicalRow       = settlement.CentreRow;
                    settlement.CollectSlots(map, grid);
                    settlement.SetHexagonFootprint();
                    settlements[region] = settlement;
                }
            }

            return settlements;
        }

        /// <summary>
        /// Three Kingdoms, as its Assembly Kit's builder places them (read from the DLL; all 201
        /// settlements of 3k_dlc07_main_map match):
        ///
        /// <list type="bullet">
        /// <item>From the region's first land hex with a town slot, column by column, the sprawl
        /// fill (<see cref="MapDataSprawlFill"/>) makes the settlement: its bounds and hexes the
        /// footprint, its main and port slot hexes the slots, its first open sea hex the dock.</item>
        /// <item>The visual position is the middle of the main and port slot hexes taken column by
        /// column, which the builder only accepts when they make a hexagon of 7 or 19 hexes.</item>
        /// <item>The logical position, which is also the port's, is the land hex of those nearest
        /// the visual position by column plus row difference, the main slot's first on a tie.</item>
        /// </list>
        /// </summary>
        public static MapDataSettlement[] FindAllThreeKingdoms(MapDataModel model)
        {
            var map         = model.Map;
            var grid        = model.Grid;
            var settlements = new MapDataSettlement[model.RegionCount];

            for (int region = 0; region < map.LandRegions.Count; ++region)
            {
                var bounds = model.RegionBounds[region];
                if (bounds.HexCount == 0)
                {
                    continue;
                }

                int seed = -1;
                for (int column = bounds.MinColumn; column <= bounds.MaxColumn && seed < 0; ++column)
                {
                    for (int row = bounds.MinRow; row <= bounds.MaxRow && seed < 0; ++row)
                    {
                        int hexIndex    = row * grid.Width + column;
                        var hex         = map.HexData[hexIndex];
                        if (hex.RegionId == region && IsPlainLand(hex) && hex.TownSlotIndex >= 0)
                        {
                            seed = hexIndex;
                        }
                    }
                }

                if (seed >= 0)
                {
                    settlements[region] = ThreeKingdomsSettlement(model, seed, region);
                }
            }

            return settlements;
        }

        private static MapDataSettlement ThreeKingdomsSettlement(MapDataModel model, int seed, int region)
        {
            var grid        = model.Grid;
            var filled      = MapDataSprawlFill.Fill(model, new[] { seed }, region);
            var settlement  = new MapDataSettlement { DockHex = MapDataSprawlFill.Dock(model, filled) };

            settlement.MainSlotHexes.AddRange(filled.Where(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == Hex.MAIN_SLOT_INDEX));
            settlement.PortSlotHexes.AddRange(filled.Where(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == Hex.PORT_SLOT_INDEX));
            settlement.SetFootprint(grid, filled);

            var slots = settlement.MainSlotHexes.Concat(settlement.PortSlotHexes).ToList();
            var byColumn = slots.OrderBy(grid.Column).ThenBy(grid.Row).ToList();

            if (THREE_KINGDOMS_HEXAGON_SIZES.Contains(slots.Count) == false || byColumn.Count == 0)
            {
                // The builder rejects such a settlement; CAIME places it as best it can.
                byColumn = byColumn.Count > 0 ? byColumn : new List<int> { seed };
            }

            int centre = byColumn[byColumn.Count / 2];
            settlement.CentreColumn = grid.Column(centre);
            settlement.CentreRow    = grid.Row(centre);

            int logical = slots
                .Where(hexIndex => IsPlainLand(model.Map.HexData[hexIndex]))
                .OrderBy(hexIndex => Math.Abs(grid.Column(hexIndex) - settlement.CentreColumn) + Math.Abs(grid.Row(hexIndex) - settlement.CentreRow))
                .DefaultIfEmpty(centre)
                .First();

            settlement.LogicalColumn    = grid.Column(logical);
            settlement.LogicalRow       = grid.Row(logical);
            return settlement;
        }

        /// <summary>Land as the builder types it: not sea, beach, cliff or impassable.</summary>
        private static bool IsPlainLand(Hex hex)
        {
            return hex.IsSea == false && hex.IsBeach == false && hex.IsCliff == false && hex.IsPassable;
        }

        private static List<int>[] MainSlotHexesByRegion(MapHexFile map, MapDataGrid grid, int regionCount)
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

            return mainSlotHexes;
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
                minColumn   = Math.Min(minColumn, grid.Column(hexIndex));
                maxColumn   = Math.Max(maxColumn, grid.Column(hexIndex));
                minRow      = Math.Min(minRow, grid.Row(hexIndex));
                maxRow      = Math.Max(maxRow, grid.Row(hexIndex));
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
            int firstSlotHexAtSea = -1;

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

                    if (firstSlotHexAtSea < 0 && hex.IsSea)
                    {
                        firstSlotHexAtSea = hexIndex;
                    }
                }
            }

            // A sea region's hex can be in the main slot; ships only dock where there is a port.
            DockHex = HasPort ? firstSlotHexAtSea : -1;
        }

        /// <summary>The <see cref="FOOTPRINT_SIZE"/>-square around the centre, set for the hexes of the hexagon.</summary>
        private void SetHexagonFootprint()
        {
            FootprintMinColumn  = CentreColumn - RADIUS;
            FootprintMinRow     = CentreRow - RADIUS;
            FootprintMaxColumn  = CentreColumn + RADIUS;
            FootprintMaxRow     = CentreRow + RADIUS;
            FootprintBits       = new byte[(FOOTPRINT_SIZE * FOOTPRINT_SIZE + 7) / 8];

            for (int bit = 0; bit < FOOTPRINT_SIZE * FOOTPRINT_SIZE; ++bit)
            {
                int column  = FootprintMinColumn + bit % FOOTPRINT_SIZE;
                int row     = FootprintMinRow + bit / FOOTPRINT_SIZE;

                if (MapDataGrid.Distance(CentreColumn, CentreRow, column, row) <= RADIUS)
                {
                    FootprintBits[bit / 8] |= (byte)(1 << (bit % 8));
                }
            }
        }

        private void SetFootprint(MapDataGrid grid, List<int> hexes)
        {
            FootprintMinColumn  = hexes.Min(grid.Column);
            FootprintMaxColumn  = hexes.Max(grid.Column);
            FootprintMinRow     = hexes.Min(grid.Row);
            FootprintMaxRow     = hexes.Max(grid.Row);

            int width = FootprintMaxColumn - FootprintMinColumn + 1;
            FootprintBits = new byte[(width * (FootprintMaxRow - FootprintMinRow + 1) + 7) / 8];

            foreach (int hexIndex in hexes)
            {
                int bit = (grid.Row(hexIndex) - FootprintMinRow) * width + grid.Column(hexIndex) - FootprintMinColumn;
                FootprintBits[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }
    }
}
