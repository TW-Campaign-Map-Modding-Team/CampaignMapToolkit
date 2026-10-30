using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// The database tables the Assembly Kit's data builder reads to build one map's map_data.esf,
    /// made from the project's own database so that it never reads the Assembly Kit's copies.
    ///
    /// Every game's data builder reads the same five tables - six for Rome II, which adds
    /// campaign_map_slots - by name from the db folder MapDataBuilder passes it, and very little of
    /// them reaches map_data.esf. Comparing its output byte for byte with each table, row and column
    /// taken away or changed, on Rome II, Attila, Warhammer 1-3, Three Kingdoms and Pharaoh, showed:
    ///   - campaign_maps: the map's row, found by mapname. minx, miny, maxx and maxy must be present,
    ///     but no game uses their values.
    ///   - campaign_map_playable_areas: the map's row. index, overlay_file, sea_trade and maxx shape
    ///     the output, and so do minx and miny on Rome II and Attila. maxy must be present; only
    ///     Rome II uses its value, and only stores it.
    ///   - regions, campaign_map_settlements, campaign_ground_types: at least one row, of any content.
    ///   - campaign_map_slots (Rome II only): must exist, and may be empty.
    /// A missing or empty table crashes or fails the data builder, and with no campaign_maps row for
    /// the map it reports success yet writes nothing, so each table is written even where none of
    /// its content matters.
    /// </summary>
    public static class MapDataBuilderTables
    {
        private const string PlaceholderValue = "unused";

        /// <summary>
        /// The tables for <paramref name="mapName"/>, keyed by table name, made from its rows in
        /// <paramref name="playableAreas"/>, an Assembly Kit campaign_map_playable_areas document.
        /// Throws <see cref="InvalidOperationException"/> when that has no row for the map, or the
        /// row has no maxx.
        /// </summary>
        public static IReadOnlyDictionary<string, XDocument> Build(string mapName, uint hexColumns, uint hexRows, XDocument playableAreas)
        {
            var playableAreaRows = playableAreas.Root
                .Elements(Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS)
                .Where(row => string.Equals((string)row.Element("mapname"), mapName, StringComparison.OrdinalIgnoreCase))
                .Select(row => new XElement(row))
                .ToList();

            if (playableAreaRows.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS} has no row for {mapName}, and map_data.esf cannot be built without one.");
            }

            foreach (var row in playableAreaRows)
            {
                CompleteBounds(row, mapName, hexColumns, hexRows);
            }

            var bounds = playableAreaRows[0];

            return new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.TABLE_CAMPAIGN_MAPS] = Table(new XElement(Constants.TABLE_CAMPAIGN_MAPS,
                    new XElement("mapname", mapName),
                    new XElement("minx", bounds.Element("minx").Value),
                    new XElement("miny", bounds.Element("miny").Value),
                    new XElement("maxx", bounds.Element("maxx").Value),
                    new XElement("maxy", bounds.Element("maxy").Value))),
                [Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS] = Table(playableAreaRows.ToArray()),
                [Constants.TABLE_REGIONS]                     = PlaceholderTable(Constants.TABLE_REGIONS, "key"),
                [Constants.TABLE_CAMPAIGN_MAP_SETTLEMENTS]    = PlaceholderTable(Constants.TABLE_CAMPAIGN_MAP_SETTLEMENTS, "settlement_id"),
                [Constants.TABLE_CAMPAIGN_GROUND_TYPES]       = PlaceholderTable(Constants.TABLE_CAMPAIGN_GROUND_TYPES, "type"),
                [Constants.TABLE_CAMPAIGN_MAP_SLOTS]          = Table(),
            };
        }

        /// <summary>Writes each table to <c>&lt;folder&gt;/&lt;table&gt;.xml</c>.</summary>
        public static void Write(IReadOnlyDictionary<string, XDocument> tables, string folder)
        {
            foreach (var table in tables)
            {
                Rpfm.DatabaseTableConverter.WriteAssemblyKitXml(table.Value, Path.Combine(folder, table.Key + ".xml"));
            }
        }

        /// <summary>
        /// A playable area's maxy from its maxx and the size of its hex grid, for a map with none in
        /// the database: no game's packs carry maxy. The grid is of flat-topped hexes, each sqrt(3)/2
        /// as tall as it is wide, so C columns span 0.75 (C - 1) + 1 hex widths and R rows span
        /// R + 0.5 hex heights. The Assembly Kit's own values were typed in by hand, and sit within
        /// about a unit of this.
        /// </summary>
        public static double EstimatePlayableAreaMaxY(double maxX, uint hexColumns, uint hexRows)
        {
            var hexWidth = maxX / (0.75 * (hexColumns - 1) + 1);
            return (hexRows + 0.5) * hexWidth * Math.Sqrt(3) / 2;
        }

        // Zero is the schema default for every bound, so it is what a map with no row in the Assembly
        // Kit's own table arrives with. minx and miny are 0 in every map the games ship; maxy is
        // worked out from the hex grid.
        private static void CompleteBounds(XElement playableArea, string mapName, uint hexColumns, uint hexRows)
        {
            if (!TryReadPositive(playableArea, "maxx", out var maxX))
            {
                throw new InvalidOperationException(
                    $"The {Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS} row for {mapName} has no maxx, and map_data.esf cannot be built without it.");
            }

            if (!TryReadPositive(playableArea, "maxy", out _))
            {
                var maxY = Math.Round(EstimatePlayableAreaMaxY(maxX, hexColumns, hexRows), 2);
                playableArea.SetElementValue("maxy", maxY.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var bound in new[] { "minx", "miny" })
            {
                if (playableArea.Element(bound) == null)
                {
                    playableArea.SetElementValue(bound, "0");
                }
            }
        }

        private static bool TryReadPositive(XElement record, string field, out double value)
        {
            value = 0;
            var text = (string)record.Element(field);
            return text != null
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value > 0;
        }

        private static XDocument PlaceholderTable(string table, string field)
        {
            return Table(new XElement(table, new XElement(field, PlaceholderValue)));
        }

        private static XDocument Table(params XElement[] rows)
        {
            return new XDocument(new XDeclaration("1.0", "UTF-8", null), new XElement("dataroot", rows));
        }
    }
}
