using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Warhammer II's map_data.esf files from the Assembly Kit's map.hex files (Templates'
    /// copies) and compares them, node by node, with the game's own files in TestData. Both match as
    /// they are. warhammer_map_1_1_twilight is the 1016-wide map whose positions show the builder
    /// rounds the hex radius as Warhammer III does, and the one with a cliff beside an impassable sea
    /// bridge.
    /// </summary>
    [TestClass]
    public class Wh2MapDataWriterTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_wh2_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Write_GreatVortexMap_MatchesTheGamesFile()
        {
            AssertMatchesTheGamesFile("wh2_main_great_vortex_map_7", new MapDataTheatre { Index = "1023253540", MaxX = 484.3f });
        }

        [TestMethod]
        public void Write_TwilightMap_MatchesTheGamesFile()
        {
            AssertMatchesTheGamesFile("warhammer_map_1_1_twilight", new MapDataTheatre { Index = "131458620", MaxX = 678.5f });
        }

        private void AssertMatchesTheGamesFile(string mapName, MapDataTheatre theatre)
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", mapName, "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, Wh2MapDataWriter.Write(map, theatre, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", mapName, "map_data.esf"), written);
        }
    }
}
