using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Attila's main_attila_map map_data.esf from Templates/main_attila_map/map.hex and
    /// compares it, node by node, with the game's own file in TestData.
    ///
    /// <para>
    /// The template's map.hex lacks what the slot positions, the campaign UI's settlement positions
    /// and the port docks are made from, so those are left out of the comparison; everything else
    /// must match.
    /// </para>
    /// </summary>
    [TestClass]
    public class AttilaMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1564135515";
        private const float  MAX_X          = 678.5f;

        /// <summary>The last three values of REGION_DATA: the dock's region, column and row.</summary>
        private const int DOCK_VALUES = 3;

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
        public void Write_MainAttilaMap_MatchesTheGamesFileApartFromPositionsAndDocks()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_attila_map", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, AttilaMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0));

            var comparison = new MapDataEsfComparison
            {
                SkipNode        = name => name.EndsWith("POSITIONS_BLOCK") || name == "REGION_KEYS",
                SkipRegionTail  = DOCK_VALUES,
            };

            comparison.AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "main_attila_map", "map_data.esf"), written);
        }
    }
}
