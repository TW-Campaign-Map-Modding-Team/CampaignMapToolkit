using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CAIME.Validators;

namespace CAIME
{
    /// <summary>
    /// Headless command-line entry point. Runs the same map-processing operations exposed
    /// under the GUI's "Process" and "Validate" menus without ever creating a WPF <see cref="App"/>,
    /// so no window is shown. Invoked from <see cref="App"/>'s entry point when the executable is
    /// started with arguments.
    /// </summary>
    internal static class CliRunner
    {
        /// <summary>
        /// True while a CLI invocation is being handled. Lets shared code (e.g. exporters)
        /// suppress modal dialogs that would otherwise block a headless run.
        /// </summary>
        public static bool IsActive { get; private set; }

        private const int ExitSuccess = 0;
        private const int ExitUsageError = 1;
        private const int ExitProcessingError = 2;

        private enum ProcessTask
        {
            Pathfinding,
            Borders,
            MapData,
            DynamicResources,
            TradeRoutes,
            Lookup,
        }

        private enum ValidateTask
        {
            Rivers,
            TownSlots,
            Roads,
            Bridges,
            Beaches,
            Regions,
            Attritions,
            Climates,
            GroundTypes,
            Impassable,
            Sprawl,
        }

        private sealed class TaskInfo<TTask>
        {
            public TTask Task;
            public string Flag;
            public string Description;
        }

        // Order here is the canonical run order used by --all and to keep runs deterministic
        // when several individual tasks are requested.
        private static readonly TaskInfo<ProcessTask>[] Tasks =
        {
            new TaskInfo<ProcessTask> { Task = ProcessTask.MapData,          Flag = "--map-data",          Description = "Process map_data.esf" },
            new TaskInfo<ProcessTask> { Task = ProcessTask.DynamicResources, Flag = "--dynamic-resources", Description = "Process dynamic resources (.esf)" },
            new TaskInfo<ProcessTask> { Task = ProcessTask.Pathfinding,      Flag = "--pathfinding",       Description = "Generate processed pathfinding data (.ppd)" },
            new TaskInfo<ProcessTask> { Task = ProcessTask.Borders,          Flag = "--borders",           Description = "Generate processed borders data (.pbd)" },
            new TaskInfo<ProcessTask> { Task = ProcessTask.TradeRoutes,      Flag = "--trade-routes",      Description = "Generate processed trade routes data (.ptd)" },
            new TaskInfo<ProcessTask> { Task = ProcessTask.Lookup,           Flag = "--lookup",            Description = "Generate lookup and minimap images" },
        };

        // Mirrors the GUI's "Validate" menu order.
        private static readonly TaskInfo<ValidateTask>[] ValidateTasks =
        {
            new TaskInfo<ValidateTask> { Task = ValidateTask.Rivers,      Flag = "--rivers",       Description = "Validate the Rivers layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.TownSlots,   Flag = "--town-slots",   Description = "Validate the Town Slots layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Roads,       Flag = "--roads",        Description = "Validate the Roads layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Bridges,     Flag = "--bridges",      Description = "Validate the Bridges layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Beaches,     Flag = "--beaches",      Description = "Validate the Beaches layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Regions,     Flag = "--regions",      Description = "Validate the Regions layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Attritions,  Flag = "--attritions",   Description = "Validate the Attritions layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Climates,    Flag = "--climates",     Description = "Validate the Climates layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.GroundTypes, Flag = "--ground-types", Description = "Validate the Ground Types layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Impassable,  Flag = "--impassable",   Description = "Validate the Impassable layer" },
            new TaskInfo<ValidateTask> { Task = ValidateTask.Sprawl,      Flag = "--town-sprawl",  Description = "Validate the Town Sprawl layer" },
        };

        /// <summary>
        /// Parses, validates and executes a CLI invocation. Returns the process exit code.
        /// </summary>
        public static int Run(string[] args)
        {
            IsActive = true;
            ConsoleManager.EnsureConsole();
            LoggerViewModel.ConsoleSink = WriteLog;

            // Restore any Assembly Kit an earlier CAIME version's RPFM session left changed when killed mid-run.
            CAIME.Rpfm.RpfmRecoveryJournal.RecoverAll();

            try
            {
                return Dispatch(args);
            }
            catch (Exception ex)
            {
                WriteError($"Unexpected error: {ex.Message}");
                return ExitProcessingError;
            }
            finally
            {
                ConsoleManager.Shutdown();
            }
        }

        // -------------------------------------------------------------------
        // Argument parsing & validation
        // -------------------------------------------------------------------

        private static int Dispatch(string[] args)
        {
            var command = args[0];

            if (IsHelpFlag(command) || string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                return ExitSuccess;
            }

            if (string.Equals(command, "process", StringComparison.OrdinalIgnoreCase))
            {
                return RunProcess(args.Skip(1).ToArray());
            }

            if (string.Equals(command, "validate", StringComparison.OrdinalIgnoreCase))
            {
                return RunValidate(args.Skip(1).ToArray());
            }

            WriteError($"Unknown command '{command}'.");
            WriteUsageHint();
            return ExitUsageError;
        }

        private static int RunProcess(string[] args)
        {
            var exitCode = ParseInvocation(args, Tasks, out var mapPath, out var tasksToRun);
            if (exitCode.HasValue)
            {
                return exitCode.Value;
            }

            if (!TryOpenProject(mapPath, out var projectManager))
            {
                return ExitProcessingError;
            }

            try
            {
                var project = projectManager.Project;
                var asskitPath = PreferencesViewModel.Instance.GetAssKitPath(project.Game);
                if (string.IsNullOrEmpty(asskitPath))
                {
                    WriteError($"No assembly kit path is configured for {project.Game}. " +
                               "Set it once in the GUI (Settings > Preferences) before using the CLI.");
                    return ExitProcessingError;
                }

                WriteInfo($"Map '{project.MapName}' ({project.Game}) loaded. Running {tasksToRun.Count} task(s).");
                return ExecuteTasks(Tasks, tasksToRun, task => RunTask(projectManager, task));
            }
            finally
            {
                projectManager.CloseProject();
            }
        }

        private static int RunValidate(string[] args)
        {
            var exitCode = ParseInvocation(args, ValidateTasks, out var mapPath, out var tasksToRun);
            if (exitCode.HasValue)
            {
                return exitCode.Value;
            }

            if (!TryOpenProject(mapPath, out var projectManager))
            {
                return ExitProcessingError;
            }

            try
            {
                var project = projectManager.Project;
                WriteInfo($"Map '{project.MapName}' ({project.Game}) loaded. Running {tasksToRun.Count} validation(s).");
                return ExecuteTasks(ValidateTasks, tasksToRun, task => RunValidateTask(project, task));
            }
            finally
            {
                projectManager.CloseProject();
            }
        }

        /// <summary>
        /// Parses the shared "--map"/"--all"/task-flag grammar for a command. Returns null to
        /// signal a successful parse (with <paramref name="mapPath"/> and <paramref name="tasksToRun"/>
        /// populated), or an exit code to return immediately (help was printed, or the command
        /// was rejected).
        /// </summary>
        private static int? ParseInvocation<TTask>(string[] args, TaskInfo<TTask>[] taskInfos, out string mapPath, out List<TTask> tasksToRun)
        {
            mapPath = null;
            tasksToRun = null;
            bool all = false;
            var selected = new List<TTask>();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (IsHelpFlag(arg))
                {
                    PrintHelp();
                    return ExitSuccess;
                }

                if (string.Equals(arg, "--map", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-m", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || IsOption(args[i + 1]))
                    {
                        WriteError($"Option '{arg}' requires a path to a .hex file.");
                        return ExitUsageError;
                    }

                    mapPath = args[++i];
                    continue;
                }

                if (string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase))
                {
                    all = true;
                    continue;
                }

                var info = taskInfos.FirstOrDefault(t => string.Equals(t.Flag, arg, StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    if (!selected.Contains(info.Task))
                    {
                        selected.Add(info.Task);
                    }

                    continue;
                }

                WriteError($"Unknown option '{arg}'.");
                WriteUsageHint();
                return ExitUsageError;
            }

            if (string.IsNullOrEmpty(mapPath))
            {
                WriteError("Missing required option '--map <path-to-.hex>'.");
                WriteUsageHint();
                return ExitUsageError;
            }

            if (all && selected.Count > 0)
            {
                WriteError("'--all' cannot be combined with individual task options.");
                return ExitUsageError;
            }

            if (!all && selected.Count == 0)
            {
                WriteError("No tasks specified. Pass --all or at least one task option.");
                WriteUsageHint();
                return ExitUsageError;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(mapPath);
            }
            catch (Exception)
            {
                WriteError($"Invalid map path '{mapPath}'.");
                return ExitUsageError;
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".hex", StringComparison.OrdinalIgnoreCase))
            {
                WriteError($"The map file must be a .hex file: '{fullPath}'.");
                return ExitUsageError;
            }

            if (!File.Exists(fullPath))
            {
                WriteError($"Map file not found: '{fullPath}'.");
                return ExitUsageError;
            }

            // Run in canonical order, restricted to what was requested.
            mapPath = fullPath;
            tasksToRun = taskInfos
                .Where(t => all || selected.Contains(t.Task))
                .Select(t => t.Task)
                .ToList();

            return null;
        }

        // -------------------------------------------------------------------
        // Execution
        // -------------------------------------------------------------------

        private static bool TryOpenProject(string mapHexPath, out ProjectManager projectManager)
        {
            WriteInfo($"Loading map: {mapHexPath}");

            projectManager = new ProjectManager();
            var project = projectManager.Open(mapHexPath);
            if (project == null)
            {
                WriteError("Failed to load the map. See the messages above for details.");
                return false;
            }

            return true;
        }

        private static int ExecuteTasks<TTask>(TaskInfo<TTask>[] taskInfos, List<TTask> tasks, Func<TTask, bool> runTask)
        {
            bool anyFailed = false;
            foreach (var task in tasks)
            {
                var name = FlagName(taskInfos, task);
                WriteInfo($"==> {name}");

                bool ok;
                try
                {
                    ok = runTask(task);
                }
                catch (Exception ex)
                {
                    WriteError($"Task '{name}' threw: {ex.Message}");
                    ok = false;
                }

                if (ok)
                {
                    WriteInfo($"    {name}: OK");
                }
                else
                {
                    WriteError($"{name}: FAILED");
                    anyFailed = true;
                }
            }

            if (anyFailed)
            {
                WriteError("One or more tasks failed.");
                return ExitProcessingError;
            }

            WriteInfo("Done.");
            return ExitSuccess;
        }

        private static bool RunTask(ProjectManager pm, ProcessTask task)
        {
            switch (task)
            {
                case ProcessTask.Pathfinding:      return pm.ExportProcessedPathfindingData();
                case ProcessTask.Borders:          return pm.ExportProcessedBordersData();
                case ProcessTask.MapData:          return pm.ProcessMapDataEsf();
                case ProcessTask.DynamicResources: return pm.ProcessDynamicResourcesEsf();
                case ProcessTask.TradeRoutes:      return pm.ExportProcessedTradeRoutesData();
                case ProcessTask.Lookup:           return pm.GenerateLookupImage();
                default:                           return false;
            }
        }

        private static bool RunValidateTask(Project project, ValidateTask task)
        {
            switch (task)
            {
                case ValidateTask.Rivers:      return RiversValidator.Validate(project);
                case ValidateTask.TownSlots:   return TownSlotsValidator.Validate(project);
                case ValidateTask.Roads:       return RoadsValidator.Validate(project);
                case ValidateTask.Bridges:     return BridgesValidator.Validate(project);
                case ValidateTask.Beaches:     return BeachesValidator.Validate(project);
                case ValidateTask.Regions:     return RegionsValidator.Validate(project);
                case ValidateTask.Attritions:  return AttritionsValidator.Validate(project);
                case ValidateTask.Climates:    return ClimatesValidator.Validate(project);
                case ValidateTask.GroundTypes: return GroundTypesValidator.Validate(project);
                case ValidateTask.Impassable:  return ImpassableValidator.Validate(project);
                case ValidateTask.Sprawl:      return SprawlValidator.Validate(project);
                default:                       return false;
            }
        }

        // -------------------------------------------------------------------
        // Help & output helpers
        // -------------------------------------------------------------------

        private static void PrintHelp()
        {
            var exe = ExeName();
            var sb = new StringBuilder();

            sb.AppendLine($"Campaign Map Toolkit (CAIME) {AppInfo.Version} - command line interface");
            sb.AppendLine();
            sb.AppendLine("USAGE:");
            sb.AppendLine($"  {exe} process --map <path-to-.hex> (--all | <task> [<task> ...])");
            sb.AppendLine($"  {exe} validate --map <path-to-.hex> (--all | <layer> [<layer> ...])");
            sb.AppendLine($"  {exe} --help");
            sb.AppendLine();
            sb.AppendLine("COMMANDS:");
            sb.AppendLine("  process              Process a campaign map (no window is shown).");
            sb.AppendLine("  validate             Validate one or more campaign map layers.");
            sb.AppendLine("  help, --help, -h     Show this help and exit.");
            sb.AppendLine();
            sb.AppendLine("OPTIONS:");
            sb.AppendLine("  --map, -m <path>     Path to the project's map .hex file. Required.");
            sb.AppendLine("  --all                Run every task below, in a sensible order.");
            sb.AppendLine();
            sb.AppendLine("PROCESS TASKS (mirror the GUI 'Process' menu):");
            foreach (var t in Tasks)
            {
                sb.AppendLine($"  {t.Flag.PadRight(20)} {t.Description}");
            }

            sb.AppendLine();
            sb.AppendLine("VALIDATE LAYERS (mirror the GUI 'Validate' menu):");
            foreach (var t in ValidateTasks)
            {
                sb.AppendLine($"  {t.Flag.PadRight(20)} {t.Description}");
            }

            sb.AppendLine();
            sb.AppendLine("NOTES:");
            sb.AppendLine("  - The assembly kit path must already be configured per game in the GUI");
            sb.AppendLine("    (Settings > Preferences) before running 'process'.");
            sb.AppendLine("  - --map-data and --dynamic-resources require the project to be saved as");
            sb.AppendLine("    map.hex under <assembly_kit>\\raw_data\\EmpireDesignData\\campaign_maps\\<map>\\.");
            sb.AppendLine();
            sb.AppendLine("EXIT CODES:");
            sb.AppendLine("  0  success    1  invalid arguments    2  processing failure");
            sb.AppendLine();
            sb.AppendLine("EXAMPLES:");
            sb.AppendLine($"  {exe} process --map \"C:\\maps\\my_map\\map.hex\" --all");
            sb.AppendLine($"  {exe} process -m map.hex --pathfinding --trade-routes");
            sb.AppendLine($"  {exe} validate -m map.hex --roads --rivers");

            Console.Out.Write(sb.ToString());
        }

        private static string FlagName<TTask>(TaskInfo<TTask>[] taskInfos, TTask task)
        {
            return taskInfos.First(t => Equals(t.Task, task)).Flag.TrimStart('-');
        }

        private static string ExeName()
        {
            try
            {
                return Path.GetFileName(Assembly.GetEntryAssembly()?.Location) ?? "CAIME.exe";
            }
            catch
            {
                return "CAIME.exe";
            }
        }

        private static bool IsHelpFlag(string s)
        {
            return string.Equals(s, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "/?", StringComparison.Ordinal);
        }

        private static bool IsOption(string s)
        {
            return !string.IsNullOrEmpty(s) && s.StartsWith("-", StringComparison.Ordinal);
        }

        private static void WriteInfo(string message)
        {
            Console.Out.WriteLine(message);
        }

        private static void WriteError(string message)
        {
            Console.Error.WriteLine($"error: {message}");
        }

        private static void WriteUsageHint()
        {
            Console.Out.WriteLine($"Run '{ExeName()} --help' for usage.");
        }

        // Bridges LoggerViewModel output (raised by the exporters) to the console.
        private static void WriteLog(string message, LogLevel level)
        {
            var writer = (level == LogLevel.Error || level == LogLevel.ErrorMessageBox)
                ? Console.Error
                : Console.Out;

            writer.WriteLine(message);
        }
    }
}
