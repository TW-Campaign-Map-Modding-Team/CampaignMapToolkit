using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Reads the database tables a project needs through rpfm_server, as Assembly Kit data XML held
    /// in memory. For each required table it gathers every fragment of that table found anywhere - the
    /// game's own packs and the project's mod with the mods it depends on - and merges them the same
    /// way the game itself does: when two fragments define a row for the same primary key, the
    /// fragment whose name sorts earlier wins; rows unique to any one fragment are all kept. When a mod
    /// and the game share the exact same fragment name too (so fragment name breaks no tie), the mod
    /// wins, since its fragments are read first.
    ///
    /// Nothing is written to the Assembly Kit. Its TWaD schemas give each table's fields, and its own
    /// copy of each table is read for the fields no pack carries (see
    /// <see cref="DatabaseTableConverter.MergeTsv"/>). RPFM extracts the fragments as TSV into a
    /// temporary folder of CAIME's own, deleted before <see cref="Prepare"/> returns.
    ///
    /// Everything is read through rpfm_server (see <see cref="RpfmService"/>), one server session for
    /// the length of <see cref="Prepare"/>. The game's own packs are found through the install folder
    /// set in RPFM, so CAIME never needs to know which of them holds the tables - that has changed
    /// between games and even within one game's lifetime. The mod is optional and named, not
    /// located: RPFM finds it, and the mods it depends on, in the game's data folder or its Steam
    /// Workshop folder. A project with none simply reads every table from the game, and a mod RPFM
    /// cannot find or read is skipped with a warning rather than blocking the session - it's a
    /// per-project convenience layered on top, not something that should be able to prevent a
    /// project from opening.
    /// </summary>
    public sealed class RpfmWorkflowSession
    {
        private readonly GameTemplate           _game;
        private readonly string                 _dbRootPath;
        private readonly string                 _modPackName;
        private readonly string                 _rpfmFolder;
        private readonly string                 _campaignMapName;
        private readonly IReadOnlyDictionary<string, bool> _regionIsSeaByKey;
        private readonly string                 _tempExtractDir;

        /// <param name="modPackName">
        /// The .pack file name of the project's mod. Optional - pass null when the project has none
        /// configured; every table then comes from the game's own packs instead.
        /// </param>
        /// <param name="mapHex">
        /// The project's already-loaded map. Its campaign map name decides which regions the region
        /// tables keep, and its land and sea regions are the source of "regions.is_sea" (see
        /// <see cref="RestrictRegionsToThisMap"/> and <see cref="BuildRegionSeaStatus"/>).
        /// </param>
        public RpfmWorkflowSession(GameTemplate game, string assemblyKitPath, string rpfmFolder, string modPackName, MapHexFile mapHex)
        {
            if (mapHex == null)
            {
                throw new ArgumentNullException(nameof(mapHex));
            }

            _game            = game;
            _dbRootPath      = Path.Combine(assemblyKitPath, "raw_data", "db");
            _modPackName     = modPackName;
            _rpfmFolder      = rpfmFolder;
            _campaignMapName = mapHex.CampaignMapName;
            _regionIsSeaByKey = BuildRegionSeaStatus(mapHex);
            _tempExtractDir  = Path.Combine(Path.GetTempPath(), "CAIME", "rpfm", Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Reads every required table, each merged from the game's packs and every mod that contains
        /// a fragment of it, keyed by table name. Throws a descriptive exception on any failure.
        /// </summary>
        public IReadOnlyDictionary<string, XDocument> Prepare()
        {
            ValidatePreconditions();

            var requiredTables = DatabaseTableProvider.GetRequiredTables(_game);

            try
            {
                using (var rpfm = RpfmService.Open(_rpfmFolder, _game))
                {
                    var fragmentsByTable = ExtractFragments(rpfm, requiredTables);
                    var rowFilters = RestrictRegionsToThisMap(fragmentsByTable);

                    return MergeTables(requiredTables, fragmentsByTable, rowFilters);
                }
            }
            finally
            {
                DeleteTempExtractDir();
            }
        }

        // -----------------------------------------------------------------
        // Pipeline steps
        // -----------------------------------------------------------------

        private void ValidatePreconditions()
        {
            if (!GameMappingProvider.IsSupported(_game))
            {
                throw new NotSupportedException($"The RPFM workflow does not support {_game}.");
            }

            if (!Directory.Exists(_dbRootPath))
            {
                throw new DirectoryNotFoundException($"Assembly Kit db folder not found: {_dbRootPath}");
            }

            // Everything about RPFM itself - its schema for the game, where the game is installed - is
            // checked by RpfmService.Open, since only the server knows it.
        }

        // Extracts every fragment of each required table into the temporary folder, the mods' first,
        // then the game's, and groups them by table in that order. The order is what makes a mod win
        // over the game when both use the exact same fragment name (see the class summary). The mods
        // and the game land in separate folders because their fragments can share a path - every
        // pack's default "db/<table>_tables/data__", say. A failure reading the game propagates and
        // aborts the workflow; a failure reading the mods only drops them. Every required table must
        // turn up in one or the other: the Assembly Kit's own copy is never a stand-in for game data.
        private Dictionary<string, List<ExtractedTableFragment>> ExtractFragments(RpfmService rpfm, IReadOnlyList<string> requiredTables)
        {
            var fragmentsByTable = new Dictionary<string, List<ExtractedTableFragment>>(StringComparer.OrdinalIgnoreCase);

            var fromMods = ExtractModFragments(rpfm, requiredTables);
            var fromGame = rpfm.ExtractGameTables(requiredTables, Path.Combine(_tempExtractDir, "game"));

            foreach (var fragment in fromMods.Concat(fromGame))
            {
                if (!fragmentsByTable.TryGetValue(fragment.Table, out var fragments))
                {
                    fragments = new List<ExtractedTableFragment>();
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

        // The mod is optional and per-project - none configured at all is a normal state, and one
        // RPFM cannot find or read shouldn't be able to block the session either, so both just leave
        // the mods out rather than throwing.
        private IReadOnlyList<ExtractedTableFragment> ExtractModFragments(RpfmService rpfm, IReadOnlyList<string> requiredTables)
        {
            if (string.IsNullOrEmpty(_modPackName))
            {
                return Array.Empty<ExtractedTableFragment>();
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
                    return Array.Empty<ExtractedTableFragment>();
                }

                var dependencies = loaded.Where(name => !string.Equals(name, _modPackName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (dependencies.Count > 0)
                {
                    LoggerViewModel.Log(
                        $"RPFM workflow: also reading the mods {_modPackName} depends on: " + string.Join(", ", dependencies), LogLevel.Info);
                }

                return rpfm.ExtractModTables(requiredTables, Path.Combine(_tempExtractDir, "mods"));
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM workflow: could not read the mods - {ex.Message}. Skipping them.", LogLevel.Warning);
                return Array.Empty<ExtractedTableFragment>();
            }
        }

        private static void LogTableSources(string source, IReadOnlyList<ExtractedTableFragment> fragments)
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
        private IReadOnlyDictionary<string, TsvRowFilter> RestrictRegionsToThisMap(Dictionary<string, List<ExtractedTableFragment>> fragmentsByTable)
        {
            var regionsOnThisMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fragment in fragmentsByTable[Constants.TABLE_CAMPAIGN_MAP_REGIONS])
            {
                foreach (var row in DatabaseTableConverter.ReadTsvRows(fragment.TsvPath))
                {
                    if (!row.TryGetValue("campaign_map", out var campaignMap) || !row.TryGetValue("region", out var region))
                    {
                        throw new InvalidDataException($"'{fragment.TsvPath}' has no campaign_map and region columns.");
                    }

                    if (string.Equals(campaignMap, _campaignMapName, StringComparison.OrdinalIgnoreCase))
                    {
                        regionsOnThisMap.Add(region);
                    }
                }
            }

            return new Dictionary<string, TsvRowFilter>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.TABLE_CAMPAIGN_MAP_REGIONS] = new TsvRowFilter("campaign_map", new[] { _campaignMapName }),
                [Constants.TABLE_REGIONS]              = new TsvRowFilter("key", regionsOnThisMap),
                [Constants.TABLE_REGIONS_TO_PROVINCES] = new TsvRowFilter("region", regionsOnThisMap),
            };
        }

        // Merges each table's extracted fragments into an Assembly Kit data XML document, keeping only
        // the rows rowFilters selects for the tables it covers.
        private Dictionary<string, XDocument> MergeTables(
            IReadOnlyList<string> tables, Dictionary<string, List<ExtractedTableFragment>> fragmentsByTable,
            IReadOnlyDictionary<string, TsvRowFilter> rowFilters)
        {
            var merged = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in tables)
            {
                var twadSchemaPath = Path.Combine(_dbRootPath, "TWaD_" + table + ".xml");
                if (!File.Exists(twadSchemaPath))
                {
                    throw new FileNotFoundException(
                        $"Assembly Kit schema file missing: {twadSchemaPath}. The Assembly Kit installation looks incomplete.");
                }

                // One parse of the schema file, not three.
                var schema            = DatabaseTableConverter.LoadSchema(twadSchemaPath);
                var booleanColumns    = DatabaseTableConverter.GetBooleanColumns(schema);
                var primaryKeyColumns = DatabaseTableConverter.GetPrimaryKeyColumns(schema);

                var existingRecords = DatabaseTableConverter.LoadExistingRecords(Path.Combine(_dbRootPath, table + ".xml"), table, primaryKeyColumns);

                var fragments = new List<(string TsvPath, string FragmentName)>();
                foreach (var fragment in fragmentsByTable[table])
                {
                    if (!File.Exists(fragment.TsvPath))
                    {
                        throw new FileNotFoundException($"RPFM extraction produced no file at '{fragment.TsvPath}'.");
                    }

                    // The fragment's own filename decides merge order when two sources define the same
                    // primary key - the same rule the game uses to combine table fragments across packs.
                    fragments.Add((fragment.TsvPath, fragment.FragmentName));
                }

                rowFilters.TryGetValue(table, out var rowFilter);
                merged[table] = DatabaseTableConverter.MergeTsv(
                    fragments, table, booleanColumns, primaryKeyColumns, schema.Fields, existingRecords, _regionIsSeaByKey, rowFilter);
            }

            return merged;
        }

        // Ground truth for "regions.is_sea": which regions this map treats as sea. That field never
        // appears in any pack's raw table data at any version - the Assembly Kit computes it itself -
        // and the Assembly Kit's own regions.xml can be arbitrarily stale (it will not know a region
        // added since the last export at all), so the open map is the only source that is right by
        // construction. It is already parsed and in memory by the time a session is built: Project.Open
        // loads the map first and only then runs the hook that prepares this workflow.
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

        private void DeleteTempExtractDir()
        {
            try
            {
                if (Directory.Exists(_tempExtractDir))
                {
                    Directory.Delete(_tempExtractDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM workflow - failed to delete temporary extraction folder: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
