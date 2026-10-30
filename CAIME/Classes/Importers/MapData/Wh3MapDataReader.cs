using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsfLibrary;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// Reads the map.hex data a Warhammer III map_data.esf (MAP_DATA v2, HEX_MAP_DATA v5) keeps.
    /// The layout was worked out by rebuilding map_data.esf from edited map.hex files with the
    /// Assembly Kit's ToolDataBuilder and comparing the output.
    /// </summary>
    public static class Wh3MapDataReader
    {
        private const int MAP_DATA_VERSION      = 2;
        private const int HEX_MAP_DATA_VERSION  = 5;
        private const int BYTES_PER_HEX         = 6;

        public static MapDataContents Read(string mapDataEsfPath)
        {
            var root = EsfCodecUtil.LoadEsfFile(mapDataEsfPath).RootNode as ParentNode;
            if (root == null || root.Name != "MAP_DATA" || root.Version != MAP_DATA_VERSION)
            {
                throw new InvalidDataException(
                    $"'{mapDataEsfPath}' is not a Warhammer III map_data.esf: its root is not MAP_DATA version {MAP_DATA_VERSION}.");
            }

            var contents        = new MapDataContents();
            var regionsData     = Child(root, "REGIONS_DATA");
            var maskedRegions   = FirstEntry(Child(regionsData, "MASKED_REGIONS_DATA"));

            ReadNameLists(regionsData, contents);
            ReadHexMap(Child(Child(FirstEntry(Child(root, "theatres")), "CAMPAIGN_THEATRE"), "HEX_MAP_DATA"), contents);

            var regionAreaTypes = ReadRegions(Child(maskedRegions, "REGIONS_BLOCK"), contents);
            ReadColours(regionsData, contents);
            ReadHexRegions(Child(maskedRegions, "REGION_AREA_INDEX_OVERRIDE"), regionAreaTypes, contents);

            return contents;
        }

        /// <summary>
        /// Six bytes per hex: ground type, climate, attrition + 1, the area of interest as the
        /// builder copies it from map.hex (index + 1 above one unrelated bit), road and river masks.
        /// </summary>
        private static void ReadHexMap(ParentNode hexMapData, MapDataContents contents)
        {
            if (hexMapData.Version != HEX_MAP_DATA_VERSION)
            {
                throw new InvalidDataException($"Unsupported HEX_MAP_DATA version {hexMapData.Version}; expected {HEX_MAP_DATA_VERSION}.");
            }

            var values          = hexMapData.Values;
            contents.Width      = (uint)ToInt(values[0]);
            contents.Height     = (uint)ToInt(values[1]);

            var hexBytes        = ((EsfValueNode<byte[]>)values[2]).Value;
            int hexCount        = contents.HexCount;

            if (hexBytes.Length != hexCount * BYTES_PER_HEX)
            {
                throw new InvalidDataException($"HEX_MAP_DATA holds {hexBytes.Length} bytes; a {contents.Width}x{contents.Height} map needs {hexCount * BYTES_PER_HEX}.");
            }

            contents.HexGroundTypes     = new sbyte[hexCount];
            contents.HexClimates        = new sbyte[hexCount];
            contents.HexAttritions      = new sbyte[hexCount];
            contents.HexAreasOfInterest = new sbyte[hexCount];
            contents.HexRoadMasks       = new byte[hexCount];
            contents.HexRiverMasks      = new byte[hexCount];

            for (int hexIndex = 0; hexIndex < hexCount; ++hexIndex)
            {
                int offset = hexIndex * BYTES_PER_HEX;

                contents.HexGroundTypes[hexIndex]       = IndexOrNone(hexBytes[offset + 0], contents.GroundTypes);
                contents.HexClimates[hexIndex]          = IndexOrNone(hexBytes[offset + 1], contents.Climates);
                contents.HexAttritions[hexIndex]        = IndexOrNone(hexBytes[offset + 2] - 1, contents.Attritions);
                contents.HexAreasOfInterest[hexIndex]   = IndexOrNone((hexBytes[offset + 3] >> 1) - 1, contents.AreasOfInterest);
                contents.HexRoadMasks[hexIndex]         = hexBytes[offset + 4];
                contents.HexRiverMasks[hexIndex]        = hexBytes[offset + 5];
            }
        }

        private static sbyte IndexOrNone(int index, List<string> names)
        {
            return index >= 0 && index < names.Count ? (sbyte)index : MapDataContents.None;
        }

        private static void ReadNameLists(ParentNode regionsData, MapDataContents contents)
        {
            var lists = regionsData.Values.OfType<EsfValueNode<string[]>>().Select(node => node.Value).ToList();
            if (lists.Count != 4)
            {
                throw new InvalidDataException($"REGIONS_DATA holds {lists.Count} name lists; expected climates, ground types, attritions and areas of interest.");
            }

            contents.Climates.AddRange(lists[0]);
            contents.GroundTypes.AddRange(lists[1]);
            contents.Attritions.AddRange(lists[2]);
            contents.AreasOfInterest.AddRange(lists[3]);
        }

        private static void ReadColours(ParentNode regionsData, MapDataContents contents)
        {
            var colours = regionsData.Values.OfType<EsfValueNode<uint[]>>().Single().Value;
            if (colours.Length != contents.Regions.Count)
            {
                throw new InvalidDataException($"REGIONS_DATA holds {colours.Length} region colours for {contents.Regions.Count} regions.");
            }

            for (int i = 0; i < colours.Length; ++i)
            {
                contents.Regions[i].Colour = colours[i];
            }
        }

        /// <returns>The area types of each region's areas, indexed by region then area.</returns>
        private static List<MapDataAreaType[]> ReadRegions(ParentNode regionsBlock, MapDataContents contents)
        {
            var regionAreaTypes = new List<MapDataAreaType[]>();

            foreach (var entry in regionsBlock.Children)
            {
                var regionData  = Child(entry, "REGION_DATA");
                var values      = regionData.Values;

                var region = new MapDataRegionInfo
                {
                    Key             = ((EsfValueNode<string>)values[0]).Value,
                    HasPassableSea  = ToInt(values[1]) != 0,
                };

                var settlement = regionData.Children.FirstOrDefault(child => child.Name == "SETTLEMENT_INFO");
                if (settlement != null)
                {
                    ReadSlotHexes(Child(settlement, "PRIMARY_SLOT_AREA_BLOCK"), contents.Width, region.PrimarySlotHexes);
                    ReadSlotHexes(Child(settlement, "PORT_SLOT_AREA_BLOCK"), contents.Width, region.PortSlotHexes);
                }

                regionAreaTypes.Add(Child(regionData, "REGION_AREAS").Children
                    .Select(area => (MapDataAreaType)ToInt(Child(area, "REGION_AREA_DATA").Values[0]))
                    .ToArray());

                contents.Regions.Add(region);
            }

            return regionAreaTypes;
        }

        private static void ReadSlotHexes(ParentNode slotBlock, uint width, List<int> hexIndices)
        {
            foreach (var entry in slotBlock.Children)
            {
                var values = entry.Values;
                hexIndices.Add(ToInt(values[1]) * (int)width + ToInt(values[0]));
            }
        }

        /// <summary>
        /// The region and area of every hex, stored as runs of (region, area, hex count) over the
        /// row-major grid.
        /// </summary>
        private static void ReadHexRegions(ParentNode overrideRuns, List<MapDataAreaType[]> regionAreaTypes, MapDataContents contents)
        {
            int hexCount            = contents.HexCount;
            contents.HexRegions     = new int[hexCount];
            contents.HexAreaTypes   = new MapDataAreaType[hexCount];

            int hexIndex = 0;
            foreach (var run in overrideRuns.Children)
            {
                var regionArea  = Child(run, "REGION_AREA_INDEX").Values;
                int region      = ToInt(regionArea[0]);
                var areaType    = regionAreaTypes[region][ToInt(regionArea[1])];
                int runLength   = ToInt(run.Values[0]);

                if (hexIndex + runLength > hexCount)
                {
                    throw new InvalidDataException($"REGION_AREA_INDEX_OVERRIDE covers more than the map's {hexCount} hexes.");
                }

                for (int end = hexIndex + runLength; hexIndex < end; ++hexIndex)
                {
                    contents.HexRegions[hexIndex]   = region;
                    contents.HexAreaTypes[hexIndex] = areaType;
                }
            }

            if (hexIndex != hexCount)
            {
                throw new InvalidDataException($"REGION_AREA_INDEX_OVERRIDE covers {hexIndex} of the map's {hexCount} hexes.");
            }
        }

        private static ParentNode Child(ParentNode parent, string name)
        {
            var child = parent.Children.FirstOrDefault(node => node.Name == name);
            if (child == null)
            {
                throw new InvalidDataException($"{parent.Name} has no {name} record.");
            }

            return child;
        }

        private static ParentNode FirstEntry(ParentNode recordArray)
        {
            if (recordArray.Children.Count == 0)
            {
                throw new InvalidDataException($"{recordArray.Name} is empty.");
            }

            return recordArray.Children[0];
        }

        private static int ToInt(EsfNode node)
        {
            switch (node)
            {
                case EsfValueNode<byte> value:      return value.Value;
                case EsfValueNode<ushort> value:    return value.Value;
                case EsfValueNode<int> value:       return value.Value;
                case EsfValueNode<uint> value:      return checked((int)value.Value);
                default:
                    throw new InvalidDataException($"Expected a number, found {node.TypeCode}.");
            }
        }
    }
}
