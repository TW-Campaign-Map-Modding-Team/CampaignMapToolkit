using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace CAIME.Exporters
{
    public static class ProcessMapData
    {
        private const string EXPORTER_NAME = "MapDataBuilder";
        private const long MIN_VALID_MAP_DATA_FILE_SIZE_BYTES = 10 * 1024; // Most maps produce a file of 1 MB or more.

        // MapDataBuilder drives the Assembly Kit's BOB pipeline, which can take minutes on a large
        // map. The bound only exists so a wedged child cannot hang the app forever.
        private const int PROCESS_TIMEOUT_MS = 30 * 60 * 1000;

        public static bool Process(Project project, string toolsDir)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo();

            if (project.Game == GameTemplate.Troy              ||
                project.Game == GameTemplate.Pharaoh           ||
                project.Game == GameTemplate.Pharaoh_Dynasties ||
                project.Game == GameTemplate.Warhammer         ||
                project.Game == GameTemplate.Warhammer2        ||
                project.Game == GameTemplate.Warhammer3        ||
                project.Game == GameTemplate.Three_Kingdoms)
            {
#if DEBUG
                startInfo.FileName = $@"{toolsDir}Debug\{EXPORTER_NAME}.x64.exe";
#else                                                   
                startInfo.FileName = $@"{toolsDir}Release\{EXPORTER_NAME}.x64.exe";
#endif                                     
            }                              
            else                           
            {
#if DEBUG
                startInfo.FileName = $@"{toolsDir}Debug\{EXPORTER_NAME}.exe";
#else
                startInfo.FileName = $@"{toolsDir}Release\{EXPORTER_NAME}.exe";
#endif
            }

            if (File.Exists(startInfo.FileName) == false)
            {
                LoggerViewModel.Log($"{EXPORTER_NAME} not found at {startInfo.FileName}. The CAIME installation is incomplete; please reinstall.", LogLevel.ErrorMessageBox);
                return false;
            }

            var asskitPath                              = PreferencesViewModel.Instance.GetAssKitPath(project.Game).Replace('\\', '/');
            var gameName                                = ProjectManager.GetGameNameFromGame(project.Game);
            var campaignMapName                         = project.MapName;
            var binariesPath                            = $"{asskitPath}/binaries";
            var rawDataPath                             = $"{asskitPath}/raw_data/EmpireDesignData";
            var dbPath                                  = $"{asskitPath}/raw_data/db";
            var workingDataPath                         = $"{asskitPath}/working_data";

            var outputFilePath                          = $"{workingDataPath}/campaign_maps/{campaignMapName}/map_data.esf";

            if (System.Diagnostics.Process.GetProcessesByName("Tweak.AssemblyKit").Length > 0)
            {
                LoggerViewModel.Log("Please close Tweak.AssemblyKit before processing the map data.", LogLevel.ErrorMessageBox);
                return false;
            }

            if (File.Exists(outputFilePath))
            {
                LoggerViewModel.Log($"Deleted existing map_data.esf file.", LogLevel.Info);
                File.Delete(outputFilePath);
            }

            LoggerViewModel.Log($"Starting {startInfo.FileName}", LogLevel.Info);
            LoggerViewModel.Log($"Binaries path: {binariesPath}", LogLevel.Info);

            startInfo.Arguments = $"game={gameName} akit_path=\"{asskitPath}\" campaign_map={campaignMapName} process=map_data";

            var run = ChildProcess.Run(startInfo, PROCESS_TIMEOUT_MS);

            var processStartTime = run.StartTime;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var bobConsoleSpoolsPath = Path.Combine(appDataPath, $"The Creative Assembly\\{gameName}\\console_spools\\").Replace('\\', '/');

            var bobStandardOutput = run.StandardOutput;
            var bobErrorOutput = run.StandardError;

            if (run.TimedOut)
            {
                LoggerViewModel.Log($"{EXPORTER_NAME} did not finish within {PROCESS_TIMEOUT_MS / 60000} minutes and was terminated.", LogLevel.Error);
            }

            var exitCode = run.ExitCode;
            LoggerViewModel.Log($"{EXPORTER_NAME} exited with code {exitCode} ({DescribeExitCode(exitCode)}).", exitCode == 0 ? LogLevel.Info : LogLevel.Error);

            if (Directory.Exists(bobConsoleSpoolsPath))
            {
                var commandFileNamePattern = "Command.txt";
                var errorsFileNamePattern = "Errors and assertions (in order).txt";

                var commandLines = ReadBobSpoolLines(bobConsoleSpoolsPath, commandFileNamePattern);
                var errorsLines = ReadBobSpoolLines(bobConsoleSpoolsPath, errorsFileNamePattern);

                var existsCommandLines = commandLines != null && commandLines.Length > 0;
                var existsErrorLines = errorsLines != null && errorsLines.Length > 0;

                if (existsCommandLines)
                {
                    foreach (var line in commandLines)
                    {
                        LoggerViewModel.Log($"BOB command output: {line}", LogLevel.Info);
                    }
                }

                if (existsErrorLines)
                {
                    foreach (var line in errorsLines)
                    {
                        LoggerViewModel.Log($"BOB errors and assertions output: {line}", LogLevel.Warning);
                    }
                }
            }

            foreach (var outputLine in bobStandardOutput)
            {
                LoggerViewModel.Log(outputLine, LogLevel.Info);
            }

            foreach (var outputLine in bobErrorOutput)
            {
                LoggerViewModel.Log(outputLine, LogLevel.Warning);
            }

            if (exitCode != 0)
            {
                LoggerViewModel.Log($"Skipping map data file validation because {EXPORTER_NAME} reported a failure.", LogLevel.Error);
                return false;
            }

            var mapDataProcessedSuccessfully = false;

            if (File.Exists(outputFilePath) == false)
            {
                LoggerViewModel.Log($"Map data file was not created at {outputFilePath}.", LogLevel.Error);
            }
            else
            {
                var fileInfo = new FileInfo(outputFilePath);
                var isFileLargeEnough = fileInfo.Length > MIN_VALID_MAP_DATA_FILE_SIZE_BYTES;
                // Uses LastWriteTime rather than CreationTime: NTFS "tunneling" reuses the
                // deleted file's original creation time when a same-named file is recreated
                // within ~15 seconds, which is exactly what happens here (see the File.Delete
                // above), so CreationTime cannot be trusted to detect a freshly written file.
                var isFileFreshlyCreated = fileInfo.LastWriteTime >= processStartTime;

                if (isFileLargeEnough == false)
                {
                    LoggerViewModel.Log($"Map data file at {outputFilePath} is only {fileInfo.Length} bytes - expected more than {MIN_VALID_MAP_DATA_FILE_SIZE_BYTES} bytes. Treating export as failed.", LogLevel.Error);
                }
                else if (isFileFreshlyCreated == false)
                {
                    LoggerViewModel.Log($"Map data file at {outputFilePath} was not created during this processing run (last written {fileInfo.LastWriteTime}, processing started {processStartTime}). Treating export as failed.", LogLevel.Error);
                }
                else
                {
                    mapDataProcessedSuccessfully = true;
                }
            }

            if (mapDataProcessedSuccessfully)
            {
                LoggerViewModel.Log($"Processed map data file was exported to {outputFilePath}", LogLevel.Info);
                if (PreferencesViewModel.Instance.IsMapDataConfigPostProcessEnabled)
                {
                    MapDataConfigPostProcessor.PostProcess(project, outputFilePath);
                }
            }
            else
            {
                LoggerViewModel.Log($"Failed to export processed map data file. See console output for details.", LogLevel.Error);
                return false;
            }

            return true;
        }

        // Mirrors the ReturnCodes flags enum in MapDataBuilder/main.cpp. MapDataBuilder can run
        // map_data and dyn_res processing independently in one invocation, so the exit code is a
        // bitwise-OR of whichever flags applied - decode every bit rather than switching on the
        // exact value.
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

        private static string[] ReadBobSpoolLines(string path, string fileNamePattern)
        {
            var matchingFiles = Directory.GetFiles(path, $"*{fileNamePattern}*", SearchOption.TopDirectoryOnly);
            if (matchingFiles.Length > 0)
            {
                try
                {
                    var latestFile = matchingFiles.OrderByDescending(File.GetLastWriteTime).First();
                    return File.ReadAllLines(latestFile);
                }
                catch (Exception ex)
                {
                    LoggerViewModel.Log($"Error reading BOB spool file: {ex.Message}", LogLevel.Error);
                }
            }

            return null;
        }
    }
}
