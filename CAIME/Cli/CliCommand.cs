using System;
using System.Linq;
using System.Text;

namespace CAIME
{
    internal abstract class CliCommand
    {
        public abstract string Name { get; }
        public abstract string Summary { get; }
        public abstract string[] Usage { get; }

        public abstract int Run(string[] args);

        protected abstract void DescribeOptions(StringBuilder help);

        protected virtual void DescribeExamples(StringBuilder help)
        {
        }

        public void PrintHelp()
        {
            var exe  = CliConsole.ExeName();
            var help = new StringBuilder();

            help.AppendLine($"{exe} {Name} - {Summary}");
            help.AppendLine();
            help.AppendLine("USAGE:");
            foreach (var usage in Usage)
            {
                help.AppendLine($"  {exe} {Name} {usage}");
            }

            help.AppendLine();
            help.AppendLine("OPTIONS:");
            DescribeOptions(help);

            var examples = new StringBuilder();
            DescribeExamples(examples);
            if (examples.Length > 0)
            {
                help.AppendLine();
                help.AppendLine("EXAMPLES:");
                foreach (var example in examples.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    help.AppendLine($"  {exe} {Name} {example}");
                }
            }

            Console.Out.Write(help.ToString());
        }

        protected static void Option(StringBuilder help, string option, string description)
        {
            const int optionColumnWidth = 28;

            var lines = description.Split('\n');
            help.AppendLine($"  {option.PadRight(optionColumnWidth)} {lines[0]}");
            foreach (var line in lines.Skip(1))
            {
                help.AppendLine($"  {string.Empty.PadRight(optionColumnWidth)} {line}");
            }
        }

        protected bool WantsHelp(string[] args)
        {
            if (args.Any(CliConsole.IsHelpFlag))
            {
                PrintHelp();
                return true;
            }

            return false;
        }

        protected int UsageError(string message)
        {
            CliConsole.Error(message);
            CliConsole.UsageHint(Name);
            return CliConsole.ExitUsageError;
        }
    }
}
