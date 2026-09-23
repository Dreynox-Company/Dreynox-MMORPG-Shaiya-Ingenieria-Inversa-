using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    [Serializable]
    public sealed class SpkV3Record
    {
        public int ordinal;
        public ulong entryId;
        public ulong dataOffset;
        public ulong storedBytes;
        public ulong decodedBytes;
        public uint recordType;
        public uint auxiliaryStart;
        public uint chunkCount;
        public byte[] metadata;

        public bool Simple => recordType == 1;
        public bool Fragmented => recordType == 3;
        public bool Resource => Simple || Fragmented;
        public string IdHex => entryId.ToString("x16");

        public byte[] Nonce
        {
            get
            {
                if (!Simple || metadata == null || metadata.Length < 12) return Array.Empty<byte>();
                byte[] output = new byte[12];
                Buffer.BlockCopy(metadata, 0, output, 0, 12);
                return output;
            }
        }

        public byte[] Tag
        {
            get
            {
                if (!Simple || metadata == null || metadata.Length < 28) return Array.Empty<byte>();
                byte[] output = new byte[16];
                Buffer.BlockCopy(metadata, 12, output, 0, 16);
                return output;
            }
        }

        public uint Flags => Simple && metadata != null && metadata.Length >= 32 ? ReadUInt32LE(metadata, 28) : 0;

        private static uint ReadUInt32LE(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }
    }

    [Serializable]
    public sealed class SpkV3AuxiliaryRecord
    {
        public int ordinal;
        public ulong dataOffset;
        public uint storedBytes;
        public byte[] metadata;
    }

    [Serializable]
    public sealed class SpkV3Index
    {
        public SpkV3Header header;
        public List<SpkV3Record> records = new List<SpkV3Record>();
        public List<SpkV3AuxiliaryRecord> auxiliary = new List<SpkV3AuxiliaryRecord>();
        public string encryptedIndexSha256;
        public string decodedIndexSha256;

        public IEnumerable<SpkV3Record> Resources => records.Where(x => x.Resource);
        public IEnumerable<SpkV3Record> SimpleResources => records.Where(x => x.Simple);
        public IEnumerable<SpkV3Record> FragmentedResources => records.Where(x => x.Fragmented);
        public IEnumerable<SpkV3Record> SpecialRecords => records.Where(x => !x.Resource);
    }

    public static class SpkV3IndexParser
    {
        public const int RecordBytes = 96;
        public const int AuxiliaryRecordBytes = 32;
        public const uint Magic = 0x9e7bd34c;
        public const uint Version3 = 0x00030000;

        public static List<SpkV3Record> ParseRecords(byte[] decoded, SpkV3Header header)
        {
            if (decoded == null) throw new ArgumentNullException(nameof(decoded));
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (header.signature != Magic) throw new InvalidDataException("Firma SPK v3 no reconocida.");
            if (header.version != Version3) throw new InvalidDataException("Versión SPK no compatible con este parser.");
            if (header.indexDecodedBytes != (ulong)decoded.Length || decoded.Length % RecordBytes != 0)
                throw new InvalidDataException("El índice decodificado no tiene la longitud declarada.");
            if ((ulong)header.recordCount * RecordBytes != header.indexDecodedBytes)
                throw new InvalidDataException("recordCount e indexDecodedBytes no describen registros de 96 bytes.");

            List<SpkV3Record> output = new List<SpkV3Record>(checked((int)header.recordCount));
            for (int i = 0; i < header.recordCount; i++)
            {
                int o = checked(i * RecordBytes);
                ulong stored = ReadUInt64LE(decoded, o + 16);
                ulong mirror = ReadUInt64LE(decoded, o + 24);
                if (stored != mirror) throw new InvalidDataException($"Registro SPK {i}: storedBytes no coincide con su espejo.");
                for (int p = o + 80; p < o + 96; p++)
                    if (decoded[p] != 0) throw new InvalidDataException($"Registro SPK {i}: cola reservada no nula.");

                uint type = ReadUInt32LE(decoded, o + 40);
                byte[] metadata = new byte[32];
                Buffer.BlockCopy(decoded, o + 48, metadata, 0, 32);
                output.Add(new SpkV3Record
                {
                    ordinal = i,
                    entryId = ReadUInt64LE(decoded, o),
                    dataOffset = ReadUInt64LE(decoded, o + 8),
                    storedBytes = stored,
                    decodedBytes = ReadUInt64LE(decoded, o + 32),
                    recordType = type,
                    auxiliaryStart = ReadUInt32LE(decoded, o + 44),
                    chunkCount = type == 3 ? ReadUInt32LE(metadata, 28) : 0,
                    metadata = metadata
                });
            }
            return output;
        }

        public static List<SpkV3AuxiliaryRecord> ParseAuxiliary(byte[] bytes, SpkV3Header header)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (header == null) throw new ArgumentNullException(nameof(header));
            ulong expected = checked((ulong)header.auxiliaryCount * AuxiliaryRecordBytes);
            if ((ulong)bytes.Length != expected) throw new InvalidDataException("La tabla auxiliar SPK está incompleta.");

            List<SpkV3AuxiliaryRecord> output = new List<SpkV3AuxiliaryRecord>(checked((int)header.auxiliaryCount));
            for (int i = 0; i < header.auxiliaryCount; i++)
            {
                int o = checked(i * AuxiliaryRecordBytes);
                ulong offset = ReadUInt64LE(bytes, o);
                uint stored = ReadUInt32LE(bytes, o + 8);
                uint mirror = ReadUInt32LE(bytes, o + 12);
                if (stored != mirror || offset < SpkV3HeaderParser.HeaderBytes || offset + stored > header.auxiliaryOffset)
                    throw new InvalidDataException($"Registro auxiliar SPK {i} fuera de rango o con espejo inválido.");
                byte[] metadata = new byte[16];
                Buffer.BlockCopy(bytes, o + 16, metadata, 0, 16);
                output.Add(new SpkV3AuxiliaryRecord { ordinal = i, dataOffset = offset, storedBytes = stored, metadata = metadata });
            }
            return output;
        }

        public static byte[] ReadAuxiliaryBytes(string spkPath, SpkV3Header header)
        {
            if (string.IsNullOrWhiteSpace(spkPath)) throw new ArgumentException("Ruta SPK vacía.", nameof(spkPath));
            ulong byteCount = checked((ulong)header.auxiliaryCount * AuxiliaryRecordBytes);
            if (byteCount > int.MaxValue) throw new InvalidDataException("Tabla auxiliar demasiado grande.");
            byte[] bytes = new byte[(int)byteCount];
            using (FileStream stream = new FileStream(spkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (header.auxiliaryOffset + byteCount > (ulong)stream.Length) throw new EndOfStreamException("Tabla auxiliar fuera del SPK.");
                stream.Position = checked((long)header.auxiliaryOffset);
                int total = 0;
                while (total < bytes.Length)
                {
                    int read = stream.Read(bytes, total, bytes.Length - total);
                    if (read <= 0) throw new EndOfStreamException("Tabla auxiliar SPK truncada.");
                    total += read;
                }
            }
            return bytes;
        }

        public static void ValidateRelationships(IReadOnlyList<SpkV3Record> records, IReadOnlyList<SpkV3AuxiliaryRecord> auxiliary, SpkV3Header header)
        {
            if (records == null || auxiliary == null || header == null) throw new ArgumentNullException();
            List<SpkV3Record> resources = records.Where(x => x.Resource).OrderBy(x => x.dataOffset).ToList();
            if (resources.Count == 0 || resources[0].dataOffset != SpkV3HeaderParser.HeaderBytes || resources[resources.Count - 1].dataOffset + resources[resources.Count - 1].storedBytes != header.auxiliaryOffset)
                throw new InvalidDataException("Los recursos no cubren exactamente la región de datos SPK.");
            for (int i = 0; i + 1 < resources.Count; i++)
                if (resources[i].dataOffset + resources[i].storedBytes != resources[i + 1].dataOffset)
                    throw new InvalidDataException($"Hueco o solapamiento entre recursos {resources[i].ordinal} y {resources[i + 1].ordinal}.");

            int cursor = 0;
            foreach (SpkV3Record row in records.Where(x => x.Fragmented))
            {
                if (row.auxiliaryStart != (uint)cursor || row.chunkCount == 0 || (ulong)cursor + row.chunkCount > (ulong)auxiliary.Count)
                    throw new InvalidDataException($"Cadena auxiliar inválida para {row.IdHex}.");
                ulong total = 0;
                for (int local = 0; local < row.chunkCount; local++)
                {
                    SpkV3AuxiliaryRecord part = auxiliary[cursor + local];
                    total += part.storedBytes;
                    if (local == 0 && part.dataOffset != row.dataOffset) throw new InvalidDataException($"Primer fragmento no coincide con dataOffset de {row.IdHex}.");
                    if (local + 1 < row.chunkCount)
                    {
                        SpkV3AuxiliaryRecord next = auxiliary[cursor + local + 1];
                        if (part.dataOffset + part.storedBytes != next.dataOffset) throw new InvalidDataException($"Fragmentos no contiguos para {row.IdHex}.");
                    }
                }
                if (total != row.storedBytes) throw new InvalidDataException($"Los fragmentos no reconstruyen storedBytes de {row.IdHex}.");
                cursor += checked((int)row.chunkCount);
            }
            if (cursor != auxiliary.Count) throw new InvalidDataException("La tabla auxiliar contiene fragmentos sin propietario.");
        }

        private static uint ReadUInt32LE(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length) throw new EndOfStreamException();
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }

        private static ulong ReadUInt64LE(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 8 > bytes.Length) throw new EndOfStreamException();
            uint lo = ReadUInt32LE(bytes, offset);
            uint hi = ReadUInt32LE(bytes, offset + 4);
            return lo | ((ulong)hi << 32);
        }
    }
}
