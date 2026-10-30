using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CAIME.Exporters
{
    /// <summary>
    /// Assembly Kit Integration: builds map_data.esf or dynamic_resources.esf with the Assembly Kit's
    /// own data builder, run by MapDataBuilder, in place of CAIME's writers. The builder works in a
    /// staging folder of CAIME's own holding the saved project files and the tables
    /// <see cref="MapDataBuilderTables"/> makes from the project's database, so only the Assembly
    /// Kit's binaries are used.
    /// </summary>
    internal static class AssemblyKitBuilder
    {
        private const string EXPORTER_NAME = "MapDataBuilder";

        // The data builder can take minutes on a large map. The bound only exists so a wedged child
        // cannot hang the app forever.
        private const int PROCESS_TIMEOUT_MS = 30 * 60 * 1000;

        private static readonly string[] DynamicResourcesInputs = { "dynamic_resources.png", "dynamic_resources_database.xml" };

        public static bool BuildMapData(Project project, string toolsDir, string outputPath)
        {
            if (Build(project, toolsDir, outputPath, "map_data", "map_data.esf", Array.Empty<string>()) == false)
            {
                return false;
            }

            if (PreferencesViewModel.Instance.IsMapDataConfigPostProcessEnabled)
            {
                MapDataConfigPostProcessor.PostProcess(project, Path.Combine(outputPath, "map_data.esf"));
            }

            return true;
        }

        public static bool BuildDynamicResources(Project project, string toolsDir, string outputPath)
        {
            return Build(project, toolsDir, outputPath, "dyn_res", "dynamic_resources.esf", DynamicResourcesInputs);
        }

        private static bool Build(Project project, string toolsDir, string outputPath, string process, string outputFileName, IEnumerable<string> projectInputs)
        {
            var exePath = GetExePath(project.Game, toolsDir);
            if (File.Exists(exePath) == false)
            {
                LoggerViewModel.Log($"{EXPORTER_NAME} not found at {exePath}. The CAIME installation is incomplete; please reinstall.", LogLevel.ErrorMessageBox);
                return false;
            }

            var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(project.Game);
            if (string.IsNullOrEmpty(asskitPath) || Directory.Exists(asskitPath) == false)
            {
                LoggerViewModel.Log($"{outputFileName} was not processed: Assembly Kit Integration needs the Assembly Kit path for {project.Game}. " +
                                    "Set it in Settings > Preferences.", LogLevel.ErrorMessageBox);
                return false;
            }

            if (project.RpfmTables == null)
            {
                LoggerViewModel.Log($"{outputFileName} was not processed: the project's database was not read through RPFM. " +
                                    "Set the RPFM path in Settings > Preferences and reopen the project.", LogLevel.ErrorMessageBox);
                return false;
            }

            var stagingPath = Path.Combine(Path.GetTempPath(), "CAIME", "assembly_kit", Guid.NewGuid().ToString("N"));
            try
            {
                if (Stage(project, stagingPath, projectInputs, outputFileName) == false)
                {
                    return false;
                }

                var workingPath = Path.Combine(stagingPath, "working");
                var startInfo   = new ProcessStartInfo(exePath,
                    $"game={ProjectManager.GetGameNameFromGame(project.Game)} akit_path=\"{ForwardSlashes(asskitPath)}\" campaign_map={project.MapName} process={process} " +
                    $"db_path=\"{ForwardSlashes(stagingPath)}\" design_path=\"{ForwardSlashes(stagingPath)}\" working_path=\"{ForwardSlashes(workingPath)}\"");

                if (Run(startInfo, project.Game) == false)
                {
                    return false;
                }

                var builtFilePath = Path.Combine(workingPath, "campaign_maps", project.MapName, outputFileName);
                if (File.Exists(builtFilePath) == false || new FileInfo(builtFilePath).Length == 0)
                {
                    LoggerViewModel.Log($"{outputFileName} was not processed: the Assembly Kit's builder reported success but wrote nothing.", LogLevel.ErrorMessageBox);
                    return false;
                }

                var outputFilePath = Path.Combine(outputPath, outputFileName);
                Directory.CreateDirectory(outputPath);
                File.Copy(builtFilePath, outputFilePath, overwrite: true);

                LoggerViewModel.Log($"{outputFileName} was built by the Assembly Kit and exported to {outputFilePath}", LogLevel.Info);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LoggerViewModel.Log($"{outputFileName} was not processed: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }
            finally
            {
                DeleteStagingFolder(stagingPath);
            }
        }

        // The data builder reads the tables from both the db and the design folder, and the project's
        // files from <design>/campaign_maps/<map>/.
        private static bool Stage(Project project, string stagingPath, IEnumerable<string> projectInputs, string outputFileName)
        {
            try
            {
                MapDataBuilderTables.Write(MapDataBuilderTables.Build(project.MapName, project.MapWidth, project.MapHeight,
                    project.RpfmTables[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS].Rows), stagingPath);
            }
            catch (InvalidOperationException ex)
            {
                LoggerViewModel.Log($"{outputFileName} was not processed: {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }

            var mapFolder = Path.Combine(stagingPath, "campaign_maps", project.MapName);
            Directory.CreateDirectory(mapFolder);

            // The saved map.hex, not the one open in the editor: saving can reorder its regions.
            File.Copy(project.FileName, Path.Combine(mapFolder, "map.hex"));

            foreach (var input in projectInputs)
            {
                var inputPath = Path.Combine(project.ProjectPath, input);
                if (File.Exists(inputPath) == false)
                {
                    LoggerViewModel.Log($"{outputFileName} was not processed: {inputPath} was not found.", LogLevel.ErrorMessageBox);
                    return false;
                }

                File.Copy(inputPath, Path.Combine(mapFolder, input));
            }

            return true;
        }

        private static bool Run(ProcessStartInfo startInfo, GameTemplate game)
        {
            LoggerViewModel.Log($"Starting {startInfo.FileName} {startInfo.Arguments}", LogLevel.Info);

            var run = ChildProcess.Run(startInfo, PROCESS_TIMEOUT_MS);
            if (run.TimedOut)
            {
                LoggerViewModel.Log($"{EXPORTER_NAME} did not finish within {PROCESS_TIMEOUT_MS / 60000} minutes and was terminated.", LogLevel.Error);
            }

            LogBobSpools(game);
            run.StandardOutput.ForEach(line => LoggerViewModel.Log(line, LogLevel.Info));
            run.StandardError.ForEach(line => LoggerViewModel.Log(line, LogLevel.Warning));

            LoggerViewModel.Log($"{EXPORTER_NAME} exited with code {run.ExitCode} ({DescribeExitCode(run.ExitCode)}).",
                run.ExitCode == 0 ? LogLevel.Info : LogLevel.ErrorMessageBox);
            return run.ExitCode == 0;
        }

        private static string GetExePath(GameTemplate game, string toolsDir)
        {
#if DEBUG
            var configuration = "Debug";
#else
            var configuration = "Release";
#endif
            // Rome II's, Attila's and Thrones of Britannia's data builders are 32-bit DLLs.
            var is32Bit = game == GameTemplate.Rome2 || game == GameTemplate.Attila || game == GameTemplate.Thrones_Of_Britannia;
            return Path.Combine(toolsDir, configuration, is32Bit ? $"{EXPORTER_NAME}.exe" : $"{EXPORTER_NAME}.x64.exe");
        }

        // Mirrors the ReturnCodes flags enum in MapDataBuilder/main.cpp.
        private static string DescribeExitCode(int exitCode)
        {
            if (exitCode == 0)
            {
                return "Success";
            }

            var descriptions = new List<string>();

            if ((exitCode & 1) != 0) descriptions.Add("Missing process name or arguments passed to MapDataBuilder");
            if ((exitCode & 2) != 0) descriptions.Add("Failed to load the game's ToolDataBuilder DLL - check the Assembly Kit path and that its binaries folder contains the DLL");
            if ((exitCode & 4) != 0) descriptions.Add("Map data export function not found in the ToolDataBuilder DLL");
            if ((exitCode & 8) != 0) descriptions.Add("Process trees function not found in the ToolDataBuilder DLL");
            if ((exitCode & 16) != 0) descriptions.Add("Dynamic resources export function not found in the ToolDataBuilder DLL");
            if ((exitCode & 32) != 0) descriptions.Add("The Assembly Kit's map data export function reported failure - check map.hex and the campaign map's region data");
            if ((exitCode & 64) != 0) descriptions.Add("The Assembly Kit's dynamic resources export function reported failure");
            if ((exitCode & 128) != 0) descriptions.Add("Failed to free the ToolDataBuilder DLL");

            return descriptions.Count > 0 ? string.Join("; ", descriptions) : "Unknown error";
        }

        private static void LogBobSpools(GameTemplate game)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var spoolsPath  = Path.Combine(appDataPath, "The Creative Assembly", ProjectManager.GetGameNameFromGame(game), "console_spools");
            if (Directory.Exists(spoolsPath) == false)
            {
                return;
            }

            foreach (var line in ReadLatestSpool(spoolsPath, "Command.txt"))
            {
                LoggerViewModel.Log($"BOB command output: {line}", LogLevel.Info);
            }

            foreach (var line in ReadLatestSpool(spoolsPath, "Errors and assertions (in order).txt"))
            {
                LoggerViewModel.Log($"BOB errors and assertions output: {line}", LogLevel.Warning);
            }
        }

        private static string[] ReadLatestSpool(string spoolsPath, string fileNamePattern)
        {
            var latestFile = Directory.GetFiles(spoolsPath, $"*{fileNamePattern}*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();

            try
            {
                return latestFile == null ? Array.Empty<string>() : File.ReadAllLines(latestFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LoggerViewModel.Log($"Error reading BOB spool file: {ex.Message}", LogLevel.Error);
                return Array.Empty<string>();
            }
        }

        private static void DeleteStagingFolder(string stagingPath)
        {
            try
            {
                if (Directory.Exists(stagingPath))
                {
                    Directory.Delete(stagingPath, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LoggerViewModel.Log($"Could not delete the Assembly Kit staging folder {stagingPath}: {ex.Message}", LogLevel.Warning);
            }
        }

        private static string ForwardSlashes(string path) => path.Replace('\\', '/');
    }
}
