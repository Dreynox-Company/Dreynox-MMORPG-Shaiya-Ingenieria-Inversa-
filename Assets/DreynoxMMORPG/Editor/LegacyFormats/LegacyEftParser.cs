using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyEftFormat
    {
        EFT,
        EF2,
        EF3
    }

    public sealed class LegacyEftFile
    {
        public LegacyEftFormat Format { get; internal set; }
        public List<string> MeshNames { get; } =
            new List<string>();
        public List<string> TextureNames { get; } =
            new List<string>();
        public List<LegacyEftEffect> Effects { get; } =
            new List<LegacyEftEffect>();
        public List<LegacyEftSequence> Sequences { get; } =
            new List<LegacyEftSequence>();
    }

    public struct LegacyEftRotation
    {
        public Quaternion Rotation;
        public float Time;
    }

    public struct LegacyEftOpacityFrame
    {
        public float Opacity;
        public float Time;
    }

    public struct LegacyEftSub3
    {
        public float Unknown1;
        public float Unknown2;
        public float Time;
    }

    public sealed class LegacyEftEffect
    {
        public string Name = string.Empty;

        public int Unknown1;
        public int Unknown2;
        public int Unknown3;
        public int Unknown4;
        public int Unknown5;
        public int Unknown6;
        public int Unknown7;
        public int Unknown8;
        public int MeshIndex;
        public int Unknown10;

        public float Unknown11;
        public float Unknown12;
        public float Unknown13;
        public float Unknown14;
        public float Unknown15;
        public float Unknown16;
        public float Unknown17;
        public float Unknown18;

        public Vector3 UnknownVector1;
        public Vector3 UnknownVector2;
        public Vector3 Position;
        public Vector3 UnknownVector4;
        public Vector3 UnknownVector5;

        public int Unknown19;
        public int Unknown20;
        public int Unknown21;

        public Vector3 UnknownVector6;

        public float Unknown22;
        public int Unknown23;
        public int Unknown24;
        public float Unknown25;
        public int Unknown26;
        public float Unknown27;
        public float Unknown28;

        public List<LegacyEftRotation> Rotations { get; } =
            new List<LegacyEftRotation>();
        public List<LegacyEftOpacityFrame> OpacityFrames { get; } =
            new List<LegacyEftOpacityFrame>();
        public List<LegacyEftSub3> Sub3 { get; } =
            new List<LegacyEftSub3>();

        public int Unknown29;
        public int Unknown30;
        public int Unknown31;
        public int Unknown32;

        public List<int> TextureIds { get; } =
            new List<int>();
    }

    public struct LegacyEftSequenceRecord
    {
        public int EffectId;
        public float Time;
    }

    public sealed class LegacyEftSequence
    {
        public string Name = string.Empty;
        public List<LegacyEftSequenceRecord> Records { get; } =
            new List<LegacyEftSequenceRecord>();
    }

    public static class LegacyEftParser
    {
        private const int MaxStringBytes = 16_384;
        private const int MaxListCount = 1_000_000;

        public static LegacyEftFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "EFT path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
                return Parse(reader);
        }

        public static LegacyEftFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream =
                   new MemoryStream(bytes, false))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
                return Parse(reader);
        }

        private static LegacyEftFile Parse(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                3);

            string signature =
                Encoding.ASCII.GetString(
                    reader.ReadBytes(3));

            LegacyEftFormat format;
            switch (signature)
            {
                case "EFT":
                    format = LegacyEftFormat.EFT;
                    break;
                case "EF2":
                    format = LegacyEftFormat.EF2;
                    break;
                case "EF3":
                    format = LegacyEftFormat.EF3;
                    break;
                default:
                    throw new InvalidDataException(
                        "Unsupported EFT signature '" +
                        signature + "'.");
            }

            var result =
                new LegacyEftFile
                {
                    Format = format
                };

            ReadStringList(
                reader,
                result.MeshNames,
                "EFT mesh");

            ReadStringList(
                reader,
                result.TextureNames,
                "EFT texture");

            int effectCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT effect",
                    MaxListCount);

            for (int i = 0;
                 i < effectCount;
                 i++)
            {
                result.Effects.Add(
                    ReadEffect(
                        reader,
                        format,
                        i));
            }

            int sequenceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT sequence",
                    MaxListCount);

            for (int i = 0;
                 i < sequenceCount;
                 i++)
            {
                result.Sequences.Add(
                    ReadSequence(reader));
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "EFT");

            return result;
        }

        private static LegacyEftEffect ReadEffect(
            BinaryReader reader,
            LegacyEftFormat format,
            int ordinal)
        {
            var effect =
                new LegacyEftEffect
                {
                    Name = ReadString(reader)
                };

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                10L * 4L +
                8L * 4L +
                5L * 12L +
                3L * 4L +
                12L +
                4L + 4L + 4L + 4L + 4L);

            effect.Unknown1 = reader.ReadInt32();
            effect.Unknown2 = reader.ReadInt32();
            effect.Unknown3 = reader.ReadInt32();
            effect.Unknown4 = reader.ReadInt32();
            effect.Unknown5 = reader.ReadInt32();
            effect.Unknown6 = reader.ReadInt32();
            effect.Unknown7 = reader.ReadInt32();
            effect.Unknown8 = reader.ReadInt32();
            effect.MeshIndex = reader.ReadInt32();
            effect.Unknown10 = reader.ReadInt32();

            effect.Unknown11 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown12 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown13 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown14 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown15 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown16 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown17 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown18 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);

            effect.UnknownVector1 =
                LegacyFormatPrimitives.ReadVector3(reader);
            effect.UnknownVector2 =
                LegacyFormatPrimitives.ReadVector3(reader);
            effect.Position =
                LegacyFormatPrimitives.ReadVector3(reader);
            effect.UnknownVector4 =
                LegacyFormatPrimitives.ReadVector3(reader);
            effect.UnknownVector5 =
                LegacyFormatPrimitives.ReadVector3(reader);

            effect.Unknown19 = reader.ReadInt32();
            effect.Unknown20 = reader.ReadInt32();
            effect.Unknown21 = reader.ReadInt32();

            effect.UnknownVector6 =
                LegacyFormatPrimitives.ReadVector3(reader);

            effect.Unknown22 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown23 = reader.ReadInt32();
            effect.Unknown24 = reader.ReadInt32();
            effect.Unknown25 =
                LegacyFormatPrimitives.ReadFiniteSingle(reader);
            effect.Unknown26 = reader.ReadInt32();

            if (format == LegacyEftFormat.EF3)
            {
                effect.Unknown27 =
                    LegacyFormatPrimitives.ReadFiniteSingle(reader);
                effect.Unknown28 =
                    LegacyFormatPrimitives.ReadFiniteSingle(reader);
            }

            int rotationCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT rotation",
                    MaxListCount);

            for (int i = 0;
                 i < rotationCount;
                 i++)
            {
                effect.Rotations.Add(
                    new LegacyEftRotation
                    {
                        Rotation =
                            LegacyFormatPrimitives
                                .ReadQuaternion(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            int opacityCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT opacity",
                    MaxListCount);

            for (int i = 0;
                 i < opacityCount;
                 i++)
            {
                effect.OpacityFrames.Add(
                    new LegacyEftOpacityFrame
                    {
                        Opacity =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            int sub3Count =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT sub3",
                    MaxListCount);

            for (int i = 0;
                 i < sub3Count;
                 i++)
            {
                effect.Sub3.Add(
                    new LegacyEftSub3
                    {
                        Unknown1 =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Unknown2 =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                16);

            effect.Unknown29 = reader.ReadInt32();
            effect.Unknown30 = reader.ReadInt32();
            effect.Unknown31 = reader.ReadInt32();
            effect.Unknown32 = reader.ReadInt32();

            int textureCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT effect texture",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)textureCount * 4L));

            for (int i = 0;
                 i < textureCount;
                 i++)
            {
                int textureId =
                    reader.ReadInt32();

                if (textureId < 0)
                {
                    throw new InvalidDataException(
                        "EFT effect " + ordinal +
                        " has negative texture id " +
                        textureId + ".");
                }

                effect.TextureIds.Add(textureId);
            }

            return effect;
        }

        private static LegacyEftSequence ReadSequence(
            BinaryReader reader)
        {
            var sequence =
                new LegacyEftSequence
                {
                    Name = ReadString(reader)
                };

            int recordCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT sequence record",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)recordCount * 8L));

            for (int i = 0;
                 i < recordCount;
                 i++)
            {
                sequence.Records.Add(
                    new LegacyEftSequenceRecord
                    {
                        EffectId =
                            reader.ReadInt32(),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            return sequence;
        }

        private static void ReadStringList(
            BinaryReader reader,
            ICollection<string> output,
            string label)
        {
            int count =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label,
                    MaxListCount);

            for (int i = 0; i < count; i++)
                output.Add(ReadString(reader));
        }

        private static string ReadString(
            BinaryReader reader)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT string",
                    MaxStringBytes);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            if (length == 0)
                return string.Empty;

            return Encoding.UTF8
                .GetString(reader.ReadBytes(length))
                .TrimEnd('\0');
        }
    }
}
