using System;
using System.Collections.Generic;
using System.IO;
using CAIME.Rpfm;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="RpfmRecoveryJournal"/>, the record that lets CAIME put the Assembly
    /// Kit back after an earlier version's RPFM session, which swapped the Assembly Kit's tables for
    /// its own, was killed mid-session (End Task, Stop Debugging) with no managed handler able to run.
    /// It is the only recovery path for that case, and one that never executes in normal use - so it
    /// is exactly the code most likely to be broken when finally needed.
    ///
    /// <para>
    /// The replay itself is exercised through the private <c>Recover(journalFile)</c> rather than the
    /// public <c>RecoverAll</c>: RecoverAll scans a fixed %AppData% directory shared with the real
    /// installation, and running it here would consume a genuine pending recovery belonging to the
    /// user.
    /// </para>
    /// </summary>
    [TestClass]
    public class RpfmRecoveryJournalTests
    {
        private string _root;
        private string _dbRoot;
        private string _backupRoot;
        private string _tempExtract;
        private string _journalDir;

        [TestInitialize]
        public void Setup()
        {
            _root        = MapHexHarness.CreateTempDir("caime_rpfm_journal");
            _dbRoot      = Path.Combine(_root, "db");
            _backupRoot  = Path.Combine(_root, "backup");
            _tempExtract = Path.Combine(_root, "extract");
            _journalDir  = Path.Combine(_root, "journals");

            Directory.CreateDirectory(_dbRoot);
            Directory.CreateDirectory(_journalDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_root);
        }

        [TestMethod]
        public void Recover_RestoresOriginals_RemovesGeneratedFilesAndWorkingDirs_ThenDeletesTheJournal()
        {
            var original = Path.Combine(_dbRoot, "campaign_regions.xml");
            File.WriteAllText(original, "original contents");

            // What a session did before it was killed: back the original up, write a replacement.
            BackUp(original);
            File.WriteAllText(original, "generated contents");

            Directory.CreateDirectory(_tempExtract);
            File.WriteAllText(Path.Combine(_tempExtract, "fragment.tsv"), "extracted");

            var journalFile = WriteJournal(new RpfmRecoveryJournal
            {
                SessionId      = Guid.NewGuid().ToString("N"),
                DbRootPath     = _dbRoot,
                BackupRootPath = _backupRoot,
                TempExtractDir = _tempExtract,
                GeneratedFiles = new List<string> { original },
            });

            Recover(journalFile);

            Assert.AreEqual("original contents", File.ReadAllText(original),
                "The user's original Assembly Kit file was not restored.");
            Assert.IsFalse(Directory.Exists(_tempExtract), "The temporary extract directory was left behind.");
            Assert.IsFalse(Directory.Exists(_backupRoot),  "The backup directory was left behind.");
            Assert.IsFalse(File.Exists(journalFile), "A fully replayed journal must be deleted.");
        }

        // Generated files are only journalled once their originals are safely backed up, so removing
        // them can never delete an untouched original - but a file the session never got to is normal.
        [TestMethod]
        public void Recover_GeneratedFileAlreadyGone_StillCompletes()
        {
            var original = Path.Combine(_dbRoot, "campaign_regions.xml");
            File.WriteAllText(original, "original contents");

            BackUp(original);

            var journalFile = WriteJournal(new RpfmRecoveryJournal
            {
                SessionId      = Guid.NewGuid().ToString("N"),
                DbRootPath     = _dbRoot,
                BackupRootPath = _backupRoot,
                TempExtractDir = _tempExtract,
                GeneratedFiles = new List<string> { original, Path.Combine(_dbRoot, "never_written.xml") },
            });

            Recover(journalFile);

            Assert.AreEqual("original contents", File.ReadAllText(original), "Restore did not run.");
            Assert.IsFalse(File.Exists(journalFile), "The journal should have been consumed.");
        }

        // A journal that cannot be parsed is worse than useless: left in place it would be retried,
        // and fail, on every future launch.
        [TestMethod]
        public void Recover_UnparseableJournal_IsDeletedRatherThanBlockingFutureLaunches()
        {
            var journalFile = Path.Combine(_journalDir, "broken.json");
            File.WriteAllText(journalFile, "{ this is not json");

            Recover(journalFile);

            Assert.IsFalse(File.Exists(journalFile), "An unreadable journal must be removed.");
        }

        [TestMethod]
        public void Recover_JournalWithoutADbRoot_IsDeleted()
        {
            var journalFile = WriteJournal(new RpfmRecoveryJournal
            {
                SessionId  = Guid.NewGuid().ToString("N"),
                DbRootPath = null,
            });

            Recover(journalFile);

            Assert.IsFalse(File.Exists(journalFile), "A journal with nothing to restore must be removed.");
        }

        // A restore that fails must leave the journal so the next launch can retry - deleting it would
        // strand the Assembly Kit in its replaced state permanently.
        [TestMethod]
        public void Recover_WhenRestoreFails_KeepsTheJournalForARetry()
        {
            var original = Path.Combine(_dbRoot, "campaign_regions.xml");
            File.WriteAllText(original, "original contents");

            BackUp(original);

            // Occupy the restore target with a directory of the same name, so the move cannot land.
            Directory.CreateDirectory(original);

            var journalFile = WriteJournal(new RpfmRecoveryJournal
            {
                SessionId      = Guid.NewGuid().ToString("N"),
                DbRootPath     = _dbRoot,
                BackupRootPath = _backupRoot,
                TempExtractDir = _tempExtract,
                GeneratedFiles = new List<string>(),
            });

            Recover(journalFile);

            Assert.IsTrue(File.Exists(journalFile),
                "A failed restore must leave the journal in place so recovery can be retried.");
            Assert.IsTrue(File.Exists(Path.Combine(_backupRoot, "campaign_regions.xml")),
                "The backed-up original must survive a failed restore.");
        }

        // What an earlier version's session did with each table it replaced: move the original into
        // its backup folder, under the same name.
        private void BackUp(string original)
        {
            Directory.CreateDirectory(_backupRoot);
            File.Move(original, Path.Combine(_backupRoot, Path.GetFileName(original)));
        }

        private string WriteJournal(RpfmRecoveryJournal journal)
        {
            var path = Path.Combine(_journalDir, (journal.SessionId ?? "journal") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(journal, Formatting.Indented));
            return path;
        }

        private static void Recover(string journalFile)
        {
            MapHexHarness.InvokePrivateStatic(typeof(RpfmRecoveryJournal), "Recover", journalFile);
        }
    }
}
