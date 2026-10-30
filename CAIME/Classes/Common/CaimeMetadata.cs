using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    public class CaimeMetadata
    {
        public const string FILENAME = "caime_metadata.json";

        // Bump this whenever the schema below changes, and add a migration
        // to the Migrations table that upgrades the previous version's JObject
        // to the new schema.
        public const int CURRENT_VERSION = 4;

        [JsonProperty("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        [JsonProperty("map_data_config_path")]
        public string MapDataConfigPath { get; set; }

        [JsonProperty("campaign_map_name")]
        public string CampaignMapName { get; set; }

        // File name of the mod .pack the RPFM database source workflow reads this campaign from, or
        // null for none. A name, not a path: RPFM finds it in the game's data folder or its Steam
        // Workshop folder, and loads the mods it depends on along with it, so one name is all a
        // project needs. Independent of the map_data config fields above - do not overwrite one when
        // writing the other (see MetadataService).
        [JsonProperty("mod_pack_name")]
        public string ModPackName { get; set; }

        // Converts the JObject read from disk one version forward. Keyed by
        // the version being migrated FROM. Add an entry here (and bump
        // CURRENT_VERSION) whenever the schema changes; leave older
        // migrations untouched so files written by older CAIME versions
        // keep loading correctly.
        private static readonly Dictionary<int, Action<JObject>> Migrations = new Dictionary<int, Action<JObject>>
        {
            // Version 0 (no "version" field, pre-2026-07 CAIME builds) -> 1: added the
            // explicit "version" field. No other fields changed.
            [0] = root => { },

            // Version 1 -> 2: added the optional "pack_file_path" field for the RPFM database
            // source workflow. Absent in v1 files; left null, which is the correct default.
            [1] = root => { },

            // Version 2 -> 3: replaced the single "pack_file_path" string with a "pack_file_paths"
            // list, so a project can layer more than one modded pack. Carries the old value forward
            // as the sole (highest-priority) entry rather than discarding it.
            [2] = root =>
            {
                var oldPath = root["pack_file_path"]?.Value<string>();
                root.Remove("pack_file_path");
                root["pack_file_paths"] = string.IsNullOrEmpty(oldPath)
                    ? new JArray()
                    : new JArray(oldPath);
            },

            // Version 3 -> 4: replaced the "pack_file_paths" list with a single "mod_pack_name",
            // since RPFM now locates the mod itself and loads its dependencies with it. Keeps the
            // first old pack's file name - the list was stored sorted by file name - and logs the rest,
            // which still load if that mod lists them as dependencies.
            [3] = root =>
            {
                var oldNames = (root["pack_file_paths"]?.Values<string>() ?? Enumerable.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(Path.GetFileName)
                    .ToList();
                root.Remove("pack_file_paths");
                root["mod_pack_name"] = oldNames.FirstOrDefault();

                if (oldNames.Count > 1)
                {
                    LoggerViewModel.Log(
                        $"This project listed several RPFM mod packs; CAIME now keeps one and loads its dependencies with it. " +
                        $"Kept {oldNames[0]}; set the mod that depends on the others via Settings > RPFM Workflow if " +
                        $"{string.Join(", ", oldNames.Skip(1))} should be read too.", LogLevel.Warning);
                }
            },
        };

        public static CaimeMetadata Load(string path)
        {
            var json = File.ReadAllText(path);
            var root = JObject.Parse(json);
            var fileVersion = root["version"]?.Value<int>() ?? 0;

            if (fileVersion > CURRENT_VERSION)
            {
                throw new NotSupportedException($"{FILENAME} is version {fileVersion}, which is newer than this version of CAIME supports (max supported version is {CURRENT_VERSION}). Please update CAIME.");
            }

            for (var v = fileVersion; v < CURRENT_VERSION; ++v)
            {
                if (!Migrations.TryGetValue(v, out var migrate))
                {
                    throw new NotSupportedException($"{FILENAME} - no migration available from version {v} to {v + 1}.");
                }

                migrate(root);
            }

            root["version"] = CURRENT_VERSION;

            return root.ToObject<CaimeMetadata>();
        }

        public void Save(string path)
        {
            Version = CURRENT_VERSION;
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
