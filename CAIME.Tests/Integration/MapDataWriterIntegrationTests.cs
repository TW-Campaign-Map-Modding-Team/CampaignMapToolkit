using System;
using System.IO;
using System.Linq;
using System.Text;
using CAIME.Exporters;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Builds map_data.esf from real Warhammer III map.hex files and compares it with the
    /// Assembly Kit's own build of each.
    ///
    /// <para>
    /// The data lives outside the repository: set <c>CAIME_MAP_DATA_DIR</c> to a folder of
    /// <c>&lt;map&gt;/map_data.esf</c> + <c>&lt;map&gt;/map.hex</c> pairs, the map_data.esf built by the
    /// Assembly Kit from that map.hex. The playable area's index and maxx are read back from the
    /// reference file. Without the variable the test is inconclusive.
    /// </para>
    ///
    /// <para>
    /// The file must match byte for byte, written with the reference's timestamp and format: older
    /// builds of the game's files are ABCA. The prologue and chaos_map_4 are also in TestData
    /// (<see cref="Unit.Wh3MapDataWriterTests"/>).
    /// </para>
    /// </summary>
    [TestClass]
    public class MapDataWriterIntegrationTests
    {
        private const uint ABCA_MAGIC               = 0xABCA;
        private const int TIMESTAMP_OFFSET          = 8;
        private const int STRING_TABLES_OFFSET_AT   = 12;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
        }

        [TestMethod]
        public void Write_RealMaps_MatchTheAssemblyKitsBuild()
        {
            var dir = Environment.GetEnvironmentVariable("CAIME_MAP_DATA_DIR");
            var folders = string.IsNullOrEmpty(dir) || Directory.Exists(dir) == false
                ? Array.Empty<string>()
                : Directory.GetDirectories(dir).Where(folder => File.Exists(Path.Combine(folder, "map_data.esf")) && File.Exists(Path.Combine(folder, "map.hex"))).ToArray();

            if (folders.Length == 0)
            {
                Assert.Inconclusive("No map_data.esf + map.hex pairs found. Set CAIME_MAP_DATA_DIR to enable this test.");
            }

            var failures = new StringBuilder();

            foreach (var folder in folders)
            {
                var mapName     = Path.GetFileName(folder);
                var referencePath = Path.Combine(folder, "map_data.esf");
                var reference   = File.ReadAllBytes(referencePath);

                var map = new MapHexFile();
                Assert.IsTrue(map.Load(Path.Combine(folder, "map.hex")), $"{mapName}: map.hex did not load.");

                var layout  = new Wh3MapDataWriter.Layout { Format = BitConverter.ToUInt32(reference, 0) == ABCA_MAGIC ? EsfFormat.Abca : EsfFormat.Abcb };
                var written = Wh3MapDataWriter.Write(map, ReadTheatre(referencePath), BitConverter.ToUInt32(reference, TIMESTAMP_OFFSET), layout);
                Compare(mapName, reference, written, failures);
            }

            if (failures.Length > 0)
            {
                Assert.Fail(failures.ToString());
            }
        }

        private static MapDataTheatre ReadTheatre(string mapDataEsfPath)
        {
            var root        = (ParentNode)EsfCodecUtil.LoadEsfFile(mapDataEsfPath).RootNode;
            var theatre     = root.Children.Single(node => node.Name == "theatres").Children[0].Children.Single(node => node.Name == "CAMPAIGN_THEATRE");
            var values      = theatre.Values;
            var maxCorner   = ((EsfValueNode<Tuple<float, float>>)values[1]).Value;

            return new MapDataTheatre { Index = ((EsfValueNode<string>)values[2]).Value, MaxX = maxCorner.Item1 };
        }

        private static void Compare(string mapName, byte[] reference, byte[] written, StringBuilder failures)
        {
            int stringTables = (int)BitConverter.ToUInt32(reference, STRING_TABLES_OFFSET_AT);

            for (int i = 0; i < Math.Min(reference.Length, written.Length); ++i)
            {
                if (reference[i] != written[i])
                {
                    failures.AppendLine($"{mapName}: the {(i < stringTables ? "nodes" : "string tables")} first differ at byte {i}.");
                    return;
                }
            }

            if (reference.Length != written.Length)
            {
                failures.AppendLine($"{mapName}: {written.Length} bytes; the Assembly Kit's build has {reference.Length}.");
            }
        }
    }
}
