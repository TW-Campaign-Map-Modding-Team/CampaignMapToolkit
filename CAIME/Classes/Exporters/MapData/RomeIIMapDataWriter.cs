using System;
using EsfLibrary;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Rome II map_data.esf: Attila's file (<see cref="AttilaMapDataWriter"/>) with the
    /// playable area's maxy as given, or <see cref="EstimateMaxY"/> rounded to two places when the
    /// packs have none, and Rome II's string hash. See FileFormatDefinitions/map_data_esf.md.
    /// </summary>
    public static class RomeIIMapDataWriter
    {
        public static byte[] Write(MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            var withMaxY = new MapDataTheatre
            {
                Index   = theatre.Index,
                MaxX    = theatre.MaxX,
                MaxY    = theatre.MaxY ?? (float)Math.Round(EstimateMaxY(theatre.MaxX, map.MapWidth, map.MapHeight), 2),
            };

            return AttilaMapDataWriter.Write(map, withMaxY, timestamp, CaHashMap.RomeII);
        }

        /// <summary>
        /// A playable area's maxy from its maxx and the size of its hex grid. The grid is of
        /// flat-topped hexes, each sqrt(3)/2 as tall as it is wide, so C columns span 0.75 (C - 1) + 1
        /// hex widths and R rows span R + 0.5 hex heights. The Assembly Kit's own values were typed in
        /// by hand, and sit within about a unit of this.
        /// </summary>
        public static double EstimateMaxY(double maxX, uint hexColumns, uint hexRows)
        {
            var hexWidth = maxX / (0.75 * (hexColumns - 1) + 1);
            return (hexRows + 0.5) * hexWidth * Math.Sqrt(3) / 2;
        }
    }
}
