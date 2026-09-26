using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyMonFormat
    {
        MO2,
        MO4
    }

    public sealed class LegacyMonFile
    {
        public string Signature { get; internal set; } = string.Empty;
        public LegacyMonFormat Format { get; internal set; }
        public List<LegacyMonRecord> Records { get; } =
            new List<LegacyMonRecord>();
    }

    public sealed class LegacyMonRecord
    {
        public string Name = string.Empty;
        public byte Unknown;

        public string WalkAnimation = string.Empty;
        public string RunAnimation = string.Empty;
        public string JumpAttack1Animation = string.Empty;
        public string Attack2Animation = string.Empty;
        public string Attack3Animation = string.Empty;
        public string DeathAnimation = string.Empty;
        public string BreathAnimation = string.Empty;
        public string DamageAnimation = string.Empty;
        public string IdleAnimation = string.Empty;

        public string Attack1Wav = string.Empty;
        public string Attack2Wav = string.Empty;
        public string Attack3Wav = string.Empty;
        public string DeathWav = string.Empty;

        public string Attack1Effect = string.Empty;
        public string Attack2Effect = string.Empty;
        public string Attack3Effect = string.Empty;
        public string DieEffect = string.Empty;
        public string AttachEffect = string.Empty;

        public List<LegacyMonObject> Objects { get; } =
            new List<LegacyMonObject>();

        public float Height;

        public List<LegacyMonEffect> Effects { get; } =
            new List<LegacyMonEffect>();
    }

    public struct LegacyMonObject
    {
        public string MeshName;
        public string TextureName;
    }

    public struct LegacyMonEffect
    {
        public int BoneId;
        public int EffectId;
    }

    public static class LegacyMonParser
    {
        private const int MaxRecords = 100_000;
        private const int MaxObjectsPerRecord = 256;
        private const int MaxEffectsPerRecord = 256;
        private const int MaxStringBytes = 4096;

        public static LegacyMonFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("MON path is required.", nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
                return Parse(reader);
        }

        public static LegacyMonFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
                return Parse(reader);
        }

        private static LegacyMonFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 7);

            string signature =
                Encoding.ASCII.GetString(reader.ReadBytes(3));

            LegacyMonFormat format;
            switch (signature)
            {
                case "MO2":
                    format = LegacyMonFormat.MO2;
                    break;

                case "MO4":
                    format = LegacyMonFormat.MO4;
                    break;

                default:
                    throw new InvalidDataException(
                        "Unsupported MON signature '" + signature + "'.");
            }

            int recordCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "MON record",
                    MaxRecords);

            var result = new LegacyMonFile
            {
                Signature = signature,
                Format = format
            };

            for (int i = 0; i < recordCount; i++)
                result.Records.Add(ReadRecord(reader, format, i));

            LegacyFormatPrimitives.EnsureFullyConsumed(reader, "MON");
            return result;
        }

        private static LegacyMonRecord ReadRecord(
            BinaryReader reader,
            LegacyMonFormat format,
            int ordinal)
        {
            var record = new LegacyMonRecord
            {
                Name = ReadString(reader, "name", ordinal)
            };

            LegacyFormatPrimitives.EnsureRemaining(reader, 1);
            record.Unknown = reader.ReadByte();

            record.WalkAnimation = ReadString(reader, "walk ANI", ordinal);
            record.RunAnimation = ReadString(reader, "run ANI", ordinal);
            record.JumpAttack1Animation =
                ReadString(reader, "jump/attack1 ANI", ordinal);
            record.Attack2Animation = ReadString(reader, "attack2 ANI", ordinal);
            record.Attack3Animation = ReadString(reader, "attack3 ANI", ordinal);
            record.DeathAnimation = ReadString(reader, "death ANI", ordinal);
            record.BreathAnimation = ReadString(reader, "breath ANI", ordinal);
            record.DamageAnimation = ReadString(reader, "damage ANI", ordinal);
            record.IdleAnimation = ReadString(reader, "idle ANI", ordinal);

            record.Attack1Wav = ReadString(reader, "attack1 WAV", ordinal);
            record.Attack2Wav = ReadString(reader, "attack2 WAV", ordinal);
            record.Attack3Wav = ReadString(reader, "attack3 WAV", ordinal);
            record.DeathWav = ReadString(reader, "death WAV", ordinal);

            record.Attack1Effect = ReadString(reader, "attack1 EFT", ordinal);
            record.Attack2Effect = ReadString(reader, "attack2 EFT", ordinal);
            record.Attack3Effect = ReadString(reader, "attack3 EFT", ordinal);
            record.DieEffect = ReadString(reader, "die EFT", ordinal);

            if (format == LegacyMonFormat.MO4)
                record.AttachEffect =
                    ReadString(reader, "attach EFT", ordinal);

            int objectCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "MON object",
                    MaxObjectsPerRecord);

            for (int i = 0; i < objectCount; i++)
            {
                record.Objects.Add(
                    new LegacyMonObject
                    {
                        MeshName = ReadString(
                            reader,
                            "mesh name",
                            ordinal),
                        TextureName = ReadString(
                            reader,
                            "texture name",
                            ordinal)
                    });
            }

            record.Height =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);

            int effectCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "MON attached effect",
                    MaxEffectsPerRecord);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)effectCount * 8L);

            for (int i = 0; i < effectCount; i++)
            {
                record.Effects.Add(
                    new LegacyMonEffect
                    {
                        BoneId = reader.ReadInt32(),
                        EffectId = reader.ReadInt32()
                    });
            }

            return record;
        }

        private static string ReadString(
            BinaryReader reader,
            string field,
            int ordinal)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "MON string",
                    MaxStringBytes);

            LegacyFormatPrimitives.EnsureRemaining(reader, length);

            if (length == 0)
                return string.Empty;

            byte[] bytes = reader.ReadBytes(length);

            string value =
                Encoding.ASCII.GetString(bytes);

            return value.TrimEnd('\0');
        }
    }
}
