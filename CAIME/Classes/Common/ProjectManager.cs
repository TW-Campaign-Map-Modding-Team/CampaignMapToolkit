using System;
using System.IO;
using System.Windows;
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

        // Non-null only while an RPFM database source workflow is active for this project. Owns the
        // temporary Assembly Kit changes and is responsible for restoring them (see CleanupRpfmSession).
        public Rpfm.RpfmWorkflowSession RpfmSession { get; set; }

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

        /// <param name="onBeforeDatabaseInit">
        /// Optional hook run once the game is known but before the database is initialised. Returning
        /// false aborts the open. The RPFM workflow uses this to prepare temporary database tables.
        /// </param>
        public bool Open(string filename, Func<GameTemplate, bool> onBeforeDatabaseInit = null)
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

            if (onBeforeDatabaseInit != null && onBeforeDatabaseInit(Game) == false)
            {
                return false;
            }

            var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(Game);
            if (string.IsNullOrEmpty(asskitPath) || Directory.Exists(asskitPath) == false)
            {
                LoggerViewModel.Log(
                    $"No Assembly Kit is configured for {Game}, so database-driven data (ground type names/costs, " +
                    "region names, campaign data) can't be loaded. Opening the map read-only for this session - set " +
                    "the Assembly Kit path in Settings > Preferences and reopen the project for full editing.",
                    LogLevel.Warning);
            }
            else
            {
                try
                {
                    if (Database.Initialise(Game, ProjectPath, MapHexFile) == false)
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LoggerViewModel.Log(
                        $"Could not load database tables for {Game} from the configured Assembly Kit ({ex.Message}). " +
                        "Opening the map read-only for this session.",
                        LogLevel.Warning);
                    Database.ShutDown();
                }
            }

            ColourTable     = new ColourTable(MapHexFile);
            MapHexEditor    = new MapHexEditor(MapHexFile, ColourTable);

            return true;
        }

        /// <summary>
        /// Restores any temporary Assembly Kit changes made by the RPFM workflow and releases the
        /// session. Safe to call when no RPFM session is active.
        /// </summary>
        public void CleanupRpfmSession()
        {
            if (RpfmSession != null)
            {
                RpfmSession.Cleanup();
                RpfmSession = null;
            }
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
        public ProjectEventHandler  OnSaveProject;
        public ProjectEventHandler  OnMapRenamed;
        public RoutedEventHandler   OnCloseProject;
        public RoutedEventHandler   OnMapResized;
        public FilePathEventHandler OnFilePathChanged;

        private string _campaignMapExportPath;
        private string _campaignMapDebugPath;

        public string   RootPath        { get; private set; }
        public string   ProjectsDir     { get; private set; }
        public string   TemplatesPath   { get; private set; }
        public string   ToolsPath       { get; private set; }
        public Project  Project         { get; private set; }

        public bool     IsProjectOpen   => Project != null;

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
        /// Folder containing the native tools (MapDataBuilder), split into Debug and Release subfolders.
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
            return CreateProject(ProjectsDir, mapName, mapWidth, mapHeight, game, template, ConfirmProjectOverwrite);
        }

        public Project CreateProject(string projectsDir, string mapName, uint mapWidth, uint mapHeight, GameTemplate game, string template, Func<bool> canOverwriteExistingProject)
        {
            var newProjectPath = GetProjectPath(projectsDir, mapName);

            if (Directory.Exists(newProjectPath))
            {
                if (canOverwriteExistingProject() == false)
                {
                    return null;
                }

                Directory.Delete(newProjectPath, true);
            }

            Directory.CreateDirectory(newProjectPath);

            if (template == TEMPLATE_NONE)
            {
                var project = Project.CreateNew(newProjectPath, game, mapName, mapWidth, mapHeight);
                if (project.Open($"{newProjectPath}map.hex", g => PrepareRpfmDatabaseIfNeeded(project, g)))
                {
                    return project;
                }

                project.CleanupRpfmSession();
                return null;
            }
            else
            {
                var templatePath = $@"{TemplatesPath}{template}\";

                // Copy files from the template
                foreach (var filePath in Directory.GetFiles(templatePath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(filePath, filePath.Replace(templatePath, newProjectPath), true);
                }

                var mapHexPath = $"{newProjectPath}map.hex";
                if (RenameTemplateMap(mapHexPath, mapName) == false)
                {
                    return null;
                }

                return Open(mapHexPath);
            }
        }

        private bool RenameTemplateMap(string mapHexPath, string mapName)
        {
            var templateProject = new Project();
            try
            {
                if (templateProject.Open(mapHexPath, g => PrepareRpfmDatabaseIfNeeded(templateProject, g)) == false)
                {
                    return false;
                }

                if (templateProject.MapName == mapName)
                {
                    return true;
                }

                var templateMapName = templateProject.MapName;
                templateProject.MapHexFile.RenameMap(mapName);

                if (templateProject.Save(SaveParameters.SaveFlags.MapHexFile, templateProject.MapHexFileName, templateProject.ProjectPath) == false)
                {
                    return false;
                }

                LoggerViewModel.Log($"Campaign map has been renamed from {templateMapName} to {mapName}", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"{mapHexPath} could not be renamed to {mapName}!\n\nReason: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }
            finally
            {
                templateProject.CleanupRpfmSession();
            }
        }

        public static string GetProjectPath(string projectsDir, string mapName)
        {
            return Path.Combine(projectsDir, mapName) + @"\";
        }

        private static bool ConfirmProjectOverwrite()
        {
            var res = MessageBox.Show("Project with the specified name already exists. Overwrite?", "Project name conflict", MessageBoxButton.YesNo);
            return res == MessageBoxResult.Yes;
        }

        public void Save(SaveParameters saveParams)
        {
            var mapHexName  = string.IsNullOrEmpty(saveParams.CustomFileName) ? Project.MapHexFileName  : saveParams.CustomFileName;
            var savePath    = string.IsNullOrEmpty(saveParams.CustomSavePath) ? Project.ProjectPath     : saveParams.CustomSavePath;

            // Auto-saves and backups write to a side file - the project itself still has
            // unsaved changes, so its dirty flag must survive.
            var clearDirtyFlag = !saveParams.IsAutoSave && !saveParams.IsAutoBackup;

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
                if (project.Open(filename, g => PrepareRpfmDatabaseIfNeeded(project, g)) == false)
                {
                    project.CleanupRpfmSession();
                    return null;
                }

                // Release any lingering RPFM session from a previously open project before replacing it.
                Project?.CleanupRpfmSession();

                Project = project;

                // Remembered as the Preferences window's default "Base game" - both immediately (while
                // this project stays open) and, via Save, for future sessions with nothing open yet.
                if (PreferencesViewModel.Instance.SelectedGameIndex != (int)project.Game)
                {
                    PreferencesViewModel.Instance.SelectedGameIndex = (int)project.Game;
                    PreferencesViewModel.Instance.Save();
                }

                var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(project.Game);
                if (string.IsNullOrEmpty(asskitPath))
                {
                    // Without an assembly kit path the export target is unknown - building a path
                    // from an empty string would be rooted and create junk directories at the
                    // drive root. Exporters re-check the path before running.
                    _campaignMapExportPath = null;
                    _campaignMapDebugPath  = null;
                    LoggerViewModel.Log($"No assembly kit path is configured for {project.Game}. " +
                                        "Exports are unavailable until it is set in Settings > Preferences.", LogLevel.Warning);
                }
                else
                {
                    _campaignMapExportPath = $"{asskitPath}\\working_data\\campaign_maps\\{project.MapName}\\";
                    Directory.CreateDirectory(_campaignMapExportPath);

                    _campaignMapDebugPath = $"{_campaignMapExportPath}debug\\";
                    Directory.CreateDirectory(_campaignMapDebugPath);
                }

                var projectLoadedArgs = new ProjectEventArgs(project);
                OnOpenProject?.Invoke(this, projectLoadedArgs);

                return project;
            }
            catch (Exception ex)
            {
                // Roll back any partial RPFM preparation so the Assembly Kit is left untouched.
                project?.CleanupRpfmSession();

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
            // Restore any temporary Assembly Kit changes made by the RPFM workflow.
            Project?.CleanupRpfmSession();

            Project = null;

            OnCloseProject?.Invoke(this, new RoutedEventArgs());
        }

        /// <summary>
        /// When the active database source is RPFM, prepares the required database tables from the
        /// configured vanilla pack, layering the project's modded packs over it when any are recorded.
        /// Returns false to abort opening. For the Assembly Kit source this is a no-op returning true.
        /// </summary>
        private bool PrepareRpfmDatabaseIfNeeded(Project project, GameTemplate game)
        {
            var prefs = PreferencesViewModel.Instance;
            if (prefs.DatabaseSource != DatabaseSource.RPFM)
            {
                return true;
            }

            if (!Rpfm.GameMappingProvider.IsSupported(game))
            {
                LoggerViewModel.Log($"The RPFM database source workflow does not support {game}.", LogLevel.ErrorMessageBox);
                return false;
            }

            var assemblyKitPath = prefs.GetAssKitPath(game);
            if (string.IsNullOrEmpty(assemblyKitPath))
            {
                LoggerViewModel.Log($"No assembly kit path is configured for {game}. Set it in Settings > Preferences.", LogLevel.ErrorMessageBox);
                return false;
            }

            var rpfmFolder = prefs.RpfmPath;
            if (string.IsNullOrEmpty(rpfmFolder))
            {
                LoggerViewModel.Log("The database source is set to RPFM but no RPFM installation path is configured. Set it in Settings > Preferences.", LogLevel.ErrorMessageBox);
                return false;
            }

            // The only hard requirement: without it, there's no RPFM data to read at all, modded pack
            // or otherwise.
            var vanillaPackPath = prefs.GetVanillaPackPath(game);
            if (string.IsNullOrEmpty(vanillaPackPath) || !File.Exists(vanillaPackPath))
            {
                LoggerViewModel.Log(
                    $"No vanilla pack is configured for {game}. Set it in Settings > Preferences (select {game} as " +
                    "the Base game first - the field is per game).", LogLevel.ErrorMessageBox);
                return false;
            }

            // The modded packs are optional and per-project: if the metadata simply doesn't record any
            // (a new project, or one that has never needed a mod override), that's a normal state, not
            // an error - every table just comes from the vanilla pack. Never prompt for them here;
            // they're set deliberately via Settings > RPFM Workflow when wanted.
            var packPaths = Rpfm.MetadataService.GetPackFilePaths(project.ProjectPath);

            try
            {
                var session = new Rpfm.RpfmWorkflowSession(game, assemblyKitPath, rpfmFolder, packPaths, vanillaPackPath, project.MapHexFile);
                session.Prepare();
                project.RpfmSession = session;

                LoggerViewModel.Log("RPFM database tables prepared successfully.", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM database preparation failed: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }
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

        private bool EnsureExportPathAvailable()
        {
            if (string.IsNullOrEmpty(_campaignMapExportPath))
            {
                LoggerViewModel.Log($"Cannot export - no assembly kit path is configured for {Project.Game}. " +
                                    "Set it in Settings > Preferences and reopen the project.", LogLevel.ErrorMessageBox);
                return false;
            }

            return true;
        }

        public bool ExportProcessedPathfindingData()
        {
            return EnsureExportPathAvailable() && PathfindingExporter.Export(Project, _campaignMapExportPath, _campaignMapDebugPath);
        }

        public bool ExportProcessedBordersData()
        {
            return EnsureExportPathAvailable() && BordersExporter.Export(Project, _campaignMapExportPath);
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
            return EnsureExportPathAvailable() && TradeRoutesProcessor.Export(Project, _campaignMapExportPath);
        }

        /// <summary>
        /// Checks the preconditions the two Assembly Kit processing steps share: an open, saved
        /// project and a configured Assembly Kit. On success <paramref name="expectedPath"/> is the
        /// directory the project must live in for the Assembly Kit to find it.
        /// </summary>
        private bool EnsureProjectReadyForProcessing(out string expectedPath)
        {
            expectedPath = null;

            if (Project == null)
            {
                LoggerViewModel.Log("No project is open.", LogLevel.ErrorMessageBox);
                return false;
            }

            if (Project.HasUnsavedChanges)
            {
                LoggerViewModel.Log("Please, save the project before trying to process it.", LogLevel.ErrorMessageBox);
                return false;
            }

            var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(Project.Game);
            if (string.IsNullOrEmpty(asskitPath))
            {
                LoggerViewModel.Log($"Cannot process - no assembly kit path is configured for {Project.Game}. " +
                                    "Set it in Settings > Preferences and reopen the project.", LogLevel.ErrorMessageBox);
                return false;
            }

            expectedPath = $"{asskitPath}\\raw_data\\EmpireDesignData\\campaign_maps\\{Project.MapName}\\";
            return true;
        }

        public bool ProcessMapDataEsf()
        {
            if (EnsureProjectReadyForProcessing(out string expectedPath) == false)
            {
                return false;
            }

            if (string.Equals(expectedPath, Project.ProjectPath) == false)
            {
                LoggerViewModel.Log($"Map Data Process denied - the project has to be named map.hex and located in {expectedPath}.\n" +
                                    $"Current project name is {Project.MapHexFileName}.hex, located in {Project.ProjectPath}", LogLevel.ErrorMessageBox);
                return false;
            }

            return ProcessMapData.Process(Project, ToolsPath);
        }

        public bool ProcessDynamicResourcesEsf()
        {
            if (EnsureProjectReadyForProcessing(out string expectedPath) == false)
            {
                return false;
            }

            if (string.Equals(expectedPath, Project.ProjectPath) == false)
            {
                LoggerViewModel.Log($"Dynamic Resources Process denied - the project has to be named map.hex and located in {expectedPath}.\n" +
                                    $"Current project name is {Project.MapHexFileName}.hex, located in {Project.ProjectPath}", LogLevel.ErrorMessageBox);
                return false;
            }

            return ProcessDynamicResources.Process(Project, ToolsPath);
        }

        public bool GenerateLookupImage()
        {
            return EnsureExportPathAvailable() && LookupImageExporter.Export(Project, _campaignMapExportPath);
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
