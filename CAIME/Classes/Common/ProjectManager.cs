using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using CAIME.Classes.Importers;
using CAIME.Exporters;
using CAIME.TradeNetwork;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace CAIME
{
    public enum GameTemplate
    {
        Invalid = -1,
        Rome2 = 0,
        Attila,
        Thrones_Of_Britannia,
        Warhammer,
        Warhammer2,
        Warhammer3,
        Three_Kingdoms,
        Troy,
        Pharaoh,
        Pharaoh_Dynasties,

        Count
    }

    public class ProjectEventArgs : RoutedEventArgs
    {
        public Project Project { get; private set; }

        public ProjectEventArgs(Project project)
        {
            Project = project;
        }
    }

    public class ProjectPathChangedEventArgs : RoutedEventArgs
    {
        public string OldPath { get; private set; }
        public string NewPath { get; private set; }
        public string OldName { get; private set; }
        public string NewName { get; private set; }

        public ProjectPathChangedEventArgs(string oldPath, string newPath, string oldName, string newName)
        {
            OldPath = oldPath;
            NewPath = newPath;
            OldName = oldName;
            NewName = newName;
        }
    }

    public class SaveParameters
    {
        public static SaveParameters Default = new SaveParameters()
        {
            IsAutoSave      = false,
            IsAutoBackup    = false,
            CustomSavePath  = null,
            CustomFileName  = null,
            Flags           = SaveFlags.All
        };

        public enum SaveFlags : byte
        {
            MapHexFile  = 1 << 1,
            Database    = 1 << 2,

            None        = 0,
            All         = MapHexFile | Database
        }

        public bool         IsAutoSave      { get; set; }
        public bool         IsAutoBackup    { get; set; }
        public string       CustomSavePath  { get; set; }
        public string       CustomFileName  { get; set; }
        public SaveFlags    Flags           { get; set; }

        public SaveParameters()
        {
            IsAutoSave      = false;
            IsAutoBackup    = false;
            CustomSavePath  = null;
            CustomFileName  = null;
            Flags           = SaveFlags.All;
        }
    }

    public delegate void ProjectEventHandler(object sender, ProjectEventArgs e);
    public delegate void FilePathEventHandler(object sender, ProjectPathChangedEventArgs e);
    
    public class Project
    {
        public string               ProjectPath     { get; private set; }
        public string               MapHexFileName  { get; private set; }

        public MapHexFile           MapHexFile      { get; private set; }
        public MapHexEditor         MapHexEditor    { get; private set; }
        public GameTemplate         Game            { get; private set; }

        public DatabaseViewModel    Database        { get; private set; }
        public ColourTable          ColourTable     { get; private set; }

        // The database tables read through RPFM, by table name; null until they have been read and
        // loaded (see LoadDatabase), and for good when no RPFM path is configured.
        public IReadOnlyDictionary<string, Rpfm.RpfmTable> RpfmTables { get; private set; }

        public bool IsDatabaseLoaded => RpfmTables != null;

        public string MapName
        {
            get
            {
                return MapHexFile == null ? null : MapHexFile.CampaignMapName;
            }
        }

        public uint MapWidth
        {
            get
            {
                return MapHexFile == null ? 0 : MapHexFile.MapWidth;
            }
        }

        public uint MapHeight
        {
            get
            {
                return MapHexFile == null ? 0 : MapHexFile.MapHeight;
            }
        }

        public bool HasUnsavedChanges
        {
            get
            {
                return MapHexFile.IsDirty;
            }
        }

        public string FileName
        {
            get
            {
                return $"{ProjectPath}{MapHexFileName}.hex";
            }
        }

        public Project()
        {
            MapHexFile  = new MapHexFile();
            Database    = new DatabaseViewModel();
        }

        /// <summary>
        /// Opens the map, ready to view and edit, without its database: that is read through RPFM
        /// afterwards (see <see cref="ProjectManager"/>) and handed to <see cref="LoadDatabase"/>.
        /// </summary>
        public bool Open(string filename)
        {
            if (MapHexFile.Load(filename) == false)
            {
                return false;
            }

            // Hand-parsing this threw on a bare relative filename (reachable from the CLI) or a
            // path with no extension.
            var directory   = Path.GetDirectoryName(Path.GetFullPath(filename));

            ProjectPath     = directory.EndsWith("\\") ? directory : directory + "\\";
            MapHexFileName  = Path.GetFileNameWithoutExtension(filename);
            Game            = ProjectManager.GetGameFromName(MapHexFile.GameName, MapHexFile.MinorFileVersion);

            ColourTable     = new ColourTable(MapHexFile);
            MapHexEditor    = new MapHexEditor(MapHexFile, ColourTable);

            return true;
        }

        /// <summary>
        /// Loads the database from <paramref name="tables"/>, read through RPFM. Returns false, leaving
        /// the project without a database, when they can't be loaded or don't fit the map.
        /// </summary>
        public bool LoadDatabase(IReadOnlyDictionary<string, Rpfm.RpfmTable> tables)
        {
            try
            {
                if (Database.Initialise(Game, ProjectPath, MapHexFile, tables))
                {
                    RpfmTables = tables;
                    return true;
                }

                // Only Pharaoh Dynasties checks the database against the map; the mismatches are logged.
                LoggerViewModel.Log(
                    $"The region indices in {Game}'s database don't match the map's (see the log). " +
                    "Saving and processing stay unavailable for this session.",
                    LogLevel.ErrorMessageBox);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log(
                    $"Could not load database tables for {Game} ({ex.Message}). " +
                    "Saving and processing stay unavailable for this session.",
                    LogLevel.Warning);
            }

            Database.ShutDown();
            return false;
        }

        public bool Save(SaveParameters.SaveFlags flags, string mapHexName, string savePath, bool clearDirtyFlag = true)
        {
            if ((flags & SaveParameters.SaveFlags.MapHexFile) != 0)
            {
                var combinedRegionOrder = Database.GetCombinedRegionOrder();
                if (!MapHexFile.Save(savePath, mapHexName, combinedRegionOrder, clearDirtyFlag))
                {
                    LoggerViewModel.Log($"Failed to save {mapHexName}.hex file!", LogLevel.Error);
                    return false;
                }
            }

            if ((flags & SaveParameters.SaveFlags.Database) != 0)
            {
                if (Database.Save(Game, MapHexFile) == false)
                {
                    LoggerViewModel.Log("Failed to save database changes!", LogLevel.Error);
                    return false;
                }
            }

            return true;
        }

        public bool Resize(uint newWidth, uint newHeight, int newPadRight, int newPadLeft, int newPadTop, int newPadBottom)
        {
            if (newWidth != MapWidth || newHeight != MapHeight)
            {
                MapHexFile.ResizeMapHex(newWidth, newHeight, newPadRight, newPadLeft, newPadTop, newPadBottom);
                return true;
            }

            return false;
        }

        public void UpdatePath(string newPath)
        {
            ProjectPath = newPath;
        }

        public void UpdateMapHexName(string newName)
        {
            MapHexFileName = newName;
        }

        public static Project CreateNew(string projectPath, GameTemplate game, string mapName, uint mapWidth, uint mapHeight)
        {
            var project             = new Project
            {
                ProjectPath         = projectPath,
                MapHexFileName      = "map",
                Game                = game,
            };

            var gameName            = ProjectManager.GetGameNameFromGame(game);
            var minorVer            = ProjectManager.GetMapHexVersion(game);

            project.MapHexFile      = MapHexFile.CreateMapHex(project.ProjectPath, project.MapHexFileName, gameName, mapName, mapWidth, mapHeight, minorVer);
            project.ColourTable     = new ColourTable(project.MapHexFile);
            project.MapHexEditor    = new MapHexEditor(project.MapHexFile, project.ColourTable);

            return project;
        }
    }

    public class ProjectManager
    {
        public static string TEMPLATE_NONE = "None";

        private readonly object _saveLock = new object();

        public ProjectEventHandler  OnOpenProject;
        public ProjectEventHandler  OnDatabaseLoaded;
        public ProjectEventHandler  OnSaveProject;
        public ProjectEventHandler  OnMapRenamed;
        public RoutedEventHandler   OnCloseProject;
        public RoutedEventHandler   OnMapResized;
        public FilePathEventHandler OnFilePathChanged;

        public string   RootPath        { get; private set; }
        public string   ProjectsDir     { get; private set; }
        public string   TemplatesPath   { get; private set; }
        public string   ToolsPath       { get; private set; }
        public Project  Project         { get; private set; }

        public bool     IsProjectOpen   => Project != null;

        // The open project's database read (see StartReadingDatabase); null when none was started.
        // Its result says whether the tables were loaded into the project.
        private Task<bool> _databaseRead;

        public bool     IsReadingDatabase => _databaseRead != null && !_databaseRead.IsCompleted;

        public ProjectManager()
        {
            RootPath        = GetRootPath();
            ProjectsDir     = $@"{RootPath}Projects\";
            TemplatesPath   = GetTemplatesPath();
            ToolsPath       = GetToolsPath();

            LoggerViewModel.Log($"App root path is {RootPath}", LogLevel.Info);
        }

        /// <summary>
        /// Application root directory. In DEBUG this resolves to the project folder, in RELEASE to the
        /// install folder, matching how the build lays out the Tools and Templates directories.
        /// </summary>
        public static string GetRootPath()
        {
#if DEBUG
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
#else
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\"));
#endif
        }

        /// <summary>
        /// Folder containing the bundled map templates (each a subfolder with a map.hex).
        /// </summary>
        public static string GetTemplatesPath()
        {
            return GetBundledFolderPath("Templates", @"..\Templates\");
        }

        /// <summary>
        /// Folder containing MapDataBuilder, for Assembly Kit Integration, split into Debug and Release subfolders.
        /// </summary>
        public static string GetToolsPath()
        {
            return GetBundledFolderPath("Tools", @"Tools\");
        }

        /// <summary>
        /// An installed release ships its bundled folders next to CAIME.exe, because the installer only
        /// packs that one folder. A build from the repository finds them relative to the root path instead.
        /// </summary>
        private static string GetBundledFolderPath(string folderName, string pathFromRoot)
        {
            var besideExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName) + @"\";
            if (Directory.Exists(besideExe))
            {
                return besideExe;
            }

            return Path.GetFullPath(Path.Combine(GetRootPath(), pathFromRoot));
        }

        public Project CreateProject(string mapName, uint mapWidth, uint mapHeight, GameTemplate game, string template)
        {
            var newProjectPath = CreateProjectDirectory(mapName);
            if (newProjectPath == null)
            {
                return null;
            }

            if (template == TEMPLATE_NONE)
            {
                // Writes the empty map.hex, which is then opened like any other.
                Project.CreateNew(newProjectPath, game, mapName, mapWidth, mapHeight);
                return Open($"{newProjectPath}map.hex");
            }
            else
            {
                var templatePath = $@"{TemplatesPath}{template}\";

                // Copy files from the template
                foreach (var filePath in Directory.GetFiles(templatePath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(filePath, filePath.Replace(templatePath, newProjectPath), true);
                }

                return Open($"{newProjectPath}map.hex");
            }
        }

        /// <summary>
        /// Creates a project from any game's map_data.esf by rebuilding the map.hex it was built
        /// from. The map is named after the folder the file is in, which for a file taken from a
        /// pack's campaign_maps folder is the campaign map's name. Several games share each of
        /// the older layouts, so <paramref name="chooseGame"/> picks one of them, or returns null
        /// to cancel; it is not asked when the file fits only one game.
        /// </summary>
        public Project ImportMapData(string mapDataEsfPath, Func<GameTemplate[], GameTemplate?> chooseGame)
        {
            var mapName = StringHelper.NameFixup(Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(mapDataEsfPath))));
            if (string.IsNullOrEmpty(mapName))
            {
                // An empty name would make the Projects folder itself the project folder.
                LoggerViewModel.Log($"{mapDataEsfPath} is not in a folder to name the map after. Put it in a folder named after the campaign map and import it again.", LogLevel.ErrorMessageBox);
                return null;
            }

            try
            {
                var contents    = MapDataReader.Read(mapDataEsfPath);
                var games       = MapDataReader.Games(contents);
                var game        = games.Length == 1 ? games[0] : chooseGame(games);
                if (game == null)
                {
                    return null;
                }

                var newProjectPath = CreateProjectDirectory(mapName);
                if (newProjectPath == null)
                {
                    return null;
                }

                var mapHexFile = MapHexFile.CreateMapHex(newProjectPath, "map", GetGameNameFromGame(game.Value), mapName, contents.Width, contents.Height, GetMapHexVersion(game.Value));
                MapDataImporter.Import(contents, mapHexFile);
                mapHexFile.Save(newProjectPath, "map");
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"EXCEPTION: {ex}", LogLevel.Debug);
                LoggerViewModel.Log($"{mapDataEsfPath} could not be imported.\n\nReason: {ex.Message}", LogLevel.ErrorMessageBox);
                return null;
            }

            LoggerViewModel.Log($"Imported {mapDataEsfPath} as campaign map '{mapName}'. Trade routes and restriction levels are not in map_data.esf and have been left empty.", LogLevel.Info);

            return Open($@"{ProjectsDir}{mapName}\map.hex");
        }

        /// <returns>The new project's folder, or null when the user declines to overwrite an existing one.</returns>
        private string CreateProjectDirectory(string mapName)
        {
            var newProjectPath = $@"{ProjectsDir}{mapName}\";

            if (Directory.Exists(newProjectPath))
            {
                var res = MessageBox.Show("Project with the specified name already exists. Overwrite?", "Project name conflict", MessageBoxButton.YesNo);
                if (res != MessageBoxResult.Yes)
                {
                    return null;
                }

                Directory.Delete(newProjectPath, true);
            }

            Directory.CreateDirectory(newProjectPath);

            return newProjectPath;
        }

        public void Save(SaveParameters saveParams)
        {
            var mapHexName  = string.IsNullOrEmpty(saveParams.CustomFileName) ? Project.MapHexFileName  : saveParams.CustomFileName;
            var savePath    = string.IsNullOrEmpty(saveParams.CustomSavePath) ? Project.ProjectPath     : saveParams.CustomSavePath;

            // Auto-saves and backups write to a side file - the project itself still has
            // unsaved changes, so its dirty flag must survive.
            var clearDirtyFlag = !saveParams.IsAutoSave && !saveParams.IsAutoBackup;

            // No game's map.hex is written without its database. Pharaoh Dynasties' is written in
            // the database's region order (see Project.Save); the rest follow the same rule.
            if (!Project.IsDatabaseLoaded && (saveParams.Flags & SaveParameters.SaveFlags.MapHexFile) != 0)
            {
                var reason = IsReadingDatabase
                    ? "until its database has been read through RPFM. Try again in a moment."
                    : "without its database, which was not read (see the log). Fix that, then reopen the project.";

                LoggerViewModel.Log($"The map can't be saved {reason}",
                    clearDirtyFlag ? LogLevel.ErrorMessageBox : LogLevel.Warning);
                return;
            }

            lock (_saveLock)
            {
                if (Project.Save(saveParams.Flags, mapHexName, savePath, clearDirtyFlag) == false)
                {
                    return;
                }
            }

            var projectSaveArgs = new ProjectEventArgs(Project);
            OnSaveProject?.Invoke(this, projectSaveArgs);
            LoggerViewModel.Log($"The project has been successfully saved!", LogLevel.Info);

            if (!saveParams.IsAutoSave && !saveParams.IsAutoBackup)
            {
                var projectPathChanged = false;
                var oldPath = Project.ProjectPath;
                var oldName = Project.MapHexFileName;

                if (savePath != Project.ProjectPath)
                {
                    Project.UpdatePath(savePath);
                    projectPathChanged = true;
                    LoggerViewModel.Log($"The project path has been changed from {oldPath} to {savePath}", LogLevel.Warning);
                }

                if (mapHexName != Project.MapHexFileName)
                {
                    Project.UpdateMapHexName(mapHexName);
                    projectPathChanged = true;
                    LoggerViewModel.Log($"The project filename has been changed from {oldName} to {mapHexName}", LogLevel.Warning);
                }

                if (projectPathChanged)
                {
                    var args = new ProjectPathChangedEventArgs(oldPath, Project.ProjectPath, oldName, Project.MapHexFileName);
                    OnFilePathChanged?.Invoke(this, args);
                }
            }
        }

        public Project Open(string filename)
        {
            Project project = null;
            try
            {
                project = new Project();
                if (project.Open(filename) == false)
                {
                    return null;
                }

                Project = project;

                // Remembered as the Preferences window's default "Base game" - both immediately (while
                // this project stays open) and, via Save, for future sessions with nothing open yet.
                if (PreferencesViewModel.Instance.SelectedGameIndex != (int)project.Game)
                {
                    PreferencesViewModel.Instance.SelectedGameIndex = (int)project.Game;
                    PreferencesViewModel.Instance.Save();
                }

                var projectLoadedArgs = new ProjectEventArgs(project);
                OnOpenProject?.Invoke(this, projectLoadedArgs);

                StartReadingDatabase(project);

                return project;
            }
            catch (Exception ex)
            {
                // Open() reports failure by returning null, so nothing here must be left as if it
                // succeeded - Project is set well before this point (needed so OnOpenProject
                // subscribers can see the new project), so a throw anywhere after that but before the
                // normal return (Directory.CreateDirectory, an OnOpenProject subscriber, etc.) would
                // otherwise leave IsProjectOpen (=> Project != null) true for a project that never
                // finished opening - which the auto-save and auto-backup timers read as licence to
                // start writing it out.
                Project = null;

                LoggerViewModel.Log($"EXCEPTION: {ex.Message}", LogLevel.Debug);
                LoggerViewModel.Log($"{filename} failed to load!\n\nReason: {ex.Message}", LogLevel.ErrorMessageBox);
            }

            return null;
        }

        public void CloseProject()
        {
            Project = null;
            _databaseRead = null;

            OnCloseProject?.Invoke(this, new RoutedEventArgs());
        }

        /// <summary>
        /// Blocks until the open project's database read has ended, and returns whether its database
        /// was loaded: false when the read failed, or when there was nothing to read. For the CLI
        /// only: on the UI thread this deadlocks, since the read ends by loading the tables there.
        /// </summary>
        public bool WaitForDatabase()
        {
            return _databaseRead != null && _databaseRead.Result;
        }

        /// <summary>
        /// Starts reading the required database tables through RPFM, from the game's own packs with
        /// the project's mod layered over them when one is recorded. Starting the RPFM server and
        /// reading the packs takes a while, so the read runs in the background: the map can be viewed
        /// and edited meanwhile, and only the steps that need the database wait for it. Once read, the
        /// tables are loaded into <paramref name="project"/> on the UI thread and
        /// <see cref="OnDatabaseLoaded"/> is raised. Without an RPFM path nothing is read, and the
        /// project stays without a database.
        /// </summary>
        private void StartReadingDatabase(Project project)
        {
            _databaseRead = null;

            var rpfmFolder = PreferencesViewModel.Instance.RpfmPath;
            if (string.IsNullOrEmpty(rpfmFolder))
            {
                LoggerViewModel.Log(
                    $"No RPFM path is configured, so {project.Game}'s database (ground type names/costs, region names, " +
                    "campaign data) can't be read, and the map can't be saved or processed this session - set the RPFM " +
                    "path in Settings > Preferences and reopen the project.",
                    LogLevel.Warning);
                return;
            }

            if (!Rpfm.GameMappingProvider.IsSupported(project.Game))
            {
                LoggerViewModel.Log($"CAIME cannot read {project.Game}'s database through RPFM.", LogLevel.ErrorMessageBox);
                _databaseRead = Task.FromResult(false);
                return;
            }

            // The mod is optional and per-project: if the metadata simply doesn't record one (a new
            // project, or one that has never needed a mod override), that's a normal state, not an
            // error - every table just comes from the game's own packs. Never prompt for it here;
            // it's set deliberately via Settings > RPFM Workflow when wanted.
            var modPackName = Rpfm.MetadataService.GetModPackName(project.ProjectPath);

            // Built here rather than in the background: it takes the map's land and sea regions as
            // they are now, before the first edit.
            var session = new Rpfm.RpfmWorkflowSession(project.Game, rpfmFolder, modPackName, project.MapHexFile);

            // Null in the CLI, which has no UI thread: there the tables are loaded on the reading thread.
            var dispatcher = Application.Current?.Dispatcher;

            LoggerViewModel.Log("Reading the database through RPFM. Saving and processing are unavailable until it has been read.", LogLevel.Info);

            _databaseRead = Task.Run(() =>
            {
                IReadOnlyDictionary<string, Rpfm.RpfmTable> tables = null;
                Exception failure = null;

                try
                {
                    tables = session.Prepare();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                return dispatcher == null
                    ? FinishReadingDatabase(project, tables, failure)
                    : dispatcher.Invoke(() => FinishReadingDatabase(project, tables, failure));
            });
        }

        private bool FinishReadingDatabase(Project project, IReadOnlyDictionary<string, Rpfm.RpfmTable> tables, Exception failure)
        {
            // Closed, or replaced by another project, while its tables were read.
            if (Project != project)
            {
                return false;
            }

            if (failure != null)
            {
                LoggerViewModel.Log($"Reading the database through RPFM failed: {failure.Message}", LogLevel.ErrorMessageBox);
                return false;
            }

            if (!project.LoadDatabase(tables))
            {
                return false;
            }

            LoggerViewModel.Log("RPFM database tables read successfully. Saving and processing are now available.", LogLevel.Info);
            OnDatabaseLoaded?.Invoke(this, new ProjectEventArgs(project));
            return true;
        }

        public void Backup(string backupPath, string backupName)
        {
            var saveParams = new SaveParameters()
            {
                IsAutoBackup    = true,
                CustomSavePath  = backupPath,
                CustomFileName  = backupName,
                Flags           = SaveParameters.SaveFlags.MapHexFile
            };

            Save(saveParams);
            LoggerViewModel.Log($"Backup finished successfully!", LogLevel.Info);
        }

        public void ResizeProject(uint newWidth, uint newHeight, int newPadRight, int newPadLeft, int newPadTop, int newPadBottom)
        {
            if (Project.Resize(newWidth, newHeight, newPadRight, newPadLeft, newPadTop, newPadBottom))
            {
                OnMapResized?.Invoke(this, new RoutedEventArgs());
                LoggerViewModel.Log("The hex map has been resized!", LogLevel.Info);
            }
        }

        public bool RenameCampaignMap(string newName)
        {
            if (Project == null || Project.MapHexFile == null)
            {
                return false;
            }

            var oldName = Project.MapName;
            Project.MapHexFile.RenameMap(newName);

            LoggerViewModel.Log($"Campaign map has been successfully renamed from {oldName} to {newName}", LogLevel.Info);
            OnMapRenamed?.Invoke(this, new ProjectEventArgs(Project));

            return true;
        }

        #region Exporters

        /// <summary>
        /// The folder the open project's processed files go to (see
        /// <see cref="PreferencesViewModel.GetCampaignMapOutputPath"/>), created; null, after telling
        /// the user, when no output folder or Assembly Kit is set.
        /// </summary>
        private string GetOutputPath()
        {
            var outputPath = PreferencesViewModel.Instance.GetCampaignMapOutputPath(Project.Game, Project.MapName);
            if (outputPath == null)
            {
                LoggerViewModel.Log($"Cannot export - no output folder, and no assembly kit path for {Project.Game}, is configured. " +
                                    "Set one in Settings > Preferences.", LogLevel.ErrorMessageBox);
                return null;
            }

            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        public bool ExportProcessedPathfindingData()
        {
            var outputPath = GetOutputPath();
            if (outputPath == null)
            {
                return false;
            }

            // Only pathfinding writes debug files.
            var debugPath = $"{outputPath}debug\\";
            Directory.CreateDirectory(debugPath);
            return PathfindingExporter.Export(Project, outputPath, debugPath);
        }

        public bool ExportProcessedBordersData()
        {
            var outputPath = GetOutputPath();
            return outputPath != null && BordersExporter.Export(Project, outputPath);
        }

        public bool ExportSVGBorders()
        {
            return SVGBordersExporter.Export(Project);
        }

        public bool ExportSVGRoads()
        {
            return SVGRoadExporter.Export(Project);
        }

        public bool ExportProcessedTradeRoutesData()
        {
            var outputPath = GetOutputPath();
            return outputPath != null && TradeRoutesProcessor.Export(Project, outputPath);
        }

        /// <summary>
        /// The output folder for the two steps that build from the saved map.hex, map_data.esf and
        /// dynamic_resources.esf: null, after telling the user, unless a project is open and saved.
        /// </summary>
        private string GetOutputPathForSavedProject()
        {
            if (Project == null)
            {
                LoggerViewModel.Log("No project is open.", LogLevel.ErrorMessageBox);
                return null;
            }

            if (Project.HasUnsavedChanges)
            {
                LoggerViewModel.Log("Please, save the project before trying to process it.", LogLevel.ErrorMessageBox);
                return null;
            }

            return GetOutputPath();
        }

        public bool ProcessMapDataEsf()
        {
            var outputPath = GetOutputPathForSavedProject();
            if (outputPath == null)
            {
                return false;
            }

            return PreferencesViewModel.Instance.IsAssemblyKitIntegrationEnabled
                ? AssemblyKitBuilder.BuildMapData(Project, ToolsPath, outputPath)
                : ProcessMapData.Process(Project, outputPath);
        }

        public bool ProcessDynamicResourcesEsf()
        {
            var outputPath = GetOutputPathForSavedProject();
            if (outputPath == null)
            {
                return false;
            }

            return PreferencesViewModel.Instance.IsAssemblyKitIntegrationEnabled
                ? AssemblyKitBuilder.BuildDynamicResources(Project, ToolsPath, outputPath)
                : ProcessDynamicResources.Process(Project, outputPath);
        }

        public bool GenerateLookupImage()
        {
            var outputPath = GetOutputPath();
            return outputPath != null && LookupImageExporter.Export(Project, outputPath);
        }

        public bool GenerateBaselineTilemap()
        {
            return BaselineTilemapExporter.Export(Project);
        }

        public void ExportLayers(LayerExportMode exportMode, uint mask)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok || string.IsNullOrEmpty(dialog.FileName))
                {
                    return;
                }

                int groundsBit          = 1 << (int)LayerType.GroundTypes;
                int riversBit           = 1 << (int)LayerType.Rivers;
                int climateBit          = 1 << (int)LayerType.Climates;
                int attritionBit        = 1 << (int)LayerType.Attritions;
                int regionsBit          = 1 << (int)LayerType.Regions;
                int regionBordersBit    = 1 << (int)LayerType.RegionBorders;
                int beachesBit          = 1 << (int)LayerType.Beaches;
                int bridgesBit          = 1 << (int)LayerType.Bridges;
                int sprawlBit           = 1 << (int)LayerType.TownSprawl;
                int slotsBit            = 1 << (int)LayerType.TownSlots;
                int roadsBit            = 1 << (int)LayerType.Roads;
                int routesBit           = 1 << (int)LayerType.TradeRoutes;
                int nogoBit             = 1 << (int)LayerType.Impassable;

                var exportPath = $"{dialog.FileName}\\";

                if ((mask & groundsBit) == groundsBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.GroundTypes);
                }

                if ((mask & riversBit) == riversBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Rivers);
                }

                if ((mask & climateBit) == climateBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Climates);
                }

                if ((mask & attritionBit) == attritionBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Attritions);
                }

                if ((mask & regionsBit) == regionsBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Regions);
                }

                if ((mask & regionBordersBit) == regionBordersBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.RegionBorders);
                }

                if ((mask & beachesBit) == beachesBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Beaches);
                }

                if ((mask & bridgesBit) == bridgesBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Bridges);
                }

                if ((mask & sprawlBit) == sprawlBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.TownSprawl);
                }

                if ((mask & slotsBit) == slotsBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.TownSlots);
                }

                if ((mask & roadsBit) == roadsBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Roads);
                }

                if ((mask & routesBit) == routesBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.TradeRoutes);
                }

                if ((mask & nogoBit) == nogoBit)
                {
                    LayerToImageExporter.Export(exportMode, exportPath, Project, LayerType.Impassable);
                }
            }
        }
        #endregion

        #region Helpers

        public static string GetGameNameFromGame(GameTemplate game)
        {
            switch (game)
            {
                case GameTemplate.Rome2:                return "rome2";
                case GameTemplate.Attila:               return "attila";
                case GameTemplate.Thrones_Of_Britannia: return "thrones";
                case GameTemplate.Warhammer:            return "warhammer";
                case GameTemplate.Warhammer2:           return "warhammer2";
                case GameTemplate.Warhammer3:           return "warhammer3";
                case GameTemplate.Troy:                 return "troy";
                case GameTemplate.Three_Kingdoms:       return "three_kingdoms";
                case GameTemplate.Pharaoh:              return "phar";
                case GameTemplate.Pharaoh_Dynasties:    return "phar";
            }

            return null;
        }

        public static GameTemplate GetGameFromName(string name, int minorVersion)
        {
            switch (name)
            {
                case "rome2":           return GameTemplate.Rome2;
                case "attila":          return GameTemplate.Attila;
                case "thrones":         return GameTemplate.Thrones_Of_Britannia;
                case "warhammer":       return GameTemplate.Warhammer;
                case "warhammer2":      return GameTemplate.Warhammer2;
                case "warhammer3":      return GameTemplate.Warhammer3;
                case "troy":            return GameTemplate.Troy;
                case "three_kingdoms":  return GameTemplate.Three_Kingdoms;
            }

            // Pharaoh and Pharaoh Dynasties share the "phar" identifier and are told apart by
            // the map.hex layout: Dynasties is the combined-region one, vanilla Pharaoh a plain 0x12.
            if (name == "phar")
            {
                if (minorVersion == MapHexFile.FAKE_DYNASTIES_MINOR_VER)
                {
                    return GameTemplate.Pharaoh_Dynasties;
                }

                if (minorVersion == GetMapHexVersion(GameTemplate.Pharaoh))
                {
                    return GameTemplate.Pharaoh;
                }
            }

            return GameTemplate.Invalid;
        }

        public static int GetMapHexVersion(GameTemplate game)
        {
            switch (game)
            {
                case GameTemplate.Rome2:
                    return 0x0D;
                case GameTemplate.Attila:
                case GameTemplate.Thrones_Of_Britannia:
                    return 0x0F;
                case GameTemplate.Warhammer:
                case GameTemplate.Warhammer2:
                case GameTemplate.Troy:
                case GameTemplate.Pharaoh:
                    return 0x12;
                case GameTemplate.Pharaoh_Dynasties:
                    return 0x14;
                case GameTemplate.Three_Kingdoms:
                    return 0x13;
                case GameTemplate.Warhammer3:
                    return 0x14;
            }

            return -1;
        }

        #endregion
    }
}
