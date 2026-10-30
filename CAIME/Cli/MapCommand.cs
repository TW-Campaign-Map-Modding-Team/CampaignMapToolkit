using System.Collections.Generic;
using System.Text;

namespace CAIME
{
    internal abstract class MapCommand : CliCommand
    {
        public const string MapOption    = "--map";
        public const string OutputOption = "--output";

        public virtual bool ModifiesMap => false;

        protected virtual bool PrintsDataToStandardOutput => false;

        protected abstract void DeclareOptions(CliOptions options);

        protected virtual bool ValidateArguments(CliArguments args)
        {
            return true;
        }

        public abstract int Execute(CliSession session, CliArguments args);

        public override int Run(string[] args)
        {
            if (WantsHelp(args))
            {
                return CliConsole.ExitSuccess;
            }

            CliConsole.DiagnosticsToStandardError = PrintsDataToStandardOutput;

            var options = new CliOptions().Value(MapOption, "-m");
            if (ModifiesMap)
            {
                options.Value(OutputOption, "-o");
            }

            DeclareOptions(options);

            if (options.TryParse(args, out var parsed) == false ||
                CliPaths.TryResolveMapPath(parsed.Value(MapOption), out var mapPath) == false ||
                TryResolveOutputPath(parsed, out var outputPath) == false ||
                ValidateArguments(parsed) == false)
            {
                CliConsole.UsageHint(Name);
                return CliConsole.ExitUsageError;
            }

            var session = CliSession.Open(mapPath);
            if (session == null)
            {
                return CliConsole.ExitProcessingError;
            }

            using (session)
            {
                var exitCode = Execute(session, parsed);
                if (exitCode != CliConsole.ExitSuccess || ModifiesMap == false)
                {
                    return exitCode;
                }

                return SaveIfChanged(session, outputPath);
            }
        }

        public bool TryParseForBatch(IReadOnlyList<string> args, out CliArguments parsed)
        {
            var options = new CliOptions();
            DeclareOptions(options);

            return options.TryParse(args, out parsed) && ValidateArguments(parsed);
        }

        public static int SaveIfChanged(CliSession session, string outputPath)
        {
            if (outputPath == null && session.Project.HasUnsavedChanges == false)
            {
                CliConsole.Info("Nothing changed, so the map was not saved.");
                return CliConsole.ExitSuccess;
            }

            return session.Save(outputPath) ? CliConsole.ExitSuccess : CliConsole.ExitProcessingError;
        }

        protected static void DescribeMapOptions(StringBuilder help, bool modifiesMap)
        {
            Option(help, "--map, -m <path>", "Path to the project's map .hex file. Required.");

            if (modifiesMap)
            {
                Option(help, "--output, -o <path>", "Save the edited map to this .hex file instead of\noverwriting the one given to --map.");
            }
        }

        private bool TryResolveOutputPath(CliArguments args, out string outputPath)
        {
            outputPath = null;

            return ModifiesMap == false
                || args.Has(OutputOption) == false
                || CliPaths.TryResolveHexPath(args.Value(OutputOption), "output", out outputPath);
        }
    }
}
