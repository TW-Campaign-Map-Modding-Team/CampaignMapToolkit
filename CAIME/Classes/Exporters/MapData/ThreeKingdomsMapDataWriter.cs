using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Three Kingdoms map_data.esf, worked out from the game's 3k_dlc07_main_map file and
    /// Templates/3k_dlc07_main_map/map.hex. It is Warhammer III's MAP_DATA v2
    /// (<see cref="Wh3MapDataWriter"/>) but for: ABCA rather than ABCB; MAP_FILE_DATA_VERSION 2;
    /// MAP_FILE_VALIDATION holding the map.hex's save time (<see cref="MapHexFile.Timestamp"/>);
    /// and packed area references and per-hex runs, as in Pharaoh. Its builder decides sea flags and
    /// which regions get settlements as Warhammer III's does. Its builder would apply campaign masks
    /// to the areas, as Warhammer II's does, but its map.hex loader gives every v0x13 map just the
    /// unnamed mask with nothing masked, so the file always has one empty mask entry.
    /// </summary>
    public static class ThreeKingdomsMapDataWriter
    {
        private static readonly Wh3MapDataWriter.Layout LAYOUT = new Wh3MapDataWriter.Layout
        {
            Format              = EsfFormat.Abca,
            DataVersion         = 2,
            ValidatesMapHex     = true,
            AreaReferences      = MapDataAreaReferences.PackedArray,
            RunsAsArray         = true,
        };

        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return Wh3MapDataWriter.Write(map, theatre, timestamp, LAYOUT);
        }
    }
}
