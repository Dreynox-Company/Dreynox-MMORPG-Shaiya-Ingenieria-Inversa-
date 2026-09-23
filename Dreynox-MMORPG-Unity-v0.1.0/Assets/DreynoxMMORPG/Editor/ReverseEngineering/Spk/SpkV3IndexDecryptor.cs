using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public sealed class SpkIndexDecryptionResult
    {
        public SpkV3Header header;
        public string encryptedSha256;
        public string decryptedSha256;
        public byte[] decryptedBytes;
        public bool startsWithZstdMagic;
    }

    public static class SpkV3IndexDecryptor
    {
        public static SpkIndexDecryptionResult Decrypt(string spkPath, SpkIndexCryptoProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            SpkV3Header header = SpkV3HeaderParser.Parse(spkPath);
            if (!header.LooksObservedV3) throw new InvalidDataException("El contenedor no coincide con el layout SPK v3 observado.");
            if (header.indexStoredBytes > int.MaxValue) throw new InvalidDataException("Índice demasiado grande para esta fase del importador.");

            byte[] cipher = new byte[(int)header.indexStoredBytes];
            using (FileStream stream = new FileStream(spkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if ((long)header.indexOffset < 0 || header.indexOffset + header.indexStoredBytes > (ulong)stream.Length)
                    throw new EndOfStreamException("El índice declarado cae fuera del archivo SPK.");
                stream.Position = (long)header.indexOffset;
                int total = 0;
                while (total < cipher.Length)
                {
                    int read = stream.Read(cipher, total, cipher.Length - total);
                    if (read <= 0) throw new EndOfStreamException("Índice SPK truncado.");
                    total += read;
                }
            }

            string encryptedHash = FileFingerprint.Sha256(cipher);
            string headerHash = FileFingerprint.ToHex(header.encryptedIndexSha256);
            if (!string.Equals(encryptedHash, headerHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SHA-256 del índice cifrado no coincide con el header.");
            if (!string.IsNullOrWhiteSpace(profile.indexSha256) && !string.Equals(profile.indexSha256, encryptedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El perfil criptográfico pertenece a otro índice SPK.");

            byte[] plain = AesGcmManaged.Decrypt(profile.SecretBytes(), header.indexNonce, cipher, header.indexTag);
            bool zstd = plain.Length >= 4 && plain[0] == 0x28 && plain[1] == 0xB5 && plain[2] == 0x2F && plain[3] == 0xFD;
            return new SpkIndexDecryptionResult
            {
                header = header,
                encryptedSha256 = encryptedHash,
                decryptedSha256 = FileFingerprint.Sha256(plain),
                decryptedBytes = plain,
                startsWithZstdMagic = zstd
            };
        }
    }
}
