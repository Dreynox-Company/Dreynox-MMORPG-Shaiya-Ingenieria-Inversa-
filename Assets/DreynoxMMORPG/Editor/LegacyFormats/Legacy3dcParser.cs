using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class Legacy3dcFile
    {
        public int Version { get; internal set; }
        public bool IsEp6 => Version == 444;
        public List<Matrix4x4> InverseBindMatrices { get; } =
            new List<Matrix4x4>();
        public List<Legacy3dcVertex> Vertices { get; } =
            new List<Legacy3dcVertex>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
    }

    public struct Legacy3dcVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public float Weight1;
        public float Weight2;
        public float Weight3;
        public byte Bone1;
        public byte Bone2;
        public byte Bone3;
        public byte Unknown;
    }

    public struct LegacyTriangle
    {
        public ushort A;
        public ushort B;
        public ushort C;
    }

    public static class Legacy3dcParser
    {
        private const int MaxBones = 1024;
        private const int MaxVertices = 5_000_000;
        private const int MaxFaces = 5_000_000;

        public static Legacy3dcFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("3DC path is required.", nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static Legacy3dcFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static Legacy3dcFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 8);

            var result = new Legacy3dcFile
            {
                Version = reader.ReadInt32()
            };

            if (result.Version != 0 && result.Version != 444)
                throw new InvalidDataException(
                    "Unsupported 3DC version " + result.Version +
                    ". Canonical ps0032 uses 0 or 444.");

            int boneCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "3DC bone",
                MaxBones);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)boneCount * 64L);

            for (int i = 0; i < boneCount; i++)
                result.InverseBindMatrices.Add(
                    LegacyFormatPrimitives.ReadMatrix4x4(reader));

            int vertexCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "3DC vertex",
                MaxVertices);

            int vertexStride = result.IsEp6 ? 48 : 40;
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)vertexCount * vertexStride + 4L);

            for (int i = 0; i < vertexCount; i++)
            {
                Legacy3dcVertex vertex = new Legacy3dcVertex
                {
                    Position = LegacyFormatPrimitives.ReadVector3(reader),
                    Weight1 = LegacyFormatPrimitives.ReadFiniteSingle(reader)
                };

                if (result.IsEp6)
                {
                    vertex.Weight2 =
                        LegacyFormatPrimitives.ReadFiniteSingle(reader);
                    vertex.Weight3 =
                        LegacyFormatPrimitives.ReadFiniteSingle(reader);
                }
                else
                {
                    vertex.Weight2 = 1f - vertex.Weight1;
                    vertex.Weight3 = 0f;
                }

                LegacyFormatPrimitives.EnsureRemaining(reader, 4);
                vertex.Bone1 = reader.ReadByte();
                vertex.Bone2 = reader.ReadByte();
                vertex.Bone3 = reader.ReadByte();
                vertex.Unknown = reader.ReadByte();

                vertex.Normal =
                    LegacyFormatPrimitives.ReadVector3(reader);
                vertex.UV =
                    LegacyFormatPrimitives.ReadVector2(reader);

                ValidateVertex(vertex, boneCount, result.IsEp6, i);
                result.Vertices.Add(vertex);
            }

            int faceCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "3DC face",
                MaxFaces);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)faceCount * 6L);

            for (int i = 0; i < faceCount; i++)
            {
                LegacyTriangle face = new LegacyTriangle
                {
                    A = reader.ReadUInt16(),
                    B = reader.ReadUInt16(),
                    C = reader.ReadUInt16()
                };

                if (face.A >= vertexCount ||
                    face.B >= vertexCount ||
                    face.C >= vertexCount)
                {
                    throw new InvalidDataException(
                        "3DC face " + i +
                        " references a vertex outside the mesh.");
                }

                result.Faces.Add(face);
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(reader, "3DC");
            return result;
        }

        private static void ValidateVertex(
            Legacy3dcVertex vertex,
            int boneCount,
            bool ep6,
            int ordinal)
        {
            if (vertex.Unknown != 0)
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " has unexpected marker " + vertex.Unknown + ".");

            if (vertex.Bone1 >= boneCount ||
                (vertex.Weight2 > 0.000001f && vertex.Bone2 >= boneCount) ||
                (ep6 && vertex.Weight3 > 0.000001f && vertex.Bone3 >= boneCount))
            {
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " references an invalid bone.");
            }

            float total = vertex.Weight1 + vertex.Weight2 + vertex.Weight3;
            if (total < 0.999f || total > 1.001f)
            {
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " has invalid skin weight sum " + total + ".");
            }
        }
    }
}
