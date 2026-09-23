using System;
using System.IO;
using System.Text;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// The block between the name lists and the colour tables, and the block after the hex data,
    /// are two lists sharing a count (see <see cref="MapHexFile.UnknownEntries"/>). Every vanilla
    /// map carries a single empty entry, which a fixed-size read happened to fit; the Warhammer 1
    /// mini-campaign maps (wh_dlc05_wood_elves_map_1, wh_dlc03_beastmen_map_1) carry a second one
    /// and loaded as "20 x 0 hexes" or threw an array-size exception.
    ///
    /// <para>
    /// The tests craft such a file from a blank map - a second entry ("32", 0) and a second
    /// trailing blob, the way the game's own files lay them out - and check that it loads at its
    /// real size and is written back byte for byte (timestamp and CRC aside).
    /// </para>
    /// </summary>
    [TestClass]
    public class MapHexEntryListTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_entrylist");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        [DataTestMethod]
        [DataRow(0x12, "warhammer")]
        [DataRow(0x14, "warhammer3")]
        public void Load_MapWithTwoEntries_ReadsTheRealSizeAndWritesItBackUnchanged(int minorVersion, string gameName)
        {
            const uint W = 8, H = 6;

            MapHexFile.CreateMapHex(_dir, "map", gameName, "test_campaign", W, H, minorVersion);
            var crafted = WithSecondEntry(File.ReadAllBytes(Path.Combine(_dir, "map.hex")), W, H);
            var path    = Path.Combine(_dir, "two_entries.hex");
            File.WriteAllBytes(path, crafted);

            var file = new MapHexFile();
            Assert.IsTrue(file.Load(path), "A map carrying two entries must load.");
            Assert.AreEqual(W, file.MapWidth,  "Width must be read after the entry list, not inside it.");
            Assert.AreEqual(H, file.MapHeight, "Height must be read after the entry list, not inside it.");

            Assert.AreEqual(2,    file.UnknownEntries.Count, "Both entries must be kept.");
            Assert.AreEqual("",   file.UnknownEntries[0].Key);
            Assert.AreEqual(0u,   file.UnknownEntries[0].Value);
            Assert.AreEqual("32", file.UnknownEntries[1].Key);
            Assert.AreEqual(0u,   file.UnknownEntries[1].Value);

            Assert.IsTrue(file.Save(_dir, "rewritten"), "Save returned false.");
            var rewritten = File.ReadAllBytes(Path.Combine(_dir, "rewritten.hex"));

            Assert.AreEqual(crafted.Length, rewritten.Length, "A load/save round trip must keep the file size.");
            for (int i = 0; i < crafted.Length; ++i)
            {
                if (i >= 8 && i < 16)
                {
                    continue; // timestamp
                }
                if (i >= crafted.Length - 4)
                {
                    continue; // CRC
                }
                Assert.AreEqual(crafted[i], rewritten[i], $"Byte {i} differs after a load/save round trip.");
            }
        }

        // 414 x 250 is wh_dlc03_beastmen_map_1's size: the game's own file stores a 13000-byte blob
        // (52 bytes per row x 250 rows), where Capacity / 8 would give 12937.
        [TestMethod]
        public void Save_TrailingBlob_PadsEachRowToWholeBytes()
        {
            MapHexFile.CreateMapHex(_dir, "map", "warhammer", "test_campaign", 414, 250, 0x12);
            var bytes  = File.ReadAllBytes(Path.Combine(_dir, "map.hex"));
            var layout = Parse(bytes);

            Assert.AreEqual(1,     BitConverter.ToInt32(bytes, layout.TrailingStart),     "A new map carries one trailing blob.");
            Assert.AreEqual(13000, BitConverter.ToInt32(bytes, layout.TrailingStart + 4), "Blob size must be ceil(width / 8) * height.");
            Assert.AreEqual(layout.TrailingStart + 4 + 4 + 13000 + 4, bytes.Length, "Nothing but the blob and the CRC may follow.");
        }

        #region File layout helpers

        private sealed class Layout
        {
            public int  EntryListStart;
            public int  EntryListEnd;
            public int  TrailingStart;
            public uint Width;
            public uint Height;
        }

        /// <summary>Walks a 0x12 / 0x13 / 0x14 map.hex up to the trailing block.</summary>
        private static Layout Parse(byte[] bytes)
        {
            using (var br = new BinaryReader(new MemoryStream(bytes)))
            {
                br.ReadInt32();                         // major
                int minor = br.ReadInt32();
                if (minor >= 0x12)
                {
                    br.ReadUInt64();                    // timestamp
                }
                ReadString(br);                         // game
                ReadString(br);                         // map name

                int lists = (minor == 0x13 || minor == 0x14) ? 7 : 6;
                for (int i = 0; i < lists; ++i)
                {
                    SkipStringArray(br);
                }

                var layout = new Layout { EntryListStart = (int)br.BaseStream.Position };

                int count = br.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    ReadString(br);
                    br.ReadUInt32();
                }
                layout.EntryListEnd = (int)br.BaseStream.Position;

                SkipIntArray(br);                       // land colours
                SkipIntArray(br);                       // sea colours
                layout.Width  = br.ReadUInt32();
                layout.Height = br.ReadUInt32();
                br.BaseStream.Position += layout.Width * layout.Height * (minor == 0x0D ? 8 : 16);
                layout.TrailingStart = (int)br.BaseStream.Position;

                return layout;
            }
        }

        /// <summary>
        /// Rewrites a one-entry map as a two-entry map the way wh_dlc05_wood_elves_map_1 is laid
        /// out: entries ("", 0) and ("32", 0) up front, two zero blobs at the end, fresh CRC.
        /// </summary>
        private static byte[] WithSecondEntry(byte[] original, uint width, uint height)
        {
            var layout   = Parse(original);
            int blobSize = (int)(((width + 7) / 8) * height);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(original, 0, layout.EntryListStart);

                bw.Write(2);
                WriteString(bw, "");
                bw.Write(0u);
                WriteString(bw, "32");
                bw.Write(0u);

                bw.Write(original, layout.EntryListEnd, layout.TrailingStart - layout.EntryListEnd);

                bw.Write(2);
                for (int i = 0; i < 2; ++i)
                {
                    bw.Write(blobSize);
                    bw.Write(new byte[blobSize]);
                }

                bw.Flush();
                var body = ms.ToArray();
                bw.Write(Crc32(body));
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static string ReadString(BinaryReader br)
        {
            int length = br.ReadInt32();
            return Encoding.ASCII.GetString(br.ReadBytes(length));
        }

        private static void WriteString(BinaryWriter bw, string value)
        {
            bw.Write(value.Length);
            bw.Write(Encoding.ASCII.GetBytes(value));
        }

        private static void SkipStringArray(BinaryReader br)
        {
            int count = br.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                ReadString(br);
            }
        }

        private static void SkipIntArray(BinaryReader br)
        {
            int count = br.ReadInt32();
            br.BaseStream.Position += 4L * count;
        }

        // Plain CRC-32 (IEEE, reflected), the same the map file's checksum uses.
        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; ++k)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
            }
            return ~crc;
        }

        #endregion
    }
}
