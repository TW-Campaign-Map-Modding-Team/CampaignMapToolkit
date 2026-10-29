using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// The RPFM operations the database workflow needs, run in one rpfm_server session for one game:
    /// read db tables, either from the game's own packs or from a mod RPFM locates by name.
    /// It contains no workflow logic; callers interpret the results. Everything opened stays open
    /// until the service is disposed, which ends the session and releases it all.
    /// </summary>
    public sealed class RpfmService : IDisposable
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

        // Rows asked for in one table.rows call. RPFM sends 100 when not told, and takes any limit.
        private const int RowsPerPage = 10000;

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
        /// Reads every fragment of <paramref name="tables"/> found in the game's own packs, read from
        /// the install folder RPFM has for the game. A table the game has no fragment of is simply
        /// absent from the result.
        /// </summary>
        public IReadOnlyList<TableFragment> ReadGameTables(IReadOnlyList<string> tables)
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

            return ReadTables(new { pack = _gamePacksKey }, tables, $"{_game}'s packs");
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
        /// Reads every fragment of <paramref name="tables"/> found in the mods loaded by
        /// <see cref="LoadMod"/>. RPFM serves the mods as one set of files, in which a mod's own file
        /// replaces one at the same path in a mod it depends on; the result does not say which mod
        /// each fragment came from.
        /// </summary>
        public IReadOnlyList<TableFragment> ReadModTables(IReadOnlyList<string> tables)
        {
            if (_modHolderPackKey == null)
            {
                throw new InvalidOperationException("Load the mod before reading tables from it.");
            }

            return ReadTables("parent_files", tables, "the mods");
        }

        /// <summary>
        /// The columns RPFM's schema defines for <paramref name="table"/> (without the <c>_tables</c>
        /// suffix), in order: the version the game's files have, or the newest one.
        /// </summary>
        public IReadOnlyList<RpfmColumn> GetTableDefinition(string table)
        {
            return _server.Call<TableDefinition>($"read the schema of {table}", "schema.definition",
                new { table_name = $"{table}_tables" }, DefaultTimeout).Columns;
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        // Lists each table's folder, which RPFM filters by prefix itself, so the game's hundreds of
        // thousands of other files are never sent. A table the source does not have lists nothing.
        private IReadOnlyList<TableFragment> ReadTables(object source, IReadOnlyList<string> tables, string sourceDescription)
        {
            var fragments = new List<TableFragment>();

            foreach (var table in tables)
            {
                var operation = $"read {table} from {sourceDescription}";

                foreach (var path in ListFiles(source, $"db/{table}_tables/", operation))
                {
                    fragments.Add(ReadFragment(source, path, operation));
                }
            }

            return fragments;
        }

        private List<string> ListFiles(object source, string prefix, string operation)
        {
            var paths = new List<string>();

            while (true)
            {
                var page = _server.Call<FileList>(operation, "files.list",
                    new { source, path_prefix = prefix, offset = paths.Count }, DefaultTimeout);

                paths.AddRange(page.Files.Select(file => file.Path));

                if (page.Files.Count == 0 || paths.Count >= page.Total)
                {
                    return paths;
                }
            }
        }

        // RPFM decodes a fragment with the table version it was saved with, so its columns can be
        // fewer than the newest version's.
        private TableFragment ReadFragment(object source, string path, string operation)
        {
            var (table, fragmentName) = TableFragment.ParsePackedPath(path, operation);

            List<string> columns = null;
            var rows = new List<IReadOnlyList<string>>();

            while (true)
            {
                var page = _server.Call<TableRows>(operation, "table.rows",
                    new { file = new { source, path }, offset = rows.Count, limit = RowsPerPage }, DefaultTimeout);

                columns = columns ?? page.Columns;
                rows.AddRange(page.Rows.Select(row => (IReadOnlyList<string>)row.Values.Select(ToText).ToList()));

                if (page.Rows.Count == 0 || rows.Count >= page.Total)
                {
                    return new TableFragment(table, fragmentName, columns, rows);
                }
            }
        }

        // A value as the converter reads it: a boolean as true/false, and a float in the shortest
        // form that reads back as the same number, which is how RPFM sends an F32, so none is rounded.
        private static string ToText(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Null:       return string.Empty;
                case JTokenType.String:     return (string)value;
                case JTokenType.Boolean:    return (bool)value ? "true" : "false";
                case JTokenType.Float:      return ((double)value).ToString("R", CultureInfo.InvariantCulture);
                case JTokenType.Integer:    return ((JValue)value).ToString(CultureInfo.InvariantCulture);
                default:                    return value.ToString(Formatting.None);
            }
        }

        private sealed class TableDefinition
        {
            [JsonProperty("columns")]
            public List<RpfmColumn> Columns { get; set; } = new List<RpfmColumn>();
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

        private sealed class FileList
        {
            [JsonProperty("files")]
            public List<ListedFile> Files { get; set; } = new List<ListedFile>();

            [JsonProperty("total")]
            public int Total { get; set; }
        }

        private sealed class ListedFile
        {
            [JsonProperty("path")]
            public string Path { get; set; }
        }

        private sealed class TableRows
        {
            [JsonProperty("columns")]
            public List<string> Columns { get; set; } = new List<string>();

            [JsonProperty("rows")]
            public List<TableRow> Rows { get; set; } = new List<TableRow>();

            [JsonProperty("total")]
            public int Total { get; set; }
        }

        private sealed class TableRow
        {
            [JsonProperty("values")]
            public List<JToken> Values { get; set; } = new List<JToken>();
        }

        // Only the member LoadMod reads from dependencies.info; the vanilla and other file lists
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

    /// <summary>A column of a table as RPFM's schema defines it.</summary>
    public sealed class RpfmColumn
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The type of its values, like <c>StringU8</c>, <c>I32</c>, <c>F32</c>, <c>Boolean</c> or
        /// <c>ColourRGB</c>; an object for a sequence.
        /// </summary>
        [JsonProperty("field_type")]
        public Newtonsoft.Json.Linq.JToken FieldType { get; set; }

        [JsonProperty("is_key")]
        public bool IsKey { get; set; }

        [JsonProperty("default_value")]
        public string DefaultValue { get; set; }
    }

    /// <summary>One fragment of a db table, read through RPFM.</summary>
    public sealed class TableFragment
    {
        private const string TableFolderSuffix = "_tables";

        /// <summary>The table's name, e.g. <c>regions</c>.</summary>
        public string Table { get; }

        /// <summary>
        /// The fragment's file name inside its pack, e.g. <c>data__</c>. Decides which fragment wins
        /// when two define the same row, the same way the game decides it.
        /// </summary>
        public string FragmentName { get; }

        /// <summary>The fragment's columns, as the table version it was saved with has them.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// Each row's values in <see cref="Columns"/> order, as text: booleans as <c>true</c> and
        /// <c>false</c>, a colour as <c>RRGGBB</c>, and a float in the shortest form that reads back as it.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        public TableFragment(string table, string fragmentName, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            Table = table;
            FragmentName = fragmentName;
            Columns = columns;
            Rows = rows;
        }

        /// <summary>
        /// The table and fragment name of a fragment's path inside its pack,
        /// <c>db/&lt;table&gt;_tables/&lt;fragment&gt;</c>, as RPFM lists it. Throws
        /// <see cref="RpfmException"/>, naming <paramref name="operation"/>, for a path of any other shape.
        /// </summary>
        public static (string Table, string FragmentName) ParsePackedPath(string packedPath, string operation)
        {
            var parts = (packedPath ?? string.Empty).Split('/');

            if (parts.Length != 3
                || !string.Equals(parts[0], "db", StringComparison.OrdinalIgnoreCase)
                || parts[1].Length <= TableFolderSuffix.Length
                || !parts[1].EndsWith(TableFolderSuffix, StringComparison.OrdinalIgnoreCase)
                || parts[2].Length == 0)
            {
                throw new RpfmException(operation, $"RPFM listed '{packedPath}', which is not a db table fragment.");
            }

            return (parts[1].Substring(0, parts[1].Length - TableFolderSuffix.Length), parts[2]);
        }
    }
}
