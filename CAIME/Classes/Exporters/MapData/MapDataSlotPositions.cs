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
        private const int MAIN_SLOT_VALUE       = 1;
        private const int PORT_SLOT_VALUE       = 2;

        /// <summary>Attila validates its positions only for settlements with more slots than this, the port counted.</summary>
        private const int MAX_UNVALIDATED_SLOTS = 4;

        /// <summary>A port position has exactly this many sea hexes among itself and its neighbours.</summary>
        private const int PORT_SEA_HEXES        = 2;

        /// <summary>
        /// The directions the Assembly Kit's slot code steps in: north (the next row), north-east,
        /// south-east, south, south-west and north-west. Odd columns sit half a row higher.
        /// </summary>
        private enum Direction { North, NorthEast, SouthEast, South, SouthWest, NorthWest }

        private const int DIRECTION_COUNT = 6;

        private sealed class ValidationFailed : System.Exception { }

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

        /// <summary>
        /// Attila, as its Assembly Kit's ToolDataBuilder places them (read from the DLL; all 864
        /// slots of main_attila_map match):
        ///
        /// <list type="number">
        /// <item>A slot's candidates are its hexes whose six neighbours all hold the same town slot
        /// index, column by column.</item>
        /// <item>A port keeps the candidates with exactly two sea hexes among themselves and their
        /// neighbours, or else its first candidate.</item>
        /// <item>Settlements of more than four slots, the port counted, are then validated: the
        /// port and the main slot against the main slot and port together, then every other slot
        /// against each slot it touches that can reach the main slot or port without going
        /// through the slot's other neighbours (<see cref="ReachesMainOrPort"/>), together with the
        /// main slot and port where either touches them. Validating keeps the candidates that
        /// neither stand on nor touch the edge of those slots' combined hexes - a port's may where
        /// the edge is sea - nor a road or river. When none would be kept the builder gives up on
        /// the settlement, and every slot keeps what it has.</item>
        /// </list>
        /// </summary>
        public static void PlaceAttila(MapDataModel model, MapDataSlotSettlement[] settlements)
        {
            foreach (var settlement in settlements.Where(settlement => settlement != null))
            {
                PlaceAttila(model, settlement);
            }
        }

        private static void PlaceAttila(MapDataModel model, MapDataSlotSettlement settlement)
        {
            var slots = new Dictionary<int, MapDataSlot> { [MAIN_SLOT_VALUE] = settlement.Slots[0] };
            for (int index = 1; index < settlement.Slots.Count; ++index)
            {
                slots[index + PORT_SLOT_VALUE] = settlement.Slots[index];
            }

            bool hasPort = settlement.Port.Hexes.Count > 0;
            if (hasPort)
            {
                slots[PORT_SLOT_VALUE] = settlement.Port;
            }

            foreach (var slot in slots.Values)
            {
                slot.Positions.Clear();
                slot.Positions.AddRange(slot.Hexes.Where(hexIndex => IsSurroundedBySameSlot(model, hexIndex)));
            }

            if (hasPort)
            {
                var port = settlement.Port.Positions;
                var kept = port.Where(hexIndex => SeaHexesAround(model, hexIndex) == PORT_SEA_HEXES).ToList();
                if (kept.Count == 0)
                {
                    kept = port.Take(1).ToList();
                }

                port.Clear();
                port.AddRange(kept);
            }

            if (settlement.Slots.Count + (hasPort ? 1 : 0) <= MAX_UNVALIDATED_SLOTS)
            {
                return;
            }

            var connections = slots.ToDictionary(pair => pair.Key, pair => Connections(model, pair.Value, pair.Key));
            var mainAndPort = hasPort ? new[] { MAIN_SLOT_VALUE, PORT_SLOT_VALUE } : new[] { MAIN_SLOT_VALUE };

            try
            {
                if (hasPort)
                {
                    Validate(model, settlement.Port, Hexes(slots, mainAndPort), isPort: true);
                }

                Validate(model, settlement.Slots[0], Hexes(slots, mainAndPort), isPort: false);

                foreach (var value in slots.Keys.Where(value => value > PORT_SLOT_VALUE).OrderBy(value => value))
                {
                    var own = connections[value];
                    foreach (int other in own.Where(other => ReachesMainOrPort(other, new HashSet<int>(own.Where(o => o != other)), connections)).ToList())
                    {
                        var group = new HashSet<int> { value, other };
                        if (other == MAIN_SLOT_VALUE)
                        {
                            group.UnionWith(mainAndPort);
                        }
                        else
                        {
                            var theirs = connections.TryGetValue(other, out var list) ? list : new List<int>();
                            if (own.Contains(MAIN_SLOT_VALUE) || theirs.Contains(MAIN_SLOT_VALUE))
                            {
                                group.Add(MAIN_SLOT_VALUE);
                            }

                            if (own.Contains(PORT_SLOT_VALUE) || theirs.Contains(PORT_SLOT_VALUE))
                            {
                                group.Add(PORT_SLOT_VALUE);
                            }
                        }

                        Validate(model, slots[value], Hexes(slots, group), isPort: false);
                    }
                }
            }
            catch (ValidationFailed)
            {
                // The builder stops validating the settlement; what is placed so far stays.
            }
        }

        /// <summary>
        /// Whether a slot reaches the main slot or the port through touching slots without passing
        /// those in <paramref name="visited"/>. The builder seeds that set with the asking slot's
        /// other neighbours but not the asking slot itself, so a path may run back through it.
        /// </summary>
        private static bool ReachesMainOrPort(int slot, HashSet<int> visited, Dictionary<int, List<int>> connections)
        {
            if (slot == MAIN_SLOT_VALUE || slot == PORT_SLOT_VALUE)
            {
                return true;
            }

            if (visited.Contains(slot) || connections.TryGetValue(slot, out var next) == false)
            {
                return false;
            }

            var further = new HashSet<int>(visited) { slot };
            return next.Any(other => ReachesMainOrPort(other, further, connections));
        }

        /// <summary>The other town slot indices beside a slot's hexes, in the order the builder meets them.</summary>
        private static List<int> Connections(MapDataModel model, MapDataSlot slot, int value)
        {
            var found = new List<int>();
            foreach (int hexIndex in slot.Hexes)
            {
                for (int direction = 0; direction < DIRECTION_COUNT; ++direction)
                {
                    int neighbour   = Step(model.Grid, hexIndex, (Direction)direction);
                    int other       = neighbour >= 0 ? SlotValue(model, neighbour) : 0;
                    if (other != 0 && other != value && found.Contains(other) == false)
                    {
                        found.Add(other);
                    }
                }
            }

            return found;
        }

        private static HashSet<int> Hexes(Dictionary<int, MapDataSlot> slots, IEnumerable<int> values)
        {
            var hexes = new HashSet<int>();
            foreach (int value in values)
            {
                if (slots.TryGetValue(value, out var slot))
                {
                    hexes.UnionWith(slot.Hexes);
                }
            }

            return hexes;
        }

        private static void Validate(MapDataModel model, MapDataSlot slot, HashSet<int> hexes, bool isPort)
        {
            var edge = new HashSet<int>(hexes.Where(hexIndex => Neighbours(model.Grid, hexIndex).Any(neighbour => hexes.Contains(neighbour) == false)));

            var kept = slot.Positions.Where(hexIndex =>
            {
                var around = new[] { hexIndex }.Concat(Neighbours(model.Grid, hexIndex).Where(neighbour => neighbour >= 0)).ToList();

                if (around.Any(h => edge.Contains(h) && (isPort == false || IsSea(model, h) == false)))
                {
                    return false;
                }

                if (around.Any(h => model.Map.HexData[h].RoadEdgeMask != 0 || model.Map.HexData[h].RiverEdgeMask != 0))
                {
                    return false;
                }

                return isPort == false || SeaHexesAround(model, hexIndex) == PORT_SEA_HEXES;
            }).ToList();

            if (kept.Count == 0)
            {
                throw new ValidationFailed();
            }

            slot.Positions.Clear();
            slot.Positions.AddRange(kept);
        }

        private static bool IsSurroundedBySameSlot(MapDataModel model, int hexIndex)
        {
            int value = SlotValue(model, hexIndex);
            return Neighbours(model.Grid, hexIndex).All(neighbour => neighbour >= 0 && SlotValue(model, neighbour) == value);
        }

        private static int SeaHexesAround(MapDataModel model, int hexIndex)
        {
            return new[] { hexIndex }.Concat(Neighbours(model.Grid, hexIndex)).Count(h => h >= 0 && IsSea(model, h));
        }

        /// <summary>Sea as the builder types it: not a bridge and not impassable.</summary>
        private static bool IsSea(MapDataModel model, int hexIndex)
        {
            var hex = model.Map.HexData[hexIndex];
            return hex.IsSea && hex.IsBridge == false && hex.IsPassable;
        }

        /// <summary>The builder's town slot value: 0 for none, 1 the main slot, 2 the port, then the others.</summary>
        private static int SlotValue(MapDataModel model, int hexIndex)
        {
            return model.Map.HexData[hexIndex].TownSlotIndex + 1;
        }

        private static IEnumerable<int> Neighbours(MapDataGrid grid, int hexIndex)
        {
            for (int direction = 0; direction < DIRECTION_COUNT; ++direction)
            {
                yield return Step(grid, hexIndex, (Direction)direction);
            }
        }

        /// <summary>The neighbour in a direction, or -1 off the map.</summary>
        private static int Step(MapDataGrid grid, int hexIndex, Direction direction)
        {
            int column  = grid.Column(hexIndex);
            int row     = grid.Row(hexIndex);
            int odd     = MapDataGeometry.IsOddColumn(column) ? 1 : 0;

            switch (direction)
            {
                case Direction.North:       row += 1;                       break;
                case Direction.NorthEast:   column += 1; row += odd;        break;
                case Direction.SouthEast:   column += 1; row += odd - 1;    break;
                case Direction.South:       row -= 1;                       break;
                case Direction.SouthWest:   column -= 1; row += odd - 1;    break;
                case Direction.NorthWest:   column -= 1; row += odd;        break;
            }

            return column >= 0 && column < grid.Width && row >= 0 && row < grid.Height ? row * grid.Width + column : -1;
        }

        private static void PlacePharaoh(MapDataModel model, MapDataSlot slot)
        {
            var open = new HashSet<int>(slot.Hexes.Where(hexIndex => IsOpen(model, hexIndex)));

            // Slot hexes are listed column by column; the builder lists the buildings the other way.
            var anchors = slot.Hexes
                .Where(hexIndex => open.Contains(hexIndex)
                                && open.Contains(Step(model.Grid, hexIndex, Direction.North))
                                && open.Contains(Step(model.Grid, hexIndex, Direction.SouthEast)))
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
    }
}
