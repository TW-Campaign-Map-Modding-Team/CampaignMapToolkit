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
    /// Warhammer III's Assembly Kit writes ABCB; every other game's shipped file is ABCA. Rome II's
    /// and Attila's string tables follow their games' string hashes (<see cref="CaStringHash"/>);
    /// the 64-bit games' files hold only the build time, so their order cannot show.
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

        public static byte[] Write(IReadOnlyList<DynamicResourceRegion> regions, DateTime buildTimeUtc, GameTemplate game)
        {
            var esf = new EsfWriter(game == GameTemplate.Warhammer3 ? EsfFormat.Abcb : EsfFormat.Abca, StringTableHash(game));
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

        private static Func<string, uint> StringTableHash(GameTemplate game)
        {
            switch (game)
            {
                case GameTemplate.Rome2:                return CaStringHash.RomeII;
                case GameTemplate.Attila:
                case GameTemplate.Thrones_Of_Britannia: return CaStringHash.Attila;
                default:                                return null;
            }
        }

        /// <summary>
        /// Year, month, day, hour, minute and second, none of them padded: Troy's troy_mythic file,
        /// built 2021-03-19 09:00:01 UTC, has "2021319901". Every shipped file's text matches the
        /// timestamp in its header this way.
        /// </summary>
        public static string BuildTimeStamp(DateTime buildTimeUtc)
        {
            return string.Concat(buildTimeUtc.Year, buildTimeUtc.Month, buildTimeUtc.Day, buildTimeUtc.Hour, buildTimeUtc.Minute, buildTimeUtc.Second);
        }

        private static void WriteAreas(EsfWriter esf, List<DynamicResourceArea> areas)
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
