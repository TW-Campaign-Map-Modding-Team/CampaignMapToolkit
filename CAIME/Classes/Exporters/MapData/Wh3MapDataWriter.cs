using System;
using System.Collections.Generic;
using System.Linq;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer III map_data.esf (MAP_DATA v2) from a map.hex, as the Assembly Kit's
    /// ToolDataBuilder does. Everything in the file comes from the map and the playable area's
    /// index and maxx; the builder reads other tables but nothing else in them reaches the file.
    ///
    /// <para>
    /// The output matches the builder's byte for byte apart from the timestamp at bytes 8-11 and
    /// the order of the string table (see <see cref="EsfWriter"/>). How each part is derived was
    /// worked out by comparing against the builder's output for CA's maps and edited ones; see the
    /// classes this one uses.
    /// </para>
    /// </summary>
    public static class Wh3MapDataWriter
    {
        private const byte   MAP_DATA_VERSION         = 2;
        private const byte   HEX_MAP_DATA_VERSION     = 5;
        private const byte   REGION_DATA_VERSION      = 1;
        private const byte   SETTLEMENT_INFO_VERSION  = 1;

        private const int    BYTES_PER_HEX            = 6;
        private const ushort NONE                     = 0xFFFF;
        private const int    MAX_RUN_LENGTH           = ushort.MaxValue;

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            var model       = new MapDataModel(map, theatre);
            var settlements = MapDataSettlement.FindAll(map, model.Grid, model.RegionCount);

            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            esf.BeginRecord("MAP_FILE_DATA_VERSION", 0);
            esf.WriteUInt16(0);
            esf.EndRecord();

            esf.BeginRecord("MAP_FILE_VALIDATION", 0);
            esf.WriteUInt64(0);
            esf.EndRecord();

            MapDataEsfParts.WriteTheatreForUi(esf, model, SettlementPositions(model, settlements));
            MapDataEsfParts.WriteTheatres(esf, model, HEX_MAP_DATA_VERSION, HexBytes(map));

            esf.BeginRecord("REGIONS_DATA", 0);
            WriteMaskedRegions(esf, model, settlements);
            MapDataEsfParts.WriteNameLists(esf, map);
            esf.WriteUInt32Array(model.RegionColours());
            esf.WriteAsciiArray(map.AreasOfInterest);
            esf.EndRecord();

            esf.EndRecord();
            return esf.ToArray(timestamp);
        }

        private static IEnumerable<(int, float, float)> SettlementPositions(MapDataModel model, MapDataSettlement[] settlements)
        {
            for (int region = 0; region < settlements.Length; ++region)
            {
                var settlement = settlements[region];
                if (settlement != null)
                {
                    yield return (region, model.Geometry.CentreX(settlement.CentreColumn), model.Geometry.CentreY(settlement.CentreColumn, settlement.CentreRow));
                }
            }
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

        private static void WriteMaskedRegions(EsfWriter esf, MapDataModel model, MapDataSettlement[] settlements)
        {
            esf.BeginBlock("MASKED_REGIONS_DATA", 0);
            esf.BeginBlockEntry();
            esf.WriteAscii(string.Empty);
            esf.WriteUInt16Array(Array.Empty<ushort>());
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(model.Geometry.MaxX, model.Geometry.MaxY);

            esf.BeginBlock("REGIONS_BLOCK", 0);
            for (int region = 0; region < model.RegionCount; ++region)
            {
                esf.BeginBlockEntry();
                WriteRegion(esf, model, settlements[region], region);
                esf.EndBlockEntry();
            }

            esf.EndBlock();

            WriteHexAreaRuns(esf, model.Areas);
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        private static void WriteRegion(EsfWriter esf, MapDataModel model, MapDataSettlement settlement, int region)
        {
            esf.BeginRecord("REGION_DATA", REGION_DATA_VERSION);

            esf.BeginRecord("REGION_INDEX", 0);
            esf.WriteUInt16((ushort)region);
            esf.EndRecord();

            esf.WriteAscii(model.RegionNames[region]);
            esf.WriteByte(model.PassableSea[region] ? (byte)1 : (byte)0);
            MapDataEsfParts.WriteRegionBounds(esf, model, region);
            MapDataEsfParts.WriteRegionLinks(esf, model, region);
            MapDataEsfParts.WriteRegionAreas(esf, model, region, MapDataAreaReferences.Blocks);

            esf.WriteBool(settlement != null);
            if (settlement != null)
            {
                WriteSettlement(esf, model.Grid, settlement);
            }

            MapDataEsfParts.WriteDock(esf, model, settlement != null ? settlement.DockHex : -1);
            esf.EndRecord();
        }

        private static void WriteSettlement(EsfWriter esf, MapDataGrid grid, MapDataSettlement settlement)
        {
            int column  = settlement.CentreColumn;
            int row     = settlement.CentreRow;

            esf.BeginRecord("SETTLEMENT_INFO", SETTLEMENT_INFO_VERSION);

            MapDataEsfParts.WriteFootprint(esf, column - MapDataSettlement.RADIUS, row - MapDataSettlement.RADIUS,
                                           column + MapDataSettlement.RADIUS, row + MapDataSettlement.RADIUS, settlement.FootprintBits);

            // The logical and the visual position: always the same hex.
            esf.WriteUInt16((ushort)column);
            esf.WriteUInt16((ushort)row);
            esf.WriteUInt16((ushort)column);
            esf.WriteUInt16((ushort)row);

            MapDataEsfParts.WriteHexList(esf, grid, "PRIMARY_SLOT_AREA_BLOCK", settlement.MainSlotHexes);

            // The port sits at the settlement's centre, and its slot block lists the port hexes
            // and then the main slot's.
            esf.WriteUInt16(settlement.HasPort ? (ushort)column : NONE);
            esf.WriteUInt16(settlement.HasPort ? (ushort)row : NONE);
            MapDataEsfParts.WriteHexList(esf, grid, "PORT_SLOT_AREA_BLOCK", settlement.HasPort ? settlement.PortSlotHexes.Concat(settlement.MainSlotHexes) : Enumerable.Empty<int>());

            esf.EndRecord();
        }

        /// <summary>
        /// The region and area of every hex, as runs over the grid in row-major order. A run's
        /// length is a ushort, so longer runs are split.
        /// </summary>
        private static void WriteHexAreaRuns(EsfWriter esf, MapDataAreas areas)
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
