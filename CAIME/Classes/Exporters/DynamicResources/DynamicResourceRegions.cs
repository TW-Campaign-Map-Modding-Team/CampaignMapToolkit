using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

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
    /// map, as the Assembly Kit's builder does. Worked out from Attila's main_attila_map, whose
    /// output this reproduces apart from the hex order of one group (see <see cref="GroupHexes"/>).
    ///
    /// <para>
    /// The image has two pixels per hex column and two per row, plus one for the odd columns'
    /// half-row offset, with row 0 at the bottom: hex (c, r) reads pixel (2c, height - 1 - (2r + c % 2)).
    /// A hex whose pixel has a database colour gets that building level's tiles, if it is in a land
    /// region.
    /// </para>
    /// </summary>
    public static class DynamicResourceRegions
    {
        private const ushort REGION_KEY_SALT = 0xFEED;

        public static List<DynamicResourceRegion> Find(MapHexFile map, DynamicResourcesImage image, IReadOnlyList<DynamicResourceType> types)
        {
            var grid        = new MapDataGrid((int)map.MapWidth, (int)map.MapHeight);
            var hexTypes    = ClassifyHexes(map, grid, image, types);
            var groups      = GroupHexes(map, grid, hexTypes);

            var regions = new Dictionary<int, DynamicResourceRegion>();
            foreach (var entry in groups.OrderBy(entry => types[entry.Key.Type].Level).ThenBy(entry => entry.Key.Type))
            {
                if (regions.TryGetValue(entry.Key.Region, out var region) == false)
                {
                    region = new DynamicResourceRegion { Region = entry.Key.Region };
                    regions.Add(entry.Key.Region, region);
                }

                var type = types[entry.Key.Type];
                var area = new DynamicResourceArea { BuildingSuperchain = type.BuildingSuperchain, Level = type.Level, TileUpgradeId = type.TileUpgradeId };
                foreach (var group in entry.Value)
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

            // The builder lists regions in the order of a map keyed by the region's u16 xored with
            // 0xFEED, which is ascending order of that value.
            return regions.Values.OrderBy(region => (DynamicResourcesWriter.RegionKey(region.Region) ^ REGION_KEY_SALT)).ToList();
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

            int landRegionCount = map.LandRegions.Count;
            var hexTypes        = new int[grid.HexCount];

            for (int hexIndex = 0; hexIndex < hexTypes.Length; ++hexIndex)
            {
                hexTypes[hexIndex] = -1;

                int region = map.HexData[hexIndex].RegionId;
                if (region < 0 || region >= landRegionCount)
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
        /// Splits each region's hexes of one type into connected groups by two-pass labelling: the
        /// builder walks the grid column by column, and a hex takes the smallest label among its
        /// already-labelled neighbours of the same region and type, or a new one. A group lists
        /// its labels in ascending order, each label's hexes in walking order, and groups come in
        /// the order of their first hex.
        ///
        /// <para>
        /// TODO: in main_attila_map one group of 2315 (att_reg_frisia_angulus, level 2, labels
        /// 0, 1, 3 and 4) lists label 4 before label 3. Labels 3 and 4 met each other before either
        /// met label 0; no rule tried (merge order, list concatenation, walking the merges breadth-
        /// or depth-first) reproduces that without breaking other groups.
        /// </para>
        /// </summary>
        private static Dictionary<GroupKey, List<List<int>>> GroupHexes(MapHexFile map, MapDataGrid grid, int[] hexTypes)
        {
            var labels      = Enumerable.Repeat(-1, grid.HexCount).ToArray();
            var labelHexes  = new List<List<int>>();
            var labelKeys   = new List<GroupKey>();
            var parent      = new List<int>();

            int Find(int label)
            {
                while (parent[label] != label)
                {
                    label = parent[label];
                }

                return label;
            }

            for (int column = 0; column < grid.Width; ++column)
            {
                for (int row = 0; row < grid.Height; ++row)
                {
                    int hexIndex = row * grid.Width + column;
                    if (hexTypes[hexIndex] < 0)
                    {
                        continue;
                    }

                    int region  = map.HexData[hexIndex].RegionId;
                    int label   = int.MaxValue;

                    for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                    {
                        int neighbour = grid.Neighbour(hexIndex, n);
                        if (IsLabelledAlike(map, hexTypes, labels, neighbour, region, hexTypes[hexIndex]))
                        {
                            label = Math.Min(label, labels[neighbour]);
                        }
                    }

                    if (label == int.MaxValue)
                    {
                        label = labelHexes.Count;
                        labelHexes.Add(new List<int>());
                        labelKeys.Add(new GroupKey { Region = region, Type = hexTypes[hexIndex] });
                        parent.Add(label);
                    }

                    labels[hexIndex] = label;
                    labelHexes[label].Add(hexIndex);

                    for (int n = 0; n < MapDataGrid.NEIGHBOUR_COUNT; ++n)
                    {
                        int neighbour = grid.Neighbour(hexIndex, n);
                        if (IsLabelledAlike(map, hexTypes, labels, neighbour, region, hexTypes[hexIndex]))
                        {
                            int a = Find(label);
                            int b = Find(labels[neighbour]);
                            parent[Math.Max(a, b)] = Math.Min(a, b);
                        }
                    }
                }
            }

            // Labels are numbered in walking order, so a group's smallest label is its first hex's
            // and ascending labels order both the groups and each group's labels.
            var groups      = new Dictionary<GroupKey, List<List<int>>>();
            var groupOfRoot = new Dictionary<int, List<int>>();

            for (int label = 0; label < labelHexes.Count; ++label)
            {
                int root = Find(label);
                if (groupOfRoot.TryGetValue(root, out var group) == false)
                {
                    group = new List<int>();
                    groupOfRoot.Add(root, group);

                    if (groups.TryGetValue(labelKeys[label], out var keyGroups) == false)
                    {
                        keyGroups = new List<List<int>>();
                        groups.Add(labelKeys[label], keyGroups);
                    }

                    keyGroups.Add(group);
                }

                group.AddRange(labelHexes[label]);
            }

            return groups;
        }

        private static bool IsLabelledAlike(MapHexFile map, int[] hexTypes, int[] labels, int neighbour, int region, int type)
        {
            return neighbour >= 0
                && labels[neighbour] >= 0
                && hexTypes[neighbour] == type
                && map.HexData[neighbour].RegionId == region;
        }
    }
}
