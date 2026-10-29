namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer II map_data.esf, worked out from the game's wh2_main_great_vortex_map_7 and
    /// warhammer_map_1_1_twilight files and the Assembly Kit's map.hex files for them. Warhammer II's
    /// builder writes Warhammer's file (<see cref="Wh1MapDataWriter"/>), but rounds the hex radius as
    /// Pharaoh and Warhammer III do, not as Attila does.
    ///
    /// <para>
    /// Neither map has a campaign mask besides the unnamed one. The builder's output for the vortex
    /// map with a mask painted in matches this writer's node for node (<see cref="MapDataMask"/>).
    /// </para>
    /// </summary>
    public static class Wh2MapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return PharaohMapDataWriter.Write(map, theatre, timestamp, MapDataRadiusRounding.TwoThirdsPerColumn);
        }
    }
}
