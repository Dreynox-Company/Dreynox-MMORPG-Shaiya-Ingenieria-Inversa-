using System;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Binary
{
    public static class BinaryRead
    {
        public static ushort UInt16LE(BinaryReader reader, long offset)
        {
            Ensure(reader, offset, 2); reader.BaseStream.Position = offset; return reader.ReadUInt16();
        }
        public static uint UInt32LE(BinaryReader reader, long offset)
        {
            Ensure(reader, offset, 4); reader.BaseStream.Position = offset; return reader.ReadUInt32();
        }
        public static ulong UInt64LE(BinaryReader reader, long offset)
        {
            Ensure(reader, offset, 8); reader.BaseStream.Position = offset; return reader.ReadUInt64();
        }
        public static float SingleLE(BinaryReader reader, long offset)
        {
            Ensure(reader, offset, 4); reader.BaseStream.Position = offset; return reader.ReadSingle();
        }
        public static byte[] Bytes(BinaryReader reader, long offset, int count)
        {
            Ensure(reader, offset, count); reader.BaseStream.Position = offset; return reader.ReadBytes(count);
        }
        public static string FixedAscii(BinaryReader reader, long offset, int count)
        {
            byte[] raw = Bytes(reader, offset, count);
            int end = Array.IndexOf(raw, (byte)0);
            if (end < 0) end = raw.Length;
            return Encoding.ASCII.GetString(raw, 0, end).Trim();
        }
        public static void Ensure(BinaryReader reader, long offset, int count)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (offset < 0 || count < 0 || checked(offset + count) > reader.BaseStream.Length)
                throw new EndOfStreamException($"Lectura fuera de rango: offset={offset}, bytes={count}, length={reader.BaseStream.Length}");
        }
    }
}
