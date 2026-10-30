using System;
using System.Collections.Generic;
using System.Linq;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Pharaoh map_data.esf (MAP_DATA v1) from a map.hex, and Warhammer's and Warhammer
    /// II's through them. See FileFormatDefinitions/map_data_esf.md.
    /// </summary>
    public static class PharaohMapDataWriter
    {
        private const byte MAP_DATA_VERSION     = 1;
        private const byte HEX_MAP_DATA_VERSION = 4;
        private const int  BYTES_PER_HEX        = 5;
        private const int  MAX_RUN_LENGTH       = ushort.MaxValue;

        private static readonly string[] NO_MASKS = { string.Empty };

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Write(map, theatre, timestamp, NO_MASKS, MapDataRadiusRounding.TwoThirdsPerColumn);
        }

        /// <summary>
        /// Writes the regions once for each of <paramref name="maskNames"/>: the UI's region list
        /// repeats, and each mask gets its own MASKED_REGIONS_DATA entry (Warhammer).
        /// </summary>
        internal static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp, IReadOnlyList<string> maskNames, MapDataRadiusRounding rounding)
        {
            var model       = new MapDataModel(map, theatre, rounding);
            var settlements = MapDataSlotSettlement.FindAll(model);
            MapDataSlotPositions.PlacePharaoh(model, settlements);

            var esf = new EsfWriter(EsfFormat.Abca);
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            esf.BeginRecord("MAP_FILE_DATA_VERSION", 0);
            esf.WriteUInt16(0);
            esf.EndRecord();

            var positions = SettlementPositions(model, settlements).ToList();
            MapDataEsfParts.WriteTheatreForUi(esf, model, maskNames.SelectMany(_ => positions));
            MapDataEsfParts.WriteTheatres(esf, model, HEX_MAP_DATA_VERSION, HexBytes(map));

            esf.BeginRecord("REGIONS_DATA", 0);
            WriteMaskedRegions(esf, model, settlements, maskNames);
            MapDataEsfParts.WriteNameLists(esf, map);
            esf.WriteUInt32Array(model.RegionColours());
            esf.EndRecord();

            esf.EndRecord();
            return esf.ToArray(timestamp);
        }

        private static IEnumerable<(int, float, float)> SettlementPositions(MapDataModel model, MapDataSlotSettlement[] settlements)
        {
            for (int region = 0; region < settlements.Length; ++region)
            {
                if (settlements[region] != null)
                {
                    int hexIndex    = settlements[region].UiHex;
                    int column      = model.Grid.Column(hexIndex);
                    yield return (region, model.Geometry.CentreX(column), model.Geometry.CentreY(column, model.Grid.Row(hexIndex)));
                }
            }
        }

        /// <summary>Five bytes a hex: ground type, climate, attrition + 1, road and river masks.</summary>
        private static byte[] HexBytes(MapHexFile map)
        {
            var bytes = new byte[map.HexData.Length * BYTES_PER_HEX];

            for (int hexIndex = 0; hexIndex < map.HexData.Length; ++hexIndex)
            {
                var hex     = map.HexData[hexIndex];
                int offset  = hexIndex * BYTES_PER_HEX;

                bytes[offset + 0] = (byte)hex.GroundTypeIndex;
                bytes[offset + 1] = (byte)hex.ClimateIndex;
                bytes[offset + 2] = (byte)(hex.AttritionIndex + 1);
                bytes[offset + 3] = hex.RoadEdgeMask;
                bytes[offset + 4] = hex.RiverEdgeMask;
            }

            return bytes;
        }

        private static void WriteMaskedRegions(EsfWriter esf, MapDataModel model, MapDataSlotSettlement[] settlements, IReadOnlyList<string> maskNames)
        {
            var runs = HexAreaRuns(model.Areas);

            esf.BeginBlock("MASKED_REGIONS_DATA", 0);
            foreach (var maskName in maskNames)
            {
                esf.BeginBlockEntry();
                esf.WriteAscii(maskName);
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

                esf.WriteUInt16Array(runs);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        private static void WriteRegion(EsfWriter esf, MapDataModel model, MapDataSlotSettlement settlement, int region)
        {
            esf.BeginRecord("REGION_DATA", 0);

            esf.BeginRecord("REGION_INDEX", 0);
            esf.WriteUInt16((ushort)region);
            esf.EndRecord();

            esf.WriteAscii(model.RegionNames[region]);
            esf.WriteByte(model.PassableSea[region] ? (byte)1 : (byte)0);
            MapDataEsfParts.WriteRegionBounds(esf, model, region);
            MapDataEsfParts.WriteRegionLinks(esf, model, region);
            MapDataEsfParts.WriteRegionAreas(esf, model, region, MapDataAreaReferences.PackedArray);

            esf.WriteBool(settlement != null);
            if (settlement != null)
            {
                MapDataEsfParts.WriteSlotSettlement(esf, model.Grid, settlement, portIncludesMainSlot: true);
            }

            MapDataEsfParts.WriteDock(esf, model, settlement != null ? settlement.DockHex : -1);
            esf.EndRecord();
        }

        /// <summary>
        /// The area of every hex, as runs over the grid in row-major order: pairs of a packed area
        /// reference and a length. A run's length is a ushort, so longer runs are split.
        /// </summary>
        private static List<ushort> HexAreaRuns(MapDataAreas areas)
        {
            var runs        = new List<ushort>();
            int hexCount    = areas.HexAreas.Length;

            for (int start = 0; start < hexCount;)
            {
                int areaId  = areas.HexAreas[start];
                int end     = start + 1;
                while (end < hexCount && areas.HexAreas[end] == areaId && end - start < MAX_RUN_LENGTH)
                {
                    ++end;
                }

                runs.Add(MapDataEsfParts.PackedAreaReference(areas.Areas[areaId]));
                runs.Add((ushort)(end - start));
                start = end;
            }

            return runs;
        }
    }
}
