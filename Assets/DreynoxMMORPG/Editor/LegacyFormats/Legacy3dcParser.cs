using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum Legacy3dcLayout
    {
        Standard,
        Skeletonless,
        TexturePrefixed
    }

    public sealed class Legacy3dcFile
    {
        public int Version { get; internal set; }
        public Legacy3dcLayout Layout { get; internal set; }
        public string EmbeddedTextureName { get; internal set; } = string.Empty;

        public bool IsEp6 => Version == 444;
        public bool HasEmbeddedSkeleton => InverseBindMatrices.Count > 0;

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
        private const int MaxEmbeddedTextureNameBytes = 260;

        public static Legacy3dcFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("3DC path is required.", nameof(path));

            byte[] bytes = File.ReadAllBytes(path);
            return Parse(bytes);
        }

        public static Legacy3dcFile Parse(byte[] bytes)
        {
            IReadOnlyList<Legacy3dcFile> sections = ParseMany(bytes);

            if (sections.Count != 1)
                throw new InvalidDataException(
                    "3DC contains " + sections.Count +
                    " concatenated sections. Use ParseMany for this resource.");

            return sections[0];
        }

        public static IReadOnlyList<Legacy3dcFile> ParseMany(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("3DC path is required.", nameof(path));

            return ParseMany(File.ReadAllBytes(path));
        }

        public static IReadOnlyList<Legacy3dcFile> ParseMany(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var sections = new List<Legacy3dcFile>();

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    if (OnlyZeroPaddingRemains(reader))
                        break;

                    long sectionStart = reader.BaseStream.Position;
                    sections.Add(ParseSection(reader, sectionStart));

                    if (reader.BaseStream.Position <= sectionStart)
                        throw new InvalidDataException(
                            "3DC parser made no forward progress.");
                }
            }

            if (sections.Count == 0)
                throw new InvalidDataException("3DC contains no mesh section.");

            return sections;
        }

        private static Legacy3dcFile ParseSection(
            BinaryReader reader,
            long sectionStart)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 4);

            int marker = reader.ReadInt32();
            reader.BaseStream.Position = sectionStart;

            Exception standardFailure = null;

            if (marker == 0 || marker == 444)
            {
                try
                {
                    return ReadStandard(reader);
                }
                catch (Exception ex)
                    when (ex is InvalidDataException ||
                          ex is EndOfStreamException)
                {
                    standardFailure = ex;
                    reader.BaseStream.Position = sectionStart;
                }

                if (marker == 0)
                {
                    try
                    {
                        return ReadSkeletonless(reader);
                    }
                    catch (Exception ex)
                        when (ex is InvalidDataException ||
                              ex is EndOfStreamException)
                    {
                        reader.BaseStream.Position = sectionStart;
                        throw new InvalidDataException(
                            "3DC section is neither standard nor " +
                            "skeletonless.",
                            standardFailure ?? ex);
                    }
                }
            }

            if (marker > 0 &&
                marker <= MaxEmbeddedTextureNameBytes)
            {
                try
                {
                    return ReadTexturePrefixed(reader);
                }
                catch (Exception ex)
                    when (ex is InvalidDataException ||
                          ex is EndOfStreamException)
                {
                    reader.BaseStream.Position = sectionStart;
                    throw new InvalidDataException(
                        "Unsupported texture-prefixed 3DC section.",
                        ex);
                }
            }

            throw new InvalidDataException(
                "Unsupported 3DC section marker " + marker + ".");
        }

        private static Legacy3dcFile ReadStandard(BinaryReader reader)
        {
            int version = reader.ReadInt32();

            if (version != 0 && version != 444)
                throw new InvalidDataException(
                    "Unsupported standard 3DC version " + version + ".");

            var result = new Legacy3dcFile
            {
                Version = version,
                Layout = Legacy3dcLayout.Standard
            };

            int boneCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "3DC bone",
                MaxBones);

            ReadBones(reader, result, boneCount);

            int vertexCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "3DC vertex",
                MaxVertices);

            ReadVertices(
                reader,
                result,
                vertexCount,
                boneCount,
                validateBoneReferences: true);

            ReadFaces(reader, result, vertexCount);
            return result;
        }

        private static Legacy3dcFile ReadSkeletonless(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version != 0)
                throw new InvalidDataException(
                    "Skeletonless 3DC requires version marker 0.");

            var result = new Legacy3dcFile
            {
                Version = 0,
                Layout = Legacy3dcLayout.Skeletonless
            };

            int vertexCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "skeletonless 3DC vertex",
                MaxVertices);

            ReadVertices(
                reader,
                result,
                vertexCount,
                0,
                validateBoneReferences: false);

            ReadFaces(reader, result, vertexCount);
            return result;
        }

        private static Legacy3dcFile ReadTexturePrefixed(BinaryReader reader)
        {
            int stringBytes = reader.ReadInt32();

            if (stringBytes <= 0 ||
                stringBytes > MaxEmbeddedTextureNameBytes)
            {
                throw new InvalidDataException(
                    "Invalid embedded 3DC texture name length " +
                    stringBytes + ".");
            }

            LegacyFormatPrimitives.EnsureRemaining(reader, stringBytes + 4L);

            string textureName =
                Encoding.ASCII
                    .GetString(reader.ReadBytes(stringBytes))
                    .TrimEnd(' ');

            if (string.IsNullOrWhiteSpace(textureName))
                throw new InvalidDataException(
                    "Texture-prefixed 3DC has an empty texture name.");

            var result = new Legacy3dcFile
            {
                Version = 0,
                Layout = Legacy3dcLayout.TexturePrefixed,
                EmbeddedTextureName = textureName
            };

            int boneCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "texture-prefixed 3DC bone",
                MaxBones);

            ReadBones(reader, result, boneCount);

            int vertexCount = LegacyFormatPrimitives.ReadCount(
                reader,
                "texture-prefixed 3DC vertex",
                MaxVertices);

            ReadVertices(
                reader,
                result,
                vertexCount,
                boneCount,
                validateBoneReferences: true);

            ReadFaces(reader, result, vertexCount);
            return result;
        }

        private static void ReadBones(
            BinaryReader reader,
            Legacy3dcFile result,
            int boneCount)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)boneCount * 64L + 4L);

            for (int i = 0; i < boneCount; i++)
            {
                result.InverseBindMatrices.Add(
                    LegacyFormatPrimitives.ReadMatrix4x4(reader));
            }
        }

        private static void ReadVertices(
            BinaryReader reader,
            Legacy3dcFile result,
            int vertexCount,
            int boneCount,
            bool validateBoneReferences)
        {
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

                ValidateVertex(
                    vertex,
                    boneCount,
                    result.IsEp6,
                    validateBoneReferences,
                    i);

                result.Vertices.Add(vertex);
            }
        }

        private static void ReadFaces(
            BinaryReader reader,
            Legacy3dcFile result,
            int vertexCount)
        {
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
        }

        private static void ValidateVertex(
            Legacy3dcVertex vertex,
            int boneCount,
            bool ep6,
            bool validateBoneReferences,
            int ordinal)
        {
            if (vertex.Unknown != 0)
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " has unexpected marker " + vertex.Unknown + ".");

            if (validateBoneReferences &&
                (vertex.Bone1 >= boneCount ||
                 (vertex.Weight2 > 0.000001f &&
                  vertex.Bone2 >= boneCount) ||
                 (ep6 &&
                  vertex.Weight3 > 0.000001f &&
                  vertex.Bone3 >= boneCount)))
            {
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " references an invalid bone.");
            }

            float total =
                vertex.Weight1 +
                vertex.Weight2 +
                vertex.Weight3;

            if (total < 0.999f || total > 1.001f)
            {
                throw new InvalidDataException(
                    "3DC vertex " + ordinal +
                    " has invalid skin weight sum " + total + ".");
            }
        }

        private static bool OnlyZeroPaddingRemains(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            long remaining = reader.BaseStream.Length - start;

            if (remaining <= 0)
                return true;

            if (remaining > 64)
                return false;

            byte[] tail = reader.ReadBytes((int)remaining);
            reader.BaseStream.Position = start;

            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] != 0)
                    return false;
            }

            reader.BaseStream.Position = reader.BaseStream.Length;
            return true;
        }
    }
}
