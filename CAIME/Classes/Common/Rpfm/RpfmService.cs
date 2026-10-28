using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CAIME.Rpfm
{
    /// <summary>
    /// The RPFM operations the database workflow needs, run in one rpfm_server session for one game:
    /// extract db tables as TSV, either from the game's own packs or from a mod RPFM locates by name.
    /// It contains no workflow logic; callers interpret the results. Everything opened stays open
    /// until the service is disposed, which ends the session and releases it all.
    /// </summary>
    public sealed class RpfmService : IDisposable
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

        /// <summary>Ceiling for a batched extraction, however many tables it covers.</summary>
        private static readonly TimeSpan MaxBatchTimeout = TimeSpan.FromMinutes(15);

        // dependencies.info sends RPFM's whole vanilla file list along with the mods' files, hundreds
        // of thousands of entries for the newer games, which takes far longer than any other request.
        private static readonly TimeSpan LoadModTimeout = TimeSpan.FromMinutes(10);

        private readonly RpfmServerClient _server;
        private readonly GameTemplate _game;

        private string _gamePacksKey;
        private string _modHolderPackKey;

        private RpfmService(RpfmServerClient server, GameTemplate game)
        {
            _server = server;
            _game = game;
        }

        /// <summary>
        /// Starts an RPFM session for <paramref name="game"/>, starting the rpfm_server in
        /// <paramref name="rpfmFolder"/> if none is running. Selecting the game is what loads its schema,
        /// which RPFM needs to decode the tables. Throws <see cref="RpfmException"/> on failure,
        /// including when RPFM has no schema for the game or does not know where it is installed.
        /// </summary>
        public static RpfmService Open(string rpfmFolder, GameTemplate game)
        {
            var server = RpfmServerClient.Connect(rpfmFolder);

            try
            {
                var gameKey = GameMappingProvider.GetGameKey(game);

                // No dependency rebuild: LoadMod asks for the one it needs.
                var status = server.CallJob<SessionStatus>($"select {game}", "session.set_game",
                    new { game = gameKey, rebuild_dependencies = false }, DefaultTimeout);

                if (!status.SchemaLoaded)
                {
                    throw new RpfmException($"load the schema for {game}",
                        "RPFM has no schema for this game. Open the game in RPFM once so it downloads its schemas.");
                }

                return new RpfmService(server, game);
            }
            catch
            {
                server.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Validates that <paramref name="rpfmFolder"/> is a usable RPFM installation by opening a
        /// session on its rpfm_server. Returns true only when the server answers as RPFM. On failure
        /// <paramref name="error"/> explains why.
        /// </summary>
        public static bool ValidateInstallation(string rpfmFolder, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(rpfmFolder) || !Directory.Exists(rpfmFolder))
            {
                error = "The selected folder does not exist.";
                return false;
            }

            if (!File.Exists(Path.Combine(rpfmFolder, RpfmServerClient.ServerExecutableName)))
            {
                error = $"{RpfmServerClient.ServerExecutableName} was not found in the selected folder. " +
                        "CAIME needs RPFM 5.3 or later.";
                return false;
            }

            try
            {
                using (RpfmServerClient.Connect(rpfmFolder))
                {
                    return true;
                }
            }
            // Not only RpfmException: starting the server can fail in the OS, and validation reports every failure.
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Extracts every fragment of <paramref name="tables"/> found in the game's own packs, read from
        /// the install folder RPFM has for the game, as TSV into <paramref name="destinationFolder"/>.
        /// A table the game has no fragment of is simply absent from the result.
        /// </summary>
        public IReadOnlyList<ExtractedTableFragment> ExtractGameTables(IReadOnlyList<string> tables, string destinationFolder)
        {
            if (_gamePacksKey == null)
            {
                // Opened as one pack: which of the game's packs a fragment lives in does not matter here.
                // RPFM reads them from the game folder in its own settings, and without one only reports
                // that a path is missing.
                try
                {
                    _gamePacksKey = _server.Call<PackSummary>($"open {_game}'s packs", "pack.open_vanilla", null, DefaultTimeout).Key;
                }
                catch (RpfmException ex)
                {
                    throw new RpfmException($"open {_game}'s packs",
                        $"{ex.Message} Check that the game's folder is set in RPFM's settings.");
                }
            }

            return ExtractTables(new { pack = _gamePacksKey }, tables, destinationFolder, $"{_game}'s packs");
        }

        /// <summary>
        /// Loads the mod <paramref name="modPackName"/> (a <c>.pack</c> file name, matched exactly)
        /// together with every mod it depends on, and theirs in turn. RPFM looks for each in the
        /// game's data folder first, then its own secondary folder, then the Steam Workshop folder,
        /// taking the first it finds, so a local copy in data wins over the Workshop download. Returns
        /// the file names of every pack RPFM loaded that contains files; the named mod missing from
        /// the result was not found or is empty.
        /// </summary>
        public IReadOnlyCollection<string> LoadMod(string modPackName)
        {
            // RPFM loads mods by name only as the "parent" dependencies of an open pack, so an empty,
            // never-saved pack carries the name.
            _modHolderPackKey = _server.Call<PackSummary>("create a pack to load the mod through", "pack.new", null, DefaultTimeout).Key;

            _server.Call("set the mod to load", "pack.update",
                new { pack = _modHolderPackKey, dependencies = new[] { new { enabled = true, name = modPackName } } }, DefaultTimeout);

            // Only the parent packs: RPFM's cache of the game's own files is not needed.
            _server.CallJob<SessionStatus>($"load {modPackName}", "dependencies.rebuild", new { only_parent_packs = true }, LoadModTimeout);

            var loaded = _server.Call<LoadedDependencies>($"list the files of {modPackName}", "dependencies.info", null, LoadModTimeout);

            return new HashSet<string>(
                loaded.ParentPackedFiles.Select(file => file.ContainerName).Where(name => name != null),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts every fragment of <paramref name="tables"/> found in the mods loaded by
        /// <see cref="LoadMod"/>, as TSV into <paramref name="destinationFolder"/>. RPFM serves the
        /// mods as one set of files, in which a mod's own file replaces one at the same path in a mod
        /// it depends on; the result does not say which mod each fragment came from.
        /// </summary>
        public IReadOnlyList<ExtractedTableFragment> ExtractModTables(IReadOnlyList<string> tables, string destinationFolder)
        {
            if (_modHolderPackKey == null)
            {
                throw new InvalidOperationException("Load the mod before extracting tables from it.");
            }

            return ExtractTables("parent_files", tables, destinationFolder, "the mods");
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        // Asks for each table's whole folder rather than for files listed beforehand: RPFM skips a
        // folder it does not have, and listing the game's packs first would mean receiving every
        // file name in them.
        private IReadOnlyList<ExtractedTableFragment> ExtractTables(
            object source, IReadOnlyList<string> tables, string destinationFolder, string sourceDescription)
        {
            if (tables.Count == 0)
            {
                return Array.Empty<ExtractedTableFragment>();
            }

            // The budget scales with the batch so one call is not held to a single table's allowance.
            var timeout = TimeSpan.FromTicks(Math.Min(DefaultTimeout.Ticks * tables.Count, MaxBatchTimeout.Ticks));

            var operation = $"extract tables from {sourceDescription}";
            var extracted = _server.Call<FilesExtracted>(operation, "files.extract", new
            {
                source,
                paths       = tables.Select(table => $"db/{table}_tables").ToArray(),
                destination = destinationFolder,
                as_tsv      = true,
            }, timeout);

            return extracted.Extracted
                .Select(path => ExtractedTableFragment.FromExtractedPath(path, operation))
                .ToList();
        }

        private sealed class SessionStatus
        {
            [JsonProperty("schema_loaded")]
            public bool SchemaLoaded { get; set; }
        }

        private sealed class PackSummary
        {
            [JsonProperty("key")]
            public string Key { get; set; }
        }

        private sealed class FilesExtracted
        {
            [JsonProperty("extracted")]
            public List<string> Extracted { get; set; } = new List<string>();
        }

        // Only the member LoadMod reads from dependencies.info; the vanilla and Assembly Kit file lists
        // that arrive with it are skipped unread rather than built in memory.
        private sealed class LoadedDependencies
        {
            [JsonProperty("parent_packed_files")]
            public List<PackedFileInfo> ParentPackedFiles { get; set; } = new List<PackedFileInfo>();
        }

        private sealed class PackedFileInfo
        {
            [JsonProperty("container_name")]
            public string ContainerName { get; set; }
        }
    }

    /// <summary>One table fragment RPFM extracted to disk as TSV.</summary>
    public sealed class ExtractedTableFragment
    {
        private const string TableFolderSuffix = "_tables";

        /// <summary>The table's name, e.g. <c>regions</c>.</summary>
        public string Table { get; }

        /// <summary>
        /// The fragment's file name inside its pack, e.g. <c>data__</c>. Decides which fragment wins
        /// when two define the same row, the same way the game decides it.
        /// </summary>
        public string FragmentName { get; }

        public string TsvPath { get; }

        private ExtractedTableFragment(string table, string fragmentName, string tsvPath)
        {
            Table = table;
            FragmentName = fragmentName;
            TsvPath = tsvPath;
        }

        /// <summary>
        /// Reads a path RPFM reports having written, which mirrors the fragment's place in its pack:
        /// <c>&lt;destination&gt;/db/&lt;table&gt;_tables/&lt;fragment&gt;.tsv</c>, with forward and back
        /// slashes mixed. Throws <see cref="RpfmException"/>, naming <paramref name="operation"/>, for a
        /// path of any other shape, such as a fragment RPFM could not write out as TSV.
        /// </summary>
        public static ExtractedTableFragment FromExtractedPath(string extractedPath, string operation)
        {
            var path = (extractedPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            var tableFolder = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
            var dbFolder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));

            if (!path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                || !tableFolder.EndsWith(TableFolderSuffix, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(dbFolder, "db", StringComparison.OrdinalIgnoreCase))
            {
                throw new RpfmException(operation, $"RPFM wrote '{extractedPath}', which is not a db table exported as TSV.");
            }

            var table = tableFolder.Substring(0, tableFolder.Length - TableFolderSuffix.Length);
            return new ExtractedTableFragment(table, Path.GetFileNameWithoutExtension(path), path);
        }
    }
}
