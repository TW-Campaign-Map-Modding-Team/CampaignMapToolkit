using System;
using System.Collections.Generic;
using System.Linq;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Pharaoh map_data.esf (MAP_DATA v1, ABCA) from a map.hex. Worked out from the game's
    /// phar_main file and Templates/phar_main/map.hex; the regions, areas, links and geometry follow
    /// the same rules as Warhammer III (<see cref="MapDataModel"/>).
    ///
    /// <para>
    /// The layout sits between Attila's and Warhammer III's: a MAP_FILE_DATA_VERSION record but no
    /// validation record; MASKED_REGIONS_DATA and REGION_INDEX as in Warhammer III; five bytes per
    /// hex, no areas of interest; packed area references and settlements with building slots as in
    /// Attila (<see cref="MapDataSlotSettlement"/>); and the per-hex runs as a u16 array. A port
    /// lists the main slot's hexes after its own, as in Warhammer III.
    ///
    /// The hex radius is rounded as Warhammer III rounds it, not as Attila does.
    /// </para>
    ///
    /// <para>
    /// Given the game's attrition (painted after the template was made), the output is byte for
    /// byte the game's apart from the timestamp and the order of the string table.
    /// </para>
    /// </summary>
    public static class PharaohMapDataWriter
    {
        private const byte MAP_DATA_VERSION     = 1;
        private const byte HEX_MAP_DATA_VERSION = 4;
        private const int  BYTES_PER_HEX        = 5;
        private const int  MAX_RUN_LENGTH       = ushort.MaxValue;

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Write(map, theatre, timestamp, MapDataRadiusRounding.TwoThirdsPerColumn);
        }

        /// <summary>
        /// Writes the regions once for each of the map's campaign masks, with that mask's hexes
        /// impassable (<see cref="MapDataMask"/>): the UI's region list repeats, and each
        /// mask gets its own MASKED_REGIONS_DATA entry.
        /// </summary>
        internal static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp, MapDataRadiusRounding rounding)
        {
            var maskedModels = MapDataMask.ForEachMask(map)
                .Select(mask => new MapDataModel(map, theatre, rounding, mask))
                .Select(maskedModel => (Model: maskedModel, Settlements: PlacedSettlements(maskedModel)))
                .ToList();
            var model = maskedModels[0].Model;

            var esf = new EsfWriter(EsfFormat.Abca);
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            esf.BeginRecord("MAP_FILE_DATA_VERSION", 0);
            esf.WriteUInt16(0);
            esf.EndRecord();

            MapDataEsfParts.WriteTheatreForUi(esf, model, maskedModels.SelectMany(masked => SettlementPositions(masked.Model, masked.Settlements)));
            MapDataEsfParts.WriteTheatres(esf, model, HEX_MAP_DATA_VERSION, HexBytes(map));

            esf.BeginRecord("REGIONS_DATA", 0);
            WriteMaskedRegions(esf, maskedModels);
            MapDataEsfParts.WriteNameLists(esf, map);
            esf.WriteUInt32Array(model.RegionColours());
            esf.EndRecord();

            esf.EndRecord();
            return esf.ToArray(timestamp);
        }

        private static MapDataSlotSettlement[] PlacedSettlements(MapDataModel model)
        {
            var settlements = MapDataSlotSettlement.FindAll(model);
            MapDataSlotPositions.PlacePharaoh(model, settlements);
            return settlements;
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

        private static void WriteMaskedRegions(EsfWriter esf, IEnumerable<(MapDataModel Model, MapDataSlotSettlement[] Settlements)> maskedModels)
        {
            esf.BeginBlock("MASKED_REGIONS_DATA", 0);
            foreach (var (model, settlements) in maskedModels)
            {
                esf.BeginBlockEntry();
                esf.WriteAscii(model.Mask.Name);
                esf.WriteUInt16Array(model.Mask.Runs);
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

                esf.WriteUInt16Array(HexAreaRuns(model.Areas));
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
