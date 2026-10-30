using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EsfLibrary
{
    /// <summary>The ESF layouts <see cref="EsfWriter"/> writes.</summary>
    public enum EsfFormat
    {
        /// <summary>Rome II to Three Kingdoms: string table entries have a 16-bit length.</summary>
        Abca,

        /// <summary>Warhammer III: as ABCA, but string table entries have a 32-bit length.</summary>
        Abcb,
    }

    /// <summary>
    /// Writes an ABCA or ABCB file node by node, byte for byte as the games' own writer does:
    /// where its sizes are padded, and, given the game's hash map, the string table's order.
    /// See FileFormatDefinitions/esf.md.
    /// </summary>
    public sealed class EsfWriter
    {
        private const int  HEADER_SIZE        = 16;
        private const int  BUFFER_SIZE        = 0x100000;
        private const int  BUFFER_LIMIT       = BUFFER_SIZE - 1;
        private const int  PLACEHOLDER_SIZE   = 5;

        private const byte RECORD_BIT         = 0x80;
        private const byte BLOCK_BIT          = 0x40;
        private const int  SHORT_INFO_MAX_NAME_INDEX = 0x1FF;
        private const int  SHORT_INFO_MAX_VERSION    = 0x0F;

        private readonly EsfFormat                  format;
        private readonly CaHashMap                  stringTable;
        private readonly List<string>               nodeNames       = new List<string>();
        private readonly Dictionary<string, int>    nodeNameIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string>               asciiStrings    = new List<string>();
        private readonly Dictionary<string, int>    asciiIndices    = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Stack<Frame>               frames          = new Stack<Frame>();
        private readonly Frame                      root            = new Frame(FrameKind.Root, HEADER_SIZE);

        // Positions in the file as it will finally be laid out, an open placeholder counting as its
        // five bytes. Flushes only ever move bufferStart forward.
        private long position   = HEADER_SIZE;
        private long bufferStart;
        private bool rootWritten;

        private enum FrameKind { Root, Record, Block, BlockEntries, BlockEntry }

        private sealed class Frame
        {
            public readonly FrameKind     Kind;
            public readonly long          PlaceholderOffset;
            public readonly MemoryStream  Content = new MemoryStream();
            public int                    EntryCount;

            public Frame(FrameKind kind, long placeholderOffset)
            {
                Kind              = kind;
                PlaceholderOffset = placeholderOffset;
            }
        }

        /// <param name="stringTable">The game builder's hash map, which orders the string table; null lists it by id.</param>
        public EsfWriter(EsfFormat format, CaHashMap stringTable = null)
        {
            this.format             = format;
            this.stringTable        = stringTable;
            frames.Push(root);
        }

        #region Records and blocks

        public void BeginRecord(string name, byte version)
        {
            WriteRecordInfo(name, version, isBlock: false);
            OpenPlaceholder(FrameKind.Record);
        }

        public void EndRecord()
        {
            var record = ClosePlaceholder(FrameKind.Record);
            WriteSize(record, record.Content.Length);
        }

        public void BeginBlock(string name, byte version)
        {
            WriteRecordInfo(name, version, isBlock: true);
            OpenPlaceholder(FrameKind.Block);
            OpenPlaceholder(FrameKind.BlockEntries);
        }

        public void BeginBlockEntry()
        {
            var entries = frames.Peek();
            if (entries.Kind != FrameKind.BlockEntries)
            {
                throw new InvalidOperationException("A block entry can only be started directly inside a block.");
            }

            ++entries.EntryCount;
            OpenPlaceholder(FrameKind.BlockEntry);
        }

        public void EndBlockEntry()
        {
            var entry = ClosePlaceholder(FrameKind.BlockEntry);
            WriteSize(entry, entry.Content.Length);
        }

        /// <summary>
        /// The block's size counts from after its entry count, which is itself written through a
        /// placeholder ahead of the entries.
        /// </summary>
        public void EndBlock()
        {
            var entries = ClosePlaceholder(FrameKind.BlockEntries);
            int countSize = WriteSize(entries, entries.EntryCount);

            var block = ClosePlaceholder(FrameKind.Block);
            WriteSize(block, block.Content.Length - countSize);
        }

        private void WriteRecordInfo(string name, byte version, bool isBlock)
        {
            int nameIndex = NodeNameIndex(name);

            // The root, and only the root, always carries the long form.
            if (rootWritten == false)
            {
                if (isBlock)
                {
                    throw new InvalidOperationException("The root node must be a record.");
                }

                rootWritten = true;
                WriteLongRecordInfo(RECORD_BIT, nameIndex, version);
                return;
            }

            if (nameIndex > SHORT_INFO_MAX_NAME_INDEX || version > SHORT_INFO_MAX_VERSION)
            {
                WriteLongRecordInfo((byte)(isBlock ? EsfType.LONG_RECORD_BLOCK : EsfType.LONG_RECORD), nameIndex, version);
                return;
            }

            int info = (RECORD_BIT << 8) | (version << 9) | nameIndex;
            if (isBlock)
            {
                info |= BLOCK_BIT << 8;
            }

            // Two writes, as the games make them: it matters for where the buffer flushes.
            WriteByteRaw((byte)(info >> 8));
            WriteByteRaw((byte)info);
        }

        private void WriteLongRecordInfo(byte typeCode, int nameIndex, byte version)
        {
            WriteByteRaw(typeCode);
            Write(BitConverter.GetBytes((ushort)nameIndex));
            WriteByteRaw(version);
        }

        #endregion

        #region Values

        public void WriteBool(bool value)
        {
            WriteByteRaw((byte)(value ? EsfType.BOOL_TRUE : EsfType.BOOL_FALSE));
        }

        public void WriteByte(byte value)
        {
            WriteByteRaw((byte)EsfType.UINT8);
            WriteByteRaw(value);
        }

        public void WriteUInt16(ushort value)
        {
            WriteByteRaw((byte)EsfType.UINT16);
            Write(BitConverter.GetBytes(value));
        }

        public void WriteUInt64(ulong value)
        {
            WriteByteRaw((byte)EsfType.UINT64);
            Write(BitConverter.GetBytes(value));
        }

        /// <summary>Writes a uint in the shortest of the optimised encodings that holds it.</summary>
        public void WriteUInt32(uint value)
        {
            if (value == 0)
            {
                WriteByteRaw((byte)EsfType.UINT32_ZERO);
            }
            else if (value == 1)
            {
                WriteByteRaw((byte)EsfType.UINT32_ONE);
            }
            else if (value <= 0xFF)
            {
                WriteByteRaw((byte)EsfType.UINT32_BYTE);
                WriteByteRaw((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                WriteByteRaw((byte)EsfType.UINT32_SHORT);
                Write(BitConverter.GetBytes((ushort)value));
            }
            else if (value <= 0xFFFFFF)
            {
                WriteByteRaw((byte)EsfType.UINT32_24BIT);
                Write(new[] { (byte)(value >> 16), (byte)(value >> 8), (byte)value });
            }
            else
            {
                WriteByteRaw((byte)EsfType.UINT32);
                Write(BitConverter.GetBytes(value));
            }
        }

        /// <summary>Writes an int in the shortest of the optimised encodings that holds it.</summary>
        public void WriteInt32(int value)
        {
            if (value == 0)
            {
                WriteByteRaw((byte)EsfType.INT32_ZERO);
            }
            else if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
            {
                WriteByteRaw((byte)EsfType.INT32_BYTE);
                WriteByteRaw((byte)(sbyte)value);
            }
            else if (value >= short.MinValue && value <= short.MaxValue)
            {
                WriteByteRaw((byte)EsfType.INT32_SHORT);
                Write(BitConverter.GetBytes((short)value));
            }
            else if (value >= -0x800000 && value <= 0x7FFFFF)
            {
                WriteByteRaw((byte)EsfType.INT32_24BIT);
                Write(new[] { (byte)(value >> 16), (byte)(value >> 8), (byte)value });
            }
            else
            {
                WriteByteRaw((byte)EsfType.INT32);
                Write(BitConverter.GetBytes(value));
            }
        }

        public void WriteCoordinates(float x, float y)
        {
            WriteByteRaw((byte)EsfType.COORD2D);
            Write(BitConverter.GetBytes(x));
            Write(BitConverter.GetBytes(y));
        }

        public void WriteAscii(string value)
        {
            WriteByteRaw((byte)EsfType.ASCII);
            Write(BitConverter.GetBytes(AsciiIndex(value)));
        }

        public void WriteByteArray(byte[] values)
        {
            WriteByteRaw((byte)EsfType.UINT8_ARRAY);
            WriteSized(values);
        }

        public void WriteUInt16Array(IReadOnlyList<ushort> values)
        {
            var bytes = new byte[values.Count * sizeof(ushort)];
            for (int i = 0; i < values.Count; ++i)
            {
                bytes[i * 2]     = (byte)values[i];
                bytes[i * 2 + 1] = (byte)(values[i] >> 8);
            }

            WriteByteRaw((byte)EsfType.UINT16_ARRAY);
            WriteSized(bytes);
        }

        public void WriteUInt32Array(IReadOnlyList<uint> values)
        {
            var bytes = new byte[values.Count * sizeof(uint)];
            for (int i = 0; i < values.Count; ++i)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, bytes, i * sizeof(uint), sizeof(uint));
            }

            WriteByteRaw((byte)EsfType.UINT32_ARRAY);
            WriteSized(bytes);
        }

        public void WriteCoordinates3DArray(IReadOnlyList<(float X, float Y, float Z)> values)
        {
            var bytes = new byte[values.Count * 3 * sizeof(float)];
            for (int i = 0; i < values.Count; ++i)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].X), 0, bytes, i * 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].Y), 0, bytes, i * 12 + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].Z), 0, bytes, i * 12 + 8, 4);
            }

            WriteByteRaw((byte)EsfType.COORD3D_ARRAY);
            WriteSized(bytes);
        }

        public void WriteAsciiArray(IReadOnlyList<string> values)
        {
            var bytes = new byte[values.Count * sizeof(int)];
            for (int i = 0; i < values.Count; ++i)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(AsciiIndex(values[i])), 0, bytes, i * sizeof(int), sizeof(int));
            }

            WriteByteRaw((byte)EsfType.ASCII_ARRAY);
            WriteSized(bytes);
        }

        private void WriteSized(byte[] data)
        {
            OpenPlaceholder(FrameKind.Record);
            Write(data);
            var array = ClosePlaceholder(FrameKind.Record);
            WriteSize(array, array.Content.Length);
        }

        #endregion

        #region Output

        /// <summary>The whole file, stamped with <paramref name="timestamp"/> (Unix time) as the games stamp it.</summary>
        public byte[] ToArray(uint timestamp)
        {
            if (frames.Count != 1 || rootWritten == false)
            {
                throw new InvalidOperationException("The file is incomplete: a record or block is still open, or nothing was written.");
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(format == EsfFormat.Abca ? 0xABCAu : 0xABCBu);
                writer.Write(0u);
                writer.Write(timestamp);
                writer.Write((uint)(HEADER_SIZE + root.Content.Length));
                writer.Write(root.Content.ToArray());

                WriteNodeNames(writer);
                writer.Write(0);    // No UTF-16 strings.
                WriteAsciiStrings(writer);

                return stream.ToArray();
            }
        }

        private void WriteNodeNames(BinaryWriter writer)
        {
            writer.Write((ushort)nodeNames.Count);
            foreach (var name in nodeNames)
            {
                writer.Write((ushort)name.Length);
                writer.Write(Encoding.ASCII.GetBytes(name));
            }
        }

        private void WriteAsciiStrings(BinaryWriter writer)
        {
            var ids = Enumerable.Range(0, asciiStrings.Count);
            if (stringTable != null)
            {
                ids = stringTable.Iterate(ids, id => stringTable.StringHash(asciiStrings[id]));
            }

            writer.Write(asciiStrings.Count);
            foreach (int id in ids)
            {
                if (format == EsfFormat.Abca)
                {
                    writer.Write(checked((ushort)asciiStrings[id].Length));
                }
                else
                {
                    writer.Write(asciiStrings[id].Length);
                }

                writer.Write(Encoding.ASCII.GetBytes(asciiStrings[id]));
                writer.Write(id);
            }
        }

        private int NodeNameIndex(string name)
        {
            if (nodeNameIndices.TryGetValue(name, out int index) == false)
            {
                RequireAscii(name);
                index = nodeNames.Count;
                nodeNames.Add(name);
                nodeNameIndices.Add(name, index);
            }

            return index;
        }

        private int AsciiIndex(string value)
        {
            if (asciiIndices.TryGetValue(value, out int index) == false)
            {
                RequireAscii(value);
                index = asciiStrings.Count;
                asciiStrings.Add(value);
                asciiIndices.Add(value, index);
            }

            return index;
        }

        private static void RequireAscii(string value)
        {
            if (value.Any(c => c > 0x7F))
            {
                throw new ArgumentException($"'{value}' has a character that is not ASCII, which ESF strings must be.", nameof(value));
            }
        }

        #endregion

        #region The games' write buffer

        private void WriteByteRaw(byte value)
        {
            Write(new[] { value });
        }

        /// <summary>
        /// One write through the buffer. A write that does not fit flushes the buffer first, as
        /// RPFM documents. A write as large as the buffer instead tops the buffer up, then goes out
        /// a full buffer at a time, leaving the rest in the buffer, so a large array keeps the
        /// buffer's boundaries where they were; RPFM lets such a write bypass the buffer, which
        /// pads different sizes in the Assembly Kit's Warhammer III map_data.esf files.
        ///
        /// <para>
        /// This matches the Assembly Kit's builds of CA's maps byte for byte. A build of a
        /// 1600x970 mod map differs in one size, padded here and not by the builder: padding
        /// changes only the size's encoding, not its value.
        /// </para>
        /// </summary>
        private void Write(byte[] data)
        {
            frames.Peek().Content.Write(data, 0, data.Length);
            Advance(data.Length);
        }

        private void Advance(long length)
        {
            if (length >= BUFFER_LIMIT)
            {
                long remaining = length;
                long space     = BUFFER_LIMIT - (position - bufferStart);

                position  += space;
                remaining -= space;
                Flush();

                while (remaining >= BUFFER_LIMIT)
                {
                    position  += BUFFER_LIMIT;
                    remaining -= BUFFER_LIMIT;
                    Flush();
                }

                position += remaining;
                return;
            }

            if (position - bufferStart + length > BUFFER_LIMIT)
            {
                Flush();
            }

            position += length;
        }

        private void Flush()
        {
            bufferStart = position;
        }

        /// <summary>
        /// The games write a placeholder as four zero bytes and then one more. It shrinks to fit
        /// its value only if no flush has passed it by the time it is filled.
        /// </summary>
        private void OpenPlaceholder(FrameKind kind)
        {
            long offset = position;
            Advance(PLACEHOLDER_SIZE - 1);
            Advance(1);
            frames.Push(new Frame(kind, offset));
        }

        private Frame ClosePlaceholder(FrameKind kind)
        {
            var frame = frames.Pop();
            if (frame.Kind != kind)
            {
                throw new InvalidOperationException($"Expected to close a {kind}, but the innermost open node is a {frame.Kind}.");
            }

            return frame;
        }

        /// <summary>
        /// Fills the placeholder of a closed frame with <paramref name="size"/> and moves its content
        /// into the enclosing frame.
        /// </summary>
        /// <returns>How many bytes the size took.</returns>
        private int WriteSize(Frame frame, long size)
        {
            bool stillBuffered = frame.PlaceholderOffset >= bufferStart;
            var encoded = EncodeSize(checked((uint)size), stillBuffered ? 0 : PLACEHOLDER_SIZE);

            position -= PLACEHOLDER_SIZE - encoded.Length;

            var parent = frames.Peek().Content;
            parent.Write(encoded, 0, encoded.Length);
            frame.Content.Position = 0;
            frame.Content.CopyTo(parent);

            return encoded.Length;
        }

        /// <summary>
        /// Seven bits a byte, most significant first, every byte but the last with its top bit set,
        /// padded at the front with 0x80 to <paramref name="minimumLength"/> bytes.
        /// </summary>
        private static byte[] EncodeSize(uint value, int minimumLength)
        {
            var groups = new List<byte>();
            do
            {
                groups.Add((byte)(value & 0x7F));
                value >>= 7;
            }
            while (value != 0);

            while (groups.Count < minimumLength)
            {
                groups.Add(0);
            }

            var encoded = new byte[groups.Count];
            for (int i = 0; i < encoded.Length; ++i)
            {
                byte group = groups[groups.Count - 1 - i];
                encoded[i] = i == encoded.Length - 1 ? group : (byte)(group | 0x80);
            }

            return encoded;
        }

        #endregion
    }
}
