using System;
using System.IO;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Builds Attila's main_attila_map map_data.esf from Templates/main_attila_map/map.hex and
    /// compares it, byte by byte, with the game's own file in TestData.
    /// </summary>
    [TestClass]
    public class AttilaMapDataWriterTests
    {
        private const string THEATRE_INDEX  = "1564135515";
        private const float  MAX_X          = 678.5f;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
        }

        [TestMethod]
        public void Write_MainAttilaMap_MatchesTheGamesFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_attila_map", "map.hex")));

            var reference   = File.ReadAllBytes(MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", "main_attila_map", "map_data.esf"));
            var written     = AttilaMapDataWriter.Write(map, new MapDataTheatre { Index = THEATRE_INDEX, MaxX = MAX_X }, BitConverter.ToUInt32(reference, 8));

            CollectionAssert.AreEqual(reference, written);
        }
    }
}
