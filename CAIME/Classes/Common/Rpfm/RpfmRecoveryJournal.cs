using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CAIME.Rpfm
{
    /// <summary>
    /// The crash-recovery record an RPFM database session of an earlier CAIME version kept while it
    /// had the Assembly Kit's db files swapped for its own: those versions moved each original into a
    /// backup folder and wrote their merged tables in its place, restoring the originals when the
    /// project closed. A session killed hard (End Task, an external kill, Visual Studio's "Stop
    /// Debugging") never got to, and left its journal behind. CAIME no longer writes to the Assembly
    /// Kit, so it writes no journals, but <see cref="RecoverAll"/> still restores the Assembly Kit from
    /// any such leftover.
    /// </summary>
    public sealed class RpfmRecoveryJournal
    {
        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("db_root_path")]
        public string DbRootPath { get; set; }

        [JsonProperty("backup_root_path")]
        public string BackupRootPath { get; set; }

        [JsonProperty("temp_extract_dir")]
        public string TempExtractDir { get; set; }

        // Files written into the Assembly Kit db root by this session. Only files that have actually
        // been generated are listed (each is added after its original was safely backed up), so
        // deleting them during recovery can never remove an untouched original.
        [JsonProperty("generated_files")]
        public List<string> GeneratedFiles { get; set; } = new List<string>();

        /// <summary>Directory where session journals live: <c>%AppData%\CampaignMapToolkit\Caime\rpfm_recovery</c>.</summary>
        public static string RecoveryDirectory
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "CampaignMapToolkit", "Caime", "rpfm_recovery");
            }
        }

        /// <summary>
        /// Replays every leftover journal found on disk, restoring each interrupted session's Assembly
        /// Kit changes and removing its temporary artefacts. Call once at application startup, before any
        /// project can be opened. Never throws - recovery failures are logged and skipped.
        /// </summary>
        public static void RecoverAll()
        {
            try
            {
                if (!Directory.Exists(RecoveryDirectory))
                {
                    return;
                }

                var journalFiles = Directory.GetFiles(RecoveryDirectory, "*.json");
                if (journalFiles.Length == 0)
                {
                    return;
                }

                LoggerViewModel.Log(
                    $"RPFM recovery: found {journalFiles.Length} interrupted session(s) from a previous run. Restoring the Assembly Kit...",
                    LogLevel.Warning);

                foreach (var journalFile in journalFiles)
                {
                    Recover(journalFile);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM recovery: unexpected error while scanning for interrupted sessions - {ex.Message}", LogLevel.Error);
            }
        }

        private static void Recover(string journalFile)
        {
            RpfmRecoveryJournal journal;
            try
            {
                journal = JsonConvert.DeserializeObject<RpfmRecoveryJournal>(File.ReadAllText(journalFile));
            }
            catch (Exception ex)
            {
                // A journal we cannot parse is worse than useless - remove it so it does not block future launches.
                LoggerViewModel.Log($"RPFM recovery: could not read journal {journalFile} - {ex.Message}. Removing it.", LogLevel.Error);
                TryDelete(journalFile);
                return;
            }

            if (journal == null || string.IsNullOrEmpty(journal.DbRootPath))
            {
                TryDelete(journalFile);
                return;
            }

            try
            {
                // 1. Remove the files this session generated. They are only listed once their originals
                //    were backed up, so this never deletes an untouched original.
                if (journal.GeneratedFiles != null)
                {
                    foreach (var generated in journal.GeneratedFiles)
                    {
                        TryDelete(generated);
                    }
                }

                // 2. Move the original Assembly Kit files back (the backup directory is authoritative).
                RestoreDirectory(journal.DbRootPath, journal.BackupRootPath);

                // 3. Clean up leftover working directories.
                TryDeleteDirectory(journal.TempExtractDir);
                TryDeleteDirectory(journal.BackupRootPath);

                LoggerViewModel.Log($"RPFM recovery: restored Assembly Kit for session {journal.SessionId}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM recovery: failed to fully restore session {journal.SessionId} - {ex.Message}", LogLevel.Error);
                // Leave the journal in place so recovery can be retried on a later launch.
                return;
            }

            TryDelete(journalFile);
        }

        // Moves every file found under backupRootPath back to the matching location under dbRootPath,
        // preserving directory structure and overwriting whatever is there. The backup folder's
        // contents are the record of what to restore. No-op when the backup folder does not exist.
        private static void RestoreDirectory(string dbRootPath, string backupRootPath)
        {
            if (!Directory.Exists(backupRootPath))
            {
                return;
            }

            foreach (var backupSource in Directory.GetFiles(backupRootPath, "*", SearchOption.AllDirectories))
            {
                var original = Path.Combine(dbRootPath, GetRelativePath(backupRootPath, backupSource));

                Directory.CreateDirectory(Path.GetDirectoryName(original));

                if (File.Exists(original))
                {
                    File.Delete(original);
                }

                File.Move(backupSource, original);
            }
        }

        // net48 has no Path.GetRelativePath.
        private static string GetRelativePath(string root, string fullPath)
        {
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;
            var relativeUri = new Uri(rootWithSeparator).MakeRelativeUri(new Uri(fullPath));
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM recovery: failed to delete file {path} - {ex.Message}", LogLevel.Warning);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM recovery: failed to delete directory {path} - {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
