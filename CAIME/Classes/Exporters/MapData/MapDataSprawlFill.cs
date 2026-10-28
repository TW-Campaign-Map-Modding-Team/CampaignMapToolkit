using System.Collections.Generic;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// The fill over town sprawl the Attila, Three Kingdoms and Pharaoh builders make around a
    /// settlement (read from Attila's and Three Kingdoms' DLLs): from the seed hexes it takes in
    /// sprawl hexes of the settlement's region and any other region's water - sea, bridges and
    /// impassable hexes - and spreads out of water only into water.
    /// </summary>
    public static class MapDataSprawlFill
    {
        /// <summary>The filled hexes, column by column.</summary>
        public static List<int> Fill(MapDataModel model, IEnumerable<int> seeds, int region)
        {
            var hexes   = model.Map.HexData;
            var seen    = new HashSet<int>(seeds);
            var pending = new Stack<int>(seen);
            var filled  = new List<int>();

            while (pending.Count > 0)
            {
                int hexIndex    = pending.Pop();
                var hex         = hexes[hexIndex];
                bool isWater    = IsWater(hex);

                if (hex.IsTownSprawl == false || (hex.RegionId != region && isWater == false))
                {
                    continue;
                }

                filled.Add(hexIndex);

                for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                {
                    int neighbour = model.Grid.Neighbour(hexIndex, n);
                    if (neighbour >= 0 && seen.Contains(neighbour) == false && hexes[neighbour].IsTownSprawl
                        && (isWater == false || IsWater(hexes[neighbour])))
                    {
                        seen.Add(neighbour);
                        pending.Push(neighbour);
                    }
                }
            }

            return filled.OrderBy(model.Grid.Column).ThenBy(model.Grid.Row).ToList();
        }

        /// <summary>Where ships dock: the fill's first open sea hex - not a bridge, such as a river crossing, nor impassable - or -1.</summary>
        public static int Dock(MapDataModel model, List<int> filled)
        {
            return filled.Where(hexIndex => IsOpenSea(model.Map.HexData[hexIndex])).DefaultIfEmpty(-1).First();
        }

        public static bool IsOpenSea(Hex hex)
        {
            return hex.IsSea && hex.IsBridge == false && hex.IsPassable;
        }

        private static bool IsWater(Hex hex)
        {
            return hex.IsSea || hex.IsPassable == false;
        }
    }
}
