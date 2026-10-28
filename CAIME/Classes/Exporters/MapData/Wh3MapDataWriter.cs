using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Classes.Importers;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>The campaign_map_playable_areas values map_data.esf is built from.</summary>
    public sealed class MapDataTheatre
    {
        /// <summary>The playable area's index, which map_data.esf stores as the theatre's name.</summary>
        public string   Index   { get; set; }

        /// <summary>The playable area's maxx: the x of the last column of hexes.</summary>
        public float    MaxX    { get; set; }
    }

    /// <summary>
    /// Builds a Warhammer III map_data.esf (MAP_DATA v2) from a map.hex, as the Assembly Kit's
    /// ToolDataBuilder does. Everything in the file comes from the map and the playable area's
    /// index and maxx; the builder reads other tables but nothing else in them reaches the file.
    ///
    /// <para>
    /// The output matches the builder's byte for byte apart from the timestamp at bytes 8-11 and
    /// the order of the string table (see <see cref="AbcbWriter"/>). How each part is derived was
    /// worked out by comparing against the builder's output for CA's maps and edited ones; see the
    /// classes this one uses.
    /// </para>
    /// </summary>
    public static class Wh3MapDataWriter
    {
        private const byte   MAP_DATA_VERSION         = 2;
        private const byte   CAMPAIGN_THEATRE_VERSION = 3;
        private const byte   HEX_MAP_DATA_VERSION     = 5;
        private const byte   REGION_DATA_VERSION      = 1;
        private const byte   REGION_AREA_DATA_VERSION = 2;
        private const byte   SETTLEMENT_INFO_VERSION  = 1;

        private const int    BYTES_PER_HEX            = 6;
        private const ushort NONE                     = 0xFFFF;
        private const int    MAX_RUN_LENGTH           = ushort.MaxValue;

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            int regionCount = map.LandRegions.Count + map.SeaRegions.Count;
            CheckEveryHexHasARegion(map, regionCount);

            var grid        = new MapDataGrid((int)map.MapWidth, (int)map.MapHeight);
            var geometry    = new MapDataGeometry(theatre.MaxX, map.MapWidth, map.MapHeight);
            var areas       = new MapDataAreas(map, grid, geometry, regionCount);
            var passableSea = FindPassableSea(areas, regionCount);
            var links       = new MapDataRegionLinks(map, grid, areas, passableSea);
            var settlements = MapDataSettlement.FindAll(map, grid, regionCount);
            var names       = map.LandRegions.Concat(map.SeaRegions).ToList();

            var esf = new AbcbWriter();
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            esf.BeginRecord("MAP_FILE_DATA_VERSION", 0);
            esf.WriteUInt16(0);
            esf.EndRecord();

            esf.BeginRecord("MAP_FILE_VALIDATION", 0);
            esf.WriteUInt64(0);
            esf.EndRecord();

            WriteTheatreForUi(esf, theatre, geometry, grid, names, settlements);
            WriteTheatre(esf, map, theatre, geometry);

            esf.BeginRecord("REGIONS_DATA", 0);
            WriteMaskedRegions(esf, map, grid, geometry, areas, links, settlements, names, passableSea);
            esf.WriteAsciiArray(map.Climates);
            esf.WriteAsciiArray(map.LandGroundTypes.Concat(map.SeaGroundTypes).ToList());
            esf.WriteAsciiArray(map.Attritions);
            esf.WriteUInt32Array(RegionColours(map));
            esf.WriteAsciiArray(map.AreasOfInterest);
            esf.EndRecord();

            esf.EndRecord();
            return esf.ToArray(timestamp);
        }

        private static void CheckEveryHexHasARegion(MapHexFile map, int regionCount)
        {
            int unassigned = map.HexData.Count(hex => hex.RegionId < 0 || hex.RegionId >= regionCount);
            if (unassigned > 0)
            {
                throw new InvalidOperationException($"{unassigned} hexes have no region. Paint a region on every hex before processing map_data.esf.");
            }
        }

        /// <summary>A region has passable sea when it has a sea or bridge area.</summary>
        private static bool[] FindPassableSea(MapDataAreas areas, int regionCount)
        {
            var passableSea = new bool[regionCount];
            foreach (var area in areas.Areas)
            {
                if (area.Type == MapDataAreaType.Sea || area.Type == MapDataAreaType.Bridge)
                {
                    passableSea[area.Region] = true;
                }
            }

            return passableSea;
        }

        /// <summary>The settlements' positions, for the campaign UI.</summary>
        private static void WriteTheatreForUi(AbcbWriter esf, MapDataTheatre theatre, MapDataGeometry geometry, MapDataGrid grid, List<string> names, MapDataSettlement[] settlements)
        {
            esf.BeginBlock("THEATRES_AND_REGIONS_FOR_UI", 0);
            esf.BeginBlockEntry();
            esf.BeginRecord("THEATRE", 0);
            esf.WriteAscii(theatre.Index);
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(geometry.MaxX, geometry.MaxY);

            esf.BeginBlock("REGION_KEYS", 0);
            for (int region = 0; region < settlements.Length; ++region)
            {
                var settlement = settlements[region];
                if (settlement == null)
                {
                    continue;
                }

                esf.BeginBlockEntry();
                esf.WriteAscii(names[region]);
                esf.WriteCoordinates(geometry.CentreX(settlement.CentreColumn), geometry.CentreY(settlement.CentreColumn, settlement.CentreRow));
                esf.EndBlockEntry();
            }

            esf.EndBlock();
            esf.EndRecord();
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        private static void WriteTheatre(AbcbWriter esf, MapHexFile map, MapDataTheatre theatre, MapDataGeometry geometry)
        {
            esf.BeginBlock("theatres", 0);
            esf.BeginBlockEntry();
            esf.BeginRecord("CAMPAIGN_THEATRE", CAMPAIGN_THEATRE_VERSION);
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(geometry.MaxX, geometry.MaxY);
            esf.WriteAscii(theatre.Index);

            esf.BeginRecord("HEX_MAP_DATA", HEX_MAP_DATA_VERSION);
            esf.WriteUInt32(map.MapWidth);
            esf.WriteUInt32(map.MapHeight);
            esf.WriteByteArray(HexBytes(map));
            esf.EndRecord();

            esf.EndRecord();
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        /// <summary>
        /// Six bytes a hex: ground type, climate, attrition + 1, the area of interest, road and river
        /// masks. The builder copies the area of interest as bits 3-10 of map.hex's bytes 8-9, so the
        /// byte holds the index + 1 shifted up past the bit below it, which CAIME always writes as 0.
        /// </summary>
        private static byte[] HexBytes(MapHexFile map)
        {
            var bytes = new byte[map.HexData.Length * BYTES_PER_HEX];
            int areaOfInterestShift = map.AreaOfInterestShift;

            for (int hexIndex = 0; hexIndex < map.HexData.Length; ++hexIndex)
            {
                var hex     = map.HexData[hexIndex];
                int offset  = hexIndex * BYTES_PER_HEX;

                bytes[offset + 0] = (byte)hex.GroundTypeIndex;
                bytes[offset + 1] = (byte)hex.ClimateIndex;
                bytes[offset + 2] = (byte)(hex.AttritionIndex + 1);
                bytes[offset + 3] = (byte)(((hex.InterestIndex + 1) << areaOfInterestShift) >> 3);
                bytes[offset + 4] = hex.RoadEdgeMask;
                bytes[offset + 5] = hex.RiverEdgeMask;
            }

            return bytes;
        }

        /// <summary>A region past the end of map.hex's colour list gets <see cref="MapDataContents.UnassignedColour"/>.</summary>
        private static uint[] RegionColours(MapHexFile map)
        {
            return ColoursFor(map.LandRegions.Count, map.ColoursLand.Colours)
                .Concat(ColoursFor(map.SeaRegions.Count, map.ColoursSea.Colours))
                .ToArray();
        }

        private static IEnumerable<uint> ColoursFor(int regionCount, int[] colours)
        {
            for (int region = 0; region < regionCount; ++region)
            {
                yield return region < colours.Length ? unchecked((uint)colours[region]) : MapDataContents.UnassignedColour;
            }
        }

        private static void WriteMaskedRegions(AbcbWriter esf, MapHexFile map, MapDataGrid grid, MapDataGeometry geometry, MapDataAreas areas,
                                               MapDataRegionLinks links, MapDataSettlement[] settlements, List<string> names, bool[] passableSea)
        {
            var regionBounds = RegionHexBounds(map, grid, names.Count);

            esf.BeginBlock("MASKED_REGIONS_DATA", 0);
            esf.BeginBlockEntry();
            esf.WriteAscii(string.Empty);
            esf.WriteUInt16Array(Array.Empty<ushort>());
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(geometry.MaxX, geometry.MaxY);

            esf.BeginBlock("REGIONS_BLOCK", 0);
            for (int region = 0; region < names.Count; ++region)
            {
                esf.BeginBlockEntry();
                WriteRegion(esf, grid, geometry, areas, links, settlements[region], region, names[region], passableSea[region], regionBounds[region]);
                esf.EndBlockEntry();
            }

            esf.EndBlock();

            WriteHexAreaRuns(esf, areas);
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        private sealed class HexBounds
        {
            public int MinColumn = int.MaxValue, MinRow = int.MaxValue, MaxColumn = int.MinValue, MaxRow = int.MinValue, HexCount;
        }

        private static HexBounds[] RegionHexBounds(MapHexFile map, MapDataGrid grid, int regionCount)
        {
            var bounds = new HexBounds[regionCount];
            for (int region = 0; region < regionCount; ++region)
            {
                bounds[region] = new HexBounds();
            }

            for (int hexIndex = 0; hexIndex < grid.HexCount; ++hexIndex)
            {
                var b       = bounds[map.HexData[hexIndex].RegionId];
                int column  = grid.Column(hexIndex);
                int row     = grid.Row(hexIndex);

                b.MinColumn = Math.Min(b.MinColumn, column);
                b.MinRow    = Math.Min(b.MinRow, row);
                b.MaxColumn = Math.Max(b.MaxColumn, column);
                b.MaxRow    = Math.Max(b.MaxRow, row);
                ++b.HexCount;
            }

            return bounds;
        }

        private static void WriteRegion(AbcbWriter esf, MapDataGrid grid, MapDataGeometry geometry, MapDataAreas areas, MapDataRegionLinks links,
                                        MapDataSettlement settlement, int region, string name, bool passableSea, HexBounds bounds)
        {
            esf.BeginRecord("REGION_DATA", REGION_DATA_VERSION);

            esf.BeginRecord("REGION_INDEX", 0);
            esf.WriteUInt16((ushort)region);
            esf.EndRecord();

            esf.WriteAscii(name);
            esf.WriteByte(passableSea ? (byte)1 : (byte)0);

            // A region without hexes keeps zeroed bounds.
            if (bounds.HexCount > 0)
            {
                RegionWorldBounds(geometry, areas, region, out float minX, out float minY, out float maxX, out float maxY);
                WriteBounds(esf, minX, minY, maxX, maxY, bounds.MinColumn, bounds.MinRow, bounds.MaxColumn, bounds.MaxRow);
            }
            else
            {
                WriteBounds(esf, 0f, 0f, 0f, 0f, 0, 0, 0, 0);
            }

            esf.WriteUInt32((uint)bounds.HexCount);

            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_TRAVERSABLE_SAME", links.TraversableSame[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_NONTRAVERSABLE_SAME", links.NonTraversableSame[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_TRAVERSABLE_OTHER", links.TraversableOther[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_NONTRAVERSABLE_OTHER", links.NonTraversableOther[region]);

            // No Warhammer III map the Assembly Kit builds has an entry here.
            WriteRegionLinks(esf, "CONNECTIVITY_LAND_INDIRECT_VIA_FORD", new List<MapDataRegionLink>());

            esf.BeginBlock("REGION_AREAS", 0);
            foreach (int areaId in areas.RegionAreas[region])
            {
                esf.BeginBlockEntry();
                WriteArea(esf, grid, geometry, areas, areas.Areas[areaId]);
                esf.EndBlockEntry();
            }

            esf.EndBlock();

            esf.WriteBool(settlement != null);
            if (settlement != null)
            {
                WriteSettlement(esf, grid, settlement);
            }

            // What the int means is unknown; the builder always writes 0. Then the sea region and hex
            // the port docks at.
            esf.WriteInt32(0);
            int dockHex = settlement != null ? settlement.DockHex : -1;
            if (dockHex >= 0)
            {
                esf.WriteUInt16((ushort)areas.Areas[areas.HexAreas[dockHex]].Region);
                esf.WriteUInt16((ushort)grid.Column(dockHex));
                esf.WriteUInt16((ushort)grid.Row(dockHex));
            }
            else
            {
                esf.WriteUInt16(NONE);
                esf.WriteUInt16(0);
                esf.WriteUInt16(0);
            }

            esf.EndRecord();
        }

        /// <summary>
        /// A region's world rectangle is the union of its areas' rectangles, not the rectangle of
        /// its own hex bounds: they differ when every area of a region spans a single column.
        /// </summary>
        private static void RegionWorldBounds(MapDataGeometry geometry, MapDataAreas areas, int region, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;

            foreach (int areaId in areas.RegionAreas[region])
            {
                var area = areas.Areas[areaId];
                geometry.Bounds(area.MinColumn, area.MinRow, area.MaxColumn, area.MaxRow, out float areaMinX, out float areaMinY, out float areaMaxX, out float areaMaxY);

                minX = Math.Min(minX, areaMinX);
                minY = Math.Min(minY, areaMinY);
                maxX = Math.Max(maxX, areaMaxX);
                maxY = Math.Max(maxY, areaMaxY);
            }
        }

        private static void WriteBounds(AbcbWriter esf, float minX, float minY, float maxX, float maxY, int minColumn, int minRow, int maxColumn, int maxRow)
        {
            esf.WriteCoordinates(minX, minY);
            esf.WriteCoordinates(maxX, maxY);
            esf.WriteUInt16((ushort)minColumn);
            esf.WriteUInt16((ushort)minRow);
            esf.WriteUInt16((ushort)maxColumn);
            esf.WriteUInt16((ushort)maxRow);
        }

        private static void WriteRegionLinks(AbcbWriter esf, string blockName, List<MapDataRegionLink> links)
        {
            esf.BeginBlock(blockName, 0);
            foreach (var link in links)
            {
                esf.BeginBlockEntry();
                esf.WriteUInt16((ushort)link.Region);
                esf.WriteUInt16((ushort)link.HexCount);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        private static void WriteArea(AbcbWriter esf, MapDataGrid grid, MapDataGeometry geometry, MapDataAreas areas, MapDataArea area)
        {
            esf.BeginRecord("REGION_AREA_DATA", REGION_AREA_DATA_VERSION);
            esf.WriteByte((byte)area.Type);

            geometry.Bounds(area.MinColumn, area.MinRow, area.MaxColumn, area.MaxRow, out float minX, out float minY, out float maxX, out float maxY);
            esf.WriteCoordinates(minX, minY);
            esf.WriteCoordinates(maxX, maxY);
            esf.WriteUInt16((ushort)area.Group);
            esf.WriteUInt16((ushort)area.MinColumn);
            esf.WriteUInt16((ushort)area.MinRow);
            esf.WriteUInt16((ushort)area.MaxColumn);
            esf.WriteUInt16((ushort)area.MaxRow);

            WriteAreaReferences(esf, areas, area.Traversable);
            WriteAreaReferences(esf, areas, area.NonTraversable);

            esf.WriteUInt16((ushort)grid.Column(area.CentreHex));
            esf.WriteUInt16((ushort)grid.Row(area.CentreHex));
            esf.WriteUInt32((uint)area.HexCount);
            esf.EndRecord();
        }

        private static void WriteAreaReferences(AbcbWriter esf, MapDataAreas areas, List<int> areaIds)
        {
            esf.BeginBlock("REGION_AREA_INDEX", 0);
            foreach (int areaId in areaIds)
            {
                esf.BeginBlockEntry();
                esf.WriteUInt16((ushort)areas.Areas[areaId].Region);
                esf.WriteUInt16((ushort)areas.Areas[areaId].IndexInRegion);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        private static void WriteSettlement(AbcbWriter esf, MapDataGrid grid, MapDataSettlement settlement)
        {
            int column  = settlement.CentreColumn;
            int row     = settlement.CentreRow;

            esf.BeginRecord("SETTLEMENT_INFO", SETTLEMENT_INFO_VERSION);

            esf.BeginRecord("LOGICAL_POSITION_BIT_ARRAY", 0);
            esf.WriteUInt16((ushort)(column - MapDataSettlement.RADIUS));
            esf.WriteUInt16((ushort)(row - MapDataSettlement.RADIUS));
            esf.WriteUInt16((ushort)(column + MapDataSettlement.RADIUS));
            esf.WriteUInt16((ushort)(row + MapDataSettlement.RADIUS));
            esf.WriteUInt32(MapDataSettlement.FOOTPRINT_SIZE);
            esf.WriteByteArray(settlement.FootprintBits);
            esf.EndRecord();

            // The logical and the visual position: always the same hex.
            esf.WriteUInt16((ushort)column);
            esf.WriteUInt16((ushort)row);
            esf.WriteUInt16((ushort)column);
            esf.WriteUInt16((ushort)row);

            WriteHexList(esf, grid, "PRIMARY_SLOT_AREA_BLOCK", settlement.MainSlotHexes);

            // The port sits at the settlement's centre, and its slot block lists the port hexes
            // and then the main slot's.
            esf.WriteUInt16(settlement.HasPort ? (ushort)column : NONE);
            esf.WriteUInt16(settlement.HasPort ? (ushort)row : NONE);
            WriteHexList(esf, grid, "PORT_SLOT_AREA_BLOCK", settlement.HasPort ? settlement.PortSlotHexes.Concat(settlement.MainSlotHexes).ToList() : new List<int>());

            esf.EndRecord();
        }

        private static void WriteHexList(AbcbWriter esf, MapDataGrid grid, string blockName, List<int> hexes)
        {
            esf.BeginBlock(blockName, 0);
            foreach (int hexIndex in hexes)
            {
                esf.BeginBlockEntry();
                esf.WriteUInt16((ushort)grid.Column(hexIndex));
                esf.WriteUInt16((ushort)grid.Row(hexIndex));
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        /// <summary>
        /// The region and area of every hex, as runs over the grid in row-major order. A run's
        /// length is a ushort, so longer runs are split.
        /// </summary>
        private static void WriteHexAreaRuns(AbcbWriter esf, MapDataAreas areas)
        {
            esf.BeginBlock("REGION_AREA_INDEX_OVERRIDE", 0);

            int hexCount = areas.HexAreas.Length;
            for (int start = 0; start < hexCount;)
            {
                int areaId  = areas.HexAreas[start];
                int end     = start + 1;
                while (end < hexCount && areas.HexAreas[end] == areaId && end - start < MAX_RUN_LENGTH)
                {
                    ++end;
                }

                esf.BeginBlockEntry();
                esf.BeginRecord("REGION_AREA_INDEX", 0);
                esf.WriteUInt16((ushort)areas.Areas[areaId].Region);
                esf.WriteUInt16((ushort)areas.Areas[areaId].IndexInRegion);
                esf.EndRecord();
                esf.WriteUInt16((ushort)(end - start));
                esf.EndBlockEntry();

                start = end;
            }

            esf.EndBlock();
        }
    }
}
