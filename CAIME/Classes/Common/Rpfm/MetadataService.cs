using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Reads and writes <c>caime_metadata.json</c> beside a project's map.hex. All writes are
    /// read-modify-write: a caller that updates one field (e.g. the RPFM mod pack name) never clobbers
    /// unrelated fields (e.g. the map_data config path written by the Map Data Editor). This is
    /// what keeps the metadata file decoupled from any single feature that touches it.
    /// </summary>
    public static class MetadataService
    {
        /// <summary>Full path to the metadata file for a project located at <paramref name="projectPath"/>.</summary>
        public static string GetMetadataPath(string projectPath)
        {
            return Path.Combine(projectPath, CaimeMetadata.FILENAME);
        }

        /// <summary>
        /// Loads the metadata for the project, or null when the file does not exist. Throws only on
        /// genuine read/parse failures so callers can distinguish "no metadata yet" from "corrupt".
        /// </summary>
        public static CaimeMetadata Load(string projectPath)
        {
            var path = GetMetadataPath(projectPath);
            if (!File.Exists(path))
            {
                return null;
            }

            return CaimeMetadata.Load(path);
        }

        /// <summary>
        /// Applies <paramref name="mutate"/> to the existing metadata (or a fresh instance when none
        /// exists) and writes the result back. Existing fields not touched by the mutation are preserved.
        /// </summary>
        public static void Update(string projectPath, Action<CaimeMetadata> mutate)
        {
            if (mutate == null)
            {
                throw new ArgumentNullException(nameof(mutate));
            }

            var path = GetMetadataPath(projectPath);

            CaimeMetadata metadata = null;
            if (File.Exists(path))
            {
                metadata = CaimeMetadata.Load(path);
            }

            if (metadata == null)
            {
                metadata = new CaimeMetadata();
            }

            mutate(metadata);
            metadata.Save(path);
        }

        /// <summary>
        /// Returns the stored mod pack name, or null when unset / no metadata file.
        /// </summary>
        public static string GetModPackName(string projectPath)
        {
            try
            {
                return NormalizeModPackName(Load(projectPath)?.ModPackName);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"MetadataService - failed to read the mod pack name: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        /// <summary>
        /// Stores the mod pack name (normalized, see <see cref="NormalizeModPackName"/>; blank clears
        /// it), preserving all other metadata fields.
        /// </summary>
        public static void SetModPackName(string projectPath, string modPackName)
        {
            var normalized = NormalizeModPackName(modPackName);
            Update(projectPath, m => m.ModPackName = normalized);
        }

        /// <summary>
        /// The name RPFM looks a mod up by: the pack's file name with its <c>.pack</c> extension,
        /// whether given a bare name ("my_mod"), a file name, or a full path. Null for blank input.
        /// Case is kept as given: RPFM compares the name to file names exactly.
        /// </summary>
        public static string NormalizeModPackName(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                return null;
            }

            var name = Path.GetFileName(nameOrPath.Trim());
            return name.EndsWith(".pack", StringComparison.OrdinalIgnoreCase) ? name : name + ".pack";
        }
    }
}
