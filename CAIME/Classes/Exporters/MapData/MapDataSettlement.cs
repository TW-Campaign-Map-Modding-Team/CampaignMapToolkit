using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// A settlement as Warhammer III's and Three Kingdoms' map_data.esf record it: a footprint, a
    /// logical and a visual position, its main slot hexes and, when it has one, its port. Both
    /// builders make it the same way (read from Warhammer III's Assembly Kit DLL; all 201
    /// settlements of 3k_dlc07_main_map match):
    ///
    /// <list type="bullet">
    /// <item>From the region's first plain land hex with a town slot, column by column, the sprawl
    /// fill (<see cref="MapDataSprawlFill"/>) makes the settlement: its bounds and hexes the
    /// footprint, its main and port slot hexes the slots, its first open sea hex the dock.</item>
    /// <item>The visual position is the middle of the main and port slot hexes taken column by
    /// column, which must make a hexagon of 7 or 19 hexes around it.</item>
    /// <item>The logical position, which is also the port's, is the plain land hex of those nearest
    /// the visual position by column plus row difference, the main slot's first on a tie.</item>
    /// </list>
    ///
    /// A settlement breaking the builder's rules (<see cref="Validate"/>) is still written, with
    /// invalid positions, and the builder only logs it; so does CAIME.
    /// </summary>
    public sealed class MapDataSettlement
    {
        /// <summary>The builder's LOGICAL_POSITION::INVALID, for both column and row.</summary>
        public const int INVALID_POSITION = 0xFFFF;

        /// <summary>A port has exactly this many hexes.</summary>
        private const int PORT_SIZE = 3;

        /// <summary>Where the settlement stands as far as the game's rules go.</summary>
        public int          LogicalColumn   { get; private set; } = INVALID_POSITION;
        public int          LogicalRow      { get; private set; } = INVALID_POSITION;

        /// <summary>Where the settlement is drawn.</summary>
        public int          CentreColumn    { get; private set; } = INVALID_POSITION;
        public int          CentreRow       { get; private set; } = INVALID_POSITION;

        /// <summary>Main slot hexes, column by column.</summary>
        public List<int>    MainSlotHexes   { get; } = new List<int>();

        /// <summary>Port slot hexes, column by column, wherever their region.</summary>
        public List<int>    PortSlotHexes   { get; } = new List<int>();

        public bool         HasPort         => PortSlotHexes.Count > 0;

        /// <summary>Whether the slots follow the builder's rules; if not, every position is invalid.</summary>
        public bool         IsValid         { get; private set; }

        /// <summary>Where ships dock, or -1.</summary>
        public int          DockHex         { get; private set; } = -1;

        public int          FootprintMinColumn  { get; private set; }
        public int          FootprintMinRow     { get; private set; }
        public int          FootprintMaxColumn  { get; private set; }
        public int          FootprintMaxRow     { get; private set; }

        /// <summary>The footprint rectangle, row by row from its lowest row, one bit per hex (lowest bit first).</summary>
        public byte[]       FootprintBits   { get; private set; }

        /// <summary>
        /// Warhammer III: every region whose sea flag (<see cref="MapDataModel.PassableSea"/>) is
        /// clear, land or sea, gets a settlement when it has a seed.
        /// </summary>
        public static MapDataSettlement[] FindAll(MapDataModel model)
        {
            return FindAll(model, Enumerable.Range(0, model.RegionCount).Where(region => model.PassableSea[region] == false));
        }

        /// <summary>Three Kingdoms: the land regions get a settlement when they have a seed.</summary>
        public static MapDataSettlement[] FindAllThreeKingdoms(MapDataModel model)
        {
            return FindAll(model, Enumerable.Range(0, model.Map.LandRegions.Count));
        }

        private static MapDataSettlement[] FindAll(MapDataModel model, IEnumerable<int> regions)
        {
            var settlements = new MapDataSettlement[model.RegionCount];

            foreach (int region in regions)
            {
                int seed = MapDataSprawlFill.Seed(model, region);
                if (seed >= 0)
                {
                    settlements[region] = Build(model, seed, region);
                }
            }

            return settlements;
        }

        private static MapDataSettlement Build(MapDataModel model, int seed, int region)
        {
            var filled      = MapDataSprawlFill.Fill(model, new[] { seed }, region);
            var settlement  = new MapDataSettlement { DockHex = MapDataSprawlFill.Dock(model, filled) };

            settlement.MainSlotHexes.AddRange(filled.Where(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == Hex.MAIN_SLOT_INDEX));
            settlement.PortSlotHexes.AddRange(filled.Where(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == Hex.PORT_SLOT_INDEX));
            settlement.SetFootprint(model.Grid, seed, filled);

            var problem = settlement.Validate(model);
            if (problem != null)
            {
                LoggerViewModel.Log($"Settlement slots in region {model.RegionNames[region]} are invalid: {problem}. " +
                                    "map_data.esf gives the settlement no position.", LogLevel.Warning);
            }

            return settlement;
        }

        /// <summary>
        /// Places the settlement when its slots follow the builder's rules, checked in its order,
        /// and returns null; otherwise returns what is wrong and leaves every position invalid.
        /// </summary>
        private string Validate(MapDataModel model)
        {
            var grid = model.Grid;

            if (MainSlotHexes.Count == 0)
            {
                return "the town sprawl around its first slot hex holds no main slot hexes";
            }

            if (DockHex >= 0 && HasPort == false)
            {
                return "its town sprawl reaches open sea but it has no port slot hexes";
            }

            var slots       = MainSlotHexes.Concat(PortSlotHexes).ToList();
            int radius      = slots.Count == 7 ? 1 : slots.Count == 19 ? 2 : -1;
            if (radius < 0)
            {
                return $"its main and port slots have {slots.Count} hexes, not 7 or 19";
            }

            var byColumn    = slots.OrderBy(grid.Column).ThenBy(grid.Row).ToList();
            int centre      = byColumn[byColumn.Count / 2];
            int centreColumn = grid.Column(centre), centreRow = grid.Row(centre);

            // The slots are distinct, so as many of them within the radius as the hexagon has hexes is the hexagon.
            if (slots.Any(hexIndex => MapDataGrid.Distance(centreColumn, centreRow, grid.Column(hexIndex), grid.Row(hexIndex)) > radius))
            {
                return $"its main and port slot hexes are not a hexagon of radius {radius}";
            }

            if (MainSlotHexes.Contains(centre) == false)
            {
                return "the middle of its slot hexes is a port hex";
            }

            int logical = slots
                .Where(hexIndex => MapDataSprawlFill.IsPlainLand(model.Map.HexData[hexIndex]))
                .OrderBy(hexIndex => Math.Abs(grid.Column(hexIndex) - centreColumn) + Math.Abs(grid.Row(hexIndex) - centreRow))
                .DefaultIfEmpty(-1)
                .First();

            if (logical < 0)
            {
                return "none of its slot hexes is plain land";
            }

            if (HasPort && PortSlotHexes.Count != PORT_SIZE)
            {
                return $"its port has {PortSlotHexes.Count} hexes, not {PORT_SIZE}";
            }

            CentreColumn    = centreColumn;
            CentreRow       = centreRow;
            LogicalColumn   = grid.Column(logical);
            LogicalRow      = grid.Row(logical);
            IsValid         = true;
            return null;
        }

        /// <summary>The fill's bounds and hexes; the bounds start at the seed, which a seed without town sprawl leaves alone in them.</summary>
        private void SetFootprint(MapDataGrid grid, int seed, List<int> filled)
        {
            var hexes = filled.Append(seed).ToList();
            FootprintMinColumn  = hexes.Min(grid.Column);
            FootprintMaxColumn  = hexes.Max(grid.Column);
            FootprintMinRow     = hexes.Min(grid.Row);
            FootprintMaxRow     = hexes.Max(grid.Row);

            int width = FootprintMaxColumn - FootprintMinColumn + 1;
            FootprintBits = new byte[(width * (FootprintMaxRow - FootprintMinRow + 1) + 7) / 8];

            foreach (int hexIndex in filled)
            {
                int bit = (grid.Row(hexIndex) - FootprintMinRow) * width + grid.Column(hexIndex) - FootprintMinColumn;
                FootprintBits[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }
    }
}
