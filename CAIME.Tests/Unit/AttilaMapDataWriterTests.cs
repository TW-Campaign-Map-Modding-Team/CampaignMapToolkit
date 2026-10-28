using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Attila's main_attila_map map_data.esf from Templates/main_attila_map/map.hex and
    /// compares it, node by node, with the game's own file in TestData.
    /// </summary>
    [TestClass]
    public class AttilaMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1564135515";
        private const float  MAX_X          = 678.5f;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_attila_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Write_MainAttilaMap_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_attila_map", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, AttilaMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "main_attila_map", "map_data.esf"), written);
        }
    }
}
