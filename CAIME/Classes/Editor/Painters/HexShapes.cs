using System;
using System.Collections.Generic;

namespace CAIME.Painters
{
    public static class HexShapes
    {
        public static List<int> BrushArea(Hex centre, int brushSize, int width, int height)
        {
            var area   = new List<int>();
            var radius = brushSize - 1;

            // Offset -> cube conversion depends on each hex's own column parity, so it is not
            // translation invariant and cannot be applied to a coordinate delta. Both the centre
            // and the candidate are converted, which is what Hex.GetDistance does.
            var centreCube = Hex.OffsetOddQ_ToCube(centre.Q, centre.R);

            for (int col = centre.Q - radius; col <= radius + centre.Q; col++)
            {
                for (int row = centre.R - radius; row <= radius + centre.R; row++)
                {
                    if (row < 0 || row >= height || col < 0 || col >= width)
                    {
                        continue;
                    }

                    if (Hex.CubeDistance(centreCube, Hex.OffsetOddQ_ToCube(col, row)) <= radius)
                    {
                        area.Add(HexGridUtility.IndexFromCoords(row, col, width));
                    }
                }
            }

            return area;
        }

        public static List<int> LineBetween(Hex src, Hex dst, int width)
        {
            var line = new List<int>();

            int cube_q1 = src.Q;
            int cube_r1 = src.R - (src.Q - (src.Q & 1)) / 2;
            int cube_s1 = -cube_q1 - cube_r1;

            int cube_q2 = dst.Q;
            int cube_r2 = dst.R - (dst.Q - (dst.Q & 1)) / 2;
            int cube_s2 = -cube_q2 - cube_r2;

            int numSteps = Math.Max(Math.Abs(cube_q2 - cube_q1), Math.Max(Math.Abs(cube_r2 - cube_r1), Math.Abs(cube_s2 - cube_s1)));

            float stepSize = 1.0f / numSteps;

            float Lerp(int a, int b, float t)
            {
                return a + (b - a) * t;
            }

            for (int i = 1; i <= numSteps - 1; i++)
            {
                float t = i * stepSize;
                float q_pos = Lerp(cube_q1, cube_q2, t);
                float r_pos = Lerp(cube_r1, cube_r2, t);
                float s_pos = Lerp(cube_s1, cube_s2, t);

                //This part is a special type of rounding
                int q = (int)Math.Round(q_pos);
                int r = (int)Math.Round(r_pos);
                int s = (int)Math.Round(s_pos);

                var q_diff = Math.Abs(q - q_pos);
                var r_diff = Math.Abs(r - r_pos);
                var s_diff = Math.Abs(s - s_pos);

                if (q_diff > r_diff && q_diff > s_diff)
                {
                    q = -(r + s);
                }
                else if (r_diff > s_diff)
                {
                    r = -(q + s);
                }
                else
                {
                    s = -(q + r);
                }

                int col = q;
                int row = r + (q - (q & 1)) / 2;

                line.Add(HexGridUtility.IndexFromCoords(row, col, width));
            }

            return line;
        }

        public static bool TryGetFloodFillMatch(LayerType sourceLayer, out Func<Hex, Hex, bool> match)
        {
            switch (sourceLayer)
            {
                case LayerType.GroundTypes:  match = (hex, nbr) => hex.GroundTypeIndex == nbr.GroundTypeIndex; return true;
                case LayerType.Attritions:   match = (hex, nbr) => hex.AttritionIndex  == nbr.AttritionIndex;  return true;
                case LayerType.Climates:     match = (hex, nbr) => hex.ClimateIndex    == nbr.ClimateIndex;    return true;
                case LayerType.Regions:      match = (hex, nbr) => hex.RegionId        == nbr.RegionId;        return true;
                case LayerType.Impassable:   match = (hex, nbr) => hex.IsImpassable    == nbr.IsImpassable;    return true;
                case LayerType.Roads:        match = (hex, nbr) => hex.IsRoad          == nbr.IsRoad;          return true;
                case LayerType.TradeRoutes:  match = (hex, nbr) => hex.IsTradeRoute    == nbr.IsTradeRoute;    return true;
                case LayerType.Restrictions: match = (hex, nbr) => hex.RestrictionLvl  == nbr.RestrictionLvl;  return true;
            }

            match = null;
            return false;
        }

        public static List<int> FloodRegion(MapHexFile map, int startIndex, Func<Hex, Hex, bool> match)
        {
            var isVisited = new bool[map.Capacity];
            var queue     = new Queue<Hex>();
            var region    = new List<int> { startIndex };

            queue.Enqueue(map.HexData[startIndex]);
            isVisited[startIndex] = true;

            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = map.GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1 || isVisited[nbrIndex])
                    {
                        continue;
                    }

                    var nbr = map.HexData[nbrIndex];
                    if (match(hex, nbr))
                    {
                        region.Add(nbrIndex);
                        queue.Enqueue(nbr);
                        isVisited[nbrIndex] = true;
                    }
                }
            }

            return region;
        }
    }
}
