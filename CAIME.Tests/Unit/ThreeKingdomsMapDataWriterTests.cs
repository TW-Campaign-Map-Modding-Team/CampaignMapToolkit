using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Three Kingdoms' 3k_dlc07_main_map map_data.esf from
    /// Templates/3k_dlc07_main_map/map.hex and compares it, node by node, with the game's own file
    /// in TestData.
    /// </summary>
    [TestClass]
    public class ThreeKingdomsMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1799249611";
        private const float  MAX_X          = 595.1f;

        /// <summary>The template map.hex's save time, which MAP_FILE_VALIDATION holds.</summary>
        private const ulong  MAP_HEX_TIMESTAMP = 1617783336;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_3k_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Load_ReadsTheSaveTime()
        {
            var map = new MapHexFile();

            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "3k_dlc07_main_map", "map.hex")));
            Assert.AreEqual(MAP_HEX_TIMESTAMP, map.Timestamp);
        }

        [TestMethod]
        public void Write_3kDlc07MainMap_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "3k_dlc07_main_map", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, ThreeKingdomsMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "3k_dlc07_main_map", "map_data.esf"), written);
        }
    }
}
