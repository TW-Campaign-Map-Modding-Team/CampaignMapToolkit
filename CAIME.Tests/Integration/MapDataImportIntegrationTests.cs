using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CAIME.Classes.Importers;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Imports real Warhammer III map_data.esf files and compares the map.hex the import saves
    /// with the map.hex each was built from.
    ///
    /// <para>
    /// The data lives outside the repository: set <c>CAIME_MAP_DATA_DIR</c> to a folder of
    /// <c>&lt;map&gt;/map_data.esf</c> + <c>&lt;map&gt;/map.hex</c> pairs, such as an Assembly Kit
    /// map.hex with the map_data.esf the Assembly Kit builds from it. Without it the test is
    /// inconclusive.
    /// </para>
    ///
    /// <para>
    /// Areas of interest are not compared: <see cref="MapHexFile"/> reads them from a lower bit
    /// than CA's map.hex stores them in, so the reference map would not read back correctly.
    /// Maps without passable sea, such as wh3_main_prologue_map, are skipped: the importer can
    /// only tell sea ground types from land ones by the ground under passable sea.
    /// </para>
    /// </summary>
    [TestClass]
    public class MapDataImportIntegrationTests
    {
        /// <summary>
        /// map_data.esf does not record whether an impassable coast hex is beach or cliff; the
        /// importer's inference matches CA on about 97-99% of them.
        /// </summary>
        private const double MIN_IMPASSABLE_COAST_AGREEMENT = 0.95;

        /// <summary>CA's wh3_main_prologue_map carries two stray region edges no region boundary explains.</summary>
        private const int MAX_REGION_EDGE_MISMATCHES = 10;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_mapdata_import");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static string[] FindMapFolders()
        {
            var dir = Environment.GetEnvironmentVariable("CAIME_MAP_DATA_DIR");
            if (string.IsNullOrEmpty(dir) || Directory.Exists(dir) == false)
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(dir)
                .Where(folder => File.Exists(Path.Combine(folder, "map_data.esf")) && File.Exists(Path.Combine(folder, "map.hex")))
                .ToArray();
        }

        [TestMethod]
        public void Import_RealMapData_RebuildsTheMapHexItWasBuiltFrom()
        {
            var folders = FindMapFolders();
            if (folders.Length == 0)
            {
                Assert.Inconclusive("No map_data.esf + map.hex pairs found. Set CAIME_MAP_DATA_DIR to enable this test.");
            }

            var failures = new StringBuilder();
            int comparedMaps = 0;

            foreach (var folder in folders)
            {
                var mapName  = Path.GetFileName(folder);
                var contents = MapDataReader.Read(Path.Combine(folder, "map_data.esf"));

                if (contents.HexAreaTypes.Any(type => type == MapDataAreaType.Sea || type == MapDataAreaType.Bridge) == false)
                {
                    continue;
                }

                var reference = new MapHexFile();
                Assert.IsTrue(reference.Load(Path.Combine(folder, "map.hex")), $"{mapName}: the reference map.hex did not load.");

                var imported = Import(contents, mapName);

                CompareLists(mapName, imported, reference, failures);
                CompareHexes(mapName, imported, reference, failures);
                CompareColours(mapName, imported, reference, failures);
                ++comparedMaps;
            }

            if (failures.Length > 0)
            {
                Assert.Fail(failures.ToString());
            }

            if (comparedMaps == 0)
            {
                Assert.Inconclusive("None of the maps in CAIME_MAP_DATA_DIR has passable sea.");
            }
        }

        private MapHexFile Import(MapDataContents contents, string mapName)
        {
            var mapDir = Path.Combine(_dir, mapName);
            Directory.CreateDirectory(mapDir);

            var map = MapHexFile.CreateMapHex(mapDir, "map", "warhammer3", mapName, contents.Width, contents.Height, 0x14);
            MapDataImporter.Import(contents, map);
            Assert.IsTrue(map.Save(mapDir, "map"), $"{mapName}: the imported map did not save.");

            var saved = new MapHexFile();
            Assert.IsTrue(saved.Load(Path.Combine(mapDir, "map.hex")), $"{mapName}: the imported map did not load back.");
            saved.UpdateHexTypes();

            return saved;
        }

        private static void CompareLists(string mapName, MapHexFile imported, MapHexFile reference, StringBuilder failures)
        {
            CompareList(mapName, "land regions", imported.LandRegions, reference.LandRegions, failures);
            CompareList(mapName, "sea regions", imported.SeaRegions, reference.SeaRegions, failures);
            CompareList(mapName, "land ground types", imported.LandGroundTypes, reference.LandGroundTypes, failures);
            CompareList(mapName, "sea ground types", imported.SeaGroundTypes, reference.SeaGroundTypes, failures);
            CompareList(mapName, "climates", imported.Climates, reference.Climates, failures);
            CompareList(mapName, "attritions", imported.Attritions, reference.Attritions, failures);
            CompareList(mapName, "areas of interest", imported.AreasOfInterest, reference.AreasOfInterest, failures);
        }

        private static void CompareList(string mapName, string listName, List<string> imported, List<string> reference, StringBuilder failures)
        {
            if (imported.SequenceEqual(reference) == false)
            {
                failures.AppendLine($"{mapName}: {listName} differ ({imported.Count} imported, {reference.Count} in map.hex).");
            }
        }

        private static void CompareHexes(string mapName, MapHexFile imported, MapHexFile reference, StringBuilder failures)
        {
            Assert.AreEqual(reference.MapWidth, imported.MapWidth, $"{mapName}: width");
            Assert.AreEqual(reference.MapHeight, imported.MapHeight, $"{mapName}: height");

            var mismatches          = new Dictionary<string, int>();
            int impassableCoast     = 0;
            int coastDisagreements  = 0;

            void Check(string field, bool same)
            {
                if (same == false)
                {
                    mismatches.TryGetValue(field, out int count);
                    mismatches[field] = count + 1;
                }
            }

            for (int hexIndex = 0; hexIndex < reference.Capacity; ++hexIndex)
            {
                var ours    = imported.HexData[hexIndex];
                var theirs  = reference.HexData[hexIndex];

                Check("region",         imported.GetRegionName(ours.RegionId) == reference.GetRegionName(theirs.RegionId));
                Check("ground type",    imported.GetGroundTypeName(ours.GroundTypeIndex) == reference.GetGroundTypeName(theirs.GroundTypeIndex));
                Check("climate",        imported.GetClimateName(ours.ClimateIndex) == reference.GetClimateName(theirs.ClimateIndex));
                Check("attrition",      imported.GetAttritionName(ours.AttritionIndex) == reference.GetAttritionName(theirs.AttritionIndex));
                Check("road mask",      ours.RoadEdgeMask == theirs.RoadEdgeMask);
                Check("river mask",     ours.RiverEdgeMask == theirs.RiverEdgeMask);
                Check("passable",       ours.IsPassable == theirs.IsPassable);
                Check("bridge",         ours.IsBridge == theirs.IsBridge);
                Check("town slot",      ours.TownSlotIndex == theirs.TownSlotIndex);
                Check("town sprawl",    ours.IsTownSprawl == theirs.IsTownSprawl);
                Check("sea",            ours.IsSea == theirs.IsSea);
                Check("region edges",   ours.RegionEdgeMask == theirs.RegionEdgeMask);

                bool isImpassableCoast = theirs.IsImpassable && theirs.IsSea == false && (theirs.IsBeach || theirs.IsCliff);
                if (isImpassableCoast)
                {
                    ++impassableCoast;
                    if (ours.IsBeach != theirs.IsBeach || ours.IsCliff != theirs.IsCliff)
                    {
                        ++coastDisagreements;
                    }
                }
                else
                {
                    Check("beach", ours.IsBeach == theirs.IsBeach);
                    Check("cliff", ours.IsCliff == theirs.IsCliff);
                }
            }

            mismatches.TryGetValue("region edges", out int regionEdgeMismatches);
            if (regionEdgeMismatches <= MAX_REGION_EDGE_MISMATCHES)
            {
                mismatches.Remove("region edges");
            }

            foreach (var mismatch in mismatches)
            {
                failures.AppendLine($"{mapName}: {mismatch.Key} differs on {mismatch.Value} of {reference.Capacity} hexes.");
            }

            double agreement = impassableCoast == 0 ? 1 : 1 - (double)coastDisagreements / impassableCoast;
            if (agreement < MIN_IMPASSABLE_COAST_AGREEMENT)
            {
                failures.AppendLine($"{mapName}: beach or cliff matches on only {agreement:P1} of {impassableCoast} impassable coast hexes.");
            }
        }

        private static void CompareColours(string mapName, MapHexFile imported, MapHexFile reference, StringBuilder failures)
        {
            CompareColours(mapName, "land", imported.ColoursLand.Colours, reference.ColoursLand.Colours, imported.LandRegions.Count, failures);
            CompareColours(mapName, "sea", imported.ColoursSea.Colours, reference.ColoursSea.Colours, imported.SeaRegions.Count, failures);
        }

        /// <summary>Regions past the end of the reference's colour array have no colour to compare.</summary>
        private static void CompareColours(string mapName, string kind, int[] imported, int[] reference, int regionCount, StringBuilder failures)
        {
            int compared = Math.Min(regionCount, reference.Length);
            int differing = Enumerable.Range(0, compared).Count(i => imported[i] != reference[i]);
            if (differing > 0)
            {
                failures.AppendLine($"{mapName}: {differing} of {compared} {kind} region colours differ.");
            }
        }
    }
}
