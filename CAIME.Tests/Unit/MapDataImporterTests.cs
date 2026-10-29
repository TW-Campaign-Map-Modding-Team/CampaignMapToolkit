using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME.Classes.Importers;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Imports a small hand-built map_data.esf into a new map. The map is 6 columns by 4 rows:
    /// <code>
    /// column  0-1        2                  3-4               5
    ///         land_a     mountains          sea_a             lake
    ///         passable   impassable land    passable sea      impassable sea
    /// </code>
    /// with hex (1, 0) a beach and hex (3, 1) a bridge.
    /// </summary>
    [TestClass]
    public class MapDataImporterTests
    {
        private const uint W = 6, H = 4;

        private const uint LAND_A_COLOUR = 0xFF112233;
        private const uint SEA_A_COLOUR  = 0xFF445566;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_mapdata_importer");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.ResetStaticMaskState();
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static int Index(int col, int row) => row * (int)W + col;

        private static MapDataContents BuildContents()
        {
            var contents = new MapDataContents { Width = W, Height = H };

            contents.GroundTypes.AddRange(new[] { "grassland", "mountain", "ocean", "lake_water" });
            contents.Climates.AddRange(new[] { "temperate", "frozen" });
            contents.Attritions.AddRange(new[] { "cold" });
            contents.AreasOfInterest.AddRange(new[] { "volcano" });

            var landA = new MapDataRegionInfo { Key = "land_a", Colour = LAND_A_COLOUR };
            landA.PrimarySlotHexes.AddRange(new[] { Index(0, 0), Index(0, 1) });
            landA.PortSlotHexes.AddRange(new[] { Index(0, 1), Index(1, 1) });

            contents.Regions.Add(landA);
            contents.Regions.Add(new MapDataRegionInfo { Key = "mountains", Colour = MapDataContents.UnassignedColour });
            contents.Regions.Add(new MapDataRegionInfo { Key = "sea_a", HasPassableSea = true, Colour = SEA_A_COLOUR });
            contents.Regions.Add(new MapDataRegionInfo { Key = "lake", Colour = MapDataContents.UnassignedColour });

            int count = contents.HexCount;
            contents.HexRegions         = new int[count];
            contents.HexAreaTypes       = new MapDataAreaType[count];
            contents.HexGroundTypes     = new sbyte[count];
            contents.HexClimates        = Enumerable.Repeat(MapDataContents.None, count).ToArray();
            contents.HexAttritions      = Enumerable.Repeat(MapDataContents.None, count).ToArray();
            contents.HexAreaOfInterestBits = new byte[count];
            contents.HexRoadMasks       = new byte[count];
            contents.HexRiverMasks      = new byte[count];

            for (int row = 0; row < H; ++row)
            {
                for (int col = 0; col < W; ++col)
                {
                    int i = Index(col, row);
                    switch (col)
                    {
                        case 0:
                        case 1: Set(contents, i, 0, MapDataAreaType.Land, 0); break;
                        case 2: Set(contents, i, 1, MapDataAreaType.Impassable, 1); break;
                        case 3:
                        case 4: Set(contents, i, 2, MapDataAreaType.Sea, 2); break;
                        default: Set(contents, i, 3, MapDataAreaType.Impassable, 3); break;
                    }
                }
            }

            contents.HexAreaTypes[Index(1, 0)] = MapDataAreaType.Beach;
            contents.HexAreaTypes[Index(3, 1)] = MapDataAreaType.Bridge;

            return contents;
        }

        private static void Set(MapDataContents contents, int hexIndex, int region, MapDataAreaType areaType, sbyte groundType)
        {
            contents.HexRegions[hexIndex]       = region;
            contents.HexAreaTypes[hexIndex]     = areaType;
            contents.HexGroundTypes[hexIndex]   = groundType;
        }

        private MapHexFile Import(MapDataContents contents)
        {
            var map = MapHexFile.CreateMapHex(_dir, "map", "warhammer3", "test_map", W, H, 0x14);
            MapDataImporter.Import(contents, map);
            return map;
        }

        [TestMethod]
        public void Import_SplitsGroundTypesWhereTheFirstGroundUnderPassableSeaIs()
        {
            var map = Import(BuildContents());

            CollectionAssert.AreEqual(new[] { "grassland", "mountain" }, map.LandGroundTypes);
            CollectionAssert.AreEqual(new[] { "ocean", "lake_water" }, map.SeaGroundTypes);
        }

        /// <summary>
        /// The lake's ground type comes before the ocean's and is only under impassable hexes, so only
        /// the cliffs on its shore show it is sea. Columns 3 and 4 become land, 4 a cliff.
        /// </summary>
        [TestMethod]
        public void Import_ImpassableLakeGround_IsSeaWhenItsShoreIsCliffs()
        {
            var contents = BuildContents();
            contents.GroundTypes.Clear();
            contents.GroundTypes.AddRange(new[] { "grassland", "mountain", "lake_water", "ocean" });

            for (int row = 0; row < H; ++row)
            {
                Set(contents, Index(3, row), 2, MapDataAreaType.Land, 0);
                Set(contents, Index(4, row), 2, MapDataAreaType.Cliff, 0);
                Set(contents, Index(5, row), 3, MapDataAreaType.Impassable, 2);
            }

            var map = Import(contents);

            CollectionAssert.AreEqual(new[] { "grassland", "mountain" }, map.LandGroundTypes);
            CollectionAssert.AreEqual(new[] { "lake_water", "ocean" }, map.SeaGroundTypes);
            Assert.IsTrue(map.HexData[Index(5, 0)].IsSea);
        }

        [TestMethod]
        public void Import_UnflaggedRegionWithOnlySeaGround_IsASeaRegion()
        {
            var map = Import(BuildContents());

            CollectionAssert.AreEqual(new[] { "land_a", "mountains" }, map.LandRegions);
            CollectionAssert.AreEqual(new[] { "sea_a", "lake" }, map.SeaRegions);
            Assert.AreEqual("lake", map.GetRegionName(map.HexData[Index(5, 2)].RegionId));
        }

        [TestMethod]
        public void Import_RegionsListedSeaFirst_KeepTheirOrder()
        {
            var contents = BuildContents();
            var seaA = contents.Regions[2];
            contents.Regions.RemoveAt(2);
            contents.Regions.Insert(0, seaA);
            for (int i = 0; i < contents.HexCount; ++i)
            {
                contents.HexRegions[i] = new[] { 1, 2, 0, 3 }[contents.HexRegions[i]];
            }

            var map = Import(contents);

            // map.hex lists land regions first, so a sea region before a land one must be land.
            CollectionAssert.AreEqual(new[] { "sea_a", "land_a", "mountains" }, map.LandRegions);
            CollectionAssert.AreEqual(new[] { "lake" }, map.SeaRegions);
            Assert.AreEqual("sea_a",     map.GetRegionName(map.HexData[Index(3, 0)].RegionId));
            Assert.AreEqual("land_a",    map.GetRegionName(map.HexData[Index(0, 0)].RegionId));
            Assert.AreEqual("mountains", map.GetRegionName(map.HexData[Index(2, 0)].RegionId));
        }

        [TestMethod]
        public void Import_KeepsRegionColoursAndReplacesUnassignedOnes()
        {
            var map = Import(BuildContents());

            Assert.AreEqual(unchecked((int)LAND_A_COLOUR), map.ColoursLand.Colours[0]);
            Assert.AreNotEqual(unchecked((int)MapDataContents.UnassignedColour), map.ColoursLand.Colours[1]);
            Assert.AreEqual(unchecked((int)SEA_A_COLOUR), map.ColoursSea.Colours[0]);
            Assert.AreNotEqual(unchecked((int)MapDataContents.UnassignedColour), map.ColoursSea.Colours[1]);
        }

        [TestMethod]
        public void Import_SetsLandSeaPassabilityBeachAndBridgeFromTheAreaTypes()
        {
            var map = Import(BuildContents());
            var hexes = map.HexData;

            Assert.IsFalse(hexes[Index(0, 0)].IsSea);
            Assert.IsTrue(hexes[Index(3, 0)].IsSea);
            Assert.IsTrue(hexes[Index(5, 0)].IsSea, "Impassable hexes with sea ground are sea.");

            Assert.IsTrue(hexes[Index(0, 0)].IsPassable);
            Assert.IsFalse(hexes[Index(2, 0)].IsPassable);

            Assert.IsTrue(hexes[Index(1, 0)].IsBeach);
            Assert.IsTrue(hexes[Index(3, 1)].IsBridge);
            Assert.IsFalse(hexes[Index(3, 0)].IsBridge);
        }

        [TestMethod]
        public void Import_ImpassableCoast_IsBeachOnlyWhereItTouchesABeach()
        {
            var map = Import(BuildContents());

            Assert.IsTrue(map.HexData[Index(2, 0)].IsBeach, "(2, 0) touches the beach at (1, 0) and the sea at (3, 0).");
            Assert.IsFalse(map.HexData[Index(2, 3)].IsBeach, "(2, 3) touches the sea but no beach.");
        }

        [TestMethod]
        public void Import_MainSlotWinsOverThePortSlot_AndSlotHexesAreSprawl()
        {
            var map = Import(BuildContents());
            var hexes = map.HexData;

            Assert.AreEqual(Hex.MAIN_SLOT_INDEX, hexes[Index(0, 0)].TownSlotIndex);
            Assert.AreEqual(Hex.MAIN_SLOT_INDEX, hexes[Index(0, 1)].TownSlotIndex, "The port block also lists main slot hexes.");
            Assert.AreEqual(Hex.PORT_SLOT_INDEX, hexes[Index(1, 1)].TownSlotIndex);
            Assert.AreEqual(Hex.INVALID_SLOT_INDEX, hexes[Index(1, 2)].TownSlotIndex);

            Assert.IsTrue(hexes[Index(0, 0)].IsTownSprawl);
            Assert.IsTrue(hexes[Index(1, 1)].IsTownSprawl);
            Assert.IsFalse(hexes[Index(1, 2)].IsTownSprawl);
        }

        [TestMethod]
        public void Import_CopiesPerHexNamesAndMasks()
        {
            var contents = BuildContents();
            int hex = Index(1, 2);
            contents.HexClimates[hex]           = 1;
            contents.HexAttritions[hex]         = 0;
            contents.HexAreaOfInterestBits[hex] = 0b10;     // Warhammer III: index + 1, above one unrelated bit
            contents.HexRoadMasks[hex]          = 0b010010;
            contents.HexRiverMasks[hex]         = 0b000100;

            var map = Import(contents);
            var imported = map.HexData[hex];

            Assert.AreEqual("frozen",  map.GetClimateName(imported.ClimateIndex));
            Assert.AreEqual("cold",    map.GetAttritionName(imported.AttritionIndex));
            Assert.AreEqual("volcano", map.GetAreaOfInterestName(imported.InterestIndex));
            Assert.AreEqual(0b010010,  imported.RoadEdgeMask);
            Assert.IsTrue(imported.IsRoad);
            Assert.AreEqual(0b000100,  imported.RiverEdgeMask);
            Assert.IsTrue(imported.IsRiver);
            Assert.IsNull(map.GetClimateName(map.HexData[Index(0, 0)].ClimateIndex));
        }

        [TestMethod]
        public void Import_CliffAreas_AreCliffs()
        {
            var contents = BuildContents();
            contents.HexAreaTypes[Index(0, 3)] = MapDataAreaType.Cliff;
            contents.HexAreaTypes[Index(1, 3)] = MapDataAreaType.BridgeCliff;

            var map = Import(contents);

            Assert.IsTrue(map.HexData[Index(0, 3)].IsCliff);
            Assert.IsTrue(map.HexData[Index(1, 3)].IsCliff);
            Assert.IsFalse(map.HexData[Index(0, 2)].IsCliff);
        }

        [TestMethod]
        public void Import_FootprintIsSprawl_AndOtherSlotsKeepTheirIndex()
        {
            var contents = BuildContents();
            contents.Regions[0].SprawlHexes.AddRange(new[] { Index(0, 0), Index(0, 1), Index(1, 1), Index(0, 2) });
            contents.Regions[0].OtherSlotHexes.Add(new List<int> { Index(1, 0) });
            contents.Regions[0].OtherSlotHexes.Add(new List<int> { Index(0, 2) });

            var hexes = Import(contents).HexData;

            Assert.IsTrue(hexes[Index(0, 2)].IsTownSprawl);
            Assert.AreEqual((sbyte)2, hexes[Index(1, 0)].TownSlotIndex);
            Assert.AreEqual((sbyte)3, hexes[Index(0, 2)].TownSlotIndex);
        }

        [TestMethod]
        public void Import_CampaignMasks_AreListedInTheMap()
        {
            var contents = BuildContents();
            contents.MaskNames.AddRange(new[] { "", "8" });

            var map = Import(contents);

            CollectionAssert.AreEqual(new[] { "", "8" }, map.UnknownEntries.Select(mask => mask.Key).ToList());
        }

        [TestMethod]
        public void Import_ThenSave_WritesRegionEdgesOnEveryRegionBoundary()
        {
            var map = Import(BuildContents());
            Assert.IsTrue(map.Save(_dir, "map"));

            var saved = new MapHexFile();
            Assert.IsTrue(saved.Load(Path.Combine(_dir, "map.hex")));

            foreach (var hex in saved.HexData)
            {
                byte expected = 0;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = saved.GetNeighbourIndex(hex, dir);
                    if (nbrIndex != -1 && saved.HexData[nbrIndex].RegionId != hex.RegionId)
                    {
                        expected |= (byte)(1 << dir);
                    }
                }

                Assert.AreEqual(expected, hex.RegionEdgeMask, $"Region edges of hex ({hex.Q}, {hex.R}).");
            }
        }

        [TestMethod]
        public void Import_MapOfADifferentSize_IsRejected()
        {
            var map = MapHexFile.CreateMapHex(_dir, "map", "warhammer3", "test_map", W + 2, H, 0x14);

            Assert.ThrowsException<System.ArgumentException>(() => MapDataImporter.Import(BuildContents(), map));
        }
    }
}
