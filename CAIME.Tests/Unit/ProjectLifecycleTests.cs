using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for the project lifecycle: <see cref="Project"/> open/save/resize and the
    /// <see cref="ProjectManager"/> state around it. The manager's open/close bookkeeping is what the
    /// auto-save and auto-backup timers read to decide whether there is something safe to write, so a
    /// project left half-open is a data-loss hazard rather than a cosmetic bug.
    ///
    /// <para>
    /// <see cref="PreferencesViewModel"/> is a process-wide singleton backed by the user's real
    /// preferences.json, and opening a project reads it. These tests blank the RPFM path so no
    /// database is read, and restore every value they touched
    /// afterwards. They keep the remembered game in step so the open path never *changes* a setting;
    /// constructing the singleton at all still rewrites the file with identical content, because
    /// <c>Load()</c> ends in a <c>Save()</c>.
    /// </para>
    /// </summary>
    [TestClass]
    public class ProjectLifecycleTests
    {
        private const GameTemplate GAME = GameTemplate.Warhammer3;

        private string _dir;
        private string _savedRpfmPath;
        private int _savedGameIndex;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_project");

            var prefs = PreferencesViewModel.Instance;
            _savedRpfmPath  = prefs.RpfmPath;
            _savedGameIndex = prefs.SelectedGameIndex;

            prefs.RpfmPath = null;

            // ProjectManager.Open persists the preferences when the remembered game changes. Matching
            // it up front keeps this suite from writing to the user's real preferences file.
            prefs.SelectedGameIndex = (int)GAME;
        }

        [TestCleanup]
        public void Cleanup()
        {
            var prefs = PreferencesViewModel.Instance;
            prefs.RpfmPath = _savedRpfmPath;
            prefs.SelectedGameIndex = _savedGameIndex;

            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Open_PopulatesPathsGameAndEditorsFromTheMapFile()
        {
            var path = CreateMap();

            var project = new Project();
            Assert.IsTrue(project.Open(path), "Open returned false for a map that had just been created.");

            Assert.AreEqual(_dir + "\\", project.ProjectPath, "ProjectPath must be the map's directory, slash-terminated.");
            Assert.AreEqual("map", project.MapHexFileName, "MapHexFileName");
            Assert.AreEqual(GAME,  project.Game, "Game must be derived from the map header.");
            Assert.AreEqual("test_campaign", project.MapName, "MapName comes from the campaign map name.");
            Assert.AreEqual(path, project.FileName, "FileName must rebuild the path it was opened from.");
            Assert.AreEqual(8u, project.MapWidth,  "MapWidth");
            Assert.AreEqual(6u, project.MapHeight, "MapHeight");

            Assert.IsNotNull(project.ColourTable,  "ColourTable must be built on open.");
            Assert.IsNotNull(project.MapHexEditor, "MapHexEditor must be built on open.");
        }

        // A structurally valid file the loader rejects (unknown version) is the case Open reports by
        // returning false. A truncated file throws instead, and ProjectManager.Open is the layer that
        // turns that into a null return - see ManagerOpen_Failing_LeavesNoProjectOpen.
        [TestMethod]
        public void Open_UnsupportedMapVersion_ReturnsFalse()
        {
            var path  = CreateMap();
            var bytes = File.ReadAllBytes(path);
            bytes[4] = 0x7E;
            File.WriteAllBytes(path, bytes);

            Assert.IsFalse(new Project().Open(path), "A map at an unsupported version must not open.");
        }

        // The database is read through RPFM after the map opens, so the map must be editable without it.
        [TestMethod]
        public void Open_OpensTheMapWithoutItsDatabase()
        {
            var project = OpenNewProject();

            Assert.IsFalse(project.IsDatabaseLoaded, "Open must not load the database itself.");
            Assert.IsNull(project.RpfmTables, "No tables have been read yet.");
            Assert.IsNotNull(project.MapHexEditor, "The map must be editable without its database.");
        }

        // Tables that can't be loaded must leave the project as it opened, without a database, so the
        // steps that need one stay unavailable rather than running against a half-loaded one.
        [TestMethod]
        public void LoadDatabase_WithTablesThatCannotBeLoaded_LeavesTheProjectWithoutADatabase()
        {
            var project = OpenNewProject();

            Assert.IsFalse(project.LoadDatabase(new Dictionary<string, Rpfm.RpfmTable>()),
                "Tables missing every required table must not load.");
            Assert.IsFalse(project.IsDatabaseLoaded, "A failed load must leave no database.");
            Assert.IsNull(project.RpfmTables, "A failed load must not keep the tables.");
        }

        // Without an RPFM path there is nothing to read: the CLI must neither wait nor fail on it.
        [TestMethod]
        public void ManagerOpen_WithoutAnRpfmPath_ReadsNoDatabase()
        {
            var manager = new ProjectManager();

            var loaded = false;
            manager.OnDatabaseLoaded += (sender, e) => loaded = true;

            var project = manager.Open(CreateMap());

            Assert.IsNotNull(project, "Precondition: the map should open.");
            Assert.IsFalse(manager.IsReadingDatabase, "No read may be started without an RPFM path.");
            Assert.IsTrue(manager.WaitForDatabase(), "Having nothing to read is not a failure.");
            Assert.IsFalse(project.IsDatabaseLoaded, "No database can have been loaded.");
            Assert.IsFalse(loaded, "OnDatabaseLoaded must not be raised.");
        }

        [TestMethod]
        public void Save_WritesTheMapHexAndClearsUnsavedChanges()
        {
            var project = OpenNewProject();

            project.MapHexFile.SetDirty();
            Assert.IsTrue(project.HasUnsavedChanges, "Precondition: the project should be dirty.");

            Assert.IsTrue(project.Save(SaveParameters.SaveFlags.MapHexFile, "map", project.ProjectPath),
                "Save returned false.");

            Assert.IsFalse(project.HasUnsavedChanges, "A save must clear the unsaved-changes flag.");
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "map.hex")), "The map file was not written.");
        }

        [TestMethod]
        public void Save_WithCustomNameAndPath_WritesThereWithoutMovingTheProject()
        {
            var project = OpenNewProject();

            var sideDir = Path.Combine(_dir, "backups");
            Directory.CreateDirectory(sideDir);

            Assert.IsTrue(project.Save(SaveParameters.SaveFlags.MapHexFile, "backup", sideDir + "\\"),
                "Save returned false.");

            Assert.IsTrue(File.Exists(Path.Combine(sideDir, "backup.hex")), "The side file was not written.");
            Assert.AreEqual(_dir + "\\", project.ProjectPath, "Project.Save must not move the project itself.");
            Assert.AreEqual("map", project.MapHexFileName, "Project.Save must not rename the project itself.");
        }

        [TestMethod]
        public void Resize_ReportsWhetherTheDimensionsActuallyChanged()
        {
            var project = OpenNewProject();

            Assert.IsFalse(project.Resize(8, 6, 0, 0, 0, 0),
                "Resizing to the current dimensions must report that nothing changed.");

            Assert.IsTrue(project.Resize(10, 8, 2, 0, 2, 0), "A real resize must report a change.");
            Assert.AreEqual(10u, project.MapWidth,  "MapWidth");
            Assert.AreEqual(8u,  project.MapHeight, "MapHeight");
        }

        [TestMethod]
        public void CreateNew_ProducesAProjectThatCanBeReopened()
        {
            var project = Project.CreateNew(_dir + "\\", GAME, "brand_new", 6, 4);

            Assert.IsNotNull(project.MapHexFile,  "CreateNew must build a map.");
            Assert.IsNotNull(project.ColourTable, "CreateNew must build a colour table.");
            Assert.IsNotNull(project.MapHexEditor, "CreateNew must build an editor.");
            Assert.AreEqual(6u, project.MapWidth,  "MapWidth");
            Assert.AreEqual(4u, project.MapHeight, "MapHeight");

            var reopened = new Project();
            Assert.IsTrue(reopened.Open(Path.Combine(_dir, "map.hex")),
                "A project created from scratch must be reopenable.");
            Assert.AreEqual("brand_new", reopened.MapName, "MapName");
            Assert.AreEqual(GAME, reopened.Game, "Game");
        }

        // Pharaoh Dynasties is created as an in-app 0xFF map, so this also covers the CreateMapHex
        // path that previously wrote a file it could not read back.
        [TestMethod]
        public void CreateNew_PharaohDynasties_ProducesAReopenableDynastiesProject()
        {
            Project.CreateNew(_dir + "\\", GameTemplate.Pharaoh_Dynasties, "dynasties_map", 6, 4);

            var reopened = new Project();
            Assert.IsTrue(reopened.Open(Path.Combine(_dir, "map.hex")),
                "A new Pharaoh Dynasties project must be reopenable.");
            Assert.AreEqual(GameTemplate.Pharaoh_Dynasties, reopened.Game,
                "A Dynasties map must be recognised as Dynasties, not vanilla Pharaoh.");
        }

        [TestMethod]
        public void ManagerOpen_Succeeding_MakesTheProjectCurrentAndRaisesOnOpen()
        {
            var path    = CreateMap();
            var manager = new ProjectManager();

            Project raised = null;
            manager.OnOpenProject += (sender, e) => raised = e.Project;

            var project = manager.Open(path);

            Assert.IsNotNull(project, "Open returned null for a valid map.");
            Assert.IsTrue(manager.IsProjectOpen, "The manager should report an open project.");
            Assert.AreSame(project, manager.Project, "The opened project must become the current one.");
            Assert.AreSame(project, raised, "OnOpenProject must carry the project that was opened.");
        }

        // Open reports failure by returning null, so nothing may be left as if it had succeeded: a
        // lingering Project reads as licence for the auto-save and auto-backup timers to write it out.
        [TestMethod]
        public void ManagerOpen_Failing_LeavesNoProjectOpen()
        {
            var path = Path.Combine(_dir, "broken.hex");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });

            var manager = new ProjectManager();

            Assert.IsNull(manager.Open(path), "Opening a broken map must return null.");
            Assert.IsFalse(manager.IsProjectOpen, "A failed open must not leave a project current.");
            Assert.IsNull(manager.Project, "A failed open must not leave a project current.");
        }

        [TestMethod]
        public void CloseProject_ClearsTheCurrentProjectAndRaisesOnClose()
        {
            var manager = new ProjectManager();
            Assert.IsNotNull(manager.Open(CreateMap()), "Precondition: the map should open.");

            var closed = false;
            manager.OnCloseProject += (sender, e) => closed = true;

            manager.CloseProject();

            Assert.IsFalse(manager.IsProjectOpen, "The manager must report no open project.");
            Assert.IsNull(manager.Project, "The current project must be cleared.");
            Assert.IsTrue(closed, "OnCloseProject must be raised.");
        }

        [TestMethod]
        public void CloseProject_WithNothingOpen_IsSafe()
        {
            new ProjectManager().CloseProject();
        }

        // A save to a different name or location is a "save as": the project follows the file.
        [TestMethod]
        public void ManagerSave_ToANewNameAndPath_MovesTheProjectAndReportsTheChange()
        {
            var manager = new ProjectManager();
            Assert.IsNotNull(manager.Open(CreateMap()), "Precondition: the map should open.");

            var sideDir = Path.Combine(_dir, "elsewhere") + "\\";
            Directory.CreateDirectory(sideDir);

            ProjectPathChangedEventArgs change = null;
            manager.OnFilePathChanged += (sender, e) => change = e;

            manager.Save(new SaveParameters
            {
                CustomSavePath = sideDir,
                CustomFileName = "renamed",
                Flags          = SaveParameters.SaveFlags.MapHexFile,
            });

            Assert.AreEqual(sideDir,   manager.Project.ProjectPath,    "The project should follow the save.");
            Assert.AreEqual("renamed", manager.Project.MapHexFileName, "The project should take the new name.");

            Assert.IsNotNull(change, "OnFilePathChanged must be raised.");
            Assert.AreEqual(_dir + "\\", change.OldPath, "OldPath");
            Assert.AreEqual(sideDir,     change.NewPath, "NewPath");
            Assert.AreEqual("map",       change.OldName, "OldName");
            Assert.AreEqual("renamed",   change.NewName, "NewName");
        }

        // An auto-backup writes a side file. The project must stay pointed at its own file and keep
        // its unsaved changes, or the next real save silently writes to the backup location.
        [TestMethod]
        public void ManagerBackup_WritesASideFileWithoutMovingOrCleaningTheProject()
        {
            var manager = new ProjectManager();
            Assert.IsNotNull(manager.Open(CreateMap()), "Precondition: the map should open.");

            manager.Project.MapHexFile.SetDirty();

            var backupDir = Path.Combine(_dir, "autobackup") + "\\";
            Directory.CreateDirectory(backupDir);

            var moved = false;
            manager.OnFilePathChanged += (sender, e) => moved = true;

            manager.Backup(backupDir, "map_backup");

            Assert.IsTrue(File.Exists(Path.Combine(backupDir, "map_backup.hex")), "The backup was not written.");
            Assert.AreEqual(_dir + "\\", manager.Project.ProjectPath,    "A backup must not move the project.");
            Assert.AreEqual("map",       manager.Project.MapHexFileName, "A backup must not rename the project.");
            Assert.IsTrue(manager.Project.HasUnsavedChanges,
                "A backup must leave the project's unsaved changes flagged.");
            Assert.IsFalse(moved, "A backup must not report a project path change.");
        }

        [TestMethod]
        public void ResizeProject_RaisesOnMapResizedOnlyWhenSomethingChanged()
        {
            var manager = new ProjectManager();
            Assert.IsNotNull(manager.Open(CreateMap()), "Precondition: the map should open.");

            var raised = 0;
            manager.OnMapResized += (sender, e) => raised++;

            manager.ResizeProject(8, 6, 0, 0, 0, 0);
            Assert.AreEqual(0, raised, "Resizing to the current dimensions must raise nothing.");

            manager.ResizeProject(10, 8, 2, 0, 2, 0);
            Assert.AreEqual(1, raised, "A real resize must raise OnMapResized.");
        }

        [TestMethod]
        public void RenameCampaignMap_WithNoProjectOpen_ReturnsFalse()
        {
            Assert.IsFalse(new ProjectManager().RenameCampaignMap("whatever"),
                "Renaming with nothing open must fail rather than throw.");
        }

        [TestMethod]
        public void RenameCampaignMap_UpdatesTheMapNameAndRaisesOnMapRenamed()
        {
            var manager = new ProjectManager();
            Assert.IsNotNull(manager.Open(CreateMap()), "Precondition: the map should open.");

            var raised = false;
            manager.OnMapRenamed += (sender, e) => raised = true;

            Assert.IsTrue(manager.RenameCampaignMap("renamed_campaign"), "Rename returned false.");
            Assert.AreEqual("renamed_campaign", manager.Project.MapName, "MapName");
            Assert.IsTrue(raised, "OnMapRenamed must be raised.");
        }

        [TestMethod]
        [DataRow("rome2",          0x0D, GameTemplate.Rome2)]
        [DataRow("attila",         0x0F, GameTemplate.Attila)]
        [DataRow("thrones",        0x0F, GameTemplate.Thrones_Of_Britannia)]
        [DataRow("warhammer",      0x12, GameTemplate.Warhammer)]
        [DataRow("warhammer2",     0x12, GameTemplate.Warhammer2)]
        [DataRow("warhammer3",     0x14, GameTemplate.Warhammer3)]
        [DataRow("troy",           0x12, GameTemplate.Troy)]
        [DataRow("three_kingdoms", 0x13, GameTemplate.Three_Kingdoms)]
        public void GetGameFromName_MapsEachGameIdentifier(string name, int minorVersion, GameTemplate expected)
        {
            Assert.AreEqual(expected, ProjectManager.GetGameFromName(name, minorVersion));
        }

        // "phar" covers both Pharaoh titles and is told apart only by the map layout, so the version
        // is what decides - this is the pair most likely to be confused for one another.
        [TestMethod]
        public void GetGameFromName_PharaohAndDynasties_AreSplitByFileVersion()
        {
            Assert.AreEqual(GameTemplate.Pharaoh,
                ProjectManager.GetGameFromName("phar", 0x12), "Vanilla Pharaoh is a plain 0x12 map.");

            Assert.AreEqual(GameTemplate.Pharaoh_Dynasties,
                ProjectManager.GetGameFromName("phar", MapHexFile.FAKE_DYNASTIES_MINOR_VER),
                "Dynasties is identified by the in-app combined-region version.");

            Assert.AreEqual(GameTemplate.Invalid,
                ProjectManager.GetGameFromName("phar", 0x0D), "An implausible Pharaoh version is invalid.");

            Assert.AreEqual(GameTemplate.Invalid,
                ProjectManager.GetGameFromName("not_a_game", 0x14), "An unknown game identifier is invalid.");
        }

        [TestMethod]
        public void GameNameAndVersion_RoundTripForEverySupportedGame()
        {
            for (var game = GameTemplate.Rome2; game < GameTemplate.Count; ++game)
            {
                var name    = ProjectManager.GetGameNameFromGame(game);
                var version = ProjectManager.GetMapHexVersion(game);

                Assert.IsNotNull(name, $"{game} has no game identifier.");
                Assert.AreNotEqual(-1, version, $"{game} has no map.hex version.");

                // Dynasties is stored as 0x14 and promoted to the in-app marker while loading.
                var inAppVersion = game == GameTemplate.Pharaoh_Dynasties
                    ? MapHexFile.FAKE_DYNASTIES_MINOR_VER
                    : version;

                Assert.AreEqual(game, ProjectManager.GetGameFromName(name, inAppVersion),
                    $"{game} did not survive a name/version round trip.");
            }
        }

        private string CreateMap()
        {
            MapHexFile.CreateMapHex(_dir, "map", "warhammer3", "test_campaign", 8, 6, 0x14);
            return Path.Combine(_dir, "map.hex");
        }

        private Project OpenNewProject()
        {
            var project = new Project();
            Assert.IsTrue(project.Open(CreateMap()), "Precondition: the map should open.");
            return project;
        }
    }
}
