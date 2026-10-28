using System;
using System.Collections.Generic;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>One building level's resource tiles in a region: groups of hexes, each a list of (column, row).</summary>
    public sealed class DynamicResourceArea
    {
        public string               BuildingSuperchain  { get; set; }
        public uint                 Level               { get; set; }
        public string               TileUpgradeId       { get; set; }
        public List<List<ushort>>   HexGroups           { get; } = new List<List<ushort>>();
    }

    public sealed class DynamicResourceRegion
    {
        public int                          Region  { get; set; }
        public List<DynamicResourceArea>    Areas   { get; } = new List<DynamicResourceArea>();
    }

    /// <summary>
    /// Writes dynamic_resources.esf (CAMPAIGN_DYNAMIC_RESOURCES), the farmland tiles that appear on
    /// the campaign map as a region's buildings level up.
    ///
    /// <para>
    /// The layout, from Attila's main_attila_map file:
    /// <code>
    /// CAMPAIGN_DYNAMIC_RESOURCES v0
    ///   ascii                         build time, UTC
    ///   REGION_RESOURCE_INFO_BLOCK v0, one entry per region with resources:
    ///     u16                         0xFE00 | region index
    ///     REGION_RESOURCE_INFO_AREAS_BLOCK v1, one entry per building level:
    ///       ascii building_superchain, u32 level, ascii tile_upgrade_id
    ///       REGION_RESOURCE_AREAS_BLOCK v0, one entry per group of hexes:
    ///         u16[]                   column, row, column, row, ...
    /// </code>
    /// Warhammer III's only map with inputs, wh3_main_prologue_map, paints its whole
    /// dynamic_resources.png one colour that no resource uses, and the Assembly Kit writes the
    /// header with no regions; this writer reproduces that byte for byte. The regions come from
    /// <see cref="DynamicResourceRegions"/>.
    /// </para>
    ///
    /// <para>
    /// TODO: Attila writes the file as ABCA (16-bit string table lengths); this writes Warhammer
    /// III's ABCB.
    /// </para>
    /// </summary>
    public static class DynamicResourcesWriter
    {
        private const ushort REGION_INDEX_FLAG = 0xFE00;

        /// <summary>The u16 the file stores for a region: 0xFE00 | its index.</summary>
        public static ushort RegionKey(int region)
        {
            return (ushort)(REGION_INDEX_FLAG | region);
        }

        public static byte[] Write(IReadOnlyList<DynamicResourceRegion> regions, DateTime buildTimeUtc)
        {
            var esf = new AbcbWriter();
            esf.BeginRecord("CAMPAIGN_DYNAMIC_RESOURCES", 0);
            esf.WriteAscii(BuildTimeStamp(buildTimeUtc));

            esf.BeginBlock("REGION_RESOURCE_INFO_BLOCK", 0);
            foreach (var region in regions)
            {
                esf.BeginBlockEntry();
                esf.WriteUInt16(RegionKey(region.Region));
                WriteAreas(esf, region.Areas);
                esf.EndBlockEntry();
            }

            esf.EndBlock();
            esf.EndRecord();

            return esf.ToArray((uint)new DateTimeOffset(buildTimeUtc, TimeSpan.Zero).ToUnixTimeSeconds());
        }

        /// <summary>
        /// yyyyMMdd then the time: Attila's file has "20141114153252", and a Warhammer III build
        /// at 03:23:34 UTC wrote "2026102832334", the hour unpadded. Whether a single-digit month
        /// or day is padded is not known.
        /// </summary>
        public static string BuildTimeStamp(DateTime buildTimeUtc)
        {
            return buildTimeUtc.ToString("yyyyMMdd") + buildTimeUtc.Hour + buildTimeUtc.ToString("mmss");
        }

        private static void WriteAreas(AbcbWriter esf, List<DynamicResourceArea> areas)
        {
            esf.BeginBlock("REGION_RESOURCE_INFO_AREAS_BLOCK", 1);
            foreach (var area in areas)
            {
                esf.BeginBlockEntry();
                esf.WriteAscii(area.BuildingSuperchain);
                esf.WriteUInt32(area.Level);
                esf.WriteAscii(area.TileUpgradeId);

                esf.BeginBlock("REGION_RESOURCE_AREAS_BLOCK", 0);
                foreach (var group in area.HexGroups)
                {
                    esf.BeginBlockEntry();
                    esf.WriteUInt16Array(group);
                    esf.EndBlockEntry();
                }

                esf.EndBlock();
                esf.EndBlockEntry();
            }

            esf.EndBlock();
        }
    }
}
