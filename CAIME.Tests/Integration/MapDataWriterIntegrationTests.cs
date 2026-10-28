using System;
using System.Collections.Generic;
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
    /// Everything before the string tables must match byte for byte, apart from the timestamp at
    /// bytes 8-11. The string tables must hold the same strings under the same ids; their order is
    /// the Assembly Kit's hash-map order, which the writer does not reproduce.
    /// </para>
    /// </summary>
    [TestClass]
    public class MapDataWriterIntegrationTests
    {
        private const int TIMESTAMP_OFFSET          = 8;
        private const int STRING_TABLES_OFFSET_AT   = 12;
        private const int HEADER_SIZE               = 16;

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

                var written = Wh3MapDataWriter.Write(map, ReadTheatre(referencePath), BitConverter.ToUInt32(reference, TIMESTAMP_OFFSET));
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
            int referenceTables = (int)BitConverter.ToUInt32(reference, STRING_TABLES_OFFSET_AT);
            int writtenTables   = (int)BitConverter.ToUInt32(written, STRING_TABLES_OFFSET_AT);

            if (referenceTables != writtenTables)
            {
                failures.AppendLine($"{mapName}: the nodes take {writtenTables - HEADER_SIZE} bytes; the Assembly Kit's take {referenceTables - HEADER_SIZE}.");
                return;
            }

            for (int i = HEADER_SIZE; i < referenceTables; ++i)
            {
                if (reference[i] != written[i])
                {
                    failures.AppendLine($"{mapName}: the nodes first differ at byte {i}.");
                    return;
                }
            }

            var referenceStrings    = ReadStringTables(reference, referenceTables);
            var writtenStrings      = ReadStringTables(written, writtenTables);

            if (referenceStrings.Count != writtenStrings.Count || referenceStrings.Except(writtenStrings).Any())
            {
                failures.AppendLine($"{mapName}: the node names or strings differ.");
            }
        }

        /// <summary>The node names (keyed by index) and ASCII strings (keyed by id), as one set of tagged entries.</summary>
        private static HashSet<string> ReadStringTables(byte[] file, int offset)
        {
            var entries = new HashSet<string>();

            using (var reader = new BinaryReader(new MemoryStream(file, offset, file.Length - offset)))
            {
                int nodeNames = reader.ReadUInt16();
                for (int i = 0; i < nodeNames; ++i)
                {
                    entries.Add($"name {i}: {Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadUInt16()))}");
                }

                Assert.AreEqual(0, reader.ReadInt32(), "map_data.esf holds no UTF-16 strings.");

                int strings = reader.ReadInt32();
                for (int i = 0; i < strings; ++i)
                {
                    var text = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadInt32()));
                    entries.Add($"string {reader.ReadInt32()}: {text}");
                }
            }

            return entries;
        }
    }
}
