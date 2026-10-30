using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME.Classes.Importers;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Campaign masks in map_data.esf, as the Assembly Kit's builders write them: each mask gets
    /// its own MASKED_REGIONS_DATA entry, with its runs of masked hexes and its own copy of the
    /// regions, in which masked hexes are impassable before the regions are split into areas. No
    /// vanilla map has a mask with hexes in it; the Warhammer II and Warhammer III builders' output
    /// for maps with a mask painted in matched the writers' node for node.
    /// </summary>
    [TestClass]
    public class MapDataMaskTests
    {
        private const int LAND_A = 0;
        private const int LAND_B = 1;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_map_data_masks");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        /// <summary>land_a in columns 0-3, land_b in columns 4-7.</summary>
        private static MapHexFile TwoLands()
        {
            var map = MapHexHarness.BuildGrid(8, 6, hex =>
            {
                hex.RegionId        = hex.Q < 4 ? LAND_A : LAND_B;
                hex.GroundTypeIndex = 0;
                hex.ClimateIndex    = 0;
            });

            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursLand), new ColoursContainer(new[] { 0x11, 0x22 }));
            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursSea), new ColoursContainer(new int[0]));
            return map;
        }

        private static CampaignMask MaskedColumn(MapHexFile map, string name, int column)
        {
            var mask = new CampaignMask(name);
            for (int row = 0; row < map.MapHeight; ++row)
            {
                mask.SetMasked(column, row, map.MapWidth, map.MapHeight, true);
            }

            return mask;
        }

        private static readonly MapDataTheatre THEATRE = new MapDataTheatre { Index = "42", MaxX = 100f };

        [TestMethod]
        public void Runs_AreRowFirstColumnAndEnd_WithTheBuildersEndForARunReachingTheRowsLastBit()
        {
            // 10 hexes a row: 2 bytes, 6 padding bits.
            var map  = MapHexHarness.BuildGrid(10, 3);
            var mask = new CampaignMask("m");
            foreach (int column in new[] { 2, 3, 4 })
            {
                mask.SetMasked(column, 0, 10, 3, true);
            }

            mask.SetMasked(8, 1, 10, 3, true);
            mask.SetMasked(9, 1, 10, 3, true);
            mask.Bitmap[1 * 2 + 1] |= 0xFC;   // row 1's padding bits, set to the end of the row
            mask.SetMasked(9, 2, 10, 3, true);
            map.SetCampaignMasks(new[] { new CampaignMask(""), mask });

            var masks = MapDataMask.ForEachMask(map);

            CollectionAssert.AreEqual(new ushort[0], masks[0].Runs);
            CollectionAssert.AreEqual(new ushort[] { 0, 2, 5, 1, 8, 2, 2, 9, 10 }, masks[1].Runs,
                "A run open at the end of the row ends at the row's byte count (2), as the builder writes it.");
            Assert.IsFalse(masks[1].IsMasked(1 * 10 + 7));
            Assert.IsTrue(masks[1].IsMasked(1 * 10 + 8));
        }

        [TestMethod]
        public void MaskedHexes_BecomeAnImpassableAreaThatSplitsTheRegion()
        {
            var map = TwoLands();
            var mask = MapDataMask.ForEachMask(WithMasks(map, MaskedColumn(map, "wall", 1)))[1];

            var unmasked = new MapDataModel(map, THEATRE);
            var masked   = new MapDataModel(map, THEATRE, MapDataRadiusRounding.TwoThirdsPerColumn, mask);

            Assert.AreEqual(1, unmasked.Areas.RegionAreas[LAND_A].Count);
            CollectionAssert.AreEqual(new[] { MapDataAreaType.Land, MapDataAreaType.Impassable, MapDataAreaType.Land },
                masked.Areas.RegionAreas[LAND_A].Select(area => masked.Areas.Areas[area].Type).ToArray(),
                "Column 0, the masked column 1 and columns 2-3 are three areas, in the order of their first hexes.");
        }

        [TestMethod]
        public void MaskedHexesAlongABorder_LeaveOnlyANonTraversableLinkAcrossIt()
        {
            var map  = TwoLands();
            var mask = MapDataMask.ForEachMask(WithMasks(map, MaskedColumn(map, "wall", 4)))[1];

            var unmasked = new MapDataModel(map, THEATRE);
            var masked   = new MapDataModel(map, THEATRE, MapDataRadiusRounding.TwoThirdsPerColumn, mask);

            var landA = masked.Areas.Areas[masked.Areas.RegionAreas[LAND_A][0]];
            var wall  = masked.Areas.RegionAreas[LAND_B][0];

            CollectionAssert.Contains(unmasked.Areas.Areas[unmasked.Areas.RegionAreas[LAND_A][0]].Traversable, unmasked.Areas.RegionAreas[LAND_B][0]);
            Assert.AreEqual(MapDataAreaType.Impassable, masked.Areas.Areas[wall].Type, "land_b's masked column is its first area.");
            CollectionAssert.AreEqual(new int[0], landA.Traversable, "land_a touches land_b only through the masked column.");
            CollectionAssert.AreEqual(new[] { wall }, landA.NonTraversable);
        }

        [TestMethod]
        public void Wh3Writer_WritesAnEntryForEachMaskWithItsNameAndRuns()
        {
            var map = TwoLands();
            WithMasks(map, MaskedColumn(map, "wall", 4));

            var entries = MaskedRegionsEntries(Wh3MapDataWriter.Write(map, THEATRE, 0));

            CollectionAssert.AreEqual(new[] { "", "wall" }, entries.Select(EntryName).ToArray());
            CollectionAssert.AreEqual(new ushort[0], EntryRuns(entries[0]));
            CollectionAssert.AreEqual(new ushort[] { 0, 4, 5, 1, 4, 5, 2, 4, 5, 3, 4, 5, 4, 4, 5, 5, 4, 5 }, EntryRuns(entries[1]));
            Assert.AreEqual(AreaCount(entries[0]) + 1, AreaCount(entries[1]), "The masked column is an area of its own.");
        }

        [TestMethod]
        public void Wh1Writer_MaskedEntryHasTheMaskedHexesAsAreasOfTheirOwn()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "wh_dlc03_beastmen_map_1", "map.hex")));
            CollectionAssert.AreEqual(new[] { "", "8" }, map.CampaignMasks.Select(mask => mask.Name).ToArray());

            var wall = MaskedColumn(map, "8", 200);
            map.SetCampaignMasks(new[] { map.CampaignMasks[0], wall });

            var entries = MaskedRegionsEntries(Wh1MapDataWriter.Write(map, new MapDataTheatre { Index = "1564135542", MaxX = 275.8f }, 0));

            CollectionAssert.AreEqual(new[] { "", "8" }, entries.Select(EntryName).ToArray());
            Assert.AreEqual(3 * (int)map.MapHeight, EntryRuns(entries[1]).Length, "One run a row.");
            Assert.IsTrue(AreaCount(entries[1]) > AreaCount(entries[0]), "The masked column must split areas off.");
        }

        [TestMethod]
        public void Resize_MovesTheMaskedHexesWithTheMap()
        {
            var map  = TwoLands();
            var mask = new CampaignMask("m");
            mask.SetMasked(2, 1, map.MapWidth, map.MapHeight, true);
            map.SetCampaignMasks(new[] { mask });

            Assert.IsTrue(map.ResizeMapHex(10, 6, 1, 1, 0, 0));

            var resized = map.CampaignMasks.Single();
            Assert.IsTrue(resized.IsMasked(3, 1, 10), "Padding one column on the left moves the hex one column right.");
            Assert.IsFalse(resized.IsMasked(2, 1, 10));
            Assert.AreEqual(CampaignMask.BitmapSize(10, 6), resized.Bitmap.Length);
        }

        private static MapHexFile WithMasks(MapHexFile map, CampaignMask mask)
        {
            map.SetCampaignMasks(new[] { new CampaignMask(""), mask });
            return map;
        }

        private List<ParentNode> MaskedRegionsEntries(byte[] mapData)
        {
            var path = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(path, mapData);

            var root = (ParentNode)EsfCodecUtil.LoadEsfFile(path).RootNode;
            return root.Children.Single(node => node.Name == "REGIONS_DATA")
                       .Children.Single(node => node.Name == "MASKED_REGIONS_DATA").Children;
        }

        private static string EntryName(ParentNode entry) => ((EsfValueNode<string>)entry.Values[0]).Value;

        private static ushort[] EntryRuns(ParentNode entry) => entry.Values.OfType<EsfValueNode<ushort[]>>().First().Value;

        private static int AreaCount(ParentNode entry)
        {
            return entry.Children.Single(node => node.Name == "REGIONS_BLOCK").Children
                .Sum(region => region.Children.Single().Children.Single(node => node.Name == "REGION_AREAS").Children.Count);
        }
    }
}
