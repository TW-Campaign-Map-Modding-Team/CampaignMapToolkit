using System;
using System.IO;

namespace CAIME
{
    internal static class CliPaths
    {
        public static bool TryResolveMapPath(string mapPath, out string fullPath)
        {
            fullPath = null;

            if (string.IsNullOrEmpty(mapPath))
            {
                CliConsole.Error("Missing required option '--map <path-to-.hex>'.");
                return false;
            }

            if (TryResolveHexPath(mapPath, "map", out fullPath) == false)
            {
                return false;
            }

            if (File.Exists(fullPath) == false)
            {
                CliConsole.Error($"Map file not found: '{fullPath}'.");
                return false;
            }

            return true;
        }

        public static bool TryResolveHexPath(string path, string description, out string fullPath)
        {
            if (TryGetFullPath(path, description, out fullPath) == false)
            {
                return false;
            }

            if (string.Equals(Path.GetExtension(fullPath), ".hex", StringComparison.OrdinalIgnoreCase) == false)
            {
                CliConsole.Error($"The {description} file must be a .hex file: '{fullPath}'.");
                return false;
            }

            return true;
        }

        public static bool TryGetFullPath(string path, string description, out string fullPath)
        {
            try
            {
                fullPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception)
            {
                CliConsole.Error($"Invalid {description} path '{path}'.");
                fullPath = null;
                return false;
            }
        }
    }
}
