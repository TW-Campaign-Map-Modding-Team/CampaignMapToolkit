using System.Collections.Generic;
using System.Linq;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>How a game's map_data.esf lists an area's touching areas.</summary>
    public enum MapDataAreaReferences
    {
        /// <summary>Warhammer III: a REGION_AREA_INDEX block of (region, area) entries.</summary>
        Blocks,

        /// <summary>Attila and Pharaoh: a u16 array of area &lt;&lt; 9 | region.</summary>
        PackedArray,
    }

    /// <summary>The parts of map_data.esf that every game writes the same way.</summary>
    public static class MapDataEsfParts
    {
        private const byte CAMPAIGN_THEATRE_VERSION = 3;
        private const byte REGION_AREA_DATA_VERSION = 2;
        private const int  PACKED_AREA_SHIFT        = 9;

        /// <summary>The theatre's bounds and, for the campaign UI, a position for each region listed.</summary>
        public static void WriteTheatreForUi(EsfWriter esf, MapDataModel model, IEnumerable<(int Region, float X, float Y)> regionPositions)
        {
            esf.BeginBlock("THEATRES_AND_REGIONS_FOR_UI", 0);
            esf.BeginBlockEntry();
            esf.BeginRecord("THEATRE", 0);
            esf.WriteAscii(model.Theatre.Index);
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(model.Geometry.MaxX, model.Geometry.MaxY);

            esf.BeginBlock("REGION_KEYS", 0);
            foreach (var position in regionPositions)
            {
                esf.BeginBlockEntry();
                esf.WriteAscii(model.RegionNames[position.Region]);
                esf.WriteCoordinates(position.X, position.Y);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
            esf.EndRecord();
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        public static void WriteTheatres(EsfWriter esf, MapDataModel model, byte hexMapDataVersion, byte[] hexBytes)
        {
            esf.BeginBlock("theatres", 0);
            esf.BeginBlockEntry();
            esf.BeginRecord("CAMPAIGN_THEATRE", CAMPAIGN_THEATRE_VERSION);
            esf.WriteCoordinates(0f, 0f);
            esf.WriteCoordinates(model.Geometry.MaxX, model.Geometry.MaxY);
            esf.WriteAscii(model.Theatre.Index);

            esf.BeginRecord("HEX_MAP_DATA", hexMapDataVersion);
            esf.WriteUInt32(model.Map.MapWidth);
            esf.WriteUInt32(model.Map.MapHeight);
            esf.WriteByteArray(hexBytes);
            esf.EndRecord();

            esf.EndRecord();
            esf.EndBlockEntry();
            esf.EndBlock();
        }

        public static void WriteNameLists(EsfWriter esf, MapHexFile map)
        {
            esf.WriteAsciiArray(map.Climates);
            esf.WriteAsciiArray(map.LandGroundTypes.Concat(map.SeaGroundTypes).ToList());
            esf.WriteAsciiArray(map.Attritions);
        }

        /// <summary>World and hex bounds, then the hex count. A region without hexes writes zeroes.</summary>
        public static void WriteRegionBounds(EsfWriter esf, MapDataModel model, int region)
        {
            var bounds = model.RegionBounds[region];
            if (bounds.HexCount == 0)
            {
                WriteBounds(esf, 0f, 0f, 0f, 0f, 0, 0, 0, 0);
            }
            else
            {
                model.RegionWorldBounds(region, out float minX, out float minY, out float maxX, out float maxY);
                WriteBounds(esf, minX, minY, maxX, maxY, bounds.MinColumn, bounds.MinRow, bounds.MaxColumn, bounds.MaxRow);
            }

            esf.WriteUInt32((uint)bounds.HexCount);
        }

        public static void WriteRegionLinks(EsfWriter esf, MapDataModel model, int region)
        {
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_TRAVERSABLE_SAME", model.Links.TraversableSame[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_NONTRAVERSABLE_SAME", model.Links.NonTraversableSame[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_TRAVERSABLE_OTHER", model.Links.TraversableOther[region]);
            WriteRegionLinks(esf, "CONNECTIVITY_DIRECT_NONTRAVERSABLE_OTHER", model.Links.NonTraversableOther[region]);

            // No Warhammer III, Attila or Pharaoh map has an entry here.
            WriteRegionLinks(esf, "CONNECTIVITY_LAND_INDIRECT_VIA_FORD", new List<MapDataRegionLink>());
        }

        public static void WriteRegionAreas(EsfWriter esf, MapDataModel model, int region, MapDataAreaReferences references)
        {
            esf.BeginBlock("REGION_AREAS", 0);
            foreach (int areaId in model.Areas.RegionAreas[region])
            {
                esf.BeginBlockEntry();
                WriteArea(esf, model, model.Areas.Areas[areaId], references);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }

        /// <summary>
        /// What the int means is unknown; the builder always writes 0. Then the sea region and hex a
        /// port docks at, or (0xFFFF, 0, 0).
        /// </summary>
        public static void WriteDock(EsfWriter esf, MapDataModel model, int dockHex)
        {
            esf.WriteInt32(0);
            if (dockHex >= 0)
            {
                esf.WriteUInt16((ushort)model.Map.HexData[dockHex].RegionId);
                esf.WriteUInt16((ushort)model.Grid.Column(dockHex));
                esf.WriteUInt16((ushort)model.Grid.Row(dockHex));
            }
            else
            {
                esf.WriteUInt16(0xFFFF);
                esf.WriteUInt16(0);
                esf.WriteUInt16(0);
            }
        }

        public static void WriteHexList(EsfWriter esf, MapDataGrid grid, string blockName, IEnumerable<int> hexes)
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

        /// <summary>An area as Attila and Pharaoh reference it: area &lt;&lt; 9 | region.</summary>
        public static ushort PackedAreaReference(MapDataArea area)
        {
            return (ushort)((area.IndexInRegion << PACKED_AREA_SHIFT) | area.Region);
        }

        /// <summary>
        /// A settlement with building slots (Attila and Pharaoh): its footprint, its slots and its
        /// port. With <paramref name="portIncludesMainSlot"/> (Pharaoh), a port's area also lists
        /// the main slot's hexes after its own, as Warhammer III's does.
        /// </summary>
        public static void WriteSlotSettlement(EsfWriter esf, MapDataGrid grid, MapDataSlotSettlement settlement, bool portIncludesMainSlot = false)
        {
            esf.BeginRecord("SETTLEMENT_INFO", 0);
            WriteFootprint(esf, settlement.FootprintMinColumn, settlement.FootprintMinRow,
                           settlement.FootprintMaxColumn, settlement.FootprintMaxRow, settlement.FootprintBits);

            esf.BeginBlock("SLOT_ARRAY_BLOCK", 0);
            foreach (var slot in settlement.Slots)
            {
                esf.BeginBlockEntry();
                WriteHexList(esf, grid, "SLOT_POSITIONS_BLOCK", slot.Positions);
                WriteHexList(esf, grid, "SLOT_AREA_BLOCK", slot.Hexes);
                esf.EndBlockEntry();
            }

            esf.EndBlock();

            var port        = settlement.Port;
            bool addMain    = portIncludesMainSlot && port.Hexes.Count > 0;
            var mainSlot    = settlement.Slots[0];

            WriteHexList(esf, grid, "PORT_POSITIONS_BLOCK", port.Positions);
            WriteHexList(esf, grid, "PORT_AREA_BLOCK", addMain ? port.Hexes.Concat(mainSlot.Hexes) : port.Hexes);
            esf.EndRecord();
        }

        /// <summary>A footprint: its hex rectangle, its width, and one bit per hex, row by row, lowest bit first.</summary>
        public static void WriteFootprint(EsfWriter esf, int minColumn, int minRow, int maxColumn, int maxRow, byte[] bits)
        {
            esf.BeginRecord("LOGICAL_POSITION_BIT_ARRAY", 0);
            esf.WriteUInt16((ushort)minColumn);
            esf.WriteUInt16((ushort)minRow);
            esf.WriteUInt16((ushort)maxColumn);
            esf.WriteUInt16((ushort)maxRow);
            esf.WriteUInt32((uint)(maxColumn - minColumn + 1));
            esf.WriteByteArray(bits);
            esf.EndRecord();
        }

        private static void WriteBounds(EsfWriter esf, float minX, float minY, float maxX, float maxY, int minColumn, int minRow, int maxColumn, int maxRow)
        {
            esf.WriteCoordinates(minX, minY);
            esf.WriteCoordinates(maxX, maxY);
            esf.WriteUInt16((ushort)minColumn);
            esf.WriteUInt16((ushort)minRow);
            esf.WriteUInt16((ushort)maxColumn);
            esf.WriteUInt16((ushort)maxRow);
        }

        private static void WriteRegionLinks(EsfWriter esf, string blockName, List<MapDataRegionLink> links)
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

        private static void WriteArea(EsfWriter esf, MapDataModel model, MapDataArea area, MapDataAreaReferences references)
        {
            esf.BeginRecord("REGION_AREA_DATA", REGION_AREA_DATA_VERSION);
            esf.WriteByte((byte)area.Type);

            model.Geometry.Bounds(area.MinColumn, area.MinRow, area.MaxColumn, area.MaxRow, out float minX, out float minY, out float maxX, out float maxY);
            esf.WriteCoordinates(minX, minY);
            esf.WriteCoordinates(maxX, maxY);
            esf.WriteUInt16((ushort)area.Group);
            esf.WriteUInt16((ushort)area.MinColumn);
            esf.WriteUInt16((ushort)area.MinRow);
            esf.WriteUInt16((ushort)area.MaxColumn);
            esf.WriteUInt16((ushort)area.MaxRow);

            WriteAreaReferences(esf, model.Areas, area.Traversable, references);
            WriteAreaReferences(esf, model.Areas, area.NonTraversable, references);

            esf.WriteUInt16((ushort)model.Grid.Column(area.CentreHex));
            esf.WriteUInt16((ushort)model.Grid.Row(area.CentreHex));
            esf.WriteUInt32((uint)area.HexCount);
            esf.EndRecord();
        }

        private static void WriteAreaReferences(EsfWriter esf, MapDataAreas areas, List<int> areaIds, MapDataAreaReferences references)
        {
            if (references == MapDataAreaReferences.PackedArray)
            {
                esf.WriteUInt16Array(areaIds.Select(areaId => PackedAreaReference(areas.Areas[areaId])).ToList());
                return;
            }

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
    }
}
