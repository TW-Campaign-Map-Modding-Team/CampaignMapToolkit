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
        /// <summary>What Warhammer III and Three Kingdoms lay out differently.</summary>
        internal sealed class Layout
        {
            public EsfFormat                Format              { get; set; } = EsfFormat.Abcb;
            public ushort                   DataVersion         { get; set; }

            /// <summary>Whether MAP_FILE_VALIDATION holds the map.hex's save time, or 0.</summary>
            public bool                     ValidatesMapHex     { get; set; }

            public MapDataAreaReferences    AreaReferences      { get; set; } = MapDataAreaReferences.Blocks;

            /// <summary>Whether the per-hex area runs are a u16 array rather than a block.</summary>
            public bool                     RunsAsArray         { get; set; }

            public MapDataSeaFlag           SeaFlag             { get; set; } = MapDataSeaFlag.FirstArea;

            public Func<MapDataModel, MapDataSettlement[]> FindSettlements { get; set; } = MapDataSettlement.FindAll;
        }

        private static readonly Layout WARHAMMER_3 = new Layout();

        private const byte   MAP_DATA_VERSION         = 2;
        private const byte   HEX_MAP_DATA_VERSION     = 5;
        private const byte   REGION_DATA_VERSION      = 1;
        private const byte   SETTLEMENT_INFO_VERSION  = 1;

        private const int    BYTES_PER_HEX            = 6;
        private const ushort NONE                     = 0xFFFF;
        private const int    MAX_RUN_LENGTH           = ushort.MaxValue;

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Write(map, theatre, timestamp, WARHAMMER_3);
        }

        internal static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp, Layout layout)
        {
            var model       = new MapDataModel(map, theatre, seaFlag: layout.SeaFlag);
            var settlements = layout.FindSettlements(model);

            var esf = new EsfWriter(layout.Format);
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            esf.BeginRecord("MAP_FILE_DATA_VERSION", 0);
            esf.WriteUInt16(layout.DataVersion);
            esf.EndRecord();

            esf.BeginRecord("MAP_FILE_VALIDATION", 0);
            esf.WriteUInt64(layout.ValidatesMapHex ? map.Timestamp : 0);
            esf.EndRecord();

            MapDataEsfParts.WriteTheatreForUi(esf, model, SettlementPositions(model, settlements));
            MapDataEsfParts.WriteTheatres(esf, model, HEX_MAP_DATA_VERSION, HexBytes(map));

            esf.BeginRecord("REGIONS_DATA", 0);
            WriteMaskedRegions(esf, model, settlements, layout);
            MapDataEsfParts.WriteNameLists(esf, map);
            esf.WriteUInt32Array(model.RegionColours());
            esf.WriteAsciiArray(map.AreasOfInterest);
            esf.EndRecord();

            esf.EndRecord();
            return esf.ToArray(timestamp);
        }

        /// <summary>
        /// The campaign UI puts a settlement at its logical position, which the builder clamps to
        /// the grid: an invalid settlement goes to the last column and row.
        /// </summary>
        private static IEnumerable<(int, float, float)> SettlementPositions(MapDataModel model, MapDataSettlement[] settlements)
        {
            for (int region = 0; region < settlements.Length; ++region)
            {
                var settlement = settlements[region];
                if (settlement != null)
                {
                    int column  = Math.Min(settlement.LogicalColumn, model.Grid.Width - 1);
                    int row     = Math.Min(settlement.LogicalRow, model.Grid.Height - 1);
                    yield return (region, model.Geometry.CentreX(column), model.Geometry.CentreY(column, row));
                }
            }
        }

        /// <summary>
        /// Six bytes a hex: ground type, climate, attrition + 1, the area of interest, road and river
        /// masks. The builder copies the area of interest as bits 3-8 of map.hex's bytes 8-9, so the
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

        private static void WriteMaskedRegions(EsfWriter esf, MapDataModel model, MapDataSettlement[] settlements, Layout layout)
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
                WriteRegion(esf, model, settlements[region], region, layout);
                esf.EndBlockEntry();
            }

            esf.EndBlock();

            if (layout.RunsAsArray)
            {
                esf.WriteUInt16Array(HexAreaRunsArray(model.Areas));
            }
            else
            {
                WriteHexAreaRuns(esf, model.Areas);
            }

            esf.EndBlockEntry();
            esf.EndBlock();
        }

        private static void WriteRegion(EsfWriter esf, MapDataModel model, MapDataSettlement settlement, int region, Layout layout)
        {
            esf.BeginRecord("REGION_DATA", REGION_DATA_VERSION);

            esf.BeginRecord("REGION_INDEX", 0);
            esf.WriteUInt16((ushort)region);
            esf.EndRecord();

            esf.WriteAscii(model.RegionNames[region]);
            esf.WriteByte(model.PassableSea[region] ? (byte)1 : (byte)0);
            MapDataEsfParts.WriteRegionBounds(esf, model, region);
            MapDataEsfParts.WriteRegionLinks(esf, model, region);
            MapDataEsfParts.WriteRegionAreas(esf, model, region, layout.AreaReferences);

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
            esf.BeginRecord("SETTLEMENT_INFO", SETTLEMENT_INFO_VERSION);

            MapDataEsfParts.WriteFootprint(esf, settlement.FootprintMinColumn, settlement.FootprintMinRow,
                                           settlement.FootprintMaxColumn, settlement.FootprintMaxRow, settlement.FootprintBits);

            esf.WriteUInt16((ushort)settlement.LogicalColumn);
            esf.WriteUInt16((ushort)settlement.LogicalRow);
            esf.WriteUInt16((ushort)settlement.CentreColumn);
            esf.WriteUInt16((ushort)settlement.CentreRow);

            MapDataEsfParts.WriteHexList(esf, grid, "PRIMARY_SLOT_AREA_BLOCK", settlement.MainSlotHexes);

            // A valid settlement's port stands at its logical position, and its slot block lists
            // the port hexes and then the main slot's; an invalid one's lists the port hexes alone.
            bool placedPort = settlement.IsValid && settlement.HasPort;
            esf.WriteUInt16(placedPort ? (ushort)settlement.LogicalColumn : NONE);
            esf.WriteUInt16(placedPort ? (ushort)settlement.LogicalRow : NONE);
            MapDataEsfParts.WriteHexList(esf, grid, "PORT_SLOT_AREA_BLOCK", placedPort ? settlement.PortSlotHexes.Concat(settlement.MainSlotHexes) : settlement.PortSlotHexes);

            esf.EndRecord();
        }

        /// <summary>
        /// The area of every hex, as runs over the grid in row-major order. A run's length is a
        /// ushort, so longer runs are split.
        /// </summary>
        private static IEnumerable<(MapDataArea Area, int Length)> HexAreaRuns(MapDataAreas areas)
        {
            int hexCount = areas.HexAreas.Length;
            for (int start = 0; start < hexCount;)
            {
                int areaId  = areas.HexAreas[start];
                int end     = start + 1;
                while (end < hexCount && areas.HexAreas[end] == areaId && end - start < MAX_RUN_LENGTH)
                {
                    ++end;
                }

                yield return (areas.Areas[areaId], end - start);
                start = end;
            }
        }

        /// <summary>Warhammer III: a block of runs, each a REGION_AREA_INDEX record and a length.</summary>
        private static void WriteHexAreaRuns(EsfWriter esf, MapDataAreas areas)
        {
            esf.BeginBlock("REGION_AREA_INDEX_OVERRIDE", 0);

            foreach (var run in HexAreaRuns(areas))
            {
                esf.BeginBlockEntry();
                esf.BeginRecord("REGION_AREA_INDEX", 0);
                esf.WriteUInt16((ushort)run.Area.Region);
                esf.WriteUInt16((ushort)run.Area.IndexInRegion);
                esf.EndRecord();
                esf.WriteUInt16((ushort)run.Length);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        /// <summary>Three Kingdoms: pairs of a packed area reference and a length.</summary>
        private static List<ushort> HexAreaRunsArray(MapDataAreas areas)
        {
            var runs = new List<ushort>();
            foreach (var run in HexAreaRuns(areas))
            {
                runs.Add(MapDataEsfParts.PackedAreaReference(run.Area));
                runs.Add((ushort)run.Length);
            }

            return runs;
        }
    }
}
