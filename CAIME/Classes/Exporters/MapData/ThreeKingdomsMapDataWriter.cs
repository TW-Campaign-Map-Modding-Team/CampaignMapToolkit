using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Three Kingdoms map_data.esf: Warhammer III's (<see cref="Wh3MapDataWriter"/>) with
    /// Three Kingdoms' layout (FileFormatDefinitions/map_data_esf.md).
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
