using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer map_data.esf: Pharaoh's file (<see cref="PharaohMapDataWriter"/>) once
    /// for each campaign mask, with Attila's hex radius rounding. See FileFormatDefinitions/map_data_esf.md.
    /// </summary>
    public static class Wh1MapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return PharaohMapDataWriter.Write(map, theatre, timestamp, map.UnknownEntries.Select(mask => mask.Key).ToList(), MapDataRadiusRounding.PerColumnAndAHalf);
        }
    }
}
