using System.IO;
using System.Linq;
using CAIME.Classes.Importers;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Imports each game's own map_data.esf from TestData into a new map, saves and reloads the
    /// map.hex, writes map_data.esf from it with that game's writer, and compares the result, node
    /// by node, with the file it was imported from. Whatever the import infers, the map must write
    /// the same file back.
    /// </summary>
    [TestClass]
    public class MapDataImportRoundTripTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_map_data_round_trip");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.ResetStaticMaskState();
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod] public void RoundTrip_RomeII()          => AssertGameFileRoundTrip(GameTemplate.Rome2, "main_rome_map");
        [TestMethod] public void RoundTrip_Attila()          => AssertGameFileRoundTrip(GameTemplate.Attila, "main_attila_map");
        [TestMethod] public void RoundTrip_Warhammer()       => AssertGameFileRoundTrip(GameTemplate.Warhammer, "warhammer_map_1_1_wood_elves");
        [TestMethod] public void RoundTrip_WarhammerMasks()  => AssertGameFileRoundTrip(GameTemplate.Warhammer, "wh_dlc03_beastmen_map_1");
        [TestMethod] public void RoundTrip_WarhammerII()     => AssertGameFileRoundTrip(GameTemplate.Warhammer2, "wh2_main_great_vortex_map_7");
        [TestMethod] public void RoundTrip_WarhammerIIBig()  => AssertGameFileRoundTrip(GameTemplate.Warhammer2, "warhammer_map_1_1_twilight");
        [TestMethod] public void RoundTrip_Troy()            => AssertGameFileRoundTrip(GameTemplate.Troy, "main_troy");
        [TestMethod] public void RoundTrip_TroyMythic()      => AssertGameFileRoundTrip(GameTemplate.Troy, "troy_mythic");
        [TestMethod] public void RoundTrip_Pharaoh()         => AssertGameFileRoundTrip(GameTemplate.Pharaoh, "phar_main");
        [TestMethod] public void RoundTrip_ThreeKingdoms()   => AssertGameFileRoundTrip(GameTemplate.Three_Kingdoms, "3k_dlc07_main_map");
        [TestMethod] public void RoundTrip_WarhammerIII()    => AssertGameFileRoundTrip(GameTemplate.Warhammer3, "wh3_main_prologue_map");
        [TestMethod] public void RoundTrip_WarhammerIIIBig() => AssertGameFileRoundTrip(GameTemplate.Warhammer3, "wh3_main_chaos_map_4");

        private void AssertGameFileRoundTrip(GameTemplate game, string mapName)
        {
            AssertRoundTrip(game, MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", mapName, "map_data.esf"));
        }

        private void AssertRoundTrip(GameTemplate game, string sourcePath)
        {
            var contents = MapDataReader.Read(sourcePath);
            CollectionAssert.Contains(MapDataReader.Games(contents), game);

            var mapDir = Path.Combine(_dir, "map");
            Directory.CreateDirectory(mapDir);

            var map = MapHexFile.CreateMapHex(mapDir, "map", ProjectManager.GetGameNameFromGame(game), "map", contents.Width, contents.Height, ProjectManager.GetMapHexVersion(game));
            MapDataImporter.Import(contents, map);

            // Saving sets land and sea from the ground types and cliffs from land and sea
            // (MapHexFile.UpdateHexTypes), so the inferred ground types must be right.
            Assert.IsTrue(map.Save(mapDir, "map"), "The imported map did not save.");

            map = new MapHexFile();
            Assert.IsTrue(map.Load(Path.Combine(mapDir, "map.hex")), "The imported map did not load back.");

            // Only Rome II's builder stores the playable area's maxy; the others derive it.
            var theatre = new MapDataTheatre
            {
                Index   = contents.TheatreIndex,
                MaxX    = contents.MaxX,
                MaxY    = game == GameTemplate.Rome2 ? contents.MaxY : (float?)null,
            };

            // The game's Warhammer III files are older builds, written as ABCA.
            var bytes = game == GameTemplate.Warhammer3
                ? Wh3MapDataWriter.Write(map, theatre, 0, new Wh3MapDataWriter.Layout { Format = EsfFormat.Abca })
                : MapDataWriter.Write(game, map, theatre, 0);

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, bytes);

            var comparison = new MapDataEsfComparison
            {
                // Three Kingdoms validates against the map.hex's save time.
                SkippedRecord       = "MAP_FILE_VALIDATION",

                // The game's Rome II file has an older builder's theatre (v2, with a colour table).
                OlderRecord         = "CAMPAIGN_THEATRE",
                OlderRecordExtra    = node => node.TypeCode == EsfType.UINT32_24BIT_ARRAY,
            };
            comparison.AssertSame(sourcePath, written);
        }
    }
}
