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

    public struct LegacyEftColorFrame
    {
        public float R;
        public float G;
        public float B;
        public float A;
        public float Time;
    }

    public struct LegacyEftFloatFrame
    {
        public float Value;
        public float Time;
    }

    public struct LegacyEftScaleFrame
    {
        public float Min;
        public float Max;
        public float Time;
    }

    public sealed class LegacyEftEffect
    {
        public string Name = string.Empty;

        public bool VelocityRandomX;
        public bool VelocityRandomY;
        public bool VelocityRandomZ;
        public bool Loop;
        public int DestinationBlend;
        public int VelocityMode;
        public int SourceBlend;
        public bool TextureLoop;
        public int MeshIndex = -1;
        public bool MotionPathEnabled;

        public float DelayPerFrame;
        public float EmitRateMax;
        public float LifeMax;
        public float EmitRateMin;
        public float LifeMin;
        public float EmitterDuration;
        public float SwirlSpeed;
        public float Unknown18;

        public Vector3 EmitPositionSpread;
        public Vector3 Acceleration;
        public Vector3 EmitOrigin;
        public Vector3 VelocityMin;
        public Vector3 VelocityMax;

        public int BaseAxis;
        public bool GravityEnabled;
        public bool AttractEnabled;
        public Vector3 AttractPoint;
        public float AttractStrength;

        public bool AngularVelocityRandom;
        public bool RotationEnabled;
        public float AngularVelocity;
        public int RotationAxis;

        public int Ef3Unused;
        public int DistanceScaleMode;

        public List<LegacyEftColorFrame> ColorFrames { get; } =
            new List<LegacyEftColorFrame>();
        public List<LegacyEftFloatFrame> VelocityScaleFrames { get; } =
            new List<LegacyEftFloatFrame>();
        public List<LegacyEftScaleFrame> ScaleFrames { get; } =
            new List<LegacyEftScaleFrame>();

        public bool MirrorTexture;
        public int InitialRotationAxis;
        public int InitialRotationMinDegrees;
        public int InitialRotationMaxDegrees;

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

            effect.VelocityRandomX =
                ReadBool32(reader);
            effect.VelocityRandomY =
                ReadBool32(reader);
            effect.VelocityRandomZ =
                ReadBool32(reader);
            effect.Loop =
                ReadBool32(reader);
            effect.DestinationBlend =
                reader.ReadInt32();
            effect.VelocityMode =
                reader.ReadInt32();
            effect.SourceBlend =
                reader.ReadInt32();
            effect.TextureLoop =
                ReadBool32(reader);
            effect.MeshIndex =
                reader.ReadInt32();
            effect.MotionPathEnabled =
                ReadBool32(reader);

            effect.DelayPerFrame =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.EmitRateMax =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.LifeMax =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.EmitRateMin =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.LifeMin =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.EmitterDuration =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.SwirlSpeed =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.Unknown18 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            effect.EmitPositionSpread =
                LegacyFormatPrimitives
                    .ReadVector3(reader);
            effect.Acceleration =
                LegacyFormatPrimitives
                    .ReadVector3(reader);
            effect.EmitOrigin =
                LegacyFormatPrimitives
                    .ReadVector3(reader);
            effect.VelocityMin =
                LegacyFormatPrimitives
                    .ReadVector3(reader);
            effect.VelocityMax =
                LegacyFormatPrimitives
                    .ReadVector3(reader);

            effect.BaseAxis =
                reader.ReadInt32();
            effect.GravityEnabled =
                ReadBool32(reader);
            effect.AttractEnabled =
                ReadBool32(reader);
            effect.AttractPoint =
                LegacyFormatPrimitives
                    .ReadVector3(reader);
            effect.AttractStrength =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            effect.AngularVelocityRandom =
                ReadBool32(reader);
            effect.RotationEnabled =
                ReadBool32(reader);
            effect.AngularVelocity =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);
            effect.RotationAxis =
                reader.ReadInt32();

            if (format == LegacyEftFormat.EF3)
            {
                effect.Ef3Unused =
                    reader.ReadInt32();
                effect.DistanceScaleMode =
                    reader.ReadInt32();
            }

            int colorCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT color frame",
                    MaxListCount);

            for (int i = 0;
                 i < colorCount;
                 i++)
            {
                effect.ColorFrames.Add(
                    new LegacyEftColorFrame
                    {
                        R =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        G =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        B =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        A =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            int velocityScaleCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT velocity scale frame",
                    MaxListCount);

            for (int i = 0;
                 i < velocityScaleCount;
                 i++)
            {
                effect.VelocityScaleFrames.Add(
                    new LegacyEftFloatFrame
                    {
                        Value =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            int scaleCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT scale frame",
                    MaxListCount);

            for (int i = 0;
                 i < scaleCount;
                 i++)
            {
                effect.ScaleFrames.Add(
                    new LegacyEftScaleFrame
                    {
                        Min =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Max =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader),
                        Time =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(reader)
                    });
            }

            effect.MirrorTexture =
                ReadBool32(reader);
            effect.InitialRotationAxis =
                reader.ReadInt32();
            effect.InitialRotationMinDegrees =
                reader.ReadInt32();
            effect.InitialRotationMaxDegrees =
                reader.ReadInt32();

            int textureCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "EFT effect texture",
                    MaxListCount);

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

        private static bool ReadBool32(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                4);

            int value =
                reader.ReadInt32();

            if (value != 0 &&
                value != 1)
            {
                throw new InvalidDataException(
                    "EFT boolean field contains " +
                    value + " instead of 0/1.");
            }

            return value != 0;
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
