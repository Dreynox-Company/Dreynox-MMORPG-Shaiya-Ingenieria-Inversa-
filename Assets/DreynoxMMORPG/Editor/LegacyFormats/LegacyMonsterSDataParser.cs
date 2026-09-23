using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyMonsterSDataFile
    {
        public List<LegacyMonsterRecord> Records { get; } =
            new List<LegacyMonsterRecord>();

        public bool TryGet(
            uint mobId,
            out LegacyMonsterRecord record)
        {
            if (mobId >= Records.Count)
            {
                record = null;
                return false;
            }

            record = Records[(int)mobId];
            return true;
        }
    }

    public sealed class LegacyMonsterRecord
    {
        public uint MobId;
        public string MobName = string.Empty;
        public short ModelId;
        public short Level;
        public byte Ai;
        public int Hp;
        public byte Day;
        public byte Size;
        public byte Element;
        public int NormalTime;
        public byte NormalStep;
        public int ChaseTime;
        public byte ChaseStep;
        public byte AttackType1;
        public byte AttackAni1;
        public byte AttackType2;
        public byte AttackAni2;
        public byte AttackType3;
        public byte AttackAni3;
        public byte AttackPlus3;
        public short QuestItemId;
    }

    public static class LegacyMonsterSDataParser
    {
        private const int MaxRecords = 100_000;
        private const int MaxStringBytes = 4096;

        public static LegacyMonsterSDataFile ParseEncrypted(
            string path,
            bool validateChecksum = true)
        {
            LegacySDataDecryptionResult decrypted =
                LegacySDataDecryptor.Decrypt(
                    path,
                    validateChecksum);

            return ParsePlain(decrypted.Plaintext);
        }

        public static LegacyMonsterSDataFile ParsePlain(
            byte[] plaintext)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));

            using (MemoryStream stream =
                   new MemoryStream(plaintext, false))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
            {
                return ParsePlain(reader);
            }
        }

        private static LegacyMonsterSDataFile ParsePlain(
            BinaryReader reader)
        {
            int count =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "Monster.SData record",
                    MaxRecords);

            var result =
                new LegacyMonsterSDataFile();

            for (int i = 0; i < count; i++)
            {
                var record =
                    new LegacyMonsterRecord
                    {
                        MobId = (uint)i,
                        MobName =
                            ReadString(
                                reader,
                                i),
                        ModelId =
                            reader.ReadInt16(),
                        Level =
                            reader.ReadInt16(),
                        Ai =
                            reader.ReadByte(),
                        Hp =
                            reader.ReadInt32(),
                        Day =
                            reader.ReadByte(),
                        Size =
                            reader.ReadByte(),
                        Element =
                            reader.ReadByte(),
                        NormalTime =
                            reader.ReadInt32(),
                        NormalStep =
                            reader.ReadByte(),
                        ChaseTime =
                            reader.ReadInt32(),
                        ChaseStep =
                            reader.ReadByte(),
                        AttackType1 =
                            reader.ReadByte(),
                        AttackAni1 =
                            reader.ReadByte(),
                        AttackType2 =
                            reader.ReadByte(),
                        AttackAni2 =
                            reader.ReadByte(),
                        AttackType3 =
                            reader.ReadByte(),
                        AttackAni3 =
                            reader.ReadByte(),
                        AttackPlus3 =
                            reader.ReadByte(),
                        QuestItemId =
                            reader.ReadInt16()
                    };

                if (record.ModelId < 0)
                {
                    throw new InvalidDataException(
                        "Monster.SData record " + i +
                        " has negative model id " +
                        record.ModelId + ".");
                }

                result.Records.Add(record);
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "Monster.SData");

            return result;
        }

        private static string ReadString(
            BinaryReader reader,
            int recordIndex)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "Monster.SData name",
                    MaxStringBytes);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            if (length == 0)
                return string.Empty;

            byte[] bytes =
                reader.ReadBytes(length);

            string value =
                Encoding.UTF8.GetString(bytes);

            return value.TrimEnd('\0');
        }
    }
}
