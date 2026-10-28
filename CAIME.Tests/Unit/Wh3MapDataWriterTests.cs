using System.IO;
using System.Linq;
using CAIME.Classes.Importers;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds map_data.esf for small made-up maps and reads it back with the importer's reader.
    /// Byte-for-byte agreement with the Assembly Kit is checked against real maps by
    /// <see cref="Integration.MapDataWriterIntegrationTests"/>.
    /// </summary>
    [TestClass]
    public class Wh3MapDataWriterTests
    {
        private const int LAND_A = 0;
        private const int LAND_B = 1;
        private const int SEA_A  = 2;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_mapdata_writer");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static MapHexFile BuildMap(uint width, uint height, System.Action<Hex> initHex)
        {
            var map = MapHexHarness.BuildGrid(width, height, hex =>
            {
                hex.RegionId        = LAND_A;
                hex.GroundTypeIndex = 0;
                hex.ClimateIndex    = 0;
                initHex?.Invoke(hex);
            });

            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursLand), new ColoursContainer(new[] { 0x11, 0x22 }));
            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursSea), new ColoursContainer(new int[0]));
            return map;
        }

        private MapDataContents WriteAndRead(MapHexFile map)
        {
            var path = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(path, Wh3MapDataWriter.Write(map, new MapDataTheatre { Index = "42", MaxX = 100f }, 0));
            return Wh3MapDataReader.Read(path);
        }

        /// <summary>Left half land_a, right half land_b, the top row sea_a.</summary>
        private static MapHexFile TwoLandsAndASea()
        {
            return BuildMap(8, 6, hex =>
            {
                hex.RegionId = hex.Q < 4 ? LAND_A : LAND_B;
                if (hex.R == 5)
                {
                    hex.RegionId        = SEA_A;
                    hex.IsSea           = true;
                    hex.GroundTypeIndex = 2;
                }
            });
        }

        [TestMethod]
        public void Write_ReadBack_KeepsRegionsAreasAndGroundTypes()
        {
            var map         = TwoLandsAndASea();
            var contents    = WriteAndRead(map);

            Assert.AreEqual(8u, contents.Width);
            Assert.AreEqual(6u, contents.Height);
            CollectionAssert.AreEqual(new[] { "land_a", "land_b", "sea_a" }, contents.Regions.Select(region => region.Key).ToArray());
            Assert.IsTrue(contents.Regions[SEA_A].HasPassableSea);
            Assert.IsFalse(contents.Regions[LAND_A].HasPassableSea);
            CollectionAssert.AreEqual(new[] { 0x11u, 0x22u, MapDataContents.UnassignedColour }, contents.Regions.Select(region => region.Colour).ToArray());

            for (int hexIndex = 0; hexIndex < map.HexData.Length; ++hexIndex)
            {
                var hex = map.HexData[hexIndex];
                Assert.AreEqual(hex.RegionId, contents.HexRegions[hexIndex], $"region of hex {hexIndex}");
                Assert.AreEqual(hex.IsSea ? MapDataAreaType.Sea : MapDataAreaType.Land, contents.HexAreaTypes[hexIndex], $"area type of hex {hexIndex}");
                Assert.AreEqual(hex.GroundTypeIndex, contents.HexGroundTypes[hexIndex], $"ground type of hex {hexIndex}");
            }
        }

        [TestMethod]
        public void Write_HexWithoutARegion_Throws()
        {
            var map = TwoLandsAndASea();
            map.HexData[3].RegionId = Hex.INVALID_REGION_INDEX;

            Assert.ThrowsException<System.InvalidOperationException>(() => Wh3MapDataWriter.Write(map, new MapDataTheatre { Index = "42", MaxX = 100f }, 0));
        }

        /// <summary>
        /// The builder's bridge-cliff check never looks at the east neighbour, (column + 1, same
        /// row), so a cliff whose only bridge is there stays a plain cliff.
        /// </summary>
        [TestMethod]
        public void Areas_CliffWithABridgeOnlyToTheEast_IsNotABridgeCliff()
        {
            var map = BuildMap(6, 6, hex =>
            {
                if (hex.Q == 3)
                {
                    hex.RegionId = SEA_A;
                    hex.IsSea    = true;
                    hex.IsBridge = hex.R == 2;
                }
            });

            map.HexData[2 * 6 + 2].IsCliff = true;  // (2, 2): the bridge (3, 2) is its east neighbour
            map.HexData[1 * 6 + 3].IsSea   = false; // (3, 1) becomes land, so (3, 2) is (3, 1)'s north
            map.HexData[1 * 6 + 3].RegionId = LAND_A;
            map.HexData[1 * 6 + 3].IsCliff = true;

            var grid    = new MapDataGrid(6, 6);
            var areas   = new MapDataAreas(map, grid, new MapDataGeometry(100f, 6, 6), 3);

            Assert.AreEqual(MapDataAreaType.Cliff, areas.HexTypes[2 * 6 + 2]);
            Assert.AreEqual(MapDataAreaType.BridgeCliff, areas.HexTypes[1 * 6 + 3]);
        }

        /// <summary>
        /// A settlement is the radius-2 hexagon around its centre; the footprint marks it in a 5x5
        /// square, row by row, lowest bit first. Values from wh3_main_prologue_map.
        /// </summary>
        [TestMethod]
        public void Settlement_Footprint_MatchesTheBuilderForBothColumnParities()
        {
            foreach (var (column, expected) in new[] { (4, new byte[] { 238, 255, 79, 0 }), (5, new byte[] { 228, 255, 239, 0 }) })
            {
                var map = BuildMap(10, 10, hex =>
                {
                    if (MapDataGrid.Distance(column, 4, hex.Q, hex.R) <= MapDataSettlement.RADIUS)
                    {
                        hex.TownSlotIndex = Hex.MAIN_SLOT_INDEX;
                    }
                });

                var settlement = MapDataSettlement.FindAll(map, new MapDataGrid(10, 10), 3)[LAND_A];

                Assert.AreEqual(column, settlement.CentreColumn);
                Assert.AreEqual(4, settlement.CentreRow);
                Assert.AreEqual(19, settlement.MainSlotHexes.Count);
                Assert.IsFalse(settlement.HasPort);
                CollectionAssert.AreEqual(expected, settlement.FootprintBits, $"centre column {column}");
            }
        }

        [TestMethod]
        public void Write_Settlement_ReadsBackItsMainSlotHexes()
        {
            var map = BuildMap(10, 10, hex =>
            {
                if (MapDataGrid.Distance(4, 4, hex.Q, hex.R) <= MapDataSettlement.RADIUS)
                {
                    hex.TownSlotIndex = Hex.MAIN_SLOT_INDEX;
                }
            });

            var contents = WriteAndRead(map);
            var expected = map.HexData.Where(hex => hex.TownSlotIndex == Hex.MAIN_SLOT_INDEX).Select(hex => hex.Index).OrderBy(i => i % 10).ThenBy(i => i / 10).ToArray();

            CollectionAssert.AreEqual(expected, contents.Regions[LAND_A].PrimarySlotHexes.ToArray());
        }

        /// <summary>
        /// The radius is maxx * ((2 / 3) / (columns - 1)) in floats; maxx / (columns - 1) / 1.5 is a
        /// unit in the last place smaller for wh3_main_chaos_map_4.
        /// </summary>
        [TestMethod]
        public void Geometry_Radius_IsRoundedAsTheBuilderRoundsIt()
        {
            var geometry = new MapDataGeometry(739.5f, 1108, 722);

            Assert.AreEqual(0.44534782f, geometry.Radius);
            Assert.AreNotEqual((float)((float)(739.5f / 1107f) / 1.5f), geometry.Radius);
            Assert.AreEqual(557.3112182617188f, geometry.MaxY);
        }
    }
}
