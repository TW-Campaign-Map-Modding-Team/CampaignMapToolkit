using System;

namespace CAIME.Exporters
{
    /// <summary>
    /// Builds a Rome II map_data.esf, worked out from the game's main_rome_map file,
    /// Templates/main_rome_map/map.hex and the Rome II Assembly Kit's builder
    /// (ToolDataBuilderDll.AssemblyKit.dll). Rome II's builder writes the same file as Attila's
    /// (<see cref="AttilaMapDataWriter"/>), but stores the playable area's maxy as given rather than
    /// the top edge of the hex grid.
    ///
    /// <para>
    /// Rome II's packs have no maxy (the playable areas table lost minx, miny and maxy after
    /// version 4), and the Assembly Kit's values were typed by hand. A theatre with no
    /// <see cref="MapDataTheatre.MaxY"/> therefore gets <see cref="EstimateMaxY"/> rounded to two
    /// places.
    /// </para>
    ///
    /// <para>
    /// The game's own main_rome_map file was built by an older builder: its CAMPAIGN_THEATRE is v2
    /// and holds a 256-colour table before HEX_MAP_DATA. The Assembly Kit's builder writes v3 with
    /// no table, as Attila's does, and so does this writer. Everything else matches the game's file.
    /// </para>
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

            return AttilaMapDataWriter.Write(map, withMaxY, timestamp);
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
