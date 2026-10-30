namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Troy map_data.esf, worked out from the game's main_troy, troy_historical and
    /// troy_mythic files and Templates' map.hex files for them. Troy's builder writes the same file
    /// as Warhammer II's (<see cref="Wh2MapDataWriter"/>): all three match it as they are, and their
    /// positions show the hex radius rounded as Warhammer II rounds it.
    /// </summary>
    public static class TroyMapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Wh2MapDataWriter.Write(map, theatre, timestamp);
        }
    }
}
