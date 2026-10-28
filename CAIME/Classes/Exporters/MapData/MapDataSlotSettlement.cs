using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>One building slot of an Attila or Pharaoh settlement.</summary>
    public sealed class MapDataSlot
    {
        /// <summary>The slot's hexes, column by column.</summary>
        public List<int> Hexes      { get; } = new List<int>();

        /// <summary>
        /// The slot's hexes where its building can stand, placed by <see cref="MapDataSlotPositions"/>.
        /// </summary>
        public List<int> Positions  { get; } = new List<int>();
    }

    /// <summary>
    /// An Attila or Pharaoh settlement, as their builders make it (read from Attila's DLL and
    /// Pharaoh's; every settlement of main_attila_map and phar_main matches): the sprawl fill
    /// (<see cref="MapDataSprawlFill"/>) from the region's first plain land hex with a town slot,
    /// column by column, gives the footprint (its bounds and hexes), the dock (its first open sea
    /// hex), and the slots: its hexes of each town slot index, the main slot (0), then 2 upwards,
    /// and the port (1).
    /// </summary>
    public sealed class MapDataSlotSettlement
    {
        public List<MapDataSlot>    Slots           { get; } = new List<MapDataSlot>();
        public MapDataSlot          Port            { get; } = new MapDataSlot();

        public int                  FootprintMinColumn  { get; private set; }
        public int                  FootprintMinRow     { get; private set; }
        public int                  FootprintMaxColumn  { get; private set; }
        public int                  FootprintMaxRow     { get; private set; }
        public byte[]               FootprintBits       { get; private set; }

        /// <summary>Where ships dock, or -1.</summary>
        public int DockHex { get; private set; } = -1;

        /// <summary>
        /// The campaign UI's position for the settlement: its main slot's first position, or its
        /// first hex when it has none.
        /// </summary>
        public int UiHex => Slots[0].Positions.Count > 0 ? Slots[0].Positions[0] : Slots[0].Hexes[0];

        /// <summary>The settlement of each land region whose fill holds main slot hexes; null for the rest.</summary>
        public static MapDataSlotSettlement[] FindAll(MapDataModel model)
        {
            var settlements = new MapDataSlotSettlement[model.RegionCount];

            for (int region = 0; region < model.Map.LandRegions.Count; ++region)
            {
                int seed = MapDataSprawlFill.Seed(model, region);
                if (seed < 0)
                {
                    continue;
                }

                var filled = MapDataSprawlFill.Fill(model, new[] { seed }, region);
                if (filled.Any(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == Hex.MAIN_SLOT_INDEX))
                {
                    settlements[region] = Find(model, filled);
                }
            }

            return settlements;
        }

        private static MapDataSlotSettlement Find(MapDataModel model, List<int> filled)
        {
            var settlement  = new MapDataSlotSettlement { DockHex = MapDataSprawlFill.Dock(model, filled) };
            var hexes       = model.Map.HexData;
            int highestSlot = filled.Max(hexIndex => (int)hexes[hexIndex].TownSlotIndex);

            settlement.Slots.Add(Slot(model, filled, Hex.MAIN_SLOT_INDEX));
            for (int slotIndex = Hex.PORT_SLOT_INDEX + 1; slotIndex <= highestSlot; ++slotIndex)
            {
                settlement.Slots.Add(Slot(model, filled, slotIndex));
            }

            settlement.Port.Hexes.AddRange(Slot(model, filled, Hex.PORT_SLOT_INDEX).Hexes);
            settlement.SetFootprint(model, filled);
            return settlement;
        }

        private static MapDataSlot Slot(MapDataModel model, List<int> filled, int slotIndex)
        {
            var slot = new MapDataSlot();
            slot.Hexes.AddRange(filled.Where(hexIndex => model.Map.HexData[hexIndex].TownSlotIndex == slotIndex));
            return slot;
        }

        private void SetFootprint(MapDataModel model, List<int> filled)
        {
            var grid = model.Grid;

            FootprintMinColumn  = filled.Min(grid.Column);
            FootprintMaxColumn  = filled.Max(grid.Column);
            FootprintMinRow     = filled.Min(grid.Row);
            FootprintMaxRow     = filled.Max(grid.Row);

            int width   = FootprintMaxColumn - FootprintMinColumn + 1;
            int height  = FootprintMaxRow - FootprintMinRow + 1;
            FootprintBits = new byte[(width * height + 7) / 8];

            foreach (int hexIndex in filled)
            {
                int bit = (grid.Row(hexIndex) - FootprintMinRow) * width + grid.Column(hexIndex) - FootprintMinColumn;
                FootprintBits[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }
    }
}
