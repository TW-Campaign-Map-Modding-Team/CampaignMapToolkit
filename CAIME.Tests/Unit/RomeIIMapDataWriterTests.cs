using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Rome II's main_rome_map map_data.esf from Templates/main_rome_map/map.hex and
    /// compares it, node by node, with the game's own file in TestData. That file's theatre was
    /// written by an older builder (v2, with a colour table), so its version and colour table are
    /// left out.
    /// </summary>
    [TestClass]
    public class RomeIIMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1564135515";
        private const float  MAX_X          = 678.5f;
        private const float  MAX_Y          = 555.09f;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_rome2_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Write_MainRomeMap_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_rome_map", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, RomeIIMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X, MaxY = MAX_Y }, 0));

            var comparison = new MapDataEsfComparison
            {
                OlderRecord         = "CAMPAIGN_THEATRE",
                OlderRecordExtra    = node => node.TypeCode == EsfType.UINT32_24BIT_ARRAY,
            };
            comparison.AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "main_rome_map", "map_data.esf"), written);
        }

        [TestMethod]
        public void Write_NoMaxY_UsesTheEstimateRoundedToTwoPlaces()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_rome_map", "map.hex")));

            var estimate = (float)System.Math.Round(RomeIIMapDataWriter.EstimateMaxY(MAX_X, map.MapWidth, map.MapHeight), 2);
            var expected = RomeIIMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X, MaxY = estimate }, 0);
            var actual   = RomeIIMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0);

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
