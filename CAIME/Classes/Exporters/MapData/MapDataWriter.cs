using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace CAIME.Exporters
{
    /// <summary>Picks each game's map_data.esf writer, and reads the playable area it is built from.</summary>
    public static class MapDataWriter
    {
        /// <summary>
        /// The map_data.esf for <paramref name="game"/>. Thrones of Britannia is taken to build as
        /// Attila does; no file of its own has been checked.
        /// </summary>
        public static byte[] Write(GameTemplate game, MapHexFile map, MapDataTheatre theatre, uint timestamp)
        {
            switch (game)
            {
                case GameTemplate.Rome2:                return RomeIIMapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Attila:
                case GameTemplate.Thrones_Of_Britannia: return AttilaMapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Warhammer:            return Wh1MapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Warhammer2:           return Wh2MapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Warhammer3:           return Wh3MapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Three_Kingdoms:       return ThreeKingdomsMapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Troy:                 return TroyMapDataWriter.Write(map, theatre, timestamp);
                case GameTemplate.Pharaoh:
                case GameTemplate.Pharaoh_Dynasties:    return PharaohMapDataWriter.Write(map, theatre, timestamp);
                default:                                throw new ArgumentOutOfRangeException(nameof(game), game, "No map_data.esf writer for this game.");
            }
        }

        /// <summary>
        /// The theatre from <paramref name="mapName"/>'s first row in <paramref name="playableAreas"/>,
        /// a campaign_map_playable_areas document in the Assembly Kit's layout: every game's builder
        /// uses only that row. Only Rome II keeps the row's maxy, which no other game's builder stores.
        /// Throws <see cref="InvalidOperationException"/> when there is no row for the map, or it has
        /// no maxx.
        /// </summary>
        public static MapDataTheatre ReadTheatre(GameTemplate game, string mapName, XDocument playableAreas)
        {
            var row = playableAreas.Root
                .Elements(Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS)
                .FirstOrDefault(r => string.Equals((string)r.Element("mapname"), mapName, StringComparison.OrdinalIgnoreCase));

            if (row == null)
            {
                throw new InvalidOperationException(
                    $"{Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS} has no row for {mapName}, and map_data.esf cannot be built without one.");
            }

            if (TryReadPositive(row, "maxx", out float maxX) == false)
            {
                throw new InvalidOperationException(
                    $"The {Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS} row for {mapName} has no maxx, and map_data.esf cannot be built without it.");
            }

            return new MapDataTheatre
            {
                Index   = (string)row.Element("index") ?? "",
                MaxX    = maxX,
                MaxY    = game == GameTemplate.Rome2 && TryReadPositive(row, "maxy", out float maxY) ? maxY : (float?)null,
            };
        }

        // Zero is the schema default for every bound, so it counts as missing.
        private static bool TryReadPositive(XElement row, string field, out float value)
        {
            value = 0;
            var text = (string)row.Element(field);
            return text != null
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value > 0;
        }
    }
}
