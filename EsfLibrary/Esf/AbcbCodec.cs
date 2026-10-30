using System.IO;
using System.Text;

namespace EsfLibrary
{
    /// <summary>
    /// The ESF layout Warhammer III writes its map_data.esf in. It is ABCA with one difference:
    /// strings in the UTF-16 and ASCII lookup lists carry a 32-bit length instead of a 16-bit one.
    /// </summary>
    public class AbcbFileCodec : AbcaFileCodec
    {
        public AbcbFileCodec() : base(0xABCB) { }

        protected override string ReadUtf16TableEntry(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.Unicode.GetString(reader.ReadBytes(length * 2));
        }

        protected override string ReadAsciiTableEntry(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        protected override void WriteUtf16TableEntry(BinaryWriter writer, string toWrite)
        {
            writer.Write(toWrite.Length);
            writer.Write(Encoding.Unicode.GetBytes(toWrite));
        }

        protected override void WriteAsciiTableEntry(BinaryWriter writer, string toWrite)
        {
            writer.Write(toWrite.Length);
            writer.Write(Encoding.ASCII.GetBytes(toWrite));
        }
    }
}
