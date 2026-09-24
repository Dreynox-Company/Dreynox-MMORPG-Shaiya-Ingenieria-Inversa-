using System;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyManiFile
    {
        public int Version;
        public int Unknown1;
        public Vector3 UnknownVector1;
        public float Unknown2;
        public float Unknown3;
        public float Unknown4;
        public int Unknown5;
        public int Unknown6;
        public Vector3 UnknownVector2;
        public float Unknown7;
        public float Unknown8;
        public int EnableRotation;
        public Vector3 RotationAxis;
        public float AnimationSpeed;
        public short UnknownShort1;
        public short UnknownShort2;
        public Vector3 UnknownVector4;
        public float Unknown11;
        public float Unknown12;
        public int Unknown13;

        public bool RotationEnabled =>
            EnableRotation != 0;
    }

    public static class LegacyManiParser
    {
        public const int SerializedBytes = 108;

        public static LegacyManiFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "MANI path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacyManiFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacyManiFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                SerializedBytes);

            var result = new LegacyManiFile();

            result.Version =
                reader.ReadInt32();

            result.Unknown1 =
                reader.ReadInt32();

            result.UnknownVector1 =
                LegacyFormatPrimitives
                    .ReadVector3(reader);

            result.Unknown2 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown3 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown4 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown5 =
                reader.ReadInt32();

            result.Unknown6 =
                reader.ReadInt32();

            result.UnknownVector2 =
                LegacyFormatPrimitives
                    .ReadVector3(reader);

            result.Unknown7 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown8 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.EnableRotation =
                reader.ReadInt32();

            if (result.EnableRotation != 0 &&
                result.EnableRotation != 1)
            {
                throw new InvalidDataException(
                    "MANI EnableRotation is not 0/1: " +
                    result.EnableRotation + ".");
            }

            result.RotationAxis =
                LegacyFormatPrimitives
                    .ReadVector3(reader);

            result.AnimationSpeed =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.UnknownShort1 =
                reader.ReadInt16();

            result.UnknownShort2 =
                reader.ReadInt16();

            result.UnknownVector4 =
                LegacyFormatPrimitives
                    .ReadVector3(reader);

            result.Unknown11 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown12 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(reader);

            result.Unknown13 =
                reader.ReadInt32();

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "MANI");

            return result;
        }
    }
}
