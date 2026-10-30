using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsfLibrary;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// Reads the map.hex data any game's map_data.esf keeps. The layouts are the ones our map_data
    /// writers write (<see cref="Exporters.MapDataWriter"/>):
    /// <list type="bullet">
    /// <item>MAP_DATA v0 (Rome II, Attila, Thrones): the regions sit in REGIONS_DATA, and each hex's
    /// region and area are in its HEX_MAP_DATA bytes.</item>
    /// <item>MAP_DATA v1 (Warhammer, Warhammer II, Troy, Pharaoh): one MASKED_REGIONS_DATA entry per
    /// campaign mask, with the hexes' areas as runs in a u16 array; settlements have building slots.</item>
    /// <item>MAP_DATA v2 (Warhammer III, Three Kingdoms): one MASKED_REGIONS_DATA entry, with the runs
    /// as a block (Warhammer III) or a u16 array (Three Kingdoms); settlements have a main and a port slot.</item>
    /// </list>
    /// </summary>
    public static class MapDataReader
    {
        private const int PACKED_AREA_SHIFT = 9;
        private const int PACKED_REGION_MASK = (1 << PACKED_AREA_SHIFT) - 1;

        public static MapDataContents Read(string mapDataEsfPath)
        {
            var root = EsfCodecUtil.LoadEsfFile(mapDataEsfPath).RootNode as ParentNode;
            if (root == null || root.Name != "MAP_DATA" || root.Version > (int)MapDataLayout.Warhammer3)
            {
                throw new InvalidDataException($"'{mapDataEsfPath}' is not a map_data.esf: its root is not MAP_DATA version 0, 1 or 2.");
            }

            var contents = new MapDataContents { Layout = (MapDataLayout)root.Version };

            var dataVersion = root.Children.FirstOrDefault(child => child.Name == "MAP_FILE_DATA_VERSION");
            if (dataVersion != null)
            {
                contents.FileDataVersion = (ushort)ToInt(dataVersion.Values[0]);
            }

            var regionsData = Child(root, "REGIONS_DATA");
            ReadNameLists(regionsData, contents);

            // Attila's layout keeps the theatres in REGIONS_DATA; the others keep them in MAP_DATA.
            var theatres = contents.Layout == MapDataLayout.Attila ? Child(regionsData, "theatres") : Child(root, "theatres");
            var hexBytes = ReadTheatre(Child(FirstEntry(theatres), "CAMPAIGN_THEATRE"), contents);

            if (contents.Layout == MapDataLayout.Attila)
            {
                var regionAreaTypes = ReadRegions(Child(regionsData, "REGIONS_BLOCK"), contents);
                ReadAttilaHexRegions(hexBytes, regionAreaTypes, contents);
                contents.Masks.Add(new MapDataMaskInfo { Name = string.Empty });
            }
            else
            {
                var maskedRegions   = Child(regionsData, "MASKED_REGIONS_DATA");
                var unmasked        = FirstEntry(maskedRegions);
                var regionAreaTypes = ReadRegions(Child(unmasked, "REGIONS_BLOCK"), contents);

                // A block in Warhammer III; in the others the u16 array after the empty one.
                var runBlock = unmasked.Children.FirstOrDefault(child => child.Name == "REGION_AREA_INDEX_OVERRIDE");
                ReadHexRegions(runBlock != null ? RunsFromBlock(runBlock) : RunsFromArray(unmasked), regionAreaTypes, contents);

                contents.Masks.AddRange(maskedRegions.Children.Select(ReadMask));
            }

            ReadColours(regionsData, contents);
            return contents;
        }

        /// <summary>
        /// The games whose map_data.esf is laid out as <paramref name="contents"/>' was. Warhammer III
        /// and Three Kingdoms differ in MAP_FILE_DATA_VERSION (0 and 2); the games sharing the other
        /// layouts cannot be told apart from the file.
        /// </summary>
        public static GameTemplate[] Games(MapDataContents contents)
        {
            switch (contents.Layout)
            {
                case MapDataLayout.Attila:
                    return new[] { GameTemplate.Rome2, GameTemplate.Attila, GameTemplate.Thrones_Of_Britannia };
                case MapDataLayout.Pharaoh:
                    return new[] { GameTemplate.Warhammer, GameTemplate.Warhammer2, GameTemplate.Troy, GameTemplate.Pharaoh, GameTemplate.Pharaoh_Dynasties };
                default:
                    return new[] { contents.FileDataVersion == 2 ? GameTemplate.Three_Kingdoms : GameTemplate.Warhammer3 };
            }
        }

        /// <summary>
        /// The theatre's bounds and name, and the hex map. A v2 theatre (the shipped Rome II file) also
        /// holds a colour table, which is skipped.
        /// </summary>
        /// <returns>HEX_MAP_DATA's bytes.</returns>
        private static byte[] ReadTheatre(ParentNode theatre, MapDataContents contents)
        {
            var maxCorner           = theatre.Values.OfType<EsfValueNode<Tuple<float, float>>>().ElementAt(1).Value;
            contents.MaxX           = maxCorner.Item1;
            contents.MaxY           = maxCorner.Item2;
            contents.TheatreIndex   = theatre.Values.OfType<EsfValueNode<string>>().First().Value;

            return ReadHexMap(Child(theatre, "HEX_MAP_DATA"), contents);
        }

        /// <summary>
        /// The bytes each hex has, by HEX_MAP_DATA version:
        /// <list type="bullet">
        /// <item>v3 (Attila's layout), seven: the region's low byte, the area's index within the region
        /// shifted up one above the region's ninth bit, then ground type, climate, attrition + 1,
        /// road and river masks.</item>
        /// <item>v4 (Pharaoh's layout), five: ground type, climate, attrition + 1, road and river masks.</item>
        /// <item>v5 (Warhammer III's layout), six: as v4 with the area of interest bits after attrition.</item>
        /// </list>
        /// The region and area bytes are read with the regions (<see cref="ReadAttilaHexRegions"/>).
        /// </summary>
        private static byte[] ReadHexMap(ParentNode hexMapData, MapDataContents contents)
        {
            int bytesPerHex;
            switch (hexMapData.Version)
            {
                case 3:     bytesPerHex = 7; break;
                case 4:     bytesPerHex = 5; break;
                case 5:     bytesPerHex = 6; break;
                default:    throw new InvalidDataException($"Unsupported HEX_MAP_DATA version {hexMapData.Version}; expected 3, 4 or 5.");
            }

            var values          = hexMapData.Values;
            contents.Width      = (uint)ToInt(values[0]);
            contents.Height     = (uint)ToInt(values[1]);

            var hexBytes        = ((EsfValueNode<byte[]>)values[2]).Value;
            int hexCount        = contents.HexCount;

            if (hexBytes.Length != hexCount * bytesPerHex)
            {
                throw new InvalidDataException($"HEX_MAP_DATA holds {hexBytes.Length} bytes; a {contents.Width}x{contents.Height} map needs {hexCount * bytesPerHex}.");
            }

            contents.HexGroundTypes         = new sbyte[hexCount];
            contents.HexClimates            = new sbyte[hexCount];
            contents.HexAttritions          = new sbyte[hexCount];
            contents.HexAreaOfInterestBits  = new byte[hexCount];
            contents.HexRoadMasks           = new byte[hexCount];
            contents.HexRiverMasks          = new byte[hexCount];
            contents.HexRegions             = new int[hexCount];
            contents.HexAreaTypes           = new MapDataAreaType[hexCount];

            int first = hexMapData.Version == 3 ? 2 : 0;
            for (int hexIndex = 0; hexIndex < hexCount; ++hexIndex)
            {
                int offset = hexIndex * bytesPerHex + first;

                contents.HexGroundTypes[hexIndex]   = IndexOrNone(hexBytes[offset + 0], contents.GroundTypes);
                contents.HexClimates[hexIndex]      = IndexOrNone(hexBytes[offset + 1], contents.Climates);
                contents.HexAttritions[hexIndex]    = IndexOrNone(hexBytes[offset + 2] - 1, contents.Attritions);

                if (hexMapData.Version == 5)
                {
                    contents.HexAreaOfInterestBits[hexIndex] = hexBytes[offset + 3];
                    ++offset;
                }

                contents.HexRoadMasks[hexIndex]     = hexBytes[offset + 3];
                contents.HexRiverMasks[hexIndex]    = hexBytes[offset + 4];
            }

            return hexBytes;
        }

        private static sbyte IndexOrNone(int index, List<string> names)
        {
            return index >= 0 && index < names.Count ? (sbyte)index : MapDataContents.None;
        }

        /// <summary>Climates, ground types and attritions, then, in Warhammer III's layout, areas of interest.</summary>
        private static void ReadNameLists(ParentNode regionsData, MapDataContents contents)
        {
            var lists = regionsData.Values.OfType<EsfValueNode<string[]>>().Select(node => node.Value).ToList();
            if (lists.Count != 3 && lists.Count != 4)
            {
                throw new InvalidDataException($"REGIONS_DATA holds {lists.Count} name lists; expected climates, ground types, attritions and perhaps areas of interest.");
            }

            contents.Climates.AddRange(lists[0]);
            contents.GroundTypes.AddRange(lists[1]);
            contents.Attritions.AddRange(lists[2]);

            if (lists.Count == 4)
            {
                contents.AreasOfInterest.AddRange(lists[3]);
            }
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
                    ReadSettlement(settlement, contents.Width, region);
                }

                regionAreaTypes.Add(Child(regionData, "REGION_AREAS").Children
                    .Select(area => (MapDataAreaType)ToInt(Child(area, "REGION_AREA_DATA").Values[0]))
                    .ToArray());

                contents.Regions.Add(region);
            }

            return regionAreaTypes;
        }

        /// <summary>
        /// The footprint, which is the town sprawl the builder filled, and the slots. A settlement with
        /// building slots (SETTLEMENT_INFO v0) lists the main slot, then slots 2 upwards, then the port;
        /// one without (v1) the main slot and the port.
        /// </summary>
        private static void ReadSettlement(ParentNode settlement, uint width, MapDataRegionInfo region)
        {
            ReadFootprint(Child(settlement, "LOGICAL_POSITION_BIT_ARRAY"), width, region.SprawlHexes);

            if (settlement.Version == 0)
            {
                var slots = Child(settlement, "SLOT_ARRAY_BLOCK").Children;
                for (int slot = 0; slot < slots.Count; ++slot)
                {
                    var hexes = slot == 0 ? region.PrimarySlotHexes : new List<int>();
                    ReadHexList(Child(slots[slot], "SLOT_AREA_BLOCK"), width, hexes);

                    if (slot > 0)
                    {
                        region.OtherSlotHexes.Add(hexes);
                    }
                }

                ReadHexList(Child(settlement, "PORT_AREA_BLOCK"), width, region.PortSlotHexes);
            }
            else
            {
                ReadHexList(Child(settlement, "PRIMARY_SLOT_AREA_BLOCK"), width, region.PrimarySlotHexes);
                ReadHexList(Child(settlement, "PORT_SLOT_AREA_BLOCK"), width, region.PortSlotHexes);
            }
        }

        /// <summary>Its hex rectangle, its width, and one bit per hex, row by row, lowest bit first.</summary>
        private static void ReadFootprint(ParentNode footprint, uint width, List<int> hexIndices)
        {
            var values      = footprint.Values;
            int minColumn   = ToInt(values[0]);
            int minRow      = ToInt(values[1]);
            int rowLength   = ToInt(values[4]);
            var bits        = ((EsfValueNode<byte[]>)values[5]).Value;

            for (int bit = 0; bit < bits.Length * 8; ++bit)
            {
                if ((bits[bit / 8] & (1 << (bit % 8))) != 0)
                {
                    hexIndices.Add((minRow + bit / rowLength) * (int)width + minColumn + bit % rowLength);
                }
            }
        }

        private static void ReadHexList(ParentNode block, uint width, List<int> hexIndices)
        {
            foreach (var entry in block.Children)
            {
                var values = entry.Values;
                hexIndices.Add(ToInt(values[1]) * (int)width + ToInt(values[0]));
            }
        }

        /// <summary>Warhammer III: a block of runs, each a REGION_AREA_INDEX record and a length.</summary>
        private static IEnumerable<(int Region, int Area, int Length)> RunsFromBlock(ParentNode runBlock)
        {
            foreach (var run in runBlock.Children)
            {
                var regionArea = Child(run, "REGION_AREA_INDEX").Values;
                yield return (ToInt(regionArea[0]), ToInt(regionArea[1]), ToInt(run.Values[0]));
            }
        }

        /// <summary>Pharaoh's layout and Three Kingdoms: pairs of area &lt;&lt; 9 | region and a length.</summary>
        /// <summary>An entry's mask: its name, then its runs, the entry's first u16 array.</summary>
        private static MapDataMaskInfo ReadMask(ParentNode maskedEntry)
        {
            return new MapDataMaskInfo
            {
                Name = ((EsfValueNode<string>)maskedEntry.Values[0]).Value,
                Runs = maskedEntry.Values.OfType<EsfValueNode<ushort[]>>().First().Value,
            };
        }

        private static IEnumerable<(int Region, int Area, int Length)> RunsFromArray(ParentNode maskedEntry)
        {
            var runs = maskedEntry.Values.OfType<EsfValueNode<ushort[]>>().Last().Value;
            for (int i = 0; i + 1 < runs.Length; i += 2)
            {
                yield return (runs[i] & PACKED_REGION_MASK, runs[i] >> PACKED_AREA_SHIFT, runs[i + 1]);
            }
        }

        /// <summary>The region and area of every hex, from runs over the row-major grid.</summary>
        private static void ReadHexRegions(IEnumerable<(int Region, int Area, int Length)> runs, List<MapDataAreaType[]> regionAreaTypes, MapDataContents contents)
        {
            int hexCount = contents.HexCount;

            int hexIndex = 0;
            foreach (var run in runs)
            {
                var areaType = AreaType(regionAreaTypes, run.Region, run.Area);

                if (run.Length < 0 || hexIndex + run.Length > hexCount)
                {
                    throw new InvalidDataException($"The region runs cover more than the map's {hexCount} hexes.");
                }

                for (int end = hexIndex + run.Length; hexIndex < end; ++hexIndex)
                {
                    contents.HexRegions[hexIndex]   = run.Region;
                    contents.HexAreaTypes[hexIndex] = areaType;
                }
            }

            if (hexIndex != hexCount)
            {
                throw new InvalidDataException($"The region runs cover {hexIndex} of the map's {hexCount} hexes.");
            }
        }

        /// <summary>Attila's layout: each hex's first two HEX_MAP_DATA bytes hold its region and area.</summary>
        private static void ReadAttilaHexRegions(byte[] hexBytes, List<MapDataAreaType[]> regionAreaTypes, MapDataContents contents)
        {
            const int BYTES_PER_HEX = 7;

            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                int offset  = hexIndex * BYTES_PER_HEX;
                int region  = hexBytes[offset] | ((hexBytes[offset + 1] & 1) << 8);
                int area    = hexBytes[offset + 1] >> 1;

                contents.HexRegions[hexIndex]   = region;
                contents.HexAreaTypes[hexIndex] = AreaType(regionAreaTypes, region, area);
            }
        }

        private static MapDataAreaType AreaType(List<MapDataAreaType[]> regionAreaTypes, int region, int area)
        {
            if (region >= regionAreaTypes.Count || area >= regionAreaTypes[region].Length)
            {
                throw new InvalidDataException($"A hex is in area {area} of region {region}, which the file does not have.");
            }

            return regionAreaTypes[region][area];
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
                case EsfValueNode<bool> value:      return value.Value ? 1 : 0;
                default:
                    throw new InvalidDataException($"Expected a number, found {node.TypeCode}.");
            }
        }
    }
}
