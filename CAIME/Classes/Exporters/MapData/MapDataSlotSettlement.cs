using System;
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
    /// An Attila or Pharaoh settlement: a slot for each map.hex town slot index the region uses
    /// (the main slot, 0, then 2 upwards), the port (slot 1), and a footprint of its town sprawl.
    /// Worked out from main_attila_map and phar_main.
    ///
    /// <para>
    /// A slot holds the region's hexes of that slot index and every hex of the same index joined
    /// to them, whatever its region: a port takes in the slot-1 hexes of the sea beside it. The
    /// footprint is the same for town sprawl.
    /// </para>
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

        /// <summary>Where ships dock, or -1: see <see cref="FindDock"/>.</summary>
        public int DockHex { get; private set; } = -1;

        /// <summary>
        /// The campaign UI's position for the settlement: its main slot's first position, or its
        /// first hex when it has none.
        /// </summary>
        public int UiHex => Slots[0].Positions.Count > 0 ? Slots[0].Positions[0] : Slots[0].Hexes[0];

        /// <summary>The settlement of each land region with main slot hexes; null for the rest.</summary>
        public static MapDataSlotSettlement[] FindAll(MapDataModel model)
        {
            int landRegions = model.Map.LandRegions.Count;
            var ownBySlot   = new SortedDictionary<int, List<int>>[landRegions];
            var ownSprawl   = new List<int>[landRegions];

            for (int region = 0; region < landRegions; ++region)
            {
                ownBySlot[region] = new SortedDictionary<int, List<int>>();
                ownSprawl[region] = new List<int>();
            }

            foreach (int hexIndex in ColumnMajor(model.Grid))
            {
                var hex = model.Map.HexData[hexIndex];
                if (hex.RegionId >= landRegions)
                {
                    continue;
                }

                if (hex.TownSlotIndex >= 0)
                {
                    if (ownBySlot[hex.RegionId].TryGetValue(hex.TownSlotIndex, out var list) == false)
                    {
                        list = new List<int>();
                        ownBySlot[hex.RegionId].Add(hex.TownSlotIndex, list);
                    }

                    list.Add(hexIndex);
                }

                if (hex.IsTownSprawl)
                {
                    ownSprawl[hex.RegionId].Add(hexIndex);
                }
            }

            var settlements = new MapDataSlotSettlement[model.RegionCount];
            for (int region = 0; region < landRegions; ++region)
            {
                if (ownBySlot[region].ContainsKey(Hex.MAIN_SLOT_INDEX))
                {
                    settlements[region] = Find(model, ownBySlot[region], ownSprawl[region]);
                }
            }

            return settlements;
        }

        private static MapDataSlotSettlement Find(MapDataModel model, SortedDictionary<int, List<int>> ownBySlot, List<int> ownSprawl)
        {
            var settlement  = new MapDataSlotSettlement();
            int highestSlot = ownBySlot.Keys.Max();

            settlement.Slots.Add(Slot(model, ownBySlot, Hex.MAIN_SLOT_INDEX));
            for (int slotIndex = Hex.PORT_SLOT_INDEX + 1; slotIndex <= highestSlot; ++slotIndex)
            {
                settlement.Slots.Add(Slot(model, ownBySlot, slotIndex));
            }

            settlement.Port.Hexes.AddRange(PortHexes(model, ownBySlot, settlement.Slots[0].Hexes));

            settlement.DockHex = FindDock(model, settlement.Slots[0].Hexes);

            // CAIME marks slot hexes as sprawl, so a region without sprawl is not expected; its main
            // slot stands in.
            var sprawl = ownSprawl.Count > 0 ? Joined(model, ownSprawl, hex => hex.IsTownSprawl) : settlement.Slots[0].Hexes;
            settlement.SetFootprint(model, sprawl);
            return settlement;
        }

        /// <summary>
        /// The dock, as both Attila's and Pharaoh's builders find it (every dock of main_attila_map
        /// and phar_main): the first open sea hex of the sprawl fill from the main slot.
        /// </summary>
        private static int FindDock(MapDataModel model, List<int> mainSlotHexes)
        {
            if (mainSlotHexes.Count == 0)
            {
                return -1;
            }

            int region = model.Map.HexData[mainSlotHexes[0]].RegionId;
            return MapDataSprawlFill.Dock(model, MapDataSprawlFill.Fill(model, mainSlotHexes, region));
        }

        /// <summary>
        /// The port starts from the region's own port hexes and the port hexes beside its main
        /// slot: a port can lie wholly in the sea next to the settlement (Pharaoh's
        /// phar_main_pi_ramesses_per_amun).
        /// </summary>
        private static List<int> PortHexes(MapDataModel model, SortedDictionary<int, List<int>> ownBySlot, List<int> mainSlotHexes)
        {
            var start = ownBySlot.TryGetValue(Hex.PORT_SLOT_INDEX, out var own) ? new List<int>(own) : new List<int>();

            foreach (int hexIndex in mainSlotHexes)
            {
                for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                {
                    int neighbour = model.Grid.Neighbour(hexIndex, n);
                    if (neighbour >= 0 && model.Map.HexData[neighbour].TownSlotIndex == Hex.PORT_SLOT_INDEX)
                    {
                        start.Add(neighbour);
                    }
                }
            }

            return start.Count > 0 ? Joined(model, start, hex => hex.TownSlotIndex == Hex.PORT_SLOT_INDEX) : start;
        }

        private static MapDataSlot Slot(MapDataModel model, SortedDictionary<int, List<int>> ownBySlot, int slotIndex)
        {
            var slot = new MapDataSlot();
            if (ownBySlot.TryGetValue(slotIndex, out var own))
            {
                slot.Hexes.AddRange(Joined(model, own, hex => hex.TownSlotIndex == slotIndex));
            }

            return slot;
        }

        /// <summary><paramref name="start"/> and every hex joined to it through hexes that match, column by column.</summary>
        private static List<int> Joined(MapDataModel model, List<int> start, Func<Hex, bool> matches)
        {
            var joined  = new HashSet<int>(start);
            var pending = new Stack<int>(start);

            while (pending.Count > 0)
            {
                int hexIndex = pending.Pop();
                for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                {
                    int neighbour = model.Grid.Neighbour(hexIndex, n);
                    if (neighbour >= 0 && joined.Contains(neighbour) == false && matches(model.Map.HexData[neighbour]))
                    {
                        joined.Add(neighbour);
                        pending.Push(neighbour);
                    }
                }
            }

            return joined.OrderBy(hexIndex => model.Grid.Column(hexIndex)).ThenBy(hexIndex => model.Grid.Row(hexIndex)).ToList();
        }

        private void SetFootprint(MapDataModel model, List<int> sprawl)
        {
            var grid = model.Grid;

            FootprintMinColumn  = sprawl.Min(grid.Column);
            FootprintMaxColumn  = sprawl.Max(grid.Column);
            FootprintMinRow     = sprawl.Min(grid.Row);
            FootprintMaxRow     = sprawl.Max(grid.Row);

            int width   = FootprintMaxColumn - FootprintMinColumn + 1;
            int height  = FootprintMaxRow - FootprintMinRow + 1;
            FootprintBits = new byte[(width * height + 7) / 8];

            foreach (int hexIndex in sprawl)
            {
                int bit = (grid.Row(hexIndex) - FootprintMinRow) * width + grid.Column(hexIndex) - FootprintMinColumn;
                FootprintBits[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }

        private static IEnumerable<int> ColumnMajor(MapDataGrid grid)
        {
            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    yield return row * grid.Width + column;
                }
            }
        }
    }
}
