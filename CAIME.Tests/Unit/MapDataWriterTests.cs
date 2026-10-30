using System;
using System.Xml.Linq;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="MapDataWriter"/>: which writer each game gets, and the theatre read
    /// from a project's campaign_map_playable_areas table.
    /// </summary>
    [TestClass]
    public class MapDataWriterTests
    {
        private const string MapName = "main_rome_map";

        [TestMethod]
        public void ReadTheatre_TakesTheMapsFirstRow()
        {
            var theatre = MapDataWriter.ReadTheatre(GameTemplate.Attila, MapName, PlayableAreas(
                PlayableArea("other_map", index: "7", maxX: "100", maxY: "100"),
                PlayableArea(MapName, index: "1564135515", maxX: "678.5", maxY: "555.09"),
                PlayableArea(MapName, index: "2", maxX: "50", maxY: "50")));

            Assert.AreEqual("1564135515", theatre.Index);
            Assert.AreEqual(678.5f, theatre.MaxX);
        }

        [TestMethod]
        public void ReadTheatre_KeepsMaxYOnlyForRomeII()
        {
            var playableAreas = PlayableAreas(PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "555.09"));

            Assert.AreEqual(555.09f, MapDataWriter.ReadTheatre(GameTemplate.Rome2, MapName, playableAreas).MaxY);
            Assert.IsNull(MapDataWriter.ReadTheatre(GameTemplate.Attila, MapName, playableAreas).MaxY,
                "Every other game's builder works maxy out from the hex grid.");
        }

        [TestMethod]
        public void ReadTheatre_ZeroMaxY_IsLeftToTheEstimate()
        {
            // Zero is the schema default, and Rome II's packs have no maxy at all.
            var theatre = MapDataWriter.ReadTheatre(GameTemplate.Rome2, MapName, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "0")));

            Assert.IsNull(theatre.MaxY);
        }

        [TestMethod]
        public void ReadTheatre_ParsesMaxXToTheSameFloat()
        {
            // Parsed straight to a float, not through a double, so the text's own float is kept.
            var theatre = MapDataWriter.ReadTheatre(GameTemplate.Pharaoh, MapName, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "756.83997", maxY: "0")));

            Assert.AreEqual(756.83997f, theatre.MaxX);
        }

        [TestMethod]
        public void ReadTheatre_MapWithNoPlayableArea_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => MapDataWriter.ReadTheatre(GameTemplate.Rome2, MapName, PlayableAreas(
                PlayableArea("other_map", index: "7", maxX: "100", maxY: "100"))));
        }

        [TestMethod]
        public void ReadTheatre_PlayableAreaWithoutMaxX_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => MapDataWriter.ReadTheatre(GameTemplate.Rome2, MapName, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "0", maxY: "555.09"))));
        }

        [TestMethod]
        public void Write_ThronesOfBritannia_WritesAttilasFile()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "main_attila_map", "map.hex")));
            var theatre = new MapDataTheatre { Index = "1", MaxX = 678.5f };

            CollectionAssert.AreEqual(AttilaMapDataWriter.Write(map, theatre, 0), MapDataWriter.Write(GameTemplate.Thrones_Of_Britannia, map, theatre, 0));
        }

        // The Assembly Kit's own values, typed in by hand, for maps whose hex grid size is known.
        [DataTestMethod]
        [DataRow(800u, 600u, 533.74, 462.43, DisplayName = "wh3_main_prologue_map")]
        [DataRow(1440u, 970u, 961.3, 748.1, DisplayName = "wh3_main_combi_map_7")]
        [DataRow(1016u, 720u, 678.5, 555.09, DisplayName = "main_rome_map")]
        [DataRow(400u, 440u, 266.53, 338.9, DisplayName = "wh_dlc05_wood_elves_map_1")]
        [DataRow(2048u, 1648u, 1367.39, 1271.48, DisplayName = "phar_combi")]
        public void RomeII_EstimateMaxY_LandsNearTheAssemblyKitsOwnValue(uint columns, uint rows, double maxX, double assemblyKitMaxY)
        {
            Assert.AreEqual(assemblyKitMaxY, RomeIIMapDataWriter.EstimateMaxY(maxX, columns, rows), 1.5);
        }

        private static XDocument PlayableAreas(params XElement[] rows)
        {
            return new XDocument(new XElement("dataroot", rows));
        }

        private static XElement PlayableArea(string mapName, string index, string maxX, string maxY)
        {
            return new XElement(Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS,
                new XElement("mapname", mapName),
                new XElement("index", index),
                new XElement("maxx", maxX),
                new XElement("maxy", maxY));
        }
    }
}
