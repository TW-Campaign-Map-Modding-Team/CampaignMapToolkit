using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// Fills a new, empty map of the same size from the contents of any game's map_data.esf.
    /// map_data.esf keeps everything map.hex holds except trade routes and restriction levels,
    /// which are left empty, and a few things that are inferred: which regions and ground types
    /// are sea, and what an impassable hex is underneath. However they are inferred, the map
    /// writes the same map_data.esf back.
    /// </summary>
    public static class MapDataImporter
    {
        public static void Import(MapDataContents contents, MapHexFile map)
        {
            if (map.MapWidth != contents.Width || map.MapHeight != contents.Height)
            {
                throw new ArgumentException($"The map is {map.MapWidth}x{map.MapHeight}; map_data.esf is {contents.Width}x{contents.Height}.", nameof(map));
            }

            int landGroundTypeCount = CountLandGroundTypes(contents);

            map.LandGroundTypes.AddRange(contents.GroundTypes.GetRange(0, landGroundTypeCount));
            map.SeaGroundTypes.AddRange(contents.GroundTypes.GetRange(landGroundTypeCount, contents.GroundTypes.Count - landGroundTypeCount));
            map.Climates.AddRange(contents.Climates);
            map.Attritions.AddRange(contents.Attritions);
            map.AreasOfInterest?.AddRange(contents.AreasOfInterest); // null in the games without them
            map.SetCampaignMasks(contents.MaskNames);

            ImportRegions(contents, map);
            ImportHexes(contents, map);
            MarkImpassableBeaches(contents, map);
            ImportSettlements(contents, map);

            BordersExporter.GenerateRegionEdges(map);
            MapHexFile.RegionMasksNeedRecalculation = true;

            map.SetDirty();
        }

        /// <summary>
        /// map.hex lists its land ground types before its sea ground types, and map_data.esf keeps
        /// both as one list, so the first ground type found under passable sea starts the sea
        /// ground types. A map without passable sea gives no answer and keeps them all as land.
        /// </summary>
        private static int CountLandGroundTypes(MapDataContents contents)
        {
            int firstSeaGroundType = contents.GroundTypes.Count;

            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                var areaType    = contents.HexAreaTypes[hexIndex];
                var groundType  = contents.HexGroundTypes[hexIndex];

                if ((areaType == MapDataAreaType.Sea || areaType == MapDataAreaType.Bridge)
                    && groundType != MapDataContents.None
                    && groundType < firstSeaGroundType)
                {
                    firstSeaGroundType = groundType;
                }
            }

            if (firstSeaGroundType == contents.GroundTypes.Count)
            {
                LoggerViewModel.Log("map_data.esf has no passable sea, so every ground type has been imported as land. Check the ground types against the database.", LogLevel.Warning);
            }

            return firstSeaGroundType;
        }

        /// <summary>
        /// Splits the regions into map.hex's land and sea lists and sets their colours. map_data.esf
        /// lists them in map.hex's order, land first, so they keep that order and only where the
        /// sea regions start is worked out.
        /// </summary>
        private static void ImportRegions(MapDataContents contents, MapHexFile map)
        {
            int landRegionCount = CountLandRegions(contents, map);
            var regions         = contents.Regions;

            map.LandRegions.AddRange(regions.Take(landRegionCount).Select(region => region.Key));
            map.SeaRegions.AddRange(regions.Skip(landRegionCount).Select(region => region.Key));

            var landColours = regions.Take(landRegionCount).Select(region => region.Colour).ToList();
            var seaColours  = regions.Skip(landRegionCount).Select(region => region.Colour).ToList();

            map.ColoursLand.SetColours(MergeColours(landColours, map.ColoursLand.Colours, count => MapHexFile.GenerateLandColours(count)));
            map.ColoursSea.SetColours(MergeColours(seaColours, map.ColoursSea.Colours, count => MapHexFile.GenerateSeaColours(count)));
        }

        /// <summary>
        /// The land regions run up to the last one that looks like land: one with a settlement, or
        /// with land ground and no sea flag. Only settlements depend on where the sea regions start,
        /// so any split that keeps them on land writes the same map_data.esf.
        /// </summary>
        private static int CountLandRegions(MapDataContents contents, MapHexFile map)
        {
            var hasLandGround = new bool[contents.Regions.Count];

            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                if (map.IsSeaGroundType(contents.HexGroundTypes[hexIndex]) == false)
                {
                    hasLandGround[contents.HexRegions[hexIndex]] = true;
                }
            }

            int landRegionCount = 0;
            for (int region = 0; region < contents.Regions.Count; ++region)
            {
                var info = contents.Regions[region];
                if (info.PrimarySlotHexes.Count > 0 || (hasLandGround[region] && info.HasPassableSea == false))
                {
                    landRegionCount = region + 1;
                }
            }

            return landRegionCount;
        }

        /// <summary>
        /// map_data.esf has no colour for a region past the end of the source map.hex's colour
        /// array; such regions keep the random colour the new map was created with.
        /// </summary>
        private static int[] MergeColours(List<uint> regionColours, int[] newMapColours, Func<int, int[]> generateColours)
        {
            var colours = new List<int>(newMapColours);
            if (colours.Count < regionColours.Count)
            {
                colours.AddRange(generateColours(regionColours.Count - colours.Count));
            }

            for (int i = 0; i < regionColours.Count; ++i)
            {
                if (regionColours[i] != MapDataContents.UnassignedColour)
                {
                    colours[i] = unchecked((int)regionColours[i]);
                }
            }

            return colours.ToArray();
        }

        /// <summary>
        /// A passable hex's area type says whether it is sea, a bridge, beach or cliff. An impassable
        /// hex's says none of that: it is sea when its ground is.
        /// </summary>
        private static void ImportHexes(MapDataContents contents, MapHexFile map)
        {
            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                var hex         = map.HexData[hexIndex];
                var areaType    = contents.HexAreaTypes[hexIndex];

                hex.RegionId        = contents.HexRegions[hexIndex];
                hex.GroundTypeIndex = contents.HexGroundTypes[hexIndex];
                hex.ClimateIndex    = contents.HexClimates[hexIndex];
                hex.AttritionIndex  = contents.HexAttritions[hexIndex];
                hex.InterestIndex   = AreaOfInterest(contents, map, hexIndex);

                hex.RoadEdgeMask    = contents.HexRoadMasks[hexIndex];
                hex.IsRoad          = hex.RoadEdgeMask != 0;
                hex.RiverEdgeMask   = contents.HexRiverMasks[hexIndex];
                hex.IsRiver         = hex.RiverEdgeMask != 0;

                hex.IsPassable      = areaType != MapDataAreaType.Impassable;
                hex.IsSea           = hex.IsPassable ? areaType == MapDataAreaType.Sea || areaType == MapDataAreaType.Bridge : map.IsSeaGroundType(hex.GroundTypeIndex);
                hex.IsBridge        = areaType == MapDataAreaType.Bridge;
                hex.IsBeach         = areaType == MapDataAreaType.Beach;
                hex.IsCliff         = areaType == MapDataAreaType.Cliff || areaType == MapDataAreaType.BridgeCliff;
            }
        }

        /// <summary>
        /// The builder copies bits 3-8 of map.hex's bytes 8-9, where the area of interest index + 1
        /// starts at <see cref="MapHexFile.AreaOfInterestShift"/>.
        /// </summary>
        private static sbyte AreaOfInterest(MapDataContents contents, MapHexFile map, int hexIndex)
        {
            int index = ((contents.HexAreaOfInterestBits[hexIndex] << 3) >> map.AreaOfInterestShift) - 1;
            return index >= 0 && index < contents.AreasOfInterest.Count ? (sbyte)index : MapDataContents.None;
        }

        /// <summary>
        /// map_data.esf does not say whether an impassable land hex on the coast is beach or cliff.
        /// CA's maps make it a beach when it touches a passable beach hex, which this follows; on
        /// wh3_main_combi_map_7 that matches 9,485 of the 9,592 such hexes.
        /// </summary>
        private static void MarkImpassableBeaches(MapDataContents contents, MapHexFile map)
        {
            foreach (var hex in map.HexData)
            {
                if (hex.IsPassable || hex.IsSea)
                {
                    continue;
                }

                bool touchesSea     = false;
                bool touchesBeach   = false;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = map.GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    touchesSea      |= map.HexData[nbrIndex].IsSea;
                    touchesBeach    |= contents.HexAreaTypes[nbrIndex] == MapDataAreaType.Beach;
                }

                hex.IsBeach = touchesSea && touchesBeach;
            }
        }

        /// <summary>
        /// A settlement's footprint is the town sprawl the builder filled. Some games' port slot
        /// block also lists the main slot's hexes, so the main slot is applied after it.
        /// </summary>
        private static void ImportSettlements(MapDataContents contents, MapHexFile map)
        {
            foreach (var region in contents.Regions)
            {
                foreach (int hexIndex in region.SprawlHexes)
                {
                    map.HexData[hexIndex].IsTownSprawl = true;
                }

                SetTownSlot(map, region.PortSlotHexes, Hex.PORT_SLOT_INDEX);
                SetTownSlot(map, region.PrimarySlotHexes, Hex.MAIN_SLOT_INDEX);

                for (int slot = 0; slot < region.OtherSlotHexes.Count; ++slot)
                {
                    SetTownSlot(map, region.OtherSlotHexes[slot], (sbyte)(Hex.PORT_SLOT_INDEX + 1 + slot));
                }
            }
        }

        private static void SetTownSlot(MapHexFile map, List<int> hexIndices, sbyte slotIndex)
        {
            foreach (int hexIndex in hexIndices)
            {
                var hex             = map.HexData[hexIndex];
                hex.TownSlotIndex   = slotIndex;
                hex.IsTownSprawl    = true;
            }
        }
    }
}
