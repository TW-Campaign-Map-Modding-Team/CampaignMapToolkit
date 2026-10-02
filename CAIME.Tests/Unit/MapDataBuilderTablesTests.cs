using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CAIME.Exporters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="MapDataBuilderTables"/>, the tables MapDataBuilder's data builder
    /// reads in place of the Assembly Kit's own when a project's database comes from RPFM. A table it
    /// lacks crashes the data builder, and a campaign_maps row it lacks makes it report success yet
    /// write nothing, so every table must always be there.
    /// </summary>
    [TestClass]
    public class MapDataBuilderTablesTests
    {
        private const string MapName = "main_rome_map";

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "caime_map_data_tables_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }

        [TestMethod]
        public void Build_KeepsOnlyTheMapsPlayableAreaRows_WithTheirOwnValues()
        {
            var tables = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1564135515", maxX: "678.5", maxY: "555.09"),
                PlayableArea("other_map", index: "7", maxX: "100", maxY: "100")));

            var rows = tables[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS].Root.Elements().ToList();

            Assert.AreEqual(1, rows.Count, "Only the open map's playable area belongs in the table.");
            Assert.AreEqual("1564135515", (string)rows[0].Element("index"));
            Assert.AreEqual("main_rome_lookup.tga", (string)rows[0].Element("overlay_file"));
            Assert.AreEqual("1", (string)rows[0].Element("sea_trade"));
            Assert.AreEqual("555.09", (string)rows[0].Element("maxy"), "A maxy the database has must be kept as it is.");
        }

        [TestMethod]
        public void Build_WorksOutMaxY_WhenTheRowHasNone()
        {
            // Zero is the schema default, so it is what a map with no row in the Assembly Kit gets.
            var tables = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "0")));

            var row = tables[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS].Root.Elements().Single();

            Assert.AreEqual("555.41", (string)row.Element("maxy"));
        }

        [TestMethod]
        public void Build_AddsMinXAndMinY_WhenTheRowHasNone()
        {
            var playableArea = PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "555.09");
            playableArea.Element("minx").Remove();
            playableArea.Element("miny").Remove();

            var row = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(playableArea))[Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS]
                .Root.Elements().Single();

            Assert.AreEqual("0", (string)row.Element("minx"));
            Assert.AreEqual("0", (string)row.Element("miny"));
        }

        [TestMethod]
        public void Build_GivesCampaignMapsTheMapsRow()
        {
            var tables = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "555.09")));

            var row = tables[Constants.TABLE_CAMPAIGN_MAPS].Root.Elements(Constants.TABLE_CAMPAIGN_MAPS).Single();

            Assert.AreEqual(MapName, (string)row.Element("mapname"));
            foreach (var bound in new[] { "minx", "miny", "maxx", "maxy" })
            {
                Assert.IsNotNull(row.Element(bound), $"The data builder fails when campaign_maps has no {bound}.");
            }
        }

        [TestMethod]
        public void Build_GivesEveryOtherTableItNeeds()
        {
            var tables = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "555.09")));

            foreach (var table in new[] { Constants.TABLE_REGIONS, Constants.TABLE_CAMPAIGN_MAP_SETTLEMENTS, Constants.TABLE_CAMPAIGN_GROUND_TYPES })
            {
                Assert.AreEqual(1, tables[table].Root.Elements(table).Count(), $"The data builder crashes when {table} has no rows.");
            }

            Assert.AreEqual(0, tables[Constants.TABLE_CAMPAIGN_MAP_SLOTS].Root.Elements().Count(),
                "Rome II's data builder needs campaign_map_slots to exist, and the Assembly Kit's own copy is empty.");
        }

        [TestMethod]
        public void Build_MapWithNoPlayableArea_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea("other_map", index: "7", maxX: "100", maxY: "100"))));
        }

        [TestMethod]
        public void Build_PlayableAreaWithoutMaxX_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "0", maxY: "555.09"))));
        }

        [TestMethod]
        public void Write_WritesEachTableAsXmlWithoutABom()
        {
            var tables = MapDataBuilderTables.Build(MapName, 1016, 720, PlayableAreas(
                PlayableArea(MapName, index: "1", maxX: "678.5", maxY: "555.09")));

            MapDataBuilderTables.Write(tables, _dir);

            foreach (var table in tables.Keys)
            {
                var path = Path.Combine(_dir, table + ".xml");
                Assert.IsTrue(File.Exists(path), $"{table}.xml was not written.");
                Assert.AreNotEqual(0xEF, File.ReadAllBytes(path)[0], "The Assembly Kit's reader crashes on a UTF-8 BOM.");
            }
        }

        // The Assembly Kit's own values, typed in by hand, for maps whose hex grid size is known.
        [DataTestMethod]
        [DataRow(800u, 600u, 533.74, 462.43, DisplayName = "wh3_main_prologue_map")]
        [DataRow(1440u, 970u, 961.3, 748.1, DisplayName = "wh3_main_combi_map_7")]
        [DataRow(1016u, 720u, 678.5, 555.09, DisplayName = "main_rome_map")]
        [DataRow(400u, 440u, 266.53, 338.9, DisplayName = "wh_dlc05_wood_elves_map_1")]
        [DataRow(2048u, 1648u, 1367.39, 1271.48, DisplayName = "phar_combi")]
        public void EstimatePlayableAreaMaxY_LandsNearTheAssemblyKitsOwnValue(uint columns, uint rows, double maxX, double assemblyKitMaxY)
        {
            var estimate = MapDataBuilderTables.EstimatePlayableAreaMaxY(maxX, columns, rows);

            Assert.AreEqual(assemblyKitMaxY, estimate, 1.5);
        }

        private static XDocument PlayableAreas(params XElement[] rows)
        {
            return new XDocument(new XElement("dataroot", rows));
        }

        private static XElement PlayableArea(string mapName, string index, string maxX, string maxY)
        {
            return new XElement(Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS,
                new XElement("index", index),
                new XElement("sea_trade", "1"),
                new XElement("overlay_file", "main_rome_lookup.tga"),
                new XElement("minx", "0"),
                new XElement("maxx", maxX),
                new XElement("mapname", mapName),
                new XElement("maxy", maxY),
                new XElement("miny", "0"));
        }
    }
}
