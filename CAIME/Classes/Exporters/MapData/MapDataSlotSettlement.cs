using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>One building slot of an Attila settlement.</summary>
    public sealed class MapDataSlot
    {
        /// <summary>The slot's hexes, column by column.</summary>
        public List<int> Hexes      { get; } = new List<int>();

        /// <summary>
        /// One to seven of the slot's hexes, where its building stands.
        ///
        /// TODO: where these come from is not known. In main_attila_map they are not marked by any
        /// map.hex bit, and are not the hexes nearest the slot's centroid or bounding-box centre,
        /// nor its innermost hexes. Their shapes (one hex, two, a triangle of three, up to a hex
        /// and its six neighbours) suggest building footprints from another source. Left empty.
        /// </summary>
        public List<int> Positions  { get; } = new List<int>();
    }

    /// <summary>
    /// An Attila settlement: a slot for each map.hex town slot index the region uses (the main
    /// slot, 0, then 2 upwards), the port (slot 1), and a footprint of its town sprawl. Worked out
    /// from main_attila_map.
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

        /// <summary>The first port hex at sea, column by column, or -1.</summary>
        public int DockHex { get; private set; } = -1;

        /// <summary>
        /// TODO: the campaign UI's position for the settlement is its main slot's first position;
        /// until positions are known, its first hex.
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

            settlement.Port.Hexes.AddRange(Slot(model, ownBySlot, Hex.PORT_SLOT_INDEX).Hexes);
            settlement.DockHex = settlement.Port.Hexes.Where(hexIndex => model.Map.HexData[hexIndex].IsSea).DefaultIfEmpty(-1).First();

            // CAIME marks slot hexes as sprawl, so a region without sprawl is not expected; its main
            // slot stands in.
            var sprawl = ownSprawl.Count > 0 ? Joined(model, ownSprawl, hex => hex.IsTownSprawl) : settlement.Slots[0].Hexes;
            settlement.SetFootprint(model, sprawl);
            return settlement;
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
