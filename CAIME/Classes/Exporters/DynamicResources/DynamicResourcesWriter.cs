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
    /// Writes dynamic_resources.esf, the farmland tiles that appear as a region's buildings level
    /// up (FileFormatDefinitions/dynamic_resources_esf.md). The regions come from
    /// <see cref="DynamicResourceRegions"/>.
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
            var esf = new EsfWriter(game == GameTemplate.Warhammer3 ? EsfFormat.Abcb : EsfFormat.Abca, HashMap(game));
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
        /// The game builder's hash map, which orders the string table and the regions. The games
        /// after Warhammer ship no file with a region to check theirs against; Warhammer III's
        /// builder is read, and the others are taken to share it, as their map_data.esf files do.
        /// </summary>
        public static CaHashMap HashMap(GameTemplate game)
        {
            switch (game)
            {
                case GameTemplate.Rome2:                return CaHashMap.RomeII;
                case GameTemplate.Attila:
                case GameTemplate.Thrones_Of_Britannia: return CaHashMap.Attila;
                case GameTemplate.Warhammer:            return CaHashMap.Warhammer;
                default:                                return CaHashMap.Murmur;
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
