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
    ///
    /// <para>
    /// TestData has no Warhammer III file, so its test imports what our map_data writer builds from
    /// Templates/wh3_main_chaos_map_2, which matches the Assembly Kit's builder.
    /// </para>
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
        [TestMethod] public void RoundTrip_Troy()            => AssertGameFileRoundTrip(GameTemplate.Troy, "main_troy", saveFirst: false);
        [TestMethod] public void RoundTrip_TroyMythic()      => AssertGameFileRoundTrip(GameTemplate.Troy, "troy_mythic", saveFirst: false);
        [TestMethod] public void RoundTrip_Pharaoh()         => AssertGameFileRoundTrip(GameTemplate.Pharaoh, "phar_main");
        [TestMethod] public void RoundTrip_ThreeKingdoms()   => AssertGameFileRoundTrip(GameTemplate.Three_Kingdoms, "3k_dlc07_main_map");

        [TestMethod]
        public void RoundTrip_WarhammerIII()
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(MapDataEsfComparison.FindRepoFile("Templates", "wh3_main_chaos_map_2", "map.hex")));

            var source = Path.Combine(_dir, "source_map_data.esf");
            File.WriteAllBytes(source, Wh3MapDataWriter.Write(map, new MapDataTheatre { Index = "1", MaxX = 1000f }, 0));

            AssertRoundTrip(GameTemplate.Warhammer3, source);
        }

        private void AssertGameFileRoundTrip(GameTemplate game, string mapName, bool saveFirst = true)
        {
            AssertRoundTrip(game, MapDataEsfComparison.FindRepoFile("TestData", "campaign_maps", mapName, "map_data.esf"), saveFirst);
        }

        /// <param name="saveFirst">
        /// Whether to save and reload the map before writing. Saving makes every land hex that
        /// touches sea and is not a beach a cliff, and every other hex not one (MapHexFile.UpdateCliffs).
        /// Troy's maps have cliffs away from the sea, which any save loses, the game's own map.hex
        /// included, so their test writes from the imported map as it is.
        /// </param>
        private void AssertRoundTrip(GameTemplate game, string sourcePath, bool saveFirst = true)
        {
            var contents = MapDataReader.Read(sourcePath);
            CollectionAssert.Contains(MapDataReader.Games(contents), game);

            var mapDir = Path.Combine(_dir, "map");
            Directory.CreateDirectory(mapDir);

            var map = MapHexFile.CreateMapHex(mapDir, "map", ProjectManager.GetGameNameFromGame(game), "map", contents.Width, contents.Height, ProjectManager.GetMapHexVersion(game));
            MapDataImporter.Import(contents, map);

            if (saveFirst)
            {
                Assert.IsTrue(map.Save(mapDir, "map"), "The imported map did not save.");

                map = new MapHexFile();
                Assert.IsTrue(map.Load(Path.Combine(mapDir, "map.hex")), "The imported map did not load back.");
            }

            // Only Rome II's builder stores the playable area's maxy; the others derive it.
            var theatre = new MapDataTheatre
            {
                Index   = contents.TheatreIndex,
                MaxX    = contents.MaxX,
                MaxY    = game == GameTemplate.Rome2 ? contents.MaxY : (float?)null,
            };

            var written = Path.Combine(_dir, "map_data.esf");
            File.WriteAllBytes(written, MapDataWriter.Write(game, map, theatre, 0));

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
