using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parsec.Cryptography;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacySDataDecryptionResult
    {
        public string Signature { get; internal set; } = string.Empty;
        public uint ExpectedChecksum { get; internal set; }
        public uint RealSize { get; internal set; }
        public bool BinaryHeader { get; internal set; }
        public byte[] Plaintext { get; internal set; } = Array.Empty<byte>();
    }

    public static class LegacySDataDecryptor
    {
        public const string SeedSignature =
            "0001CBCEBC5B2784D3FC9A2A9DB84D1C3FEB6E99";

        public const int HeaderBytes = 64;
        public const int BlockBytes = 16;

        public static bool IsEncrypted(byte[] data)
        {
            if (data == null ||
                data.Length < HeaderBytes)
                return false;

            string signature =
                Encoding.ASCII.GetString(
                    data,
                    0,
                    SeedSignature.Length);

            return string.Equals(
                signature,
                SeedSignature,
                StringComparison.Ordinal);
        }

        public static LegacySDataDecryptionResult Decrypt(
            string path,
            bool validateChecksum = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "SData path is required.",
                    nameof(path));

            return Decrypt(
                File.ReadAllBytes(path),
                validateChecksum);
        }

        public static LegacySDataDecryptionResult Decrypt(
            byte[] encrypted,
            bool validateChecksum = true)
        {
            if (encrypted == null)
                throw new ArgumentNullException(nameof(encrypted));

            if (!IsEncrypted(encrypted))
            {
                return new LegacySDataDecryptionResult
                {
                    Signature = string.Empty,
                    ExpectedChecksum = 0,
                    RealSize = (uint)encrypted.Length,
                    BinaryHeader = false,
                    Plaintext = (byte[])encrypted.Clone()
                };
            }

            if (encrypted.Length < HeaderBytes ||
                encrypted.Length % BlockBytes != 0)
            {
                throw new InvalidDataException(
                    "Encrypted SData size is not 16-byte aligned.");
            }

            string signature =
                Encoding.ASCII.GetString(
                    encrypted,
                    0,
                    SeedSignature.Length);

            int cursor = 40;
            uint expectedChecksum =
                BitConverter.ToUInt32(
                    encrypted,
                    cursor);

            cursor += 4;
            bool binaryHeader = false;

            if (expectedChecksum == 0)
            {
                binaryHeader = true;

                expectedChecksum =
                    BitConverter.ToUInt32(
                        encrypted,
                        cursor);

                cursor += 4;
            }

            uint realSize =
                BitConverter.ToUInt32(
                    encrypted,
                    cursor);

            int encryptedBytes =
                encrypted.Length - HeaderBytes;

            byte[] alignedPlaintext =
                new byte[encryptedBytes];

            for (int offset = 0;
                 offset < encryptedBytes;
                 offset += BlockBytes)
            {
                byte[] input =
                    new byte[BlockBytes];

                Buffer.BlockCopy(
                    encrypted,
                    HeaderBytes + offset,
                    input,
                    0,
                    BlockBytes);

                byte[] output;
                Seed.DecryptChunk(input, out output);

                Buffer.BlockCopy(
                    output,
                    0,
                    alignedPlaintext,
                    offset,
                    BlockBytes);
            }

            if (realSize > alignedPlaintext.Length)
            {
                throw new InvalidDataException(
                    "SData real size exceeds decrypted payload.");
            }

            byte[] plaintext =
                new byte[realSize];

            Buffer.BlockCopy(
                alignedPlaintext,
                0,
                plaintext,
                0,
                (int)realSize);

            if (validateChecksum)
            {
                uint actual =
                    CalculateChecksum(plaintext);

                if (actual != expectedChecksum)
                {
                    throw new InvalidDataException(
                        "SData checksum mismatch. Expected 0x" +
                        expectedChecksum.ToString("X8") +
                        ", got 0x" +
                        actual.ToString("X8") + ".");
                }
            }

            return new LegacySDataDecryptionResult
            {
                Signature = signature,
                ExpectedChecksum = expectedChecksum,
                RealSize = realSize,
                BinaryHeader = binaryHeader,
                Plaintext = plaintext
            };
        }

        public static uint CalculateChecksum(byte[] plaintext)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));

            uint checksum = uint.MaxValue;

            for (int i = 0; i < plaintext.Length; i++)
            {
                uint index =
                    (checksum & 0xFFu) ^
                    plaintext[i];

                uint key =
                    Seed.ByteArrayToUInt32(
                        SeedConstants.ChecksumTable,
                        index * 4u);

                Seed.EndiannessSwap(ref key);

                checksum >>= 8;
                checksum ^= key;
            }

            return ~checksum;
        }
    }
}
