using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CAIME.Validators;

namespace CAIME
{
    /// <summary>
    /// Headless command-line entry point. Runs the same operations the GUI exposes - processing,
    /// validation, project creation, painting, layer import and export, queries and settings -
    /// without ever creating a WPF <see cref="App"/>, so no window is shown. Invoked from
    /// <see cref="App"/>'s entry point when the executable is started with arguments.
    /// </summary>
    internal static class CliRunner
    {
        /// <summary>
        /// True while a CLI invocation is being handled. Lets shared code (e.g. exporters)
        /// suppress modal dialogs that would otherwise block a headless run.
        /// </summary>
        public static bool IsActive { get; private set; }

        private const int ExitSuccess         = CliConsole.ExitSuccess;
        private const int ExitUsageError      = CliConsole.ExitUsageError;
        private const int ExitProcessingError = CliConsole.ExitProcessingError;

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

        private static readonly CliCommand[] Commands = CreateCommands();

        private static CliCommand[] CreateCommands()
        {
            var commands = new List<CliCommand>
            {
                new CreateCommand(),
                new PaintCommand(),
                new EraseCommand(),
                new LineCommand(),
                new FillCommand(),
                new PickCommand(),
                new FillSwatchesCommand(),
                new ImportLayerCommand(),
                new ExportLayerCommand(),
                new QueryCommand(),
                new ConfigCommand(),
            };

            commands.Add(new BatchCommand(
                name => commands.OfType<MapCommand>().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)),
                () => commands.OfType<MapCommand>().Where(c => (c is BatchCommand) == false).Select(c => c.Name)));

            return commands.ToArray();
        }

        /// <summary>
        /// Parses, validates and executes a CLI invocation. Returns the process exit code.
        /// </summary>
        public static int Run(string[] args)
        {
            IsActive = true;
            ConsoleManager.EnsureConsole();
            LoggerViewModel.ConsoleSink = WriteLog;

            // Recover any Assembly Kit changes left behind by an RPFM session that was killed mid-run.
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

            if (CliConsole.IsHelpFlag(command))
            {
                PrintHelp();
                return ExitSuccess;
            }

            if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
            {
                return args.Length > 1 ? Dispatch(new[] { args[1], "--help" }) : Dispatch(new[] { "--help" });
            }

            if (string.Equals(command, "process", StringComparison.OrdinalIgnoreCase))
            {
                return RunProcess(args.Skip(1).ToArray());
            }

            if (string.Equals(command, "validate", StringComparison.OrdinalIgnoreCase))
            {
                return RunValidate(args.Skip(1).ToArray());
            }

            var cliCommand = Commands.FirstOrDefault(c => string.Equals(c.Name, command, StringComparison.OrdinalIgnoreCase));
            if (cliCommand != null)
            {
                return cliCommand.Run(args.Skip(1).ToArray());
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
                // Deterministically restore any temporary RPFM database changes before returning.
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
                // Deterministically restore any temporary RPFM database changes before returning.
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

                if (CliConsole.IsHelpFlag(arg))
                {
                    PrintHelp();
                    return ExitSuccess;
                }

                if (string.Equals(arg, "--map", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-m", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || CliConsole.IsOption(args[i + 1]))
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

            if (CliPaths.TryResolveMapPath(mapPath, out var fullPath) == false)
            {
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
            sb.AppendLine($"  {exe} <command> [options]");
            sb.AppendLine($"  {exe} <command> --help      Show a command's options and examples.");
            sb.AppendLine();
            sb.AppendLine("COMMANDS:");
            sb.AppendLine($"  {"process".PadRight(16)} Process a campaign map (no window is shown).");
            sb.AppendLine($"  {"validate".PadRight(16)} Validate one or more campaign map layers.");
            foreach (var command in Commands)
            {
                sb.AppendLine($"  {command.Name.PadRight(16)} {command.Summary}");
            }

            sb.AppendLine($"  {"help".PadRight(16)} Show this help, or 'help <command>' for one command.");
            sb.AppendLine();
            sb.AppendLine("PROCESS AND VALIDATE:");
            sb.AppendLine($"  {exe} process --map <path-to-.hex> (--all | <task> [<task> ...])");
            sb.AppendLine($"  {exe} validate --map <path-to-.hex> (--all | <layer> [<layer> ...])");
            sb.AppendLine();
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
            sb.AppendLine("  - The assembly kit path must be configured for the map's game before running");
            sb.AppendLine("    'process' - in the GUI (Settings > Preferences) or with 'config set'.");
            sb.AppendLine("  - --map-data and --dynamic-resources require the project to be saved as");
            sb.AppendLine("    map.hex under <assembly_kit>\\raw_data\\EmpireDesignData\\campaign_maps\\<map>\\.");
            sb.AppendLine("  - Commands that edit a map save it in place unless --output is given.");
            sb.AppendLine("  - Hexes are given as x,y - the X and Y the editor's status bar shows.");
            sb.AppendLine();
            sb.AppendLine("EXIT CODES:");
            sb.AppendLine("  0  success    1  invalid arguments    2  processing failure");
            sb.AppendLine();
            sb.AppendLine("EXAMPLES:");
            sb.AppendLine($"  {exe} process --map \"C:\\maps\\my_map\\map.hex\" --all");
            sb.AppendLine($"  {exe} validate -m map.hex --roads --rivers");
            sb.AppendLine($"  {exe} create --name my_map --game warhammer3 --width 800 --height 600");
            sb.AppendLine($"  {exe} paint -m map.hex --layer regions --swatch my_region --hex 120,45");
            sb.AppendLine($"  {exe} query -m map.hex --hex 120,45");

            Console.Out.Write(sb.ToString());
        }

        private static string FlagName<TTask>(TaskInfo<TTask>[] taskInfos, TTask task)
        {
            return taskInfos.First(t => Equals(t.Task, task)).Flag.TrimStart('-');
        }

        private static string ExeName()
        {
            return CliConsole.ExeName();
        }

        private static void WriteInfo(string message)
        {
            CliConsole.Info(message);
        }

        private static void WriteError(string message)
        {
            CliConsole.Error(message);
        }

        private static void WriteUsageHint()
        {
            CliConsole.UsageHint();
        }

        // Bridges LoggerViewModel output (raised by the exporters) to the console.
        private static void WriteLog(string message, LogLevel level)
        {
            if (level == LogLevel.Error || level == LogLevel.ErrorMessageBox)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                CliConsole.Diagnostic(message);
            }
        }
    }
}
