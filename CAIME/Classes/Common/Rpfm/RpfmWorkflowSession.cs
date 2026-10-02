using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Owns one run of the RPFM database preparation pipeline as a single transaction. For each
    /// required table it gathers every fragment of that table found anywhere - the game's own packs
    /// and the project's mod with the mods it depends on - and merges them the same way the game itself does: when two
    /// fragments define a row for the same primary key, the fragment whose name sorts earlier wins;
    /// rows unique to any one fragment are all kept. When a mod and the game share the exact same
    /// fragment name too (so fragment name breaks no tie), the mod wins, since its fragments are
    /// read first. It then backs up the original Assembly Kit db files, converts the merged tables to
    /// Assembly Kit XML, and - crucially - guarantees that the Assembly Kit is left exactly as it was
    /// found once the session is cleaned up (whether the project closes, another opens, the app
    /// exits, or any step fails).
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
    ///
    /// Usage:
    ///   var session = new RpfmWorkflowSession(...);
    ///   session.Prepare();          // throws on failure, having already rolled everything back
    ///   ... existing DB loading ...
    ///   session.Cleanup();          // restores originals, removes generated files
    /// </summary>
    public sealed class RpfmWorkflowSession : IDisposable
    {
        // Best-effort crash recovery: any session that has begun mutating the Assembly Kit registers
        // here so a process exit / unhandled exception can still restore the originals.
        private static readonly HashSet<RpfmWorkflowSession> ActiveSessions = new HashSet<RpfmWorkflowSession>();
        private static readonly object ActiveSessionsLock = new object();

        static RpfmWorkflowSession()
        {
            AppDomain.CurrentDomain.ProcessExit       += (s, e) => CleanupAll();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => CleanupAll();
        }

        private readonly GameTemplate           _game;
        private readonly string                 _dbRootPath;
        private readonly string                 _modPackName;
        private readonly string                 _rpfmFolder;
        private readonly IReadOnlyDictionary<string, bool> _regionIsSeaByKey;
        private readonly BackupService           _backup;
        private readonly string                 _tempExtractDir;
        private readonly List<string>            _generatedFiles = new List<string>();
        private readonly RpfmRecoveryJournal     _journal;

        private bool _cleanedUp;

        /// <param name="modPackName">
        /// The .pack file name of the project's mod. Optional - pass null when the project has none
        /// configured; every table then comes from the game's own packs instead.
        /// </param>
        /// <param name="mapHex">
        /// The project's already-loaded map, used only as a ground-truth source for "regions.is_sea"
        /// (see <see cref="BuildRegionSeaStatus"/>). Optional - pass null when there is no map; that
        /// field then falls back to the existing Assembly Kit record or the schema default same as any
        /// other field RPFM cannot supply.
        /// </param>
        public RpfmWorkflowSession(GameTemplate game, string assemblyKitPath, string rpfmFolder, string modPackName, MapHexFile mapHex = null)
        {
            _game            = game;
            _dbRootPath      = Path.Combine(assemblyKitPath, "raw_data", "db");
            _modPackName     = modPackName;
            _rpfmFolder      = rpfmFolder;
            _regionIsSeaByKey = BuildRegionSeaStatus(mapHex);

            // Both working directories are private to this session and sit outside the db root.
            // The old one-second-resolution timestamps inside the db root meant two sessions
            // started in the same second shared them: the second session's File.Move into an
            // occupied path threw, and either session's cleanup destroyed the other's originals.
            var sessionId    = Guid.NewGuid().ToString("N");
            var sessionRoot  = Path.Combine(assemblyKitPath, "caime_rpfm", sessionId);

            _backup          = new BackupService(_dbRootPath, Path.Combine(sessionRoot, "backup"));
            _tempExtractDir  = Path.Combine(sessionRoot, "extract");

            // Persisted record used to recover if the process is killed before cleanup can run.
            _journal = new RpfmRecoveryJournal
            {
                SessionId      = sessionId,
                DbRootPath     = _dbRootPath,
                BackupRootPath = _backup.BackupRootPath,
                TempExtractDir = _tempExtractDir,
            };
        }

        /// <summary>
        /// Runs the full preparation pipeline. On success the Assembly Kit db folder contains the
        /// resolved tables - each one merged from the game's packs and every mod that contains a
        /// fragment of it - as Assembly Kit XML, ready for the normal loader. On any failure the
        /// Assembly Kit is fully restored and a descriptive exception is thrown.
        /// </summary>
        public void Prepare()
        {
            ValidatePreconditions();

            var requiredTables = DatabaseTableProvider.GetRequiredTables(_game);

            using (var rpfm = RpfmService.Open(_rpfmFolder, _game))
            {
                // From here on files are written; any failure must roll everything back.
                try
                {
                    // Write the recovery journal before touching any file, so a hard kill at any point
                    // from here on leaves a record the next launch can replay.
                    _journal.Save();

                    // Table presence is a soft requirement: only the required tables found in a mod or
                    // the game take part in the workflow. Tables absent from both are left as-is, so the
                    // normal loader falls back to the Assembly Kit's own copy. Finding nothing at all is
                    // valid too - the workflow simply does nothing.
                    var fragmentsByTable = ExtractFragments(rpfm, requiredTables);
                    var presentTables = requiredTables.Where(fragmentsByTable.ContainsKey).ToList();

                    BackupOriginals(presentTables);
                    EnsureNoConflicts(presentTables);
                    ConvertToAssemblyKitXml(presentTables, fragmentsByTable);

                    // Temporary TSVs are no longer needed once converted.
                    DeleteTempExtractDir();

                    Register(this);
                }
                catch
                {
                    RestoreAndCleanup();
                    throw;
                }
            }
        }

        /// <summary>
        /// Restores the original Assembly Kit files, removes generated XML and any temporary files,
        /// and deletes the backup directory. Idempotent.
        /// </summary>
        public void Cleanup()
        {
            RestoreAndCleanup();
        }

        public void Dispose()
        {
            RestoreAndCleanup();
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
        // aborts the workflow; a failure reading the mods only drops them.
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

            var stillMissing = requiredTables.Where(t => !fragmentsByTable.ContainsKey(t)).ToList();
            if (fragmentsByTable.Count == 0)
            {
                LoggerViewModel.Log(
                    "RPFM workflow: none of the required database tables were found in the mods or the game's packs. " +
                    "The Assembly Kit's own tables will be used unchanged.", LogLevel.Info);
            }
            else if (stillMissing.Count > 0)
            {
                LoggerViewModel.Log(
                    "RPFM workflow: these tables were not found in the mods or the game's packs and will be loaded " +
                    "from the Assembly Kit: " + string.Join(", ", stillMissing), LogLevel.Info);
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

        // Move the original data XML for each table being replaced into the backup directory. Only
        // tables found in a mod or the game are backed up; everything else stays untouched.
        private void BackupOriginals(IReadOnlyList<string> tablesToReplace)
        {
            foreach (var table in tablesToReplace)
            {
                _backup.Backup(Path.Combine(_dbRootPath, table + ".xml"));
            }
        }

        // Abort if a conflicting file for any table already exists, regardless of extension. After the
        // backup moved the originals aside, the only way a match survives is a stray leftover from an
        // earlier interrupted run.
        private void EnsureNoConflicts(IReadOnlyList<string> tables)
        {
            foreach (var table in tables)
            {
                var conflicts = Directory.GetFiles(_dbRootPath, table + ".*", SearchOption.TopDirectoryOnly);
                if (conflicts.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"A conflicting file already exists in the Assembly Kit db folder: {conflicts[0]}");
                }
            }
        }

        // Merges each present table's extracted fragments into Assembly Kit XML.
        private void ConvertToAssemblyKitXml(IReadOnlyList<string> tables, Dictionary<string, List<ExtractedTableFragment>> fragmentsByTable)
        {
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
                var schemaFields      = schema.Fields;

                // BackupOriginals already moved this table's pre-existing Assembly Kit XML here (if it
                // had one) before this method runs - read it back as a fallback source for fields RPFM
                // has no way to supply (see MergeTsvToXml).
                var backedUpXmlPath = Path.Combine(_backup.BackupRootPath, table + ".xml");
                var existingRecords = DatabaseTableConverter.LoadExistingRecords(backedUpXmlPath, table, primaryKeyColumns);

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

                var outputXml = Path.Combine(_dbRootPath, table + ".xml");
                DatabaseTableConverter.MergeTsvToXml(fragments, table, booleanColumns, primaryKeyColumns, schemaFields, existingRecords, _regionIsSeaByKey, outputXml);
                _generatedFiles.Add(outputXml);

                // Record the generated file only now that it exists and its original is safely backed
                // up, so crash-recovery can delete it without risking an untouched original.
                _journal.GeneratedFiles.Add(outputXml);
                _journal.Save();
            }
        }

        // Ground truth for "regions.is_sea": which regions this map treats as sea. That field never
        // appears in any pack's raw table data at any version - the Assembly Kit computes it itself -
        // and the Assembly Kit's own regions.xml can be arbitrarily stale (it will not know a region
        // added since the last export at all), so the open map is the only source that is right by
        // construction. It is already parsed and in memory by the time a session is built: Project.Open
        // loads the map first and only then runs the hook that prepares this workflow.
        private static IReadOnlyDictionary<string, bool> BuildRegionSeaStatus(MapHexFile mapHex)
        {
            // Absent regions are "unknown to this map" rather than "land", so callers fall back to
            // another source for them instead of being told something wrong.
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var region in mapHex?.LandRegions ?? Enumerable.Empty<string>())
            {
                result[region] = false;
            }

            foreach (var region in mapHex?.SeaRegions ?? Enumerable.Empty<string>())
            {
                result[region] = true;
            }

            return result;
        }

        // -----------------------------------------------------------------
        // Teardown / rollback (shared by success cleanup and failure rollback)
        // -----------------------------------------------------------------

        private void RestoreAndCleanup()
        {
            if (_cleanedUp)
            {
                return;
            }

            _cleanedUp = true;
            Unregister(this);

            // Remove generated XML first, then move the originals back over the top.
            foreach (var generated in _generatedFiles)
            {
                TryDeleteFile(generated);
            }
            _generatedFiles.Clear();

            DeleteTempExtractDir();

            try
            {
                _backup.Restore();
                _backup.DeleteBackupDirectory();
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM cleanup - failed to restore Assembly Kit backup: {ex.Message}", LogLevel.Error);
            }

            // The Assembly Kit is restored - the recovery journal is no longer needed. Deleted last so
            // that a crash at any earlier point still leaves it for the next launch to replay.
            _journal.Delete();
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
                LoggerViewModel.Log($"RPFM cleanup - failed to delete temporary extraction folder: {ex.Message}", LogLevel.Warning);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM cleanup - failed to delete generated file {path}: {ex.Message}", LogLevel.Warning);
            }
        }

        // -----------------------------------------------------------------
        // Active-session registry for crash recovery
        // -----------------------------------------------------------------

        private static void Register(RpfmWorkflowSession session)
        {
            lock (ActiveSessionsLock)
            {
                ActiveSessions.Add(session);
            }
        }

        private static void Unregister(RpfmWorkflowSession session)
        {
            lock (ActiveSessionsLock)
            {
                ActiveSessions.Remove(session);
            }
        }

        private static void CleanupAll()
        {
            RpfmWorkflowSession[] snapshot;
            lock (ActiveSessionsLock)
            {
                snapshot = ActiveSessions.ToArray();
            }

            foreach (var session in snapshot)
            {
                try { session.RestoreAndCleanup(); } catch { /* best effort during shutdown */ }
            }
        }
    }
}
