using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Troy's map_data.esf files from Templates' map.hex files and compares them, node by
    /// node, with the game's own files in TestData. Both match as they are. troy_historical is not
    /// tested: its map.hex and map_data.esf are byte for byte main_troy's.
    /// </summary>
    [TestClass]
    public class TroyMapDataWriterTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_troy_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Write_MainTroy_MatchesTheGamesFile()
        {
            AssertMatchesTheGamesFile("main_troy", new MapDataTheatre { Index = "1999143295", MaxX = 640.6f });
        }

        [TestMethod]
        public void Write_TroyMythic_MatchesTheGamesFile()
        {
            AssertMatchesTheGamesFile("troy_mythic", new MapDataTheatre { Index = "773255857", MaxX = 640.6f });
        }

        private void AssertMatchesTheGamesFile(string mapName, MapDataTheatre theatre)
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", mapName, "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, TroyMapDataWriter.Write(map, theatre, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", mapName, "map_data.esf"), written);
        }
    }
}
