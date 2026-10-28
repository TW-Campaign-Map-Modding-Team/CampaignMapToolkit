using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer map_data.esf, worked out from the game's warhammer_map_1_1_wood_elves,
    /// wh_dlc03_beastmen_map_1 and wh_dlc05_wood_elves_map_1 files and the Assembly Kit's map.hex
    /// files for them. Warhammer's builder writes Pharaoh's file (<see cref="PharaohMapDataWriter"/>),
    /// but once for each campaign mask the map.hex lists (<see cref="MapHexFile.UnknownEntries"/>):
    /// the UI's region list repeats for each mask, and each gets a MASKED_REGIONS_DATA entry under
    /// its name. The main maps have only the unnamed mask; the mini-campaign maps add "8" and "32".
    /// The hex radius is rounded as Attila rounds it, not as Pharaoh does.
    ///
    /// <para>
    /// Each mask in map.hex also has a bitmap of hexes, and the builder makes a hex whose bit is set
    /// impassable in that mask's entry (read from Warhammer III's Assembly Kit). Every vanilla
    /// bitmap is empty, which makes every entry a copy of the unnamed one, and CAIME discards the
    /// bitmaps, so it writes copies too.
    /// </para>
    /// </summary>
    public static class Wh1MapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return PharaohMapDataWriter.Write(map, theatre, timestamp, map.UnknownEntries.Select(mask => mask.Key).ToList(), MapDataRadiusRounding.PerColumnAndAHalf);
        }
    }
}
