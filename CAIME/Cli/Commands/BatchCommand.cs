using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal sealed class BatchCommand : MapCommand
    {
        private const string FileOption = "--file";

        private sealed class BatchStep
        {
            public int          LineNumber;
            public string       Line;
            public MapCommand   Command;
            public CliArguments Arguments;
        }

        private readonly Func<string, MapCommand> _findCommand;
        private readonly Func<IEnumerable<string>> _commandNames;

        public BatchCommand(Func<string, MapCommand> findCommand, Func<IEnumerable<string>> commandNames)
        {
            _findCommand  = findCommand;
            _commandNames = commandNames;
        }

        public override string Name    => "batch";
        public override string Summary => "Run a file of map commands on one map, loading and saving it once.";
        public override bool ModifiesMap => true;

        public override string[] Usage => new[]
        {
            "--map <path> --file <commands-file>",
        };

        protected override void DeclareOptions(CliOptions options)
        {
            options.Value(FileOption, "-f");
        }

        protected override bool ValidateArguments(CliArguments args)
        {
            if (args.Has(FileOption) == false)
            {
                CliConsole.Error($"Missing required option '{FileOption} <commands-file>'.");
                return false;
            }

            if (File.Exists(args.Value(FileOption)) == false)
            {
                CliConsole.Error($"Commands file not found: '{args.Value(FileOption)}'.");
                return false;
            }

            return true;
        }

        public override int Execute(CliSession session, CliArguments args)
        {
            if (TryReadSteps(args.Value(FileOption), out var steps) == false)
            {
                CliConsole.Error("The commands file has errors, so nothing was run.");
                return CliConsole.ExitUsageError;
            }

            foreach (var step in steps)
            {
                CliConsole.Info($"==> [{step.LineNumber}] {step.Line}");

                var exitCode = step.Command.Execute(session, step.Arguments);
                if (exitCode != CliConsole.ExitSuccess)
                {
                    CliConsole.Error($"Line {step.LineNumber} failed, so the batch stopped and the map was not saved.");
                    return exitCode;
                }
            }

            CliConsole.Info($"Ran {steps.Count} command(s).");
            return CliConsole.ExitSuccess;
        }

        private bool TryReadSteps(string file, out List<BatchStep> steps)
        {
            steps = new List<BatchStep>();
            var lines = File.ReadAllLines(file);
            var valid = true;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var lineNumber = i + 1;
                if (TryParseStep(line, lineNumber, out var step))
                {
                    steps.Add(step);
                }
                else
                {
                    valid = false;
                }
            }

            if (valid && steps.Count == 0)
            {
                CliConsole.Error($"'{file}' has no commands in it.");
                return false;
            }

            return valid;
        }

        private bool TryParseStep(string line, int lineNumber, out BatchStep step)
        {
            step = null;

            if (TryTokenise(line, out var tokens) == false)
            {
                CliConsole.Error($"Line {lineNumber}: a quote is never closed.");
                return false;
            }

            var command = _findCommand(tokens[0]);
            if (command == null || command is BatchCommand)
            {
                CliConsole.Error($"Line {lineNumber}: '{tokens[0]}' can't be used in a batch. Commands: {string.Join(", ", _commandNames())}.");
                return false;
            }

            if (command.TryParseForBatch(tokens.Skip(1).ToList(), out var arguments) == false)
            {
                CliConsole.Error($"Line {lineNumber}: the {command.Name} command above is invalid. Batch lines take no --map or --output.");
                return false;
            }

            step = new BatchStep { LineNumber = lineNumber, Line = line, Command = command, Arguments = arguments };
            return true;
        }

        public static bool TryTokenise(string line, out List<string> tokens)
        {
            tokens = new List<string>();

            var current  = new StringBuilder();
            var inQuotes = false;
            var hasToken = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasToken = true;
                }
                else if (char.IsWhiteSpace(c) && inQuotes == false)
                {
                    if (hasToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasToken = true;
                }
            }

            if (hasToken)
            {
                tokens.Add(current.ToString());
            }

            return inQuotes == false;
        }

        protected override void DescribeOptions(StringBuilder help)
        {
            DescribeMapOptions(help, ModifiesMap);
            Option(help, "--file, -f <commands-file>", "Text file with one command per line, written as on the\n" +
                                                       "command line but without the executable, --map and\n" +
                                                       "--output. Blank lines and lines starting with '#' are\n" +
                                                       "skipped. Quote values that contain spaces.");
            help.AppendLine();
            help.AppendLine("  Every line is checked before any runs. Lines run in order on the same loaded");
            help.AppendLine("  map; if one fails the batch stops and the map is not saved. Commands allowed:");
            help.AppendLine($"  {string.Join(", ", _commandNames())}.");
            help.AppendLine();
            help.AppendLine("  Example commands file:");
            help.AppendLine("    # fill the database's swatches, then paint with them");
            help.AppendLine("    fill-swatches --all");
            help.AppendLine("    paint --layer regions --swatch my_region --hex 10,10 --brush-size 4");
            help.AppendLine("    line --layer roads --from 10,10 --to 60,32");
            help.AppendLine("    fill --layer climates --swatch climate_temperate --hex 10,10 --source-layer regions");
            help.AppendLine("    pick --hex 10,10 --layer regions");
        }

        protected override void DescribeExamples(StringBuilder help)
        {
            help.AppendLine("-m map.hex --file edits.txt");
            help.AppendLine("-m map.hex --file edits.txt --output map_edited.hex");
        }
    }
}
