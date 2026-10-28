using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer II map_data.esf, worked out from the game's wh2_main_great_vortex_map_7 and
    /// warhammer_map_1_1_twilight files and the Assembly Kit's map.hex files for them. Warhammer II's
    /// builder writes Warhammer's file (<see cref="Wh1MapDataWriter"/>), but rounds the hex radius as
    /// Pharaoh and Warhammer III do, not as Attila does.
    ///
    /// <para>
    /// Neither map has a campaign mask besides the unnamed one, so writing a region list for each
    /// mask, as Warhammer does, is assumed rather than seen.
    /// </para>
    /// </summary>
    public static class Wh2MapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return PharaohMapDataWriter.Write(map, theatre, timestamp, map.UnknownEntries.Select(mask => mask.Key).ToList(), MapDataRadiusRounding.TwoThirdsPerColumn);
        }
    }
}
