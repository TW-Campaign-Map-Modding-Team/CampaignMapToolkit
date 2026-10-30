using System;

namespace CAIME.Exporters
{
    /// <summary>How a game's map_data builder works out the hex radius, in floats.</summary>
    public enum MapDataRadiusRounding
    {
        /// <summary>Warhammer III and Pharaoh: maxx * ((2 / 3) / (columns - 1)).</summary>
        TwoThirdsPerColumn,

        /// <summary>Attila: maxx / ((columns - 1) * 1.5).</summary>
        PerColumnAndAHalf,
    }

    /// <summary>
    /// Where the map_data builder puts a hex in world space. Hexes are flat-topped: column 0's
    /// centres lie on x = 0 and the last column's on x = the playable area's maxx, and odd columns
    /// sit half a row higher than even ones.
    ///
    /// <para>
    /// Every value is a float worked out in the same steps as the Assembly Kit's builder, because
    /// map_data.esf stores them and a different rounding changes the file. The hex radius is maxx
    /// times ((2 / 3) / (columns - 1)); maxx / (columns - 1) / 1.5 differs by a unit in the last
    /// place for some maps, such as wh3_main_chaos_map_4. Attila's builder computes it as
    /// maxx / ((columns - 1) * 1.5) instead.
    /// </para>
    /// </summary>
    public sealed class MapDataGeometry
    {
        public float Radius         { get; }
        public float ColumnSpacing  { get; }
        public float RowSpacing     { get; }
        public float HalfRow        { get; }
        public float MaxX           { get; }
        public float MaxY           { get; }

        public MapDataGeometry(float maxX, uint columns, uint rows, MapDataRadiusRounding rounding = MapDataRadiusRounding.TwoThirdsPerColumn, float? maxY = null)
        {
            if (columns < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), "A map needs at least two columns of hexes.");
            }

            Radius              = rounding == MapDataRadiusRounding.TwoThirdsPerColumn
                                ? (float)(maxX * (float)((2f / 3f) / (float)(columns - 1)))
                                : (float)(maxX / (float)((float)(columns - 1) * 1.5f));
            ColumnSpacing       = (float)(1.5f * Radius);
            RowSpacing          = (float)((float)Math.Sqrt(3f) * Radius);
            HalfRow             = (float)(RowSpacing / 2f);
            MaxX                = maxX;
            MaxY                = maxY ?? (float)((float)(rows + 0.5f) * RowSpacing);
        }

        public float CentreX(int column)
        {
            return (float)(column * ColumnSpacing);
        }

        public float CentreY(int column, int row)
        {
            float y = (float)(row * RowSpacing);
            return IsOddColumn(column) ? (float)(y + HalfRow) : y;
        }

        /// <summary>
        /// The world rectangle covering every hex of the hex rectangle [c0, c1] x [r0, r1], as the
        /// builder writes it for regions and areas. A span of more than one column holds hexes of
        /// both column parities, so it reaches down to the even columns' bottom edge and up to the
        /// odd columns' top edge.
        /// </summary>
        public void Bounds(int c0, int r0, int c1, int r1, out float minX, out float minY, out float maxX, out float maxY)
        {
            bool hasEven = c0 != c1 || IsOddColumn(c0) == false;
            bool hasOdd  = c0 != c1 || IsOddColumn(c0);

            minX = (float)(CentreX(c0) - Radius);
            maxX = (float)(CentreX(c1) + Radius);
            minY = hasEven ? (float)((float)(r0 * RowSpacing) - HalfRow) : (float)((float)((float)(r0 * RowSpacing) + HalfRow) - HalfRow);
            maxY = hasOdd  ? (float)((float)((float)(r1 * RowSpacing) + HalfRow) + HalfRow) : (float)((float)(r1 * RowSpacing) + HalfRow);
        }

        public static bool IsOddColumn(int column)
        {
            return (column & 1) == 1;
        }
    }
}
