using System;
using System.Collections.Generic;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// Fills a new, empty map of the same size from the contents of a map_data.esf. map_data.esf
    /// keeps everything map.hex holds except trade routes and restriction levels, which are left
    /// empty, and whether an impassable coast hex is beach or cliff, which is inferred.
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
            map.AreasOfInterest.AddRange(contents.AreasOfInterest);

            var regionIds = ImportRegions(contents, map);

            ImportHexes(contents, map, regionIds);
            MarkImpassableBeaches(contents, map);
            ImportTownSlots(contents, map);

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
        /// Splits the regions into map.hex's land and sea lists, keeping their order, and sets
        /// their colours.
        /// </summary>
        /// <returns>The map's region id for each map_data.esf region.</returns>
        private static int[] ImportRegions(MapDataContents contents, MapHexFile map)
        {
            var isSea           = ClassifySeaRegions(contents, map);
            var regionIds       = new int[contents.Regions.Count];
            var landColours     = new List<uint>();
            var seaColours      = new List<uint>();

            for (int region = 0; region < contents.Regions.Count; ++region)
            {
                if (isSea[region] == false)
                {
                    regionIds[region] = map.LandRegions.Count;
                    map.LandRegions.Add(contents.Regions[region].Key);
                    landColours.Add(contents.Regions[region].Colour);
                }
            }

            for (int region = 0; region < contents.Regions.Count; ++region)
            {
                if (isSea[region])
                {
                    regionIds[region] = map.LandRegions.Count + map.SeaRegions.Count;
                    map.SeaRegions.Add(contents.Regions[region].Key);
                    seaColours.Add(contents.Regions[region].Colour);
                }
            }

            map.ColoursLand.SetColours(MergeColours(landColours, map.ColoursLand.Colours, count => MapHexFile.GenerateLandColours(count)));
            map.ColoursSea.SetColours(MergeColours(seaColours, map.ColoursSea.Colours, count => MapHexFile.GenerateSeaColours(count)));

            return regionIds;
        }

        /// <summary>
        /// The builder only flags sea regions that have passable sea. A region without the flag
        /// whose hexes all have sea ground, such as an impassable lake, is a sea region too.
        /// </summary>
        private static bool[] ClassifySeaRegions(MapDataContents contents, MapHexFile map)
        {
            var hasHexes        = new bool[contents.Regions.Count];
            var hasLandGround   = new bool[contents.Regions.Count];

            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                int region = contents.HexRegions[hexIndex];

                hasHexes[region] = true;
                if (map.IsSeaGroundType(contents.HexGroundTypes[hexIndex]) == false)
                {
                    hasLandGround[region] = true;
                }
            }

            var isSea = new bool[contents.Regions.Count];
            for (int region = 0; region < isSea.Length; ++region)
            {
                isSea[region] = contents.Regions[region].HasPassableSea || (hasHexes[region] && hasLandGround[region] == false);
            }

            return isSea;
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

        private static void ImportHexes(MapDataContents contents, MapHexFile map, int[] regionIds)
        {
            for (int hexIndex = 0; hexIndex < contents.HexCount; ++hexIndex)
            {
                var hex         = map.HexData[hexIndex];
                var areaType    = contents.HexAreaTypes[hexIndex];

                hex.RegionId        = regionIds[contents.HexRegions[hexIndex]];
                hex.GroundTypeIndex = contents.HexGroundTypes[hexIndex];
                hex.ClimateIndex    = contents.HexClimates[hexIndex];
                hex.AttritionIndex  = contents.HexAttritions[hexIndex];
                hex.InterestIndex   = contents.HexAreasOfInterest[hexIndex];

                hex.RoadEdgeMask    = contents.HexRoadMasks[hexIndex];
                hex.IsRoad          = hex.RoadEdgeMask != 0;
                hex.RiverEdgeMask   = contents.HexRiverMasks[hexIndex];
                hex.IsRiver         = hex.RiverEdgeMask != 0;

                hex.IsSea           = map.IsSeaGroundType(hex.GroundTypeIndex);
                hex.IsPassable      = areaType != MapDataAreaType.Impassable;
                hex.IsBridge        = areaType == MapDataAreaType.Bridge;
                hex.IsBeach         = areaType == MapDataAreaType.Beach;
            }
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
        /// The port slot block also lists the main slot's hexes, so the main slot is applied after
        /// it. Every hex in a town slot is town sprawl.
        /// </summary>
        private static void ImportTownSlots(MapDataContents contents, MapHexFile map)
        {
            foreach (var region in contents.Regions)
            {
                SetTownSlot(map, region.PortSlotHexes, Hex.PORT_SLOT_INDEX);
                SetTownSlot(map, region.PrimarySlotHexes, Hex.MAIN_SLOT_INDEX);
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
