using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public static class SpkV3CompleteIndexReader
    {
        public static SpkV3Index Read(string spkPath, SpkIndexCryptoProfile profile, string zstdExecutable = null)
        {
            if (!File.Exists(spkPath)) throw new FileNotFoundException("SPK no encontrado.", spkPath);
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            SpkIndexDecryptionResult encryptedStage = SpkV3IndexDecryptor.Decrypt(spkPath, profile);
            if (!encryptedStage.startsWithZstdMagic) throw new InvalidDataException("El plaintext autenticado del índice no es Zstandard.");
            if (encryptedStage.header.indexDecodedBytes > int.MaxValue) throw new InvalidDataException("Índice decodificado demasiado grande.");

            byte[] decoded = ZstdCliDecoder.Decompress(encryptedStage.decryptedBytes, (int)encryptedStage.header.indexDecodedBytes, zstdExecutable);
            var records = SpkV3IndexParser.ParseRecords(decoded, encryptedStage.header);
            byte[] auxBytes = SpkV3IndexParser.ReadAuxiliaryBytes(spkPath, encryptedStage.header);
            var auxiliary = SpkV3IndexParser.ParseAuxiliary(auxBytes, encryptedStage.header);
            SpkV3IndexParser.ValidateRelationships(records, auxiliary, encryptedStage.header);

            return new SpkV3Index
            {
                header = encryptedStage.header,
                records = records,
                auxiliary = auxiliary,
                encryptedIndexSha256 = encryptedStage.encryptedSha256,
                decodedIndexSha256 = FileFingerprint.Sha256(decoded)
            };
        }
    }
}
