using System.Collections.Generic;

namespace CAIME.Exporters
{
    /// <summary>A region a region touches, and how many of the first region's hexes touch it.</summary>
    public struct MapDataRegionLink
    {
        public int Region;
        public int HexCount;
    }

    /// <summary>
    /// Which regions each region touches, as map_data.esf lists them. A touching region is
    /// traversable when a unit can step across somewhere along the border, and counts the hexes
    /// that can; otherwise it counts every hex on the border. "Same" pairs a region with regions of
    /// its own kind - a region with passable sea is one kind, every other region the other.
    /// </summary>
    public sealed class MapDataRegionLinks
    {
        public List<MapDataRegionLink>[] TraversableSame        { get; }
        public List<MapDataRegionLink>[] NonTraversableSame     { get; }
        public List<MapDataRegionLink>[] TraversableOther       { get; }
        public List<MapDataRegionLink>[] NonTraversableOther    { get; }

        public MapDataRegionLinks(MapHexFile map, MapDataGrid grid, MapDataAreas areas, bool[] hasPassableSea)
        {
            int regionCount     = hasPassableSea.Length;
            TraversableSame     = NewLists(regionCount);
            NonTraversableSame  = NewLists(regionCount);
            TraversableOther    = NewLists(regionCount);
            NonTraversableOther = NewLists(regionCount);

            var touched         = new List<int>[regionCount];
            var traversed       = new List<int>[regionCount];
            var touchedHexes    = new Dictionary<long, int>();
            var traversedHexes  = new Dictionary<long, int>();
            var hexTouches      = new List<int>(MapDataGrid.NEIGHBOUR_COUNT);
            var hexTraverses    = new List<int>(MapDataGrid.NEIGHBOUR_COUNT);
            var hexes           = map.HexData;

            for (int region = 0; region < regionCount; ++region)
            {
                touched[region]     = new List<int>();
                traversed[region]   = new List<int>();
            }

            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex    = row * grid.Width + column;
                    int region      = hexes[hexIndex].RegionId;

                    hexTouches.Clear();
                    hexTraverses.Clear();

                    for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                    {
                        int neighbour = grid.Neighbour(hexIndex, n);
                        if (neighbour < 0 || hexes[neighbour].RegionId == region)
                        {
                            continue;
                        }

                        int other = hexes[neighbour].RegionId;
                        AddOnce(hexTouches, other);
                        if (MapDataAreas.IsTraversable(areas.HexTypes[hexIndex], areas.HexTypes[neighbour]))
                        {
                            AddOnce(hexTraverses, other);
                        }
                    }

                    Count(region, hexTouches, touched, touchedHexes);
                    Count(region, hexTraverses, traversed, traversedHexes);
                }
            }

            for (int region = 0; region < regionCount; ++region)
            {
                foreach (int other in traversed[region])
                {
                    var list = hasPassableSea[region] == hasPassableSea[other] ? TraversableSame : TraversableOther;
                    list[region].Add(new MapDataRegionLink { Region = other, HexCount = traversedHexes[Key(region, other)] });
                }

                foreach (int other in touched[region])
                {
                    if (traversedHexes.ContainsKey(Key(region, other)))
                    {
                        continue;
                    }

                    var list = hasPassableSea[region] == hasPassableSea[other] ? NonTraversableSame : NonTraversableOther;
                    list[region].Add(new MapDataRegionLink { Region = other, HexCount = touchedHexes[Key(region, other)] });
                }
            }
        }

        private static void Count(int region, List<int> others, List<int>[] order, Dictionary<long, int> hexCounts)
        {
            foreach (int other in others)
            {
                long key = Key(region, other);
                if (hexCounts.TryGetValue(key, out int count))
                {
                    hexCounts[key] = count + 1;
                }
                else
                {
                    hexCounts.Add(key, 1);
                    order[region].Add(other);
                }
            }
        }

        private static void AddOnce(List<int> list, int value)
        {
            if (list.Contains(value) == false)
            {
                list.Add(value);
            }
        }

        private static long Key(int region, int other)
        {
            return ((long)region << 32) | (uint)other;
        }

        private static List<MapDataRegionLink>[] NewLists(int count)
        {
            var lists = new List<MapDataRegionLink>[count];
            for (int i = 0; i < count; ++i)
            {
                lists[i] = new List<MapDataRegionLink>();
            }

            return lists;
        }
    }
}
