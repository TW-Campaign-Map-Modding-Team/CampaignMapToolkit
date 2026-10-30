using System.IO;
using System.Linq;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Warhammer's map_data.esf files from the Assembly Kit's map.hex files and compares them,
    /// node by node, with the game's own files in TestData.
    ///
    /// <para>
    /// wh_dlc03_beastmen_map_1 has a second mask ("8") and matches as it is. The Assembly Kit's
    /// warhammer_map_1_1_wood_elves map.hex (also Templates') was saved six days after the game's
    /// file was built and paints 38 hexes into other regions; the test repaints them as the game's
    /// file has them (TestData/.../game_regions.txt). That map is the one whose positions show the
    /// builder rounds the hex radius as Attila's does. wh_dlc05_wood_elves_map_1 (mask "32") matches
    /// the same way once 85 lake hexes are painted into wh_main_sea_lake; it is not in TestData.
    /// </para>
    /// </summary>
    [TestClass]
    public class Wh1MapDataWriterTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_wh1_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Write_BeastmenMap_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "wh_dlc03_beastmen_map_1", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, Wh1MapDataWriter.Write(map, new MapDataTheatre { Index = "1564135542", MaxX = 275.8f }, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "wh_dlc03_beastmen_map_1", "map_data.esf"), written);
        }

        [TestMethod]
        public void Write_MainMap_WithTheGamesRegions_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "warhammer_map_1_1_wood_elves", "map.hex")));

            var regions = File.ReadAllLines(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "warhammer_map_1_1_wood_elves", "game_regions.txt"));
            foreach (var line in regions.Where(line => line.StartsWith("#") == false))
            {
                var parts = line.Split(' ');
                map.HexData[int.Parse(parts[0])].RegionId = int.Parse(parts[1]);
            }

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, Wh1MapDataWriter.Write(map, new MapDataTheatre { Index = "1564135556", MaxX = 678.5f }, 0));

            new MapDataEsfComparison().AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "warhammer_map_1_1_wood_elves", "map_data.esf"), written);
        }
    }
}
