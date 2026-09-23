using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public struct LegacySmodVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int BoneId;
        public Vector2 UV;
    }

    public sealed class LegacySmodMesh
    {
        public string TextureName;
        public List<LegacySmodVertex> Vertices { get; } =
            new List<LegacySmodVertex>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
    }

    public sealed class LegacySmodCollisionMesh
    {
        public List<Vector3> Vertices { get; } =
            new List<Vector3>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
    }

    public sealed class LegacySmodFile
    {
        public Vector3 Center;
        public float DistanceToCenter;
        public LegacyBounds ViewBox;
        public List<LegacySmodMesh> Meshes { get; } =
            new List<LegacySmodMesh>();
        public LegacyBounds CollisionBox;
        public List<LegacySmodCollisionMesh> CollisionMeshes { get; } =
            new List<LegacySmodCollisionMesh>();
    }

    public static class LegacySmodParser
    {
        private const int MaxMeshes = 10000;
        private const int MaxVertices = 5000000;
        private const int MaxFaces = 5000000;
        private const int MaxTextureNameBytes = 4096;

        public static LegacySmodFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "SMOD path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacySmodFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacySmodFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                12L + 4L + 24L + 4L);

            var result = new LegacySmodFile
            {
                Center =
                    LegacyFormatPrimitives.ReadVector3(reader),
                DistanceToCenter =
                    LegacyFormatPrimitives.ReadFiniteSingle(reader),
                ViewBox =
                    ReadBounds(reader)
            };

            int meshCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD textured mesh",
                    MaxMeshes);

            for (int i = 0; i < meshCount; i++)
                result.Meshes.Add(ReadMesh(reader, i));

            result.CollisionBox =
                ReadBounds(reader);

            int collisionCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD collision mesh",
                    MaxMeshes);

            for (int i = 0; i < collisionCount; i++)
            {
                result.CollisionMeshes.Add(
                    ReadCollisionMesh(reader, i));
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "SMOD");

            return result;
        }

        private static LegacySmodMesh ReadMesh(
            BinaryReader reader,
            int ordinal)
        {
            var mesh = new LegacySmodMesh
            {
                TextureName = ReadLengthPrefixedAscii(reader)
            };

            if (string.IsNullOrWhiteSpace(mesh.TextureName))
            {
                throw new InvalidDataException(
                    "SMOD mesh " + ordinal +
                    " has an empty texture name.");
            }

            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD vertex",
                    MaxVertices);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)vertexCount * 36L + 4L);

            for (int i = 0; i < vertexCount; i++)
            {
                LegacySmodVertex vertex =
                    new LegacySmodVertex
                    {
                        Position =
                            LegacyFormatPrimitives.ReadVector3(reader),
                        Normal =
                            LegacyFormatPrimitives.ReadVector3(reader),
                        BoneId =
                            reader.ReadInt32(),
                        UV =
                            LegacyFormatPrimitives.ReadVector2(reader)
                    };

                mesh.Vertices.Add(vertex);
            }

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD face",
                    MaxFaces);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)faceCount * 6L);

            for (int i = 0; i < faceCount; i++)
            {
                LegacyTriangle face =
                    new LegacyTriangle
                    {
                        A = reader.ReadUInt16(),
                        B = reader.ReadUInt16(),
                        C = reader.ReadUInt16()
                    };

                ValidateFace(
                    face,
                    vertexCount,
                    "SMOD mesh " + ordinal,
                    i);

                mesh.Faces.Add(face);
            }

            return mesh;
        }

        private static LegacySmodCollisionMesh ReadCollisionMesh(
            BinaryReader reader,
            int ordinal)
        {
            var mesh =
                new LegacySmodCollisionMesh();

            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD collision vertex",
                    MaxVertices);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)vertexCount * 12L + 4L);

            for (int i = 0; i < vertexCount; i++)
            {
                mesh.Vertices.Add(
                    LegacyFormatPrimitives.ReadVector3(reader));
            }

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD collision face",
                    MaxFaces);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)faceCount * 6L);

            for (int i = 0; i < faceCount; i++)
            {
                LegacyTriangle face =
                    new LegacyTriangle
                    {
                        A = reader.ReadUInt16(),
                        B = reader.ReadUInt16(),
                        C = reader.ReadUInt16()
                    };

                ValidateFace(
                    face,
                    vertexCount,
                    "SMOD collision mesh " + ordinal,
                    i);

                mesh.Faces.Add(face);
            }

            return mesh;
        }

        private static string ReadLengthPrefixedAscii(
            BinaryReader reader)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SMOD texture string byte",
                    MaxTextureNameBytes);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            byte[] bytes =
                reader.ReadBytes(length);

            int nullIndex =
                Array.IndexOf(bytes, (byte)0);

            int decodedLength =
                nullIndex >= 0
                    ? nullIndex
                    : bytes.Length;

            return Encoding.ASCII
                .GetString(
                    bytes,
                    0,
                    decodedLength)
                .Trim();
        }

        private static LegacyBounds ReadBounds(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                24L);

            return new LegacyBounds
            {
                Lower =
                    LegacyFormatPrimitives.ReadVector3(reader),
                Upper =
                    LegacyFormatPrimitives.ReadVector3(reader)
            };
        }

        private static void ValidateFace(
            LegacyTriangle face,
            int vertexCount,
            string label,
            int ordinal)
        {
            if (face.A >= vertexCount ||
                face.B >= vertexCount ||
                face.C >= vertexCount)
            {
                throw new InvalidDataException(
                    label + " face " + ordinal +
                    " references a vertex outside the mesh.");
            }
        }
    }
}
