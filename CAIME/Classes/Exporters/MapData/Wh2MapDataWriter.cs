using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Warhammer II map_data.esf: Warhammer's file (<see cref="Wh1MapDataWriter"/>) with
    /// Warhammer III's hex radius rounding.
    /// </summary>
    public static class Wh2MapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            return PharaohMapDataWriter.Write(map, theatre, timestamp, map.UnknownEntries.Select(mask => mask.Key).ToList(), MapDataRadiusRounding.TwoThirdsPerColumn);
        }
    }
}
