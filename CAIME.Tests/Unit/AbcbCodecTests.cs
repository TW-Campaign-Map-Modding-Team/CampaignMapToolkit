using System.IO;
using System.Text;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Warhammer III writes map_data.esf as ABCB: the ABCA layout, except that strings in the
    /// UTF-16 and ASCII lookup lists have a 32-bit length. The test file is a MAP_DATA root holding
    /// one ASCII string and one ASCII string array.
    /// </summary>
    [TestClass]
    public class AbcbCodecTests
    {
        private const int TIMESTAMP_OFFSET = 8;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = MapHexHarness.CreateTempDir("caime_abcb");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private static byte[] BuildAbcbFile()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0xABCBu);
                writer.Write(0);
                writer.Write(0);            // timestamp
                writer.Write(0);            // offset of the node names, written below

                writer.Write((byte)0x80);   // the root record, always in the long form
                writer.Write((ushort)0);    // node name 0: MAP_DATA
                writer.Write((byte)2);      // version
                writer.Write((byte)15);     // size of its children
                writer.Write((byte)0x0F);   // ASCII string reference...
                writer.Write(0);            // ...to "hello"
                writer.Write((byte)0x4F);   // ASCII string reference array...
                writer.Write((byte)8);      // ...of 8 bytes...
                writer.Write(1);            // ...referencing "world"
                writer.Write(0);            // ...and "hello"

                var nodeNamesOffset = (int)stream.Position;
                writer.Write((ushort)1);
                writer.Write((ushort)"MAP_DATA".Length);
                writer.Write(Encoding.ASCII.GetBytes("MAP_DATA"));

                writer.Write(0);            // no UTF-16 strings
                writer.Write(2);            // two ASCII strings, each with a 32-bit length
                WriteAsciiTableEntry(writer, "hello", 0);
                WriteAsciiTableEntry(writer, "world", 1);

                stream.Position = 12;
                writer.Write(nodeNamesOffset);

                return stream.ToArray();
            }
        }

        private static void WriteAsciiTableEntry(BinaryWriter writer, string text, int id)
        {
            writer.Write(text.Length);
            writer.Write(Encoding.ASCII.GetBytes(text));
            writer.Write(id);
        }

        private string WriteFile(string name, byte[] bytes)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [TestMethod]
        public void Load_AbcbFile_ResolvesStringsFromTheLongLengthTables()
        {
            var file = EsfCodecUtil.LoadEsfFile(WriteFile("map_data.esf", BuildAbcbFile()));
            var root = (ParentNode)file.RootNode;

            Assert.IsInstanceOfType(file.Codec, typeof(AbcbFileCodec));
            Assert.AreEqual("MAP_DATA", root.Name);
            Assert.AreEqual(2, root.Version);
            Assert.AreEqual("hello", ((EsfValueNode<string>)root.Values[0]).Value);
            CollectionAssert.AreEqual(new[] { "world", "hello" }, ((EsfValueNode<string[]>)root.Values[1]).Value);
        }

        [TestMethod]
        public void Write_LoadedAbcbFile_WritesTheSameBytesBack()
        {
            var original = BuildAbcbFile();
            var file = EsfCodecUtil.LoadEsfFile(WriteFile("map_data.esf", original));

            var rewrittenPath = Path.Combine(_dir, "rewritten.esf");
            EsfCodecUtil.WriteEsfFile(rewrittenPath, file);
            var rewritten = File.ReadAllBytes(rewrittenPath);

            Assert.AreEqual(original.Length, rewritten.Length);
            for (int i = 0; i < original.Length; ++i)
            {
                if (i >= TIMESTAMP_OFFSET && i < TIMESTAMP_OFFSET + 4)
                {
                    continue;
                }
                Assert.AreEqual(original[i], rewritten[i], $"Byte {i} differs.");
            }
        }
    }
}
