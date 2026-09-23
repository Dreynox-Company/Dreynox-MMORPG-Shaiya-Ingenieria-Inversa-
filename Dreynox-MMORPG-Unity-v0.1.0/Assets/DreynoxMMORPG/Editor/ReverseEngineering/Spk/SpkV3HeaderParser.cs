using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public static class SpkV3HeaderParser
    {
        public const int HeaderBytes = 128;
        public static SpkV3Header Parse(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream)) return Parse(reader);
        }
        public static SpkV3Header Parse(BinaryReader reader)
        {
            BinaryRead.Ensure(reader, 0, HeaderBytes);
            return new SpkV3Header
            {
                signature = BinaryRead.UInt32LE(reader, 0x00),
                version = BinaryRead.UInt32LE(reader, 0x04),
                indexOffset = BinaryRead.UInt64LE(reader, 0x08),
                indexStoredBytes = BinaryRead.UInt64LE(reader, 0x10),
                indexDecodedBytes = BinaryRead.UInt64LE(reader, 0x18),
                recordCount = BinaryRead.UInt32LE(reader, 0x20),
                blockBytes = BinaryRead.UInt32LE(reader, 0x24),
                indexNonce = BinaryRead.Bytes(reader, 0x28, 12),
                indexTag = BinaryRead.Bytes(reader, 0x34, 16),
                encryptedIndexSha256 = BinaryRead.Bytes(reader, 0x44, 32),
                auxiliaryOffset = BinaryRead.UInt64LE(reader, 0x64),
                auxiliaryCount = BinaryRead.UInt32LE(reader, 0x6C)
            };
        }
    }
}
