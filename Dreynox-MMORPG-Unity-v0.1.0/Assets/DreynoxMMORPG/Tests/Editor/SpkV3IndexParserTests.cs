using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Spk;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class SpkV3IndexParserTests
    {
        [Test]
        public void ParseRecords_UsesObserved96ByteLayout()
        {
            SpkV3Header h = Header(1, 96, 128 + 25);
            byte[] row = new byte[96];
            PutU64(row, 0, 0x0000dc71edfcd25dUL);
            PutU64(row, 8, 128);
            PutU64(row, 16, 25);
            PutU64(row, 24, 25);
            PutU64(row, 32, 100);
            PutU32(row, 40, 1);
            PutU32(row, 44, uint.MaxValue);
            for (int i = 0; i < 12; i++) row[48 + i] = (byte)(i + 1);
            for (int i = 0; i < 16; i++) row[60 + i] = (byte)(0xA0 + i);
            PutU32(row, 76, 7);

            var records = SpkV3IndexParser.ParseRecords(row, h);
            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].IdHex, Is.EqualTo("0000dc71edfcd25d"));
            Assert.That(records[0].dataOffset, Is.EqualTo(128));
            Assert.That(records[0].storedBytes, Is.EqualTo(25));
            Assert.That(records[0].decodedBytes, Is.EqualTo(100));
            Assert.That(records[0].Simple, Is.True);
            Assert.That(records[0].Flags, Is.EqualTo(7));
            Assert.That(records[0].Nonce.Length, Is.EqualTo(12));
            Assert.That(records[0].Tag.Length, Is.EqualTo(16));
        }

        [Test]
        public void ParseRecords_RejectsStoredMirrorMismatch()
        {
            SpkV3Header h = Header(1, 96, 129);
            byte[] row = new byte[96];
            PutU64(row, 8, 128);
            PutU64(row, 16, 1);
            PutU64(row, 24, 2);
            PutU32(row, 40, 1);
            Assert.Throws<InvalidDataException>(() => SpkV3IndexParser.ParseRecords(row, h));
        }

        [Test]
        public void ValidateRelationships_AcceptsContiguousSimpleCoverage()
        {
            SpkV3Header h = Header(2, 192, 158);
            var records = new[]
            {
                new SpkV3Record { ordinal = 0, dataOffset = 128, storedBytes = 10, recordType = 1, metadata = new byte[32] },
                new SpkV3Record { ordinal = 1, dataOffset = 138, storedBytes = 20, recordType = 1, metadata = new byte[32] }
            };
            Assert.DoesNotThrow(() => SpkV3IndexParser.ValidateRelationships(records, Array.Empty<SpkV3AuxiliaryRecord>(), h));
        }

        private static SpkV3Header Header(uint count, ulong decoded, ulong auxiliaryOffset)
        {
            return new SpkV3Header
            {
                signature = SpkV3IndexParser.Magic,
                version = SpkV3IndexParser.Version3,
                recordCount = count,
                indexDecodedBytes = decoded,
                auxiliaryOffset = auxiliaryOffset,
                auxiliaryCount = 0,
                indexNonce = new byte[12],
                indexTag = new byte[16],
                encryptedIndexSha256 = new byte[32]
            };
        }

        private static void PutU32(byte[] b, int o, uint v)
        {
            b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
        }
        private static void PutU64(byte[] b, int o, ulong v)
        {
            PutU32(b, o, (uint)v); PutU32(b, o + 4, (uint)(v >> 32));
        }
    }
}
