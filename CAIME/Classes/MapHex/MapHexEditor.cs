using System;
using System.Windows;

namespace CAIME
{
    public partial class MapHexEditor
    {
        public enum ChangeOperation
        {
            Create,
            Delete,
            Rename
        }

        public MapHexFile   MapHexFile  { get; private set; }
        public ColourTable  ColourTable { get; private set; }

        public MapHexEditor(MapHexFile mapHexFile, ColourTable colourTable)
        {
            MapHexFile  = mapHexFile;
            ColourTable = colourTable;
        }

        /// <summary>
        /// Warns once a list grows past what its per-hex index field can store. The entry is still
        /// added: map.hex keeps any number of names, and only hexes are limited. The lists are kept
        /// sorted, so the entries past the limit are the last ones by name, and saving fails once a
        /// hex uses one of them.
        /// </summary>
        private static void WarnIfPastFileLimit(int count, int indexBits, string entries)
        {
            int limit = (1 << indexBits) - 1;

            if (count > limit)
            {
                LoggerViewModel.Log(
                    $"The map now has {count} {entries}, but hexes can only use the first {limit}. " +
                    $"Painting hexes with the last {count - limit} by name will make the map fail to save.",
                    LogLevel.Warning);
            }
        }
    }
}
