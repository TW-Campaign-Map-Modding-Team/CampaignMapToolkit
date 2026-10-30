using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using CAIME.Classes.Importers;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>A RESOURCE_TYPE of dynamic_resources_database.xml: the colour that paints one building level's tiles.</summary>
    public sealed class DynamicResourceType
    {
        public string   BuildingSuperchain  { get; set; }
        public uint     Level               { get; set; }
        public string   TileUpgradeId       { get; set; }

        /// <summary>0xRRGGBB.</summary>
        public int      Colour              { get; set; }

        public static List<DynamicResourceType> LoadDatabase(string xmlPath)
        {
            return XDocument.Load(xmlPath).Root.Elements("RESOURCE_TYPE")
                .Select(element => new DynamicResourceType
                {
                    BuildingSuperchain  = (string)element.Attribute("building_superchain"),
                    Level               = uint.Parse((string)element.Attribute("level"), CultureInfo.InvariantCulture),
                    TileUpgradeId       = (string)element.Attribute("tile_upgrade_id"),
                    Colour              = (Channel(element, "red") << 16) | (Channel(element, "green") << 8) | Channel(element, "blue"),
                })
                .ToList();
        }

        private static int Channel(XElement element, string name)
        {
            return int.Parse((string)element.Attribute(name), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// dynamic_resources.png's pixels as 0xRRGGBB, row 0 at the top as stored.
    /// </summary>
    public sealed class DynamicResourcesImage
    {
        public int      Width   { get; }
        public int      Height  { get; }
        public int[]    Pixels  { get; }

        public DynamicResourcesImage(int width, int height, int[] pixels)
        {
            Width   = width;
            Height  = height;
            Pixels  = pixels;
        }

        public static DynamicResourcesImage Load(string pngPath)
        {
            using (var bitmap = new Bitmap(pngPath))
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var pixels = new int[bitmap.Width * bitmap.Height];
                    for (int y = 0; y < bitmap.Height; ++y)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width, bitmap.Width);
                    }

                    for (int i = 0; i < pixels.Length; ++i)
                    {
                        pixels[i] &= 0xFFFFFF;
                    }

                    return new DynamicResourcesImage(bitmap.Width, bitmap.Height, pixels);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
        }
    }

    /// <summary>
    /// Works out dynamic_resources.esf's regions from dynamic_resources.png, its database and the
    /// map, as Rome II's and Attila's builders do (FileFormatDefinitions/dynamic_resources_esf.md).
    /// </summary>
    public static class DynamicResourceRegions
    {
        private const uint REGION_HASH_SALT = 0x4A545EED;

        public static List<DynamicResourceRegion> Find(MapHexFile map, DynamicResourcesImage image, IReadOnlyList<DynamicResourceType> types, GameTemplate game)
        {
            var grid        = new MapDataGrid((int)map.MapWidth, (int)map.MapHeight);
            var hexTypes    = ClassifyHexes(map, grid, image, types);
            var hexLevels   = BuildingLevels(hexTypes, types);
            var groups      = GroupHexes(map, grid, hexLevels, out var keysInWalkOrder);

            var regionsInWalkOrder  = new List<int>();
            var keysByRegion        = new Dictionary<int, List<GroupKey>>();
            foreach (var key in keysInWalkOrder)
            {
                if (keysByRegion.TryGetValue(key.Region, out var regionKeys) == false)
                {
                    regionKeys = new List<GroupKey>();
                    keysByRegion.Add(key.Region, regionKeys);
                    regionsInWalkOrder.Add(key.Region);
                }

                regionKeys.Add(key);
            }

            var hashMap = DynamicResourcesWriter.HashMap(game);
            var regions = new List<DynamicResourceRegion>();
            foreach (int regionIndex in hashMap.Iterate(regionsInWalkOrder, r => DynamicResourcesWriter.RegionKey(r) ^ REGION_HASH_SALT))
            {
                var region = new DynamicResourceRegion { Region = regionIndex };
                foreach (var key in hashMap.Iterate(keysByRegion[regionIndex], k => hashMap.StringHash(LevelKey(types[k.Type]))))
                {
                    // The tile upgrade is the one of the level's first hex in the region.
                    var type = types[hexTypes[groups[key][0][0]]];
                    var area = new DynamicResourceArea { BuildingSuperchain = type.BuildingSuperchain, Level = type.Level, TileUpgradeId = type.TileUpgradeId };
                    foreach (var group in groups[key])
                    {
                        var coordinates = new List<ushort>(group.Count * 2);
                        foreach (int hexIndex in group)
                        {
                            coordinates.Add((ushort)grid.Column(hexIndex));
                            coordinates.Add((ushort)grid.Row(hexIndex));
                        }

                        area.HexGroups.Add(coordinates);
                    }

                    region.Areas.Add(area);
                }

                regions.Add(region);
            }

            return regions;
        }

        /// <summary>The builder's key for a building level: its superchain followed by its level in decimal.</summary>
        private static string LevelKey(DynamicResourceType type)
        {
            return type.BuildingSuperchain + type.Level.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Each hex's building level, as the first database type with its superchain and level, or
        /// -1. The builder's key for a level compares only those two, so types that differ only in
        /// their tile upgrade are one level (main_rome_map paints farmland_northern colours inside
        /// farmland_mediterranean regions, and gets mediterranean tiles there).
        /// </summary>
        private static int[] BuildingLevels(int[] hexTypes, IReadOnlyList<DynamicResourceType> types)
        {
            var firstOfLevel    = new Dictionary<(string, uint), int>();
            var levelOfType     = new int[types.Count];
            for (int type = 0; type < types.Count; ++type)
            {
                var level = (types[type].BuildingSuperchain, types[type].Level);
                if (firstOfLevel.ContainsKey(level) == false)
                {
                    firstOfLevel.Add(level, type);
                }

                levelOfType[type] = firstOfLevel[level];
            }

            return hexTypes.Select(type => type < 0 ? -1 : levelOfType[type]).ToArray();
        }

        /// <summary>The database type of each hex, or -1.</summary>
        private static int[] ClassifyHexes(MapHexFile map, MapDataGrid grid, DynamicResourcesImage image, IReadOnlyList<DynamicResourceType> types)
        {
            var typeByColour = new Dictionary<int, int>();
            for (int type = 0; type < types.Count; ++type)
            {
                if (typeByColour.ContainsKey(types[type].Colour) == false)
                {
                    typeByColour.Add(types[type].Colour, type);
                }
            }

            var areaTypes   = MapDataAreas.ClassifyHexes(map, grid);
            var hexTypes    = new int[grid.HexCount];

            for (int hexIndex = 0; hexIndex < hexTypes.Length; ++hexIndex)
            {
                hexTypes[hexIndex] = -1;

                if (map.HexData[hexIndex].RegionId < 0 || areaTypes[hexIndex] == MapDataAreaType.Sea)
                {
                    continue;
                }

                int column  = grid.Column(hexIndex);
                int x       = column * 2;
                int y       = image.Height - 1 - (grid.Row(hexIndex) * 2 + (column & 1));

                if (x < image.Width && y >= 0 && typeByColour.TryGetValue(image.Pixels[y * image.Width + x], out int type))
                {
                    hexTypes[hexIndex] = type;
                }
            }

            return hexTypes;
        }

        private struct GroupKey
        {
            public int Region;
            public int Type;
        }

        /// <summary>
        /// Splits each region's hexes of one building level into groups as the builder does. It
        /// walks the grid column by column and adds each hex to the first of its level's groups
        /// that holds a neighbour, or starts a new group. Then, until a pass changes nothing, it
        /// appends every group that touches an earlier one to that earlier group, in group order,
        /// and drops the emptied groups.
        /// </summary>
        /// <param name="keysInWalkOrder">Each region and level, in the walking order of its first hex.</param>
        private static Dictionary<GroupKey, List<List<int>>> GroupHexes(MapHexFile map, MapDataGrid grid, int[] hexLevels, out List<GroupKey> keysInWalkOrder)
        {
            keysInWalkOrder = new List<GroupKey>();
            var walked      = new Dictionary<GroupKey, List<HexGroup>>();

            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex = row * grid.Width + column;
                    if (hexLevels[hexIndex] < 0)
                    {
                        continue;
                    }

                    var key = new GroupKey { Region = map.HexData[hexIndex].RegionId, Type = hexLevels[hexIndex] };
                    if (walked.TryGetValue(key, out var groups) == false)
                    {
                        groups = new List<HexGroup>();
                        walked.Add(key, groups);
                        keysInWalkOrder.Add(key);
                    }

                    var group = groups.FirstOrDefault(g => g.Touches(grid, hexIndex));
                    if (group == null)
                    {
                        group = new HexGroup();
                        groups.Add(group);
                    }

                    group.Add(hexIndex);
                }
            }

            var result = new Dictionary<GroupKey, List<List<int>>>();
            foreach (var entry in walked)
            {
                MergeTouchingGroups(grid, entry.Value);
                result.Add(entry.Key, entry.Value.Select(group => group.Hexes).ToList());
            }

            return result;
        }

        private static void MergeTouchingGroups(MapDataGrid grid, List<HexGroup> groups)
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < groups.Count; ++i)
                {
                    for (int j = i + 1; j < groups.Count && groups[i].Hexes.Count > 0; ++j)
                    {
                        if (groups[j].Hexes.Any(hexIndex => groups[i].Touches(grid, hexIndex)))
                        {
                            groups[i].AddAll(groups[j]);
                            groups[j].Clear();
                            merged = true;
                        }
                    }
                }

                groups.RemoveAll(group => group.Hexes.Count == 0);
            }
            while (merged);
        }

        private sealed class HexGroup
        {
            private readonly HashSet<int> _members = new HashSet<int>();

            public List<int> Hexes { get; } = new List<int>();

            public bool Touches(MapDataGrid grid, int hexIndex)
            {
                for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                {
                    if (_members.Contains(grid.Neighbour(hexIndex, n)))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Add(int hexIndex)
            {
                Hexes.Add(hexIndex);
                _members.Add(hexIndex);
            }

            public void AddAll(HexGroup other)
            {
                foreach (int hexIndex in other.Hexes)
                {
                    Add(hexIndex);
                }
            }

            public void Clear()
            {
                Hexes.Clear();
                _members.Clear();
            }
        }
    }
}
