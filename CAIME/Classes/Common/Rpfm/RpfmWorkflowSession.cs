using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Reads the database tables a project needs through rpfm_server (see <see cref="RpfmService"/>),
    /// in one server session. Each table is merged from its fragments in the game's packs and in the
    /// project's mod and the mods it depends on (see <see cref="DatabaseTableConverter.MergeFragments"/>);
    /// a mod wins over the game when they share a fragment name.
    ///
    /// RPFM finds the game's packs through its install folder, and the mod by name in the game's
    /// data or Workshop folder. The mod is optional: a project with none reads the game alone, and
    /// a mod RPFM can't find or read is skipped with a warning.
    /// </summary>
    public sealed class RpfmWorkflowSession
    {
        private readonly GameTemplate           _game;
        private readonly string                 _modPackName;
        private readonly string                 _rpfmFolder;
        private readonly string                 _campaignMapName;
        private readonly IReadOnlyDictionary<string, bool> _regionIsSeaByKey;

        /// <param name="modPackName">
        /// The .pack file name of the project's mod. Optional - pass null when the project has none
        /// configured; every table then comes from the game's own packs instead.
        /// </param>
        /// <param name="mapHex">
        /// The project's already-loaded map. Its campaign map name decides which regions the region
        /// tables keep, and its land and sea regions are the source of "regions.is_sea" (see
        /// <see cref="RestrictRegionsToThisMap"/> and <see cref="BuildRegionSeaStatus"/>).
        /// </param>
        public RpfmWorkflowSession(GameTemplate game, string rpfmFolder, string modPackName, MapHexFile mapHex)
        {
            if (mapHex == null)
            {
                throw new ArgumentNullException(nameof(mapHex));
            }

            _game            = game;
            _modPackName     = modPackName;
            _rpfmFolder      = rpfmFolder;
            _campaignMapName = mapHex.CampaignMapName;
            _regionIsSeaByKey = BuildRegionSeaStatus(mapHex);
        }

        /// <summary>
        /// Reads every required table, each merged from the game's packs and every mod that contains
        /// a fragment of it, keyed by table name. Throws a descriptive exception on any failure.
        /// </summary>
        public IReadOnlyDictionary<string, RpfmTable> Prepare()
        {
            ValidatePreconditions();

            var requiredTables = DatabaseTableProvider.GetRequiredTables(_game);

            using (var rpfm = RpfmService.Open(_rpfmFolder, _game))
            {
                var fragmentsByTable = ReadFragments(rpfm, requiredTables);
                var rowFilters = RestrictRegionsToThisMap(fragmentsByTable);

                return MergeTables(rpfm, requiredTables, fragmentsByTable, rowFilters);
            }
        }

        private void ValidatePreconditions()
        {
            if (!GameMappingProvider.IsSupported(_game))
            {
                throw new NotSupportedException($"The RPFM workflow does not support {_game}.");
            }

            // RPFM's own schema and install folder for the game are checked by RpfmService.Open.
        }

        // Reads every fragment of each required table, the mods' first, then the game's: that order
        // makes a mod win over the game on the same fragment name. A failure reading the game aborts
        // the workflow; one reading the mods only drops them. Every required table must turn up.
        private Dictionary<string, List<TableFragment>> ReadFragments(RpfmService rpfm, IReadOnlyList<string> requiredTables)
        {
            var fragmentsByTable = new Dictionary<string, List<TableFragment>>(StringComparer.OrdinalIgnoreCase);

            var fromMods = ReadModFragments(rpfm, requiredTables);
            var fromGame = rpfm.ReadGameTables(requiredTables);

            foreach (var fragment in fromMods.Concat(fromGame))
            {
                if (!fragmentsByTable.TryGetValue(fragment.Table, out var fragments))
                {
                    fragments = new List<TableFragment>();
                    fragmentsByTable[fragment.Table] = fragments;
                }

                fragments.Add(fragment);
            }

            LogTableSources("the mods", fromMods);
            LogTableSources("the game's packs", fromGame);

            var missing = requiredTables.Where(t => !fragmentsByTable.ContainsKey(t)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"RPFM found no {string.Join(", ", missing)} {(missing.Count == 1 ? "table" : "tables")} " +
                    $"in {_game}'s packs or the project's mod. " +
                    "Check that the game folder set in RPFM's settings points at a complete install.");
            }

            return fragmentsByTable;
        }

        // The mod is optional: none configured, or one RPFM cannot find or read, leaves the mods out.
        private IReadOnlyList<TableFragment> ReadModFragments(RpfmService rpfm, IReadOnlyList<string> requiredTables)
        {
            if (string.IsNullOrEmpty(_modPackName))
            {
                return Array.Empty<TableFragment>();
            }

            try
            {
                var loaded = rpfm.LoadMod(_modPackName);

                if (!loaded.Contains(_modPackName))
                {
                    LoggerViewModel.Log(
                        $"RPFM workflow: RPFM found no mod named {_modPackName} in the game's data folder or its Steam " +
                        "Workshop folder - skipping it. Check the name (including its capitalisation) via Settings > " +
                        "RPFM Workflow, and that the mod is installed.", LogLevel.Warning);
                    return Array.Empty<TableFragment>();
                }

                var dependencies = loaded.Where(name => !string.Equals(name, _modPackName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (dependencies.Count > 0)
                {
                    LoggerViewModel.Log(
                        $"RPFM workflow: also reading the mods {_modPackName} depends on: " + string.Join(", ", dependencies), LogLevel.Info);
                }

                return rpfm.ReadModTables(requiredTables);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM workflow: could not read the mods - {ex.Message}. Skipping them.", LogLevel.Warning);
                return Array.Empty<TableFragment>();
            }
        }

        private static void LogTableSources(string source, IReadOnlyList<TableFragment> fragments)
        {
            var tables = fragments.Select(f => f.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tables.Count > 0)
            {
                LoggerViewModel.Log($"RPFM workflow: merging tables found in {source}: " + string.Join(", ", tables), LogLevel.Info);
            }
        }

        // Only this campaign map's regions go into the region tables: CAIME reads no other. A region
        // campaign_map_regions puts on this map that the hex map does not have yet is still kept, so
        // the project opens. Rows keep their order, and with it the region ids CAIME numbers by table
        // order.
        private IReadOnlyDictionary<string, RowFilter> RestrictRegionsToThisMap(Dictionary<string, List<TableFragment>> fragmentsByTable)
        {
            var regionsOnThisMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fragment in fragmentsByTable[Constants.TABLE_CAMPAIGN_MAP_REGIONS])
            {
                foreach (var row in DatabaseTableConverter.ReadRows(fragment))
                {
                    if (!row.TryGetValue("campaign_map", out var campaignMap) || !row.TryGetValue("region", out var region))
                    {
                        throw new InvalidDataException(
                            $"The {fragment.Table} fragment {fragment.FragmentName} has no campaign_map and region columns.");
                    }

                    if (string.Equals(campaignMap, _campaignMapName, StringComparison.OrdinalIgnoreCase))
                    {
                        regionsOnThisMap.Add(region);
                    }
                }
            }

            return new Dictionary<string, RowFilter>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.TABLE_CAMPAIGN_MAP_REGIONS] = new RowFilter("campaign_map", new[] { _campaignMapName }),
                [Constants.TABLE_REGIONS]              = new RowFilter("key", regionsOnThisMap),
                [Constants.TABLE_REGIONS_TO_PROVINCES] = new RowFilter("region", regionsOnThisMap),
            };
        }

        // Merges each table's fragments into one document under RPFM's schema for it,
        // keeping only the rows rowFilters selects for the tables it covers.
        private Dictionary<string, RpfmTable> MergeTables(
            RpfmService rpfm, IReadOnlyList<string> tables, Dictionary<string, List<TableFragment>> fragmentsByTable,
            IReadOnlyDictionary<string, RowFilter> rowFilters)
        {
            var merged = new Dictionary<string, RpfmTable>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in tables)
            {
                var schema            = DatabaseTableConverter.SchemaFromRpfm(rpfm.GetTableDefinition(table), FieldsNoPackCarries(table));
                var booleanColumns    = DatabaseTableConverter.GetBooleanColumns(schema);
                var primaryKeyColumns = DatabaseTableConverter.GetPrimaryKeyColumns(schema);

                // The fragment's own file name decides merge order when two sources define the same
                // primary key - the same rule the game uses to combine table fragments across packs.
                rowFilters.TryGetValue(table, out var rowFilter);
                merged[table] = new RpfmTable(schema, DatabaseTableConverter.MergeFragments(
                    fragmentsByTable[table], table, booleanColumns, primaryKeyColumns, schema.Fields, _regionIsSeaByKey, rowFilter));
            }

            return merged;
        }

        // "regions.is_sea", which tells land regions from sea, is in no pack's table; it is filled in
        // from the open map (see BuildRegionSeaStatus).
        private static IEnumerable<XmlSchemaField> FieldsNoPackCarries(string table)
        {
            if (string.Equals(table, Constants.TABLE_REGIONS, StringComparison.OrdinalIgnoreCase))
            {
                yield return XmlSchema.CreateField("is_sea", XmlSchemaFieldType.YesNo, false, "0");
            }
        }

        // Which regions this map treats as sea; no pack's table has "regions.is_sea", so the open map is the source.
        private static IReadOnlyDictionary<string, bool> BuildRegionSeaStatus(MapHexFile mapHex)
        {
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var region in mapHex.LandRegions ?? Enumerable.Empty<string>())
            {
                result[region] = false;
            }

            foreach (var region in mapHex.SeaRegions ?? Enumerable.Empty<string>())
            {
                result[region] = true;
            }

            return result;
        }
    }

    /// <summary>A database table read through RPFM: its schema, and its records as data XML.</summary>
    public sealed class RpfmTable
    {
        public XmlSchema Schema { get; }
        public XDocument Rows   { get; }

        public RpfmTable(XmlSchema schema, XDocument rows)
        {
            Schema  = schema;
            Rows    = rows;
        }
    }
}
