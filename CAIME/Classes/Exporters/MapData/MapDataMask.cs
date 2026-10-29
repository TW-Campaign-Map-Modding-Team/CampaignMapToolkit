using System.Collections.Generic;
using System.Linq;
using CAIME.Classes.Importers;

namespace CAIME.Exporters
{
    /// <summary>
    /// A campaign mask as map_data.esf's MASKED_REGIONS_DATA entry for it needs it. In its entry a
    /// masked hex is impassable before the regions are split into areas, so everything worked out
    /// from the areas is the mask's own: the Warhammer II and Warhammer III builders' output for a
    /// map with a painted mask matches this node for node.
    /// </summary>
    public sealed class MapDataMask
    {
        public string           Name        { get; }

        /// <summary>
        /// The masked hexes as the builder's HEX_MAP_MASK_DATA::generate_mask_data lists them: a
        /// (row, first column, end column) triple for each run of set bits in a bitmap row.
        /// </summary>
        public List<ushort>     Runs        { get; }

        private readonly bool[] maskedHexes;

        private MapDataMask(string name, bool[] maskedHexes, List<ushort> runs)
        {
            Name                = name;
            this.maskedHexes    = maskedHexes;
            Runs                = runs;
        }

        /// <summary>
        /// One for each of the map's campaign masks, in map.hex's order; the unnamed mask alone for
        /// a map.hex that lists none.
        /// </summary>
        public static List<MapDataMask> ForEachMask(MapHexFile map)
        {
            var masks = map.CampaignMasks.Count > 0 ? map.CampaignMasks : new List<CampaignMask> { new CampaignMask(string.Empty) };

            return masks
                .Select(mask => new MapDataMask(mask.Name, MaskedHexes(mask, map.MapWidth, map.MapHeight), MaskRuns(mask.Bitmap, map.MapWidth)))
                .ToList();
        }

        public bool IsMasked(int hexIndex) => maskedHexes[hexIndex];

        /// <summary>A masked hex is impassable whatever it is unmasked.</summary>
        public MapDataAreaType Apply(MapDataAreaType type, int hexIndex)
        {
            return maskedHexes[hexIndex] ? MapDataAreaType.Impassable : type;
        }

        private static bool[] MaskedHexes(CampaignMask mask, uint width, uint height)
        {
            var masked = new bool[width * height];
            for (int row = 0; row < height; ++row)
            {
                for (int column = 0; column < width; ++column)
                {
                    masked[row * width + column] = mask.IsMasked(column, row, width);
                }
            }

            return masked;
        }

        /// <summary>
        /// Every bit of a row is scanned, the padding bits of its last byte included. A run's end is
        /// its first clear column, but a run still open at the end of the row ends at the row's byte
        /// count, not its bit count: the builder's own quirk, reproduced.
        /// </summary>
        private static List<ushort> MaskRuns(byte[] bitmap, uint width)
        {
            var runs        = new List<ushort>();
            int bytesPerRow = CampaignMask.BytesPerRow(width);
            int rows        = bitmap.Length / bytesPerRow;

            for (int row = 0; row < rows; ++row)
            {
                int start = -1;

                for (int bit = 0; bit < bytesPerRow * 8; ++bit)
                {
                    bool set = (bitmap[row * bytesPerRow + bit / 8] & (1 << (bit % 8))) != 0;
                    if (set && start < 0)
                    {
                        start = bit;
                    }
                    else if (set == false && start >= 0)
                    {
                        AddRun(runs, row, start, bit);
                        start = -1;
                    }
                }

                if (start >= 0)
                {
                    AddRun(runs, row, start, bytesPerRow);
                }
            }

            return runs;
        }

        private static void AddRun(List<ushort> runs, int row, int start, int end)
        {
            runs.Add((ushort)row);
            runs.Add((ushort)start);
            runs.Add((ushort)end);
        }
    }
}
