using System;
using System.Collections.Generic;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds an Attila map_data.esf (MAP_DATA v0, ABCA) from a map.hex. Worked out from the game's
    /// main_attila_map file and Templates/main_attila_map/map.hex; the regions, areas, links and
    /// geometry follow the same rules as Warhammer III (<see cref="MapDataModel"/>).
    ///
    /// <para>
    /// Differences from Warhammer III: no version records; REGIONS_DATA holds empty trade_nodes and
    /// bridges records, the theatre, the bounds and the regions directly, with no
    /// MASKED_REGIONS_DATA, no per-hex region runs and no areas of interest; seven bytes per hex,
    /// the first two the region and area; REGION_DATA has no REGION_INDEX; an area's touching
    /// areas are packed u16s; and settlements have several building slots
    /// (<see cref="MapDataSlotSettlement"/>).
    /// </para>
    ///
    /// <para>
    /// Slot positions follow the Assembly Kit's builder (<see cref="MapDataSlotPositions.PlaceAttila"/>).
    /// </para>
    /// </summary>
    public static class AttilaMapDataWriter
    {
        private const byte MAP_DATA_VERSION     = 0;
        private const byte HEX_MAP_DATA_VERSION = 3;
        private const int  BYTES_PER_HEX        = 7;

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Write(map, theatre, timestamp, CaStringHash.Attila);
        }

        /// <param name="stringTableHash">The game's CALibs string hash, which orders the string table.</param>
        internal static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp, Func<string, uint> stringTableHash)
        {
            var model       = new MapDataModel(map, theatre, MapDataRadiusRounding.PerColumnAndAHalf);
            var settlements = MapDataSlotSettlement.FindAll(model);
            MapDataSlotPositions.PlaceAttila(model, settlements);

            var esf = new EsfWriter(EsfFormat.Abca, stringTableHash);
            esf.BeginRecord("MAP_DATA", MAP_DATA_VERSION);

            MapDataEsfParts.WriteTheatreForUi(esf, model, SettlementPositions(model, settlements));

            esf.BeginRecord("REGIONS_DATA", 0);

            esf.BeginRecord("trade_nodes", 0);
            esf.BeginBlock("trade_nodes", 0);
            esf.EndBlock();
            esf.EndRecord();

            esf.BeginRecord("bridges", 0);
            esf.WriteCoordinates3DArray(Array.Empty<(float, float, float)>());
            esf.EndRecord();

            MapDataEsfParts.WriteTheatres(esf, model, HEX_MAP_DATA_VERSION, HexBytes(model));
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

        /// <summary>
        /// Seven bytes a hex: the region's low byte; the area's index within the region shifted up
        /// one, under which sits, presumably, the region's ninth bit (main_attila_map has fewer than
        /// 256 regions); ground type, climate, attrition + 1, road and river masks.
        /// </summary>
        private static byte[] HexBytes(MapDataModel model)
        {
            var hexes   = model.Map.HexData;
            var bytes   = new byte[hexes.Length * BYTES_PER_HEX];

            for (int hexIndex = 0; hexIndex < hexes.Length; ++hexIndex)
            {
                var hex     = hexes[hexIndex];
                var area    = model.Areas.Areas[model.Areas.HexAreas[hexIndex]];
                int offset  = hexIndex * BYTES_PER_HEX;

                bytes[offset + 0] = (byte)hex.RegionId;
                bytes[offset + 1] = (byte)((area.IndexInRegion << 1) | ((hex.RegionId >> 8) & 1));
                bytes[offset + 2] = (byte)hex.GroundTypeIndex;
                bytes[offset + 3] = (byte)hex.ClimateIndex;
                bytes[offset + 4] = (byte)(hex.AttritionIndex + 1);
                bytes[offset + 5] = hex.RoadEdgeMask;
                bytes[offset + 6] = hex.RiverEdgeMask;
            }

            return bytes;
        }

        private static void WriteRegion(EsfWriter esf, MapDataModel model, MapDataSlotSettlement settlement, int region)
        {
            esf.BeginRecord("REGION_DATA", 0);
            esf.WriteAscii(model.RegionNames[region]);
            esf.WriteByte(model.PassableSea[region] ? (byte)1 : (byte)0);
            MapDataEsfParts.WriteRegionBounds(esf, model, region);
            MapDataEsfParts.WriteRegionLinks(esf, model, region);
            MapDataEsfParts.WriteRegionAreas(esf, model, region, MapDataAreaReferences.PackedArray);

            esf.WriteBool(settlement != null);
            if (settlement != null)
            {
                MapDataEsfParts.WriteSlotSettlement(esf, model.Grid, settlement);
            }

            MapDataEsfParts.WriteDock(esf, model, settlement != null ? settlement.DockHex : -1);
            esf.EndRecord();
        }
    }
}
