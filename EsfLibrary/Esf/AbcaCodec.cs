using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace EsfLibrary
{
    public class AbcaFileCodec: AbcfFileCodec
    {
        #region Marker Bits
        static byte RECORD_BIT = 0x80; // 10000000
        // if set, this is a array of records
        static ushort BLOCK_BIT = 0x40;  // 01000000
        // if not set, record info is encodec in 2 bytes
        static byte LONG_INFO = 0x20; // 00100000
        #endregion

        #region Added Writers
        protected void WriteBoolNoop(BinaryWriter writer, bool value) { }
        protected void WriteFloatNoop(BinaryWriter writer, float value) { }
        #endregion

        public AbcaFileCodec() : base(0xABCA) { }

        protected AbcaFileCodec(uint id) : base(id) { }

        public override EsfNode Decode(BinaryReader reader, byte typeCode)
        {
            EsfNode result;
            byte recordBit = (byte)(typeCode & RECORD_BIT);
            if(recordBit == 0 || reader.BaseStream.Position == headerLength + 1)
            {
                switch((EsfType)typeCode)
                {
                case EsfType.INT32_ZERO:
                case EsfType.INT32_BYTE:
                case EsfType.INT32_SHORT:
                case EsfType.INT32_24BIT:
                case EsfType.INT32:
                    result = new OptimizedIntNode();
                    (result as OptimizedIntNode).Decode(reader, (EsfType)typeCode);
                    break;
                default:
                    // for non-blocks and root node, previous decoding is used
                    result = base.Decode(reader, typeCode);
                    break;
                }
            }
            else
            {
                bool blockBit = ((typeCode & BLOCK_BIT) != 0);
                // use new block decoding
                result = blockBit
                    ? ReadRecordArrayNode(reader, typeCode)
                    : ReadRecordNode(reader, typeCode);
            }
            return result;
        }

        protected override EsfNode ReadRecordArrayNode(BinaryReader reader, byte typeCode)
        {
            RecordArrayNode result = new RecordArrayNode(this, typeCode);
            result.Decode(reader, EsfType.RECORD_BLOCK);
            return result;
        }

        // Adds readers for optimized values
        public override EsfNode CreateValueNode(EsfType typeCode, bool optimize = true)
        {
            EsfNode result;
            switch(typeCode)
            {
            case EsfType.BOOL:
            case EsfType.BOOL_TRUE:
            case EsfType.BOOL_FALSE:
                if(optimize)
                {
                    return new OptimizedBoolNode();
                }
                else
                {
                    result = new BoolNode();
                }
                break;
            case EsfType.UINT32:
            case EsfType.UINT32_ZERO:
            case EsfType.UINT32_ONE:
            case EsfType.UINT32_BYTE:
            case EsfType.UINT32_SHORT:
            case EsfType.UINT32_24BIT:
                return new OptimizedUIntNode
                {
                    SingleByteMin = !optimize
                };
            case EsfType.INT32:
            case EsfType.INT32_ZERO:
            case EsfType.INT32_BYTE:
            case EsfType.INT32_SHORT:
            case EsfType.INT32_24BIT:
                return new OptimizedIntNode
                {
                    SingleByteMin = !optimize
                };
            case EsfType.SINGLE:
            case EsfType.SINGLE_ZERO:
                if(optimize)
                {
                    return new OptimizedFloatNode();
                }
                else
                {
                    result = new FloatNode();
                }
                break;
            //TODO confirm intended data types of types indicated by unknown enum values
            case EsfType.UNKNOWN_23:
                result = new SByteNode();
                break;
            case EsfType.UNKNOWN_24:
                result = new ShortNode();
                break;
            default:
                return base.CreateValueNode(typeCode);
            }
            result.TypeCode = typeCode;
            return result;
        }

        #region Array Nodes
        protected override EsfNode CreateArrayNode(EsfType typeCode)
        {
            EsfNode result;
            // support array types for new primitives
            // this sets the type code of the base type to later have an easier time
            switch(typeCode)
            {
            case EsfType.BOOL_TRUE_ARRAY:
            case EsfType.BOOL_FALSE_ARRAY:
            case EsfType.UINT_ZERO_ARRAY:
            case EsfType.UINT_ONE_ARRAY:
            case EsfType.INT32_ZERO_ARRAY:
            case EsfType.SINGLE_ZERO_ARRAY:
                // trying to read this should result in an infinite loop
                throw new InvalidDataException(string.Format("Array {0:x} of zero-byte entries makes no sense", typeCode));
            case EsfType.UINT32_BYTE_ARRAY:
            case EsfType.UINT32_SHORT_ARRAY:
            case EsfType.UINT32_24BIT_ARRAY:
                result = new OptimizedArrayNode<uint>(this, typeCode, delegate (uint val) {
                    return new OptimizedUIntNode
                    {
                        Value = val,
                        SingleByteMin = true
                    };
                });
                break;
            case EsfType.INT32_BYTE_ARRAY:
            case EsfType.INT32_SHORT_ARRAY:
                result = new OptimizedArrayNode<int>(this, typeCode, delegate (int val) {
                    return new OptimizedIntNode
                    {
                        Value = val,
                        SingleByteMin = true
                    };
                });
                break;
            default:
                result = base.CreateArrayNode(typeCode);
                return result;
            }
            result.TypeCode = (EsfType)typeCode;
            return result;
        }

        protected override byte[] ReadArray(BinaryReader reader)
        {
            long size = ReadSize(reader);
            return reader.ReadBytes((int)size);
        }
        #endregion

        #region Record Nodes
        // Section can now be compressed
        public override RecordNode ReadRecordNode(BinaryReader reader, byte typeCode, bool forceDecode = false)
        {
            RecordNode node = base.ReadRecordNode(reader, typeCode, forceDecode) as RecordNode;
            if(forceDecode && node.Name == CompressedNode.TAG_NAME)
            {
                // decompress node
                // Console.WriteLine("reading compressed node");
                node = new CompressedNode(this, node);
            }
            if(node is MemoryMappedRecordNode)
            {
                // we don't need to invalidate following sections because
                // all the sizes are relative so we won't need to adjust them
                (node as MemoryMappedRecordNode).InvalidateSiblings = false;
            }
            return node;
        }
        #endregion

        #region Version-dependent overridables ABCA
        public override int ReadSize(BinaryReader reader)
        {
            byte read = reader.ReadByte();
            long result = 0;
            while((read & 0x80) != 0)
            {
                result = (result << 7) + (read & (byte)0x7f);
                read = reader.ReadByte();
            }
            result = (result << 7) + (read & (byte)0x7f);
            return (int)result;
        }
        public override int ReadCount(BinaryReader reader)
        {
            return ReadSize(reader);
        }
        public override void WriteSize(BinaryWriter writer, long size)
        {
            if(size == 0)
            {
                writer.Write((byte)0);
                return;
            }
            byte leftmostBitsClear = 0x80;
            byte leftmostBitsSet = 0x7f;

            // store rightmost to leftmost bytes
            Stack<byte> encoded = new Stack<byte>();
            while(size != 0)
            {
                // only keep 7 leftmost bits
                byte leftmost = (byte)(size & leftmostBitsSet);
                encoded.Push(leftmost);
                // and throw them away from the original
                size = size >> 7;
            }
            // and write them the other way around
            while(encoded.Count != 0)
            {
                byte write = encoded.Pop();
                write |= (encoded.Count != 0) ? leftmostBitsClear : (byte)0;
                writer.Write(write);
            }
        }
        public override void WriteOffset(BinaryWriter writer, long offset)
        {
            WriteSize(writer, offset);
        }

        // allow de/encoding of short info (2 byte)
        public override void ReadRecordInfo(BinaryReader reader, byte encoded, out string name, out byte version)
        {
            // root node (and only root node) is stored with long name/version info...
            if(reader.BaseStream.Position == headerLength + 1 || (encoded & LONG_INFO) != 0)
            {
                base.ReadRecordInfo(reader, encoded, out name, out version);
            }
            else
            {
                version = (byte)((encoded & 31) >> 1);
                ushort nameIndex = (ushort)((encoded & 1) << 8);
                nameIndex += reader.ReadByte();
                name = GetNodeName(nameIndex);
            }
        }
        public override void WriteRecordInfo(BinaryWriter writer, byte typeCode, string name, byte version)
        {
            ushort nameIndex = GetNodeNameIndex(name);
            // always encode root node with long (4 byte) info
            bool canUseShort = nameIndex != 0;
            // we only have 9 bits for type in short encoding
            canUseShort &= (nameIndex < 0x200);
            // and 4 for version
            canUseShort &= version < 0x10;
            if(canUseShort)
            {
                ushort shortInfo = encodeShortRecordInfo(typeCode, nameIndex, version);
                byte write = (byte)((shortInfo >> 8) & 0xff);
                writer.Write(write);
                writer.Write((byte)shortInfo);
            }
            else
            {
                switch((EsfType)typeCode)
                {
                case EsfType.RECORD:
                    typeCode = (byte)((nameIndex == 0) ? EsfType.RECORD : EsfType.LONG_RECORD);
                    break;
                case EsfType.RECORD_BLOCK:
                    typeCode = (byte)EsfType.LONG_RECORD_BLOCK;
                    break;
                default:
                    throw new InvalidDataException(string.Format("Trying to encode record info for wrong type code {0}", typeCode));
                }
                base.WriteRecordInfo(writer, typeCode, name, version);
            }
        }
        public static ushort encodeShortRecordInfo(byte typeCode, ushort nameIndex, byte version)
        {
            ushort shortInfo = (ushort)(version << 9); // shift left to leave place for the type
            shortInfo |= nameIndex; // type uses rightmost 9 bits
            shortInfo |= (((EsfType)typeCode == EsfType.RECORD_BLOCK) ? (ushort)(BLOCK_BIT << 8) : (ushort)0);  // set block bit for record arrays
            shortInfo |= (ushort)(RECORD_BIT << 8);
            return shortInfo;
        }

        public override void EncodeSized(BinaryWriter writer, List<EsfNode> nodes, bool writeCount = false)
        {
            byte[] encoded;
            MemoryStream bufferStream = new MemoryStream();
            using(BinaryWriter w = new BinaryWriter(bufferStream))
            {
                foreach(EsfNode node in nodes)
                {
                    Encode(w, node);
                }
                encoded = bufferStream.ToArray();
            }
            WriteSize(writer, encoded.LongLength);
            if(writeCount)
            {
                WriteSize(writer, nodes.Count);
            }
            writer.Write(encoded);
            encoded = null;
            //GC.Collect();
        }
        #endregion
    }
}