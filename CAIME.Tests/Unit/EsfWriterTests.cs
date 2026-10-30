using System;
using System.IO;
using System.Linq;
using CAIME.Tests.Helpers;
using EsfLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class EsfWriterTests
    {
        private const int HEADER_SIZE = 16;

        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            _dir = MapHexHarness.CreateTempDir("caime_abcb_writer");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        private ParentNode ReadBack(byte[] bytes)
        {
            var path = Path.Combine(_dir, "written.esf");
            File.WriteAllBytes(path, bytes);
            return (ParentNode)EsfCodecUtil.LoadEsfFile(path).RootNode;
        }

        [TestMethod]
        public void Write_SmallFile_ReadsBackThroughTheAbcbCodec()
        {
            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 2);
            esf.WriteAscii("hello");
            esf.WriteUInt16(513);
            esf.WriteUInt32(70000);
            esf.WriteCoordinates(1.5f, -2.25f);
            esf.BeginBlock("ITEMS", 0);
            esf.BeginBlockEntry();
            esf.WriteAscii("world");
            esf.EndBlockEntry();
            esf.BeginBlockEntry();
            esf.WriteAscii("hello");
            esf.EndBlockEntry();
            esf.EndBlock();
            esf.WriteAsciiArray(new[] { "world", "again" });
            esf.EndRecord();

            var root = ReadBack(esf.ToArray(0));

            Assert.AreEqual("ROOT", root.Name);
            Assert.AreEqual(2, root.Version);
            Assert.AreEqual("hello", ((EsfValueNode<string>)root.Values[0]).Value);
            Assert.AreEqual((ushort)513, ((EsfValueNode<ushort>)root.Values[1]).Value);
            Assert.AreEqual(70000u, ((EsfValueNode<uint>)root.Values[2]).Value);
            Assert.AreEqual(Tuple.Create(1.5f, -2.25f), ((EsfValueNode<Tuple<float, float>>)root.Values[3]).Value);
            CollectionAssert.AreEqual(new[] { "world", "again" }, ((EsfValueNode<string[]>)root.Values[4]).Value);

            var items = root.Children.Single();
            Assert.AreEqual("ITEMS", items.Name);
            CollectionAssert.AreEqual(new[] { "world", "hello" }, items.Children.Select(entry => ((EsfValueNode<string>)entry.Values[0]).Value).ToArray());
        }

        [TestMethod]
        public void WriteUInt32_UsesTheShortestOptimisedEncoding()
        {
            var expected = new (uint Value, EsfType Type)[]
            {
                (0, EsfType.UINT32_ZERO), (1, EsfType.UINT32_ONE), (255, EsfType.UINT32_BYTE),
                (256, EsfType.UINT32_SHORT), (0x10000, EsfType.UINT32_24BIT), (0x1000000, EsfType.UINT32),
            };

            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);
            foreach (var item in expected)
            {
                esf.WriteUInt32(item.Value);
            }

            esf.EndRecord();

            var values = ReadBack(esf.ToArray(0)).Values;
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].Type, values[i].TypeCode, $"{expected[i].Value}");
                Assert.AreEqual(expected[i].Value, ((EsfValueNode<uint>)values[i]).Value);
            }
        }

        /// <summary>
        /// Sizes take the shortest encoding while their placeholder is still in the write buffer.
        /// The root, written in the long form, is at offset 16; its size follows its four info bytes.
        /// </summary>
        [TestMethod]
        public void Write_SizesStillInTheBuffer_TakeTheirShortestEncoding()
        {
            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);
            esf.WriteUInt16(7);
            esf.EndRecord();

            var bytes = esf.ToArray(0);

            Assert.AreEqual(0x80, bytes[HEADER_SIZE]);
            Assert.AreEqual(3, bytes[HEADER_SIZE + 4], "The root's size, one byte.");
            Assert.AreEqual((byte)EsfType.UINT16, bytes[HEADER_SIZE + 5]);
        }

        /// <summary>
        /// An array larger than the 1 MiB write buffer flushes it while the sizes around it are
        /// open, so they keep all five bytes, padded with 0x80.
        /// </summary>
        [TestMethod]
        public void Write_SizesOpenAcrossAFlush_ArePaddedToFiveBytes()
        {
            var data = new byte[3 * 1024 * 1024];

            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);
            esf.WriteByteArray(data);
            esf.EndRecord();

            var bytes = esf.ToArray(0);
            int rootSize = 1 + 5 + data.Length;

            CollectionAssert.AreEqual(EncodePadded(rootSize), bytes.Skip(HEADER_SIZE + 4).Take(5).ToArray());
            Assert.AreEqual((byte)EsfType.UINT8_ARRAY, bytes[HEADER_SIZE + 9]);
            CollectionAssert.AreEqual(EncodePadded(data.Length), bytes.Skip(HEADER_SIZE + 10).Take(5).ToArray());

            var root = ReadBack(bytes);
            Assert.AreEqual(data.Length, ((EsfValueNode<byte[]>)root.Values[0]).Value.Length);
        }

        private static byte[] EncodePadded(int value)
        {
            var bytes = new byte[5];
            for (int i = 4; i >= 0; --i)
            {
                bytes[i] = (byte)((value & 0x7F) | (i == 4 ? 0 : 0x80));
                value >>= 7;
            }

            return bytes;
        }

        [TestMethod]
        public void Write_StringIds_AreGivenInOrderOfFirstUse()
        {
            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);
            esf.WriteAscii("b");
            esf.WriteAscii("a");
            esf.WriteAscii("b");
            esf.EndRecord();

            var bytes = esf.ToArray(0);
            int stringsAt = HEADER_SIZE + 4 + 1 + 15;   // the root's info, its size, three references

            Assert.AreEqual(0, BitConverter.ToInt32(bytes, HEADER_SIZE + 4 + 1 + 1));
            Assert.AreEqual(1, BitConverter.ToInt32(bytes, HEADER_SIZE + 4 + 1 + 6));
            Assert.AreEqual(0, BitConverter.ToInt32(bytes, HEADER_SIZE + 4 + 1 + 11));
            Assert.AreEqual(stringsAt, (int)BitConverter.ToUInt32(bytes, 12));
        }

        [TestMethod]
        public void ToArray_WithARecordStillOpen_Throws()
        {
            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);

            Assert.ThrowsException<InvalidOperationException>(() => esf.ToArray(0));
        }

        [TestMethod]
        public void WriteAscii_WithANonAsciiCharacter_Throws()
        {
            var esf = new EsfWriter(EsfFormat.Abcb);
            esf.BeginRecord("ROOT", 0);

            Assert.ThrowsException<ArgumentException>(() => esf.WriteAscii("café"));
        }
    }
}
