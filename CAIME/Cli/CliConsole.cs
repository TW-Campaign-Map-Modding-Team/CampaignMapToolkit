using System;
using System.IO;
using System.Reflection;

namespace CAIME
{
    internal static class CliConsole
    {
        public const int ExitSuccess         = 0;
        public const int ExitUsageError      = 1;
        public const int ExitProcessingError = 2;

        public static bool DiagnosticsToStandardError { get; set; }

        public static void Info(string message)
        {
            Console.Out.WriteLine(message);
        }

        public static void Diagnostic(string message)
        {
            var writer = DiagnosticsToStandardError ? Console.Error : Console.Out;
            writer.WriteLine(message);
        }

        public static void Error(string message)
        {
            Console.Error.WriteLine($"error: {message}");
        }

        public static void Warning(string message)
        {
            Console.Error.WriteLine($"warning: {message}");
        }

        public static void UsageHint(string command = null)
        {
            var target = command == null ? "--help" : $"{command} --help";
            Console.Out.WriteLine($"Run '{ExeName()} {target}' for usage.");
        }

        public static bool IsHelpFlag(string s)
        {
            return string.Equals(s, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "/?", StringComparison.Ordinal);
        }

        public static bool IsOption(string s)
        {
            return !string.IsNullOrEmpty(s) && s.StartsWith("-", StringComparison.Ordinal);
        }

        public static string ExeName()
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
    }
}
