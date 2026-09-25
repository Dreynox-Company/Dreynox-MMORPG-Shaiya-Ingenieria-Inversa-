using System;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    internal static class LegacyFormatPrimitives
    {
        public static Matrix4x4 ReadMatrix4x4(BinaryReader reader)
        {
            EnsureRemaining(reader, 64);

            Matrix4x4 value = new Matrix4x4();

            // Shaiya serializes matrices column by column.
            value.m00 = ReadFiniteSingle(reader);
            value.m10 = ReadFiniteSingle(reader);
            value.m20 = ReadFiniteSingle(reader);
            value.m30 = ReadFiniteSingle(reader);

            value.m01 = ReadFiniteSingle(reader);
            value.m11 = ReadFiniteSingle(reader);
            value.m21 = ReadFiniteSingle(reader);
            value.m31 = ReadFiniteSingle(reader);

            value.m02 = ReadFiniteSingle(reader);
            value.m12 = ReadFiniteSingle(reader);
            value.m22 = ReadFiniteSingle(reader);
            value.m32 = ReadFiniteSingle(reader);

            value.m03 = ReadFiniteSingle(reader);
            value.m13 = ReadFiniteSingle(reader);
            value.m23 = ReadFiniteSingle(reader);
            value.m33 = ReadFiniteSingle(reader);

            return value;
        }

        public static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader));
        }

        public static Vector2 ReadVector2(BinaryReader reader)
        {
            return new Vector2(
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader));
        }

        public static Quaternion ReadQuaternion(BinaryReader reader)
        {
            Quaternion value = new Quaternion(
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader),
                ReadFiniteSingle(reader));

            float magnitude = Mathf.Sqrt(
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w);

            if (magnitude <= 0.000001f)
                throw new InvalidDataException("Zero-length quaternion.");

            return new Quaternion(
                value.x / magnitude,
                value.y / magnitude,
                value.z / magnitude,
                value.w / magnitude);
        }

        public static int ReadCount(
            BinaryReader reader,
            string label,
            int maximum)
        {
            EnsureRemaining(reader, 4);
            int count = reader.ReadInt32();

            if (count < 0 || count > maximum)
                throw new InvalidDataException(
                    label + " count is outside the supported range: " + count + ".");

            return count;
        }

        public static float ReadFiniteSingle(BinaryReader reader)
        {
            EnsureRemaining(reader, 4);
            float value = reader.ReadSingle();

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("Non-finite floating-point value.");

            return value;
        }

        public static void EnsureRemaining(BinaryReader reader, long bytes)
        {
            long remaining =
                reader.BaseStream.Length - reader.BaseStream.Position;

            if (bytes < 0 || remaining < bytes)
                throw new EndOfStreamException(
                    "Legacy resource ended unexpectedly. Required " +
                    bytes + " bytes, remaining " + remaining + ".");
        }

        public static void EnsureFullyConsumed(
            BinaryReader reader,
            string label)
        {
            long remaining =
                reader.BaseStream.Length - reader.BaseStream.Position;

            if (remaining == 0)
                return;

            byte[] tail = reader.ReadBytes((int)remaining);
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] != 0)
                    throw new InvalidDataException(
                        label + " contains " + remaining +
                        " unparsed non-zero trailing bytes.");
            }
        }
    }

    internal static class LegacyCoordinateBridge
    {
        private static readonly Matrix4x4 Reflection =
            Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

        public static Vector3 Position(Vector3 value)
        {
            return new Vector3(value.x, value.y, -value.z);
        }

        public static Vector3 Direction(Vector3 value)
        {
            return new Vector3(value.x, value.y, -value.z);
        }

        public static Quaternion Rotation(Quaternion value)
        {
            // Basis change R' = S * R * S for S = diag(1,1,-1).
            return new Quaternion(
                -value.x,
                -value.y,
                value.z,
                value.w);
        }

        public static Matrix4x4 Matrix(Matrix4x4 value)
        {
            return Reflection * value * Reflection;
        }
    }
}
