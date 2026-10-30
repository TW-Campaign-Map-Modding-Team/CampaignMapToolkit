using System;
using System.Collections.Generic;
using CAIME.Classes.Importers;

namespace CAIME.Exporters
{
    public sealed class MapDataArea
    {
        public int              Region          { get; set; }
        public int              IndexInRegion   { get; set; }
        public MapDataAreaType  Type            { get; set; }
        public int              MinColumn       { get; set; } = int.MaxValue;
        public int              MinRow          { get; set; } = int.MaxValue;
        public int              MaxColumn       { get; set; } = int.MinValue;
        public int              MaxRow          { get; set; } = int.MinValue;
        public int              HexCount        { get; set; }

        /// <summary>
        /// Areas share a group when units can walk between them: land, beach and bridge-cliff areas
        /// that touch (or touch the same bridge), and sea and bridge areas that touch. Cliffs and
        /// impassable areas are each a group of their own.
        /// </summary>
        public int              Group           { get; set; }

        public int              CentreHex       { get; set; } = -1;

        /// <summary>Touching areas, as global area indices, in the order the builder finds them.</summary>
        public List<int>        Traversable     { get; } = new List<int>();
        public List<int>        NonTraversable  { get; } = new List<int>();
    }

    /// <summary>
    /// Splits every region into areas - connected hexes of the same <see cref="MapDataAreaType"/> -
    /// and works out what map_data.esf records about each, the way the Assembly Kit's builder does.
    /// </summary>
    public sealed class MapDataAreas
    {
        public MapDataAreaType[]    HexTypes        { get; }

        /// <summary>The global index of each hex's area.</summary>
        public int[]                HexAreas        { get; }

        /// <summary>Areas ordered by their first hex in row-major order, which also orders each region's areas.</summary>
        public List<MapDataArea>    Areas           { get; } = new List<MapDataArea>();

        /// <summary>Each region's areas, as global area indices.</summary>
        public List<int>[]          RegionAreas     { get; }

        public MapDataAreas(MapHexFile map, MapDataGrid grid, MapDataGeometry geometry, int regionCount)
        {
            HexTypes    = ClassifyHexes(map, grid);
            HexAreas    = new int[grid.HexCount];
            RegionAreas = new List<int>[regionCount];

            for (int region = 0; region < regionCount; ++region)
            {
                RegionAreas[region] = new List<int>();
            }

            FindAreas(map, grid);
            AssignGroups(grid);
            FindNeighbours(grid);
            FindCentres(grid, geometry);
        }

        /// <summary>Whether the builder counts a step from one area type to another as traversable.</summary>
        public static bool IsTraversable(MapDataAreaType from, MapDataAreaType to)
        {
            bool fromLand = from == MapDataAreaType.Land || from == MapDataAreaType.Beach;
            bool toLand   = to == MapDataAreaType.Land || to == MapDataAreaType.Beach;
            bool fromSea  = from == MapDataAreaType.Sea || from == MapDataAreaType.Bridge;
            bool toSea    = to == MapDataAreaType.Sea || to == MapDataAreaType.Bridge;

            return (fromLand && toLand)
                || (fromSea && toSea)
                || (from == MapDataAreaType.Beach && toSea)
                || (fromSea && to == MapDataAreaType.Beach);
        }

        internal static MapDataAreaType[] ClassifyHexes(MapHexFile map, MapDataGrid grid)
        {
            var hexes = map.HexData;
            var types = new MapDataAreaType[grid.HexCount];

            for (int hexIndex = 0; hexIndex < types.Length; ++hexIndex)
            {
                var hex = hexes[hexIndex];

                if (hex.IsPassable == false)
                {
                    types[hexIndex] = MapDataAreaType.Impassable;
                }
                else if (hex.IsSea)
                {
                    types[hexIndex] = hex.IsBridge ? MapDataAreaType.Bridge : MapDataAreaType.Sea;
                }
                else if (hex.IsBeach)
                {
                    types[hexIndex] = MapDataAreaType.Beach;
                }
                else if (hex.IsCliff)
                {
                    types[hexIndex] = TouchesSeaBridge(hexes, grid, hexIndex) ? MapDataAreaType.BridgeCliff : MapDataAreaType.Cliff;
                }
                else
                {
                    types[hexIndex] = MapDataAreaType.Land;
                }
            }

            return types;
        }

        /// <summary>
        /// Whether a hex other than the east one beside it is a passable sea bridge. An impassable one
        /// does not count (warhammer_map_1_1_twilight has a cliff that touches only that).
        /// </summary>
        private static bool TouchesSeaBridge(Hex[] hexes, MapDataGrid grid, int hexIndex)
        {
            for (int n = MapDataGrid.FIRST_NON_EAST_NEIGHBOUR; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
            {
                int neighbour = grid.Neighbour(hexIndex, n);
                if (neighbour >= 0 && hexes[neighbour].IsPassable && hexes[neighbour].IsSea && hexes[neighbour].IsBridge)
                {
                    return true;
                }
            }

            return false;
        }

        private void FindAreas(MapHexFile map, MapDataGrid grid)
        {
            var hexes   = map.HexData;
            var queue   = new Queue<int>();

            for (int hexIndex = 0; hexIndex < HexAreas.Length; ++hexIndex)
            {
                HexAreas[hexIndex] = -1;
            }

            for (int start = 0; start < HexAreas.Length; ++start)
            {
                if (HexAreas[start] >= 0)
                {
                    continue;
                }

                int region  = hexes[start].RegionId;
                var type    = HexTypes[start];
                var area    = new MapDataArea { Region = region, IndexInRegion = RegionAreas[region].Count, Type = type };
                int areaId  = Areas.Count;

                Areas.Add(area);
                RegionAreas[region].Add(areaId);

                HexAreas[start] = areaId;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int hexIndex = queue.Dequeue();
                    AddHex(area, grid, hexIndex);

                    for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                    {
                        int neighbour = grid.Neighbour(hexIndex, n);
                        if (neighbour >= 0 && HexAreas[neighbour] < 0 && hexes[neighbour].RegionId == region && HexTypes[neighbour] == type)
                        {
                            HexAreas[neighbour] = areaId;
                            queue.Enqueue(neighbour);
                        }
                    }
                }
            }
        }

        private static void AddHex(MapDataArea area, MapDataGrid grid, int hexIndex)
        {
            int column  = grid.Column(hexIndex);
            int row     = grid.Row(hexIndex);

            area.MinColumn  = Math.Min(area.MinColumn, column);
            area.MinRow     = Math.Min(area.MinRow, row);
            area.MaxColumn  = Math.Max(area.MaxColumn, column);
            area.MaxRow     = Math.Max(area.MaxRow, row);
            ++area.HexCount;
        }

        /// <summary>
        /// Groups are numbered in region order, then area order. Land-type areas touching the same
        /// bridge area join through it, but the bridge keeps its own, sea, group.
        /// </summary>
        private void AssignGroups(MapDataGrid grid)
        {
            int areaCount   = Areas.Count;
            var groups      = new DisjointSets(areaCount * 2);

            for (int hexIndex = 0; hexIndex < HexAreas.Length; ++hexIndex)
            {
                int area = HexAreas[hexIndex];

                for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                {
                    int neighbour = grid.Neighbour(hexIndex, n);
                    if (neighbour < 0 || HexAreas[neighbour] == area)
                    {
                        continue;
                    }

                    int other       = HexAreas[neighbour];
                    var areaClass   = GroupClass(Areas[area].Type);
                    var otherClass  = GroupClass(Areas[other].Type);

                    if (areaClass != GroupClassKind.Own && areaClass == otherClass)
                    {
                        groups.Union(area, other);
                    }
                    else if (areaClass == GroupClassKind.Land && Areas[other].Type == MapDataAreaType.Bridge)
                    {
                        groups.Union(area, areaCount + other);
                    }
                }
            }

            var groupIds    = new Dictionary<int, int>();
            int nextGroup   = 0;

            foreach (var regionAreas in RegionAreas)
            {
                foreach (int areaId in regionAreas)
                {
                    var area = Areas[areaId];
                    if (GroupClass(area.Type) == GroupClassKind.Own)
                    {
                        area.Group = nextGroup++;
                        continue;
                    }

                    int root = groups.Find(areaId);
                    if (groupIds.TryGetValue(root, out int group) == false)
                    {
                        group = nextGroup++;
                        groupIds.Add(root, group);
                    }

                    area.Group = group;
                }
            }
        }

        private enum GroupClassKind { Land, Sea, Own }

        private static GroupClassKind GroupClass(MapDataAreaType type)
        {
            switch (type)
            {
                case MapDataAreaType.Land:
                case MapDataAreaType.Beach:
                case MapDataAreaType.BridgeCliff:
                    return GroupClassKind.Land;
                case MapDataAreaType.Sea:
                case MapDataAreaType.Bridge:
                    return GroupClassKind.Sea;
                default:
                    return GroupClassKind.Own;
            }
        }

        /// <summary>
        /// The builder walks the grid column by column, and each hex's neighbours in
        /// <see cref="MapDataGrid"/> order, listing each touching area the first time it meets it.
        /// </summary>
        private void FindNeighbours(MapDataGrid grid)
        {
            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex    = row * grid.Width + column;
                    var area        = Areas[HexAreas[hexIndex]];

                    for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                    {
                        int neighbour = grid.Neighbour(hexIndex, n);
                        if (neighbour < 0 || HexAreas[neighbour] == HexAreas[hexIndex])
                        {
                            continue;
                        }

                        var list = IsTraversable(HexTypes[hexIndex], HexTypes[neighbour]) ? area.Traversable : area.NonTraversable;
                        if (list.Contains(HexAreas[neighbour]) == false)
                        {
                            list.Add(HexAreas[neighbour]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// An area's centre is its hex nearest the middle of its world rectangle. The builder
        /// compares squared float distances, column by column, keeping the first of equals, and
        /// float rounding decides between hexes that are exactly as near.
        /// </summary>
        private void FindCentres(MapDataGrid grid, MapDataGeometry geometry)
        {
            var middleX     = new float[Areas.Count];
            var middleY     = new float[Areas.Count];
            var nearest     = new float[Areas.Count];

            for (int areaId = 0; areaId < Areas.Count; ++areaId)
            {
                var area = Areas[areaId];
                geometry.Bounds(area.MinColumn, area.MinRow, area.MaxColumn, area.MaxRow, out float minX, out float minY, out float maxX, out float maxY);

                middleX[areaId] = (float)((float)(minX + maxX) / 2f);
                middleY[areaId] = (float)((float)(minY + maxY) / 2f);
                nearest[areaId] = float.MaxValue;
            }

            for (int column = 0; column < grid.Width; ++column)
            {
                float x = geometry.CentreX(column);

                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex    = row * grid.Width + column;
                    int areaId      = HexAreas[hexIndex];
                    float dx        = (float)(x - middleX[areaId]);
                    float dy        = (float)(geometry.CentreY(column, row) - middleY[areaId]);
                    float distance  = (float)((float)(dx * dx) + (float)(dy * dy));

                    if (distance < nearest[areaId])
                    {
                        nearest[areaId]         = distance;
                        Areas[areaId].CentreHex = hexIndex;
                    }
                }
            }
        }

        private sealed class DisjointSets
        {
            private readonly int[] parent;

            public DisjointSets(int count)
            {
                parent = new int[count];
                for (int i = 0; i < count; ++i)
                {
                    parent[i] = i;
                }
            }

            public int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            public void Union(int a, int b)
            {
                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA != rootB)
                {
                    parent[rootB] = rootA;
                }
            }
        }
    }
}
