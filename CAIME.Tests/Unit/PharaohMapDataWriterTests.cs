using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Pharaoh's phar_main map_data.esf from Templates/phar_main/map.hex and compares it,
    /// node by node, with the game's own file in TestData.
    ///
    /// <para>
    /// The template's map.hex is older than the one the game's file was built from: CA has since
    /// painted attrition on about 45,000 hexes. Each hex's attrition byte is left out of the
    /// comparison; everything else must match. With the game's attrition put back in, the
    /// writer's output is byte for byte the game's apart from the order of the string table.
    /// </para>
    /// </summary>
    [TestClass]
    public class PharaohMapDataWriterTests
    {
        private const string THEATRE_INDEX      = "202976015";

        /// <summary>The db's maxx, a unit in the last place below 756.84f.</summary>
        private const float  MAX_X              = 756.83997f;

        private const int    BYTES_PER_HEX      = 5;
        private const int    ATTRITION_BYTE     = 2;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_pharaoh_map_data");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Load_VanillaPharaohMap_Loads()
        {
            var map = new MapHexFile();

            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "phar_main", "map.hex")));
            Assert.AreEqual(1134u, map.MapWidth);
        }

        [TestMethod]
        public void Write_PharMain_MatchesTheGamesFileApartFromAttrition()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "phar_main", "map.hex")));

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, PharaohMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, 0));

            var comparison = new MapDataEsfComparison
            {
                MaskedArrayParent   = "HEX_MAP_DATA",
                CompareByte         = index => index % BYTES_PER_HEX != ATTRITION_BYTE,
            };

            comparison.AssertSame(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "phar_main", "map_data.esf"), written);
        }
    }
}
