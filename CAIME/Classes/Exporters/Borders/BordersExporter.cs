using Force.Crc32;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace CAIME
{
    // TODO:
    // 1. Add user dialog for selecting which region edges to export as borders 🗸
    // 2. Improve border part facing determination 🗸
    // 3. Figure out how borders work in Troy 🗸
    // 4. Providing more information about modifying borders (e.g. where to find the textures / materials used), do some more research on it

    /// <summary>
    /// Derives region border parts from a map.hex file and writes the processed
    /// <c>borders.pbd</c> file (a CRC32-terminated PBD container of border parts and points).
    /// </summary>
    public sealed class BordersExporter
    {
        private readonly MapHexFile  _mapHexFile;
        private readonly List<Hex>[] _usedForBorderTo;

        public List<BorderPart> Borders { get; set; }

        public BordersExporter(MapHexFile mapHexFile)
        {
            _mapHexFile      = mapHexFile;
            _usedForBorderTo = new List<Hex>[mapHexFile.Capacity];
            Borders          = new List<BorderPart>();
        }

        // ====================================================================
        // Public entry point
        // ====================================================================

        public static bool Export(Project project, string exportPath)
        {
            if (project.Game == GameTemplate.Troy ||
                project.Game == GameTemplate.Pharaoh ||
                project.Game == GameTemplate.Pharaoh_Dynasties)
            {
                const string note = "In Troy and Pharaoh, borders are rendered from region edges directly (map_data.esf). The generated borders.pbd file won't affect the borders.";

                // Avoid a modal dialog that would block a headless CLI run.
                if (CliRunner.IsActive)
                {
                    LoggerViewModel.Log(note, LogLevel.Warning);
                }
                else
                {
                    MessageBox.Show(note);
                }
            }

#if DEBUG
            var profiler = new ExecutionTimeProfiler();
#endif

            BorderExportOptionsViewModel.SeedGameDefaultsIfUnset(project);

            GenerateRegionEdges(project.MapHexFile);

            var exporter = new BordersExporter(project.MapHexFile);
            exporter.GenerateBorders();
            exporter.FilterBorders();
            exporter.Borders = exporter.Borders.OrderBy(x => x.regFrom).ThenBy(y => y.regTo).ToList();

            exportPath = $"{exportPath}display\\borders\\";
            Directory.CreateDirectory(exportPath);
            exporter.Write($"{exportPath}borders.pbd");

#if DEBUG
            profiler.Stop();
#endif

            LoggerViewModel.Log("Processed borders data has been successfully exported.", LogLevel.Info);

            return true;
        }

        public static void GenerateRegionEdges(MapHexFile mapHexFile)
        {
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                mapHexFile.HexData[hexIndex].IsBorder = false;
                Hex hex = mapHexFile.GetHex(hexIndex);
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                {
                    Hex neighbour = mapHexFile.GetNeighbour(hex, dir);
                    if (neighbour == null)
                        continue;

                    if (hex.RegionId != neighbour.RegionId)
                    {
                        mapHexFile.HexData[hexIndex].IsBorder = true;
                    }
                }
            }
        }

        private void GenerateBorders()
        {
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; hexIndex++)
            {
                Hex hex = _mapHexFile.HexData[hexIndex];
                if (!hex.IsBorder)
                    continue;

                HashSet<int> adjRegions = new HashSet<int>();
                List<Hex> adjHexes = new List<Hex>();
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                {
                    Hex neighbour = _mapHexFile.GetNeighbour(hex, dir);
                    if (neighbour == null)
                        continue;
                    if (hex.RegionId != neighbour.RegionId)
                    {
                        adjRegions.Add(neighbour.RegionId);
                        adjHexes.Add(neighbour);
                    }
                }
                if (adjRegions.Count < 2)
                    continue;

                //If at least two other regions adjacent to "hex" -> starting point for border or point next to it
                List<Hex> startingPoints = new List<Hex>();
                foreach (Hex candidate in adjHexes)
                {
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                    {
                        Hex neighbour = _mapHexFile.GetNeighbour(candidate, dir);
                        if (neighbour == null)
                            continue;

                        if ((candidate.RegionId != neighbour.RegionId) && (adjHexes.IndexOf(neighbour) != -1)) //"Canditate" adjacent to same (third) region as "hex"
                            startingPoints.Add(candidate);
                    }
                }
                foreach (Hex startingPoint in startingPoints)
                {
                    //Don't check whether border should be generated here (using sprawl data), otherwise circular border generation would have to make a lot of checks
                    int startPIndex = _mapHexFile.GetHexIndex(startingPoint);
                    if ((_usedForBorderTo[startPIndex] != null) && _usedForBorderTo[startPIndex].IndexOf(hex) != -1)
                        continue;

                    CalculateBorder(startingPoint, hex);
                }
            }

            //Circular borders
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; hexIndex++)
            {
                Hex hex = _mapHexFile.GetHex(hexIndex);
                if (!hex.IsBorder || _usedForBorderTo[hexIndex] != null)
                    continue;

                Hex neighbour = null;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
                {
                    Hex candidate = _mapHexFile.GetNeighbour(hex, dir);
                    if (candidate == null)
                        continue;

                    if (hex.RegionId != candidate.RegionId)
                    {
                        neighbour = candidate;
                        break;
                    }
                }
                if (neighbour == null) //no differing-region neighbour -> not actually a border hex
                    continue;

                CalculateBorder(hex, neighbour);
            }
        }

        private void CalculateBorder(Hex startingPoint, Hex firstComplementary)
        {
            int regionFrom = startingPoint.RegionId;
            int regionTo = firstComplementary.RegionId;
            BorderPart part = new BorderPart(regionFrom, regionTo);
            BorderPart compPart = new BorderPart(regionTo, regionFrom);

            Hex hex1 = startingPoint;
            Hex hex2 = firstComplementary;

            Hex prevHex1 = null;
            Hex prevHex2 = null;

            List<BorderPoint> points1 = part.BorderPoints;
            List<BorderPoint> points2 = compPart.BorderPoints;

            bool reachedEnd = false;

            // Every step consumes at least one hex edge, so a walk longer than the map has edges
            // means the border data is malformed and the loop would never terminate.
            long stepsRemaining = _mapHexFile.Capacity * HexGridUtility.NEIGHBOURS_COUNT;

            while (!reachedEnd)
            {
                if (--stepsRemaining < 0)
                {
                    LoggerViewModel.Log(
                        $"BordersExporter: abandoned the border walk between regions {regionFrom} and {regionTo} " +
                        "after it failed to close. The exported borders for this region pair are incomplete.",
                        LogLevel.Error);
                    break;
                }

                List<Hex> hex1Reg1Hexes;
                List<Hex> hex1Reg2Hexes;
                GetNeighbours(hex1, hex1.RegionId, hex2.RegionId, out hex1Reg1Hexes, out hex1Reg2Hexes);

                List<Hex> hex2Reg1Hexes;
                List<Hex> hex2Reg2Hexes;
                GetNeighbours(hex2, hex1.RegionId, hex2.RegionId, out hex2Reg1Hexes, out hex2Reg2Hexes);

                List<Hex> sharedReg1 = hex1Reg1Hexes.Intersect(hex2Reg1Hexes).ToList();
                sharedReg1.Remove(prevHex1);

                int index = _mapHexFile.GetHexIndex(hex1);
                if (_usedForBorderTo[index] == null)
                    _usedForBorderTo[index] = new List<Hex>();
                _usedForBorderTo[index].Add(hex2);

                index = _mapHexFile.GetHexIndex(hex2);
                if (_usedForBorderTo[index] == null)
                    _usedForBorderTo[index] = new List<Hex>();
                _usedForBorderTo[index].Add(hex1);

                if (points1.Count != 0 && points2.Count != 0 && points1[0] == hex1 && points2[0] == hex2)
                {
                    if (points1.Last() == hex1 && points1.Count > 1)
                        points1.RemoveAt(points1.Count - 1);
                    if (points2.Last() == hex2 && points2.Count > 1)
                        points2.RemoveAt(points2.Count - 1);

                    reachedEnd = true;
                }
                else if (sharedReg1.Count == 0) // don't swap from and to
                {
                    List<Hex> sharedReg2 = hex1Reg2Hexes.Intersect(hex2Reg2Hexes).ToList();
                    sharedReg2.Remove(prevHex2);

                    points2.Add((BorderPoint)hex2);
                    if (sharedReg2.Count != 0)
                    {
                        int hexIndex = _mapHexFile.GetHexIndex(hex1);
                        if (_usedForBorderTo[hexIndex].Count > 5)
                        {
                            HashSet<Hex> nonDuplicates = _usedForBorderTo[hexIndex].ToHashSet();
                            if (nonDuplicates.Count == 6) //end of circular border part around a single hex
                            {
                                points1.Add((BorderPoint)hex1);
                                break;
                            }
#if DEBUG
                            else if (nonDuplicates.Count > 6)
                            {
                                MessageBox.Show("Something went wrong");
                            }
#endif
                        }
                        //hex1 = hex1
                        //prevHex1 = prevHex1
                        prevHex2 = hex2;
                        hex2 = sharedReg2[0];
                        //points1 = points1
                        //points2 = points2
                        //Step(hex1, prevHex1, sharedReg2[0], hex2, points1, points2);
                    }
                    else // end of regular border part
                    {
                        points1.Add((BorderPoint)hex1);
                        reachedEnd = true;
                    }
                }
                else //swap from and to
                {
                    points1.Add((BorderPoint)hex1);

                    prevHex2 = hex1;
                    hex1 = hex2;
                    prevHex1 = prevHex2;
                    hex2 = sharedReg1[0];
                    List<BorderPoint> helper = points1;
                    points1 = points2;
                    points2 = helper;
                    //Step(hex2, prevHex2, sharedReg1[0], hex1, points2, points1);
                }
            }
            DetermineFacing(part, compPart);
            if (part.BorderPoints.Count > 1)
                Borders.Add(part);
            if (compPart.BorderPoints.Count > 1)
                Borders.Add(compPart);
        }

        private void GetNeighbours(Hex hex, int region1_ID, int region2_ID, out List<Hex> reg1Hexes, out List<Hex> reg2Hexes)
        {
            reg1Hexes = new List<Hex>();
            reg2Hexes = new List<Hex>();
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; dir++)
            {
                Hex neighbour = _mapHexFile.GetNeighbour(hex, dir);
                if (neighbour == null)
                    continue;

                if (neighbour.RegionId == region1_ID)
                    reg1Hexes.Add(neighbour);
                else if (neighbour.RegionId == region2_ID)
                    reg2Hexes.Add(neighbour);
            }
        }

        private static void DetermineFacing(BorderPart part1, BorderPart part2)
        {
            double distanceA = 0;
            double distanceB = 0;
            if (part1.BorderPoints.Count == 1)
            {
                if (part2.BorderPoints.Count == 1)
                    return;

                Vector2 point1 = new Vector2(part1.BorderPoints[0].X, part1.BorderPoints[0].Y);

                //part2.BordersPoints inverted
                Vector2 vec2 = new Vector2(part2.BorderPoints[0].X - part2.BorderPoints[1].X, part2.BorderPoints[0].Y - part2.BorderPoints[1].Y);
                Vector2 rightVec2 = new Vector2(vec2.Y, -vec2.X);
                Vector2 point2 = new Vector2(part2.BorderPoints[0].X, part2.BorderPoints[0].Y) + 5 * rightVec2;
                distanceA = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));

                //part2.BordersPoints not inverted
                rightVec2 = rightVec2 * -1;
                point2 = new Vector2(part2.BorderPoints[1].X, part2.BorderPoints[1].Y) + 5 * rightVec2;

                distanceB = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
            }
            else if (part2.BorderPoints.Count == 1)
            {
                Vector2 point2 = new Vector2(part2.BorderPoints[0].X, part2.BorderPoints[0].Y);

                //part1.BordersPoints inverted
                Vector2 vec1 = new Vector2(part1.BorderPoints[0].X - part1.BorderPoints[1].X, part1.BorderPoints[0].Y - part1.BorderPoints[1].Y);
                Vector2 rightVec1 = new Vector2(vec1.Y, -vec1.X);
                Vector2 point1 = new Vector2(part1.BorderPoints[0].X, part1.BorderPoints[0].Y) + 5 * rightVec1;
                distanceA = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));

                //part1.BordersPoints not inverted
                rightVec1 = rightVec1 * -1;
                point1 = new Vector2(part1.BorderPoints[1].X, part1.BorderPoints[1].Y) + 5 * rightVec1;

                distanceB = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
            }
            else
            {
                Vector2 vec1 = new Vector2(part1.BorderPoints[1].X - part1.BorderPoints[0].X, part1.BorderPoints[1].Y - part1.BorderPoints[0].Y);
                Vector2 rightVec1 = new Vector2(vec1.Y, -vec1.X);
                Vector2 point1 = new Vector2(part1.BorderPoints[1].X, part1.BorderPoints[1].Y) + 5 * rightVec1;

                //part2.BordersPoints inverted
                Vector2 vec2 = new Vector2(part2.BorderPoints[0].X - part2.BorderPoints[1].X, part2.BorderPoints[0].Y - part2.BorderPoints[1].Y);
                Vector2 rightVec2 = new Vector2(vec2.Y, -vec2.X);
                Vector2 point2 = new Vector2(part2.BorderPoints[0].X, part2.BorderPoints[0].Y) + 5 * rightVec2;

                distanceA = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));

                rightVec1 = rightVec1 * -1;
                //part1.BorderPoints inverted
                point1 = new Vector2(part1.BorderPoints[0].X, part1.BorderPoints[0].Y) + 5 * rightVec1;

                rightVec2 = rightVec2 * -1;
                point2 = new Vector2(part2.BorderPoints[1].X, part2.BorderPoints[1].Y) + 5 * rightVec2;

                distanceB = Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
            }

            if (distanceA > distanceB)
                part2.BorderPoints.Reverse();
            else
                part1.BorderPoints.Reverse();
        }

        /// <summary>
        /// How a region is classified for border export. Land and sea are each split by whether the
        /// region carries town sprawl data; TI and MI are the sprawl-less variants.
        /// </summary>
        private enum RegionKind
        {
            Land = 0,
            TI   = 1,
            Sea  = 2,
            MI   = 3,
        }

        /// <summary>
        /// The user's per-pair export choices as a [from, to] table, replacing a 16-way if/else.
        /// The options are set per game template at run time, so the table is built per export.
        /// </summary>
        private static BorderExportOption[,] BuildExportOptionTable()
        {
            var table = new BorderExportOption[4, 4];

            table[(int)RegionKind.Land, (int)RegionKind.Land] = BorderExportOptionsViewModel.Land_Land;
            table[(int)RegionKind.Land, (int)RegionKind.TI  ] = BorderExportOptionsViewModel.Land_TI;
            table[(int)RegionKind.Land, (int)RegionKind.Sea ] = BorderExportOptionsViewModel.Land_Sea;
            table[(int)RegionKind.Land, (int)RegionKind.MI  ] = BorderExportOptionsViewModel.Land_MI;

            table[(int)RegionKind.TI,   (int)RegionKind.Land] = BorderExportOptionsViewModel.TI_Land;
            table[(int)RegionKind.TI,   (int)RegionKind.TI  ] = BorderExportOptionsViewModel.TI_TI;
            table[(int)RegionKind.TI,   (int)RegionKind.Sea ] = BorderExportOptionsViewModel.TI_Sea;
            table[(int)RegionKind.TI,   (int)RegionKind.MI  ] = BorderExportOptionsViewModel.TI_MI;

            table[(int)RegionKind.Sea,  (int)RegionKind.Land] = BorderExportOptionsViewModel.Sea_Land;
            table[(int)RegionKind.Sea,  (int)RegionKind.TI  ] = BorderExportOptionsViewModel.Sea_TI;
            table[(int)RegionKind.Sea,  (int)RegionKind.Sea ] = BorderExportOptionsViewModel.Sea_Sea;
            table[(int)RegionKind.Sea,  (int)RegionKind.MI  ] = BorderExportOptionsViewModel.Sea_MI;

            table[(int)RegionKind.MI,   (int)RegionKind.Land] = BorderExportOptionsViewModel.MI_Land;
            table[(int)RegionKind.MI,   (int)RegionKind.TI  ] = BorderExportOptionsViewModel.MI_TI;
            table[(int)RegionKind.MI,   (int)RegionKind.Sea ] = BorderExportOptionsViewModel.MI_Sea;
            table[(int)RegionKind.MI,   (int)RegionKind.MI  ] = BorderExportOptionsViewModel.MI_MI;

            return table;
        }

        private void FilterBorders()
        {
            int totalRegionsCount = _mapHexFile.LandRegions.Count + _mapHexFile.SeaRegions.Count;

            bool[] hasSprawlData = new bool[totalRegionsCount];
            bool[] isSea         = new bool[totalRegionsCount];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; hexIndex++)
            {
                Hex hex = _mapHexFile.HexData[hexIndex];

                // RegionId is -1 while a hex is unassigned, and can outrun the region list on a
                // malformed map. Such a hex belongs to no region and classifies nothing.
                if ((uint)hex.RegionId >= (uint)totalRegionsCount)
                    continue;

                if (hex.IsTownSprawl)
                    hasSprawlData[hex.RegionId] = true;
                if (hex.IsSea)
                    isSea[hex.RegionId] = true;
            }

            var kind = new RegionKind[totalRegionsCount];
            for (int regionId = 0; regionId < totalRegionsCount; regionId++)
            {
                kind[regionId] = isSea[regionId]
                               ? (hasSprawlData[regionId] ? RegionKind.Sea  : RegionKind.MI)
                               : (hasSprawlData[regionId] ? RegionKind.Land : RegionKind.TI);
            }

            var exportOption = BuildExportOptionTable();
            var newBorders   = new List<BorderPart>();
            int unclassified = 0;

            foreach (BorderPart part in Borders)
            {
                if ((uint)part.regFrom >= (uint)totalRegionsCount ||
                    (uint)part.regTo   >= (uint)totalRegionsCount)
                {
                    ++unclassified;
                    continue;
                }

                if (exportOption[(int)kind[part.regFrom], (int)kind[part.regTo]])
                    newBorders.Add(part);
            }

            if (unclassified > 0)
            {
                LoggerViewModel.Log(
                    $"BordersExporter: skipped {unclassified} border part(s) referencing a region index outside the region list.",
                    LogLevel.Warning);
            }

            Borders = newBorders;
        }

        /// <summary>Writes a region name as a character count followed by that many ASCII bytes.</summary>
        private static void WriteRegionName(BinaryWriter bw, string name)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(name);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        public void Write(string path)
        {
            File.Delete(path);

            using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path)))
            {
                bw.Write(19529729);

                int totalRegionsCount = _mapHexFile.LandRegions.Count + _mapHexFile.SeaRegions.Count;
                bw.Write(totalRegionsCount);

                foreach (string reg in _mapHexFile.LandRegions)
                    WriteRegionName(bw, reg);

                foreach (string reg in _mapHexFile.SeaRegions)
                    WriteRegionName(bw, reg);

                var counts = new int[totalRegionsCount + 1, totalRegionsCount + 1];

                int currentRegFrom = -1;
                int currentRegTo = -1;

                foreach (BorderPart BPart in Borders)
                {
                    if (BPart.regFrom != currentRegFrom)
                    {
                        counts[0, 0]++;
                        currentRegFrom = BPart.regFrom;
                        currentRegTo = -1;
                    }
                    if (BPart.regTo != currentRegTo)
                    {
                        counts[BPart.regFrom + 1, 0]++;
                        currentRegTo = BPart.regTo;
                    }
                    counts[BPart.regFrom + 1, BPart.regTo + 1]++;
                }

                bw.Write(counts[0, 0]);

                currentRegFrom = -1;
                currentRegTo = -1;

                foreach (BorderPart BPart in Borders)
                {
                    if (BPart.regFrom != currentRegFrom)
                    {
                        currentRegFrom = BPart.regFrom;
                        currentRegTo = -1;
                        bw.Write(BPart.regFrom);
                        bw.Write(counts[currentRegFrom + 1, 0]);
                    }
                    if (BPart.regTo != currentRegTo)
                    {
                        currentRegTo = BPart.regTo;
                        bw.Write(BPart.regTo);
                        bw.Write(counts[BPart.regFrom + 1, BPart.regTo + 1]);
                    }
                    bw.Write(BPart.BorderPoints.Count);
                    foreach (BorderPoint bP in BPart.BorderPoints)
                    {
                        bw.Write(Convert.ToInt16(bP.X));
                        bw.Write(Convert.ToInt16(bP.Y));
                    }
                }
            }

        #region CRC32
            byte[] inputData = File.ReadAllBytes(path);
            byte[] outputData = new byte[inputData.Length + 4];
            Array.Copy(inputData, 0, outputData, 0, inputData.Length);
            Crc32Algorithm.ComputeAndWriteToEnd(outputData);
            File.WriteAllBytes(path, outputData);
            #endregion
        }
    }
}
