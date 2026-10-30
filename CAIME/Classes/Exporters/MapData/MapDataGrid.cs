namespace CAIME.Exporters
{
    /// <summary>
    /// A hex's neighbours in the order the Assembly Kit's map_data builder visits them: east, west,
    /// north, south, then the two diagonals. The order decides the order of every neighbour list in
    /// map_data.esf, so it is part of the format.
    ///
    /// <para>
    /// Hexes are flat-topped and odd columns sit half a row higher, so an even column's diagonal
    /// neighbours are a row down and an odd column's a row up. "North" is the next row.
    /// </para>
    /// </summary>
    public sealed class MapDataGrid
    {
        public const int NEIGHBOUR_COUNT = 6;

        /// <summary>The east neighbour, (column + 1, same row). See <see cref="FIRST_NON_EAST_NEIGHBOUR"/>.</summary>
        public const int EAST = 0;

        /// <summary>
        /// The builder's check for cliffs beside a bridge starts its neighbour loop one entry late
        /// and so never looks east. That quirk decides which cliffs are bridge cliffs.
        /// </summary>
        public const int FIRST_NON_EAST_NEIGHBOUR = 1;

        private static readonly int[] EvenColumnOffsets = { 1, 0,  -1, 0,  0, 1,  0, -1,  1, -1,  -1, -1 };
        private static readonly int[] OddColumnOffsets  = { 1, 0,  -1, 0,  0, 1,  0, -1,  1,  1,  -1,  1 };

        public int Width    { get; }
        public int Height   { get; }
        public int HexCount => Width * Height;

        /// <summary>Neighbour indices, <see cref="NEIGHBOUR_COUNT"/> per hex, -1 outside the map.</summary>
        public int[] Neighbours { get; }

        public MapDataGrid(int width, int height)
        {
            Width       = width;
            Height      = height;
            Neighbours  = new int[width * height * NEIGHBOUR_COUNT];

            for (int row = 0; row < height; ++row)
            {
                for (int column = 0; column < width; ++column)
                {
                    var offsets = MapDataGeometry.IsOddColumn(column) ? OddColumnOffsets : EvenColumnOffsets;
                    int first   = (row * width + column) * NEIGHBOUR_COUNT;

                    for (int n = 0; n < NEIGHBOUR_COUNT; ++n)
                    {
                        int c = column + offsets[n * 2];
                        int r = row + offsets[n * 2 + 1];
                        Neighbours[first + n] = c >= 0 && c < width && r >= 0 && r < height ? r * width + c : -1;
                    }
                }
            }
        }

        public int Neighbour(int hexIndex, int n)
        {
            return Neighbours[hexIndex * NEIGHBOUR_COUNT + n];
        }

        public int Column(int hexIndex) => hexIndex % Width;

        public int Row(int hexIndex) => hexIndex / Width;

        /// <summary>
        /// Hex distance on the flat-topped grid, with odd columns half a row higher.
        /// </summary>
        public static int Distance(int c0, int r0, int c1, int r1)
        {
            int z0 = r0 - (c0 >> 1);
            int z1 = r1 - (c1 >> 1);
            int dx = c1 - c0;
            int dz = z1 - z0;
            int dy = -dx - dz;
            return System.Math.Max(System.Math.Abs(dx), System.Math.Max(System.Math.Abs(dy), System.Math.Abs(dz)));
        }
    }
}
