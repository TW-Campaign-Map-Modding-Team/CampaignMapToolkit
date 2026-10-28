using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Classes.Importers;

namespace CAIME.Exporters
{
    /// <summary>The campaign_map_playable_areas values map_data.esf is built from.</summary>
    public sealed class MapDataTheatre
    {
        /// <summary>The playable area's index, which map_data.esf stores as the theatre's name.</summary>
        public string   Index   { get; set; }

        /// <summary>The playable area's maxx: the x of the last column of hexes.</summary>
        public float    MaxX    { get; set; }

        /// <summary>
        /// The playable area's maxy, for the games that store it as given (Rome II, see
        /// <see cref="RomeIIMapDataWriter"/>). Left null, the top edge of the hex grid is used, as
        /// every other game's builder does.
        /// </summary>
        public float?   MaxY    { get; set; }
    }

    /// <summary>How a builder decides a region's sea flag (<see cref="MapDataModel.PassableSea"/>).</summary>
    public enum MapDataSeaFlag
    {
        /// <summary>
        /// The region has a sea or bridge area. Worked out from the games' files; it agrees with
        /// <see cref="FirstArea"/> on every vanilla map.
        /// </summary>
        AnySeaArea,

        /// <summary>
        /// The region's first area that is not impassable, row by row, is sea or bridge. Read from
        /// Warhammer III's Assembly Kit DLL.
        /// </summary>
        FirstArea,
    }

    /// <summary>A region's hex rectangle and hex count.</summary>
    public sealed class MapDataHexBounds
    {
        public int MinColumn    { get; set; } = int.MaxValue;
        public int MinRow       { get; set; } = int.MaxValue;
        public int MaxColumn    { get; set; } = int.MinValue;
        public int MaxRow       { get; set; } = int.MinValue;
        public int HexCount     { get; set; }
    }

    /// <summary>
    /// What every game's map_data.esf derives from a map.hex the same way: the grid, world
    /// geometry, areas, region links and bounds. Each game's writer lays these out in its own format.
    /// </summary>
    public sealed class MapDataModel
    {
        public MapHexFile           Map             { get; }
        public MapDataTheatre       Theatre         { get; }
        public MapDataGrid          Grid            { get; }
        public MapDataGeometry      Geometry        { get; }
        public MapDataAreas         Areas           { get; }
        public bool[]               PassableSea     { get; }
        public MapDataRegionLinks   Links           { get; }
        public List<string>         RegionNames     { get; }
        public MapDataHexBounds[]   RegionBounds    { get; }

        public int RegionCount => RegionNames.Count;

        public MapDataModel(MapHexFile map, MapDataTheatre theatre, MapDataRadiusRounding rounding = MapDataRadiusRounding.TwoThirdsPerColumn,
                            MapDataSeaFlag seaFlag = MapDataSeaFlag.AnySeaArea)
        {
            Map         = map;
            Theatre     = theatre;
            RegionNames = map.LandRegions.Concat(map.SeaRegions).ToList();

            CheckEveryHexHasARegion();

            Grid            = new MapDataGrid((int)map.MapWidth, (int)map.MapHeight);
            Geometry        = new MapDataGeometry(theatre.MaxX, map.MapWidth, map.MapHeight, rounding, theatre.MaxY);
            Areas           = new MapDataAreas(map, Grid, Geometry, RegionCount);
            PassableSea     = FindPassableSea(seaFlag);
            Links           = new MapDataRegionLinks(map, Grid, Areas, PassableSea);
            RegionBounds    = FindRegionBounds();
        }

        public bool IsLandRegion(int region) => region < Map.LandRegions.Count;

        /// <summary>
        /// A region's world rectangle is the union of its areas' rectangles, not the rectangle of
        /// its own hex bounds: they differ when every area of a region spans a single column.
        /// </summary>
        public void RegionWorldBounds(int region, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;

            foreach (int areaId in Areas.RegionAreas[region])
            {
                var area = Areas.Areas[areaId];
                Geometry.Bounds(area.MinColumn, area.MinRow, area.MaxColumn, area.MaxRow, out float areaMinX, out float areaMinY, out float areaMaxX, out float areaMaxY);

                minX = Math.Min(minX, areaMinX);
                minY = Math.Min(minY, areaMinY);
                maxX = Math.Max(maxX, areaMaxX);
                maxY = Math.Max(maxY, areaMaxY);
            }
        }

        /// <summary>A region past the end of map.hex's colour list gets <see cref="MapDataContents.UnassignedColour"/>.</summary>
        public uint[] RegionColours()
        {
            return ColoursFor(Map.LandRegions.Count, Map.ColoursLand.Colours)
                .Concat(ColoursFor(Map.SeaRegions.Count, Map.ColoursSea.Colours))
                .ToArray();
        }

        private static IEnumerable<uint> ColoursFor(int regionCount, int[] colours)
        {
            for (int region = 0; region < regionCount; ++region)
            {
                yield return region < colours.Length ? unchecked((uint)colours[region]) : MapDataContents.UnassignedColour;
            }
        }

        private void CheckEveryHexHasARegion()
        {
            int unassigned = Map.HexData.Count(hex => hex.RegionId < 0 || hex.RegionId >= RegionCount);
            if (unassigned > 0)
            {
                throw new InvalidOperationException($"{unassigned} hexes have no region. Paint a region on every hex before processing map_data.esf.");
            }
        }

        /// <summary>The regions' sea flags, from their areas in row-major order of their first hexes.</summary>
        private bool[] FindPassableSea(MapDataSeaFlag seaFlag)
        {
            var passableSea = new bool[RegionCount];
            var decided     = new bool[RegionCount];

            foreach (var area in Areas.Areas)
            {
                bool isSea = area.Type == MapDataAreaType.Sea || area.Type == MapDataAreaType.Bridge;

                if (seaFlag == MapDataSeaFlag.AnySeaArea)
                {
                    passableSea[area.Region] |= isSea;
                }
                else if (decided[area.Region] == false && area.Type != MapDataAreaType.Impassable)
                {
                    passableSea[area.Region]    = isSea;
                    decided[area.Region]        = true;
                }
            }

            return passableSea;
        }

        private MapDataHexBounds[] FindRegionBounds()
        {
            var bounds = new MapDataHexBounds[RegionCount];
            for (int region = 0; region < RegionCount; ++region)
            {
                bounds[region] = new MapDataHexBounds();
            }

            for (int hexIndex = 0; hexIndex < Grid.HexCount; ++hexIndex)
            {
                var b       = bounds[Map.HexData[hexIndex].RegionId];
                int column  = Grid.Column(hexIndex);
                int row     = Grid.Row(hexIndex);

                b.MinColumn = Math.Min(b.MinColumn, column);
                b.MinRow    = Math.Min(b.MinRow, row);
                b.MaxColumn = Math.Max(b.MaxColumn, column);
                b.MaxRow    = Math.Max(b.MaxRow, row);
                ++b.HexCount;
            }

            return bounds;
        }
    }
}
