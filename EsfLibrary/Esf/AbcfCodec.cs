using System;
using System.Collections.Generic;
using System.IO;

namespace EsfLibrary {
    public class AbcfFileCodec : AbceCodec {
        protected int headerLength = 16;

        #region String Lookup lists
        protected Dictionary<string, int> Utf16StringList = new Dictionary<string, int>();
        protected Dictionary<string, int> AsciiStringList = new Dictionary<string, int>();

        // Reverse indices and used-id sets for the two lists above. A map_data.esf has hundreds of
        // thousands of string nodes over a table of thousands of entries, so resolving a reference
        // by scanning every key (or probing free ids with ContainsValue) is quadratic.
        Dictionary<int, string> Utf16StringById = new Dictionary<int, string>();
        Dictionary<int, string> AsciiStringById = new Dictionary<int, string>();
        HashSet<int> Utf16UsedIds = new HashSet<int>();
        HashSet<int> AsciiUsedIds = new HashSet<int>();
        #endregion

        #region String Reference Functions
        static Dictionary<string, int> ReadStringList(BinaryReader reader, ValueReader<string> readString) {
            // amount of strings in the list
            int count = reader.ReadInt32();
            Dictionary<string, int> result = new Dictionary<string, int>(count);
            for (int i = 0; i < count; i++) {
                // first string, then reference ID
                string read = readString(reader);
                result.Add(read, reader.ReadInt32());
            }
            return result;
        }
        static void WriteStringList(BinaryWriter writer, Dictionary<string, int> stringList, ValueWriter<string> writeString) {
            writer.Write(stringList.Count);
            foreach(string s in stringList.Keys) {
                writeString(writer, s);
                writer.Write(stringList[s]);
            }
        }
        /// <summary>
        /// Rebuilds the id index and used-id set for a freshly read string list. Where two names
        /// share an id, the first one in the list wins - the behaviour the old reverse scan had.
        /// </summary>
        static void IndexStringList(Dictionary<string, int> byName, Dictionary<int, string> byId, HashSet<int> usedIds) {
            byId.Clear();
            usedIds.Clear();

            foreach (var entry in byName) {
                usedIds.Add(entry.Value);

                if (!byId.ContainsKey(entry.Value)) {
                    byId.Add(entry.Value, entry.Key);
                }
            }
        }

        /// <summary>
        /// Id of <paramref name="name"/> in the given list, registering it against the lowest free
        /// id if it is not there yet. An id already in use is never handed out twice.
        /// </summary>
        static int GetOrAddReference(string name, Dictionary<string, int> byName, Dictionary<int, string> byId, HashSet<int> usedIds) {
            if (byName.TryGetValue(name, out int existing)) {
                return existing;
            }

            int index = byName.Count;
            while (usedIds.Contains(index)) {
                index++;
            }

            byName.Add(name, index);
            usedIds.Add(index);

            if (!byId.ContainsKey(index)) {
                byId.Add(index, name);
            }

            return index;
        }

        void WriteStringReference(BinaryWriter writer, string toWrite, Dictionary<string, int> byName, Dictionary<int, string> byId, HashSet<int> usedIds) {
            writer.Write(GetOrAddReference(toWrite, byName, byId, usedIds));
        }
        #endregion
  
        public AbcfFileCodec(uint id = 0xABCF) : base(id) { }

        // re-rout the string reading to looking up in the appropriate table
        public override string ReadUtf16String(BinaryReader reader) {
            return ReadStringReference(reader, Utf16StringById);
        }
        public override string ReadAsciiString(BinaryReader reader) {
            return ReadStringReference(reader, AsciiStringById);
        }
        public void WriteAsciiReference(BinaryWriter w, string s) {
            WriteStringReference(w, s, AsciiStringList, AsciiStringById, AsciiUsedIds);
        }
        public void WriteUtf16Reference(BinaryWriter w, string s) {
            WriteStringReference(w, s, Utf16StringList, Utf16StringById, Utf16UsedIds);
        }

        public override EsfNode CreateValueNode(EsfType typeCode, bool optimize = false) {
            StringNode result;
            switch (typeCode) {
                case EsfType.UTF16:
                    result = new StringNode(ReadUtf16String, WriteUtf16Reference);
                    break;
                case EsfType.ASCII:
                    result = new StringNode(ReadAsciiString, WriteAsciiReference);
                    break;

                // HACK: RoninX
                case EsfType.ASCII_W21:
                    result = new StringNode(ReadAsciiString, WriteAsciiReference);
                    break;
                case EsfType.ASCII_W25:
                    result = new StringNode(ReadAsciiString, WriteAsciiReference);
                    break;

                default:
                    return base.CreateValueNode(typeCode);
            }
            result.TypeCode = typeCode;
            return result;
        }

        // override to read the two string lists after the node names
        protected override void ReadNodeNames(BinaryReader reader) {
            base.ReadNodeNames(reader);
            // create lookup lists (positioned immediately after the node names)
            Utf16StringList = ReadStringList(reader, ReadUtf16TableEntry);
            AsciiStringList = ReadStringList(reader, ReadAsciiTableEntry);

            IndexStringList(Utf16StringList, Utf16StringById, Utf16UsedIds);
            IndexStringList(AsciiStringList, AsciiStringById, AsciiUsedIds);
        }
        protected override void WriteNodeNames(BinaryWriter writer) {
            base.WriteNodeNames(writer);
            WriteStringList(writer, Utf16StringList, WriteUtf16TableEntry);
            WriteStringList(writer, AsciiStringList, WriteAsciiTableEntry);
        }

        // How a string in the two lookup lists is stored; later formats widen its length prefix.
        protected virtual string ReadUtf16TableEntry(BinaryReader reader) {
            return ReadUtf16(reader);
        }
        protected virtual string ReadAsciiTableEntry(BinaryReader reader) {
            return ReadAscii(reader);
        }
        protected virtual void WriteUtf16TableEntry(BinaryWriter writer, string toWrite) {
            WriteUtf16(writer, toWrite);
        }
        protected virtual void WriteAsciiTableEntry(BinaryWriter writer, string toWrite) {
            WriteAscii(writer, toWrite);
        }

        #region CAIME: User-friendly stuff
        /// <summary>
        /// Registers <paramref name="name"/> in the ASCII string table if it is not there yet, and
        /// returns the id it is stored under - the name's own id, not the table's size.
        /// </summary>
        public int AddAsciiName(string name) {
            return GetOrAddReference(name, AsciiStringList, AsciiStringById, AsciiUsedIds);
        }

        /// <inheritdoc cref="AddAsciiName"/>
        public int AddUtf16Name(string name) {
            return GetOrAddReference(name, Utf16StringList, Utf16StringById, Utf16UsedIds);
        }
        #endregion
    }
}
