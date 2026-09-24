using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyDbMonsterDataFile
    {
        private readonly Dictionary<long, LegacyDbMonsterDataRecord> _byId =
            new Dictionary<long, LegacyDbMonsterDataRecord>();

        public List<string> Fields { get; } =
            new List<string>();

        public List<LegacyDbMonsterDataRecord> Records { get; } =
            new List<LegacyDbMonsterDataRecord>();

        public bool TryGet(
            long id,
            out LegacyDbMonsterDataRecord record)
        {
            return _byId.TryGetValue(id, out record);
        }

        internal void Add(LegacyDbMonsterDataRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (_byId.ContainsKey(record.Id))
            {
                throw new InvalidDataException(
                    "DBMonsterData contains duplicate id " +
                    record.Id + ".");
            }

            _byId.Add(record.Id, record);
            Records.Add(record);
        }
    }

    public sealed class LegacyDbMonsterDataRecord
    {
        public long Id;
        public long Image;
        public long Level;
        public long Ai;
        public long Hp;
        public long Size;
        public long Element;
        public long NormalTime;
        public long NormalStep;
        public long ChaseTime;
        public long ChaseStep;
        public long ChaseRange;
        public long AttackAni1;
        public long AttackType1;
        public long AttackTime1;
        public long AttackRange1;
        public long Attack1;
        public long AttackPlus1;
        public long AttackAttrib1;
        public long AttackSpecial1;
        public long AttackOk1;
        public long AttackAni2;
        public long AttackType2;
        public long AttackTime2;
        public long AttackRange2;
        public long Attack2;
        public long AttackPlus2;
        public long AttackAttrib2;
        public long AttackSpecial2;
        public long AttackOk2;
        public long AttackAni3;
        public long AttackType3;
        public long AttackTime3;
        public long AttackRange3;
        public long Attack3;
        public long AttackPlus3;
        public long AttackAttrib3;
        public long AttackSpecial3;
        public long AttackOk3;
    }

    public sealed class LegacyDbMonsterTextFile
    {
        private readonly Dictionary<long, string> _byId =
            new Dictionary<long, string>();

        public int Count => _byId.Count;

        public bool TryGetName(
            long id,
            out string name)
        {
            return _byId.TryGetValue(id, out name);
        }

        internal void Add(long id, string name)
        {
            if (_byId.ContainsKey(id))
            {
                throw new InvalidDataException(
                    "DBMonsterText contains duplicate id " +
                    id + ".");
            }

            _byId.Add(
                id,
                name ?? string.Empty);
        }
    }

    public static class LegacyDbMonsterDataParser
    {
        private const int BinaryHeaderBytes = 128;
        private const int MaxFields = 512;
        private const int MaxRecords = 100_000;
        private const int MaxStringBytes = 16_384;

        public static LegacyDbMonsterDataFile ParseData(
            string path)
        {
            LegacySDataDecryptionResult decrypted =
                LegacySDataDecryptor.Decrypt(
                    path,
                    validateChecksum: false);

            return ParseDataPlain(
                decrypted.Plaintext);
        }

        public static LegacyDbMonsterTextFile ParseText(
            string path)
        {
            LegacySDataDecryptionResult decrypted =
                LegacySDataDecryptor.Decrypt(
                    path,
                    validateChecksum: false);

            return ParseTextPlain(
                decrypted.Plaintext);
        }

        public static LegacyDbMonsterDataFile ParseDataPlain(
            byte[] plaintext)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));

            using (MemoryStream stream =
                   new MemoryStream(plaintext, false))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.Unicode))
            {
                SkipBinaryHeader(reader);

                List<string> fields =
                    ReadFields(reader);

                int recordCount =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "DBMonsterData record",
                        MaxRecords);

                Dictionary<string, int> column =
                    BuildColumnMap(fields);

                RequireColumns(
                    column,
                    "id",
                    "image",
                    "level",
                    "ai",
                    "hp",
                    "size",
                    "attrib",
                    "normaltime",
                    "normalstep",
                    "chasetime",
                    "chasestep",
                    "chaserange",
                    "attackani1",
                    "attacktype1",
                    "attacktime1",
                    "attackrange1",
                    "attack1",
                    "attackplus1",
                    "attackattrib1",
                    "attackspecial1",
                    "attackok1",
                    "attackani2",
                    "attacktype2",
                    "attacktime2",
                    "attackrange2",
                    "attack2",
                    "attackplus2",
                    "attackattrib2",
                    "attackspecial2",
                    "attackok2",
                    "attackani3",
                    "attacktype3",
                    "attacktime3",
                    "attackrange3",
                    "attack3",
                    "attackplus3",
                    "attackattrib3",
                    "attackspecial3",
                    "attackok3");

                var result =
                    new LegacyDbMonsterDataFile();

                result.Fields.AddRange(fields);

                long bytesPerRecord =
                    checked((long)fields.Count * 8L);

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    bytesPerRecord * recordCount);

                long[] values =
                    new long[fields.Count];

                for (int row = 0;
                     row < recordCount;
                     row++)
                {
                    for (int i = 0; i < values.Length; i++)
                        values[i] = reader.ReadInt64();

                    var record =
                        new LegacyDbMonsterDataRecord
                        {
                            Id = V(values, column, "id"),
                            Image = V(values, column, "image"),
                            Level = V(values, column, "level"),
                            Ai = V(values, column, "ai"),
                            Hp = V(values, column, "hp"),
                            Size = V(values, column, "size"),
                            Element = V(values, column, "attrib"),
                            NormalTime = V(values, column, "normaltime"),
                            NormalStep = V(values, column, "normalstep"),
                            ChaseTime = V(values, column, "chasetime"),
                            ChaseStep = V(values, column, "chasestep"),
                            ChaseRange = V(values, column, "chaserange"),
                            AttackAni1 = V(values, column, "attackani1"),
                            AttackType1 = V(values, column, "attacktype1"),
                            AttackTime1 = V(values, column, "attacktime1"),
                            AttackRange1 = V(values, column, "attackrange1"),
                            Attack1 = V(values, column, "attack1"),
                            AttackPlus1 = V(values, column, "attackplus1"),
                            AttackAttrib1 = V(values, column, "attackattrib1"),
                            AttackSpecial1 = V(values, column, "attackspecial1"),
                            AttackOk1 = V(values, column, "attackok1"),
                            AttackAni2 = V(values, column, "attackani2"),
                            AttackType2 = V(values, column, "attacktype2"),
                            AttackTime2 = V(values, column, "attacktime2"),
                            AttackRange2 = V(values, column, "attackrange2"),
                            Attack2 = V(values, column, "attack2"),
                            AttackPlus2 = V(values, column, "attackplus2"),
                            AttackAttrib2 = V(values, column, "attackattrib2"),
                            AttackSpecial2 = V(values, column, "attackspecial2"),
                            AttackOk2 = V(values, column, "attackok2"),
                            AttackAni3 = V(values, column, "attackani3"),
                            AttackType3 = V(values, column, "attacktype3"),
                            AttackTime3 = V(values, column, "attacktime3"),
                            AttackRange3 = V(values, column, "attackrange3"),
                            Attack3 = V(values, column, "attack3"),
                            AttackPlus3 = V(values, column, "attackplus3"),
                            AttackAttrib3 = V(values, column, "attackattrib3"),
                            AttackSpecial3 = V(values, column, "attackspecial3"),
                            AttackOk3 = V(values, column, "attackok3")
                        };

                    if (record.Id <= 0)
                    {
                        throw new InvalidDataException(
                            "DBMonsterData row " + row +
                            " has invalid id " +
                            record.Id + ".");
                    }

                    if (record.Image < 0)
                    {
                        throw new InvalidDataException(
                            "DBMonsterData id " + record.Id +
                            " has negative image/model index " +
                            record.Image + ".");
                    }

                    result.Add(record);
                }

                LegacyFormatPrimitives.EnsureFullyConsumed(
                    reader,
                    "DBMonsterData.SData");

                return result;
            }
        }

        public static LegacyDbMonsterTextFile ParseTextPlain(
            byte[] plaintext)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));

            using (MemoryStream stream =
                   new MemoryStream(plaintext, false))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
            {
                SkipBinaryHeader(reader);

                List<string> fields =
                    ReadFields(reader);

                if (fields.Count != 2 ||
                    !string.Equals(
                        fields[0],
                        "id",
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        fields[1],
                        "name",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "DBMonsterText column schema changed.");
                }

                int recordCount =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "DBMonsterText record",
                        MaxRecords);

                var result =
                    new LegacyDbMonsterTextFile();

                for (int row = 0;
                     row < recordCount;
                     row++)
                {
                    LegacyFormatPrimitives.EnsureRemaining(
                        reader,
                        12);

                    long id =
                        reader.ReadInt64();

                    int length =
                        LegacyFormatPrimitives.ReadCount(
                            reader,
                            "DBMonsterText name",
                            MaxStringBytes);

                    LegacyFormatPrimitives.EnsureRemaining(
                        reader,
                        length);

                    byte[] bytes =
                        reader.ReadBytes(length);

                    string name =
                        DecodeLegacySpanish(bytes)
                        .TrimEnd(' ');

                    result.Add(id, name);
                }

                LegacyFormatPrimitives.EnsureFullyConsumed(
                    reader,
                    "DBMonsterText.SData");

                return result;
            }
        }

        private static void SkipBinaryHeader(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                BinaryHeaderBytes + 4L);

            reader.BaseStream.Position +=
                BinaryHeaderBytes;
        }

        private static List<string> ReadFields(
            BinaryReader reader)
        {
            int fieldCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "BinarySData field",
                    MaxFields);

            var fields =
                new List<string>(fieldCount);

            for (int i = 0; i < fieldCount; i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    1);

                int charCount =
                    reader.ReadByte();

                int byteCount =
                    checked(charCount * 2);

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    byteCount);

                string field =
                    Encoding.Unicode.GetString(
                        reader.ReadBytes(byteCount));

                fields.Add(field);
            }

            return fields;
        }

        private static Dictionary<string, int> BuildColumnMap(
            IReadOnlyList<string> fields)
        {
            var result =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < fields.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(fields[i]) ||
                    result.ContainsKey(fields[i]))
                {
                    throw new InvalidDataException(
                        "BinarySData has blank or duplicate field at " +
                        i + ".");
                }

                result.Add(fields[i], i);
            }

            return result;
        }

        private static void RequireColumns(
            IDictionary<string, int> columns,
            params string[] required)
        {
            for (int i = 0; i < required.Length; i++)
            {
                if (!columns.ContainsKey(required[i]))
                {
                    throw new InvalidDataException(
                        "DBMonsterData missing column '" +
                        required[i] + "'.");
                }
            }
        }

        private static long V(
            IReadOnlyList<long> values,
            IReadOnlyDictionary<string, int> columns,
            string field)
        {
            return values[columns[field]];
        }

        private static string DecodeLegacySpanish(
            byte[] bytes)
        {
            if (bytes == null ||
                bytes.Length == 0)
                return string.Empty;

            // The Spanish client stores these strings as a single-byte
            // Western encoding. Mapping byte -> Unicode code point preserves
            // ñ/á/é/etc without relying on platform code-page providers.
            char[] chars =
                new char[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
                chars[i] = (char)bytes[i];

            return new string(chars);
        }
    }
}
