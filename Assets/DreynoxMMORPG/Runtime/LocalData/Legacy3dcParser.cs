using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum Legacy3dcLayout { Standard, Skeletonless, TexturePrefixed }
    public sealed class Legacy3dcFile
    {
        public int Version { get; internal set; }
        public Legacy3dcLayout Layout { get; internal set; }
        public string EmbeddedTextureName { get; internal set; } = string.Empty;
        public int ReconstructedNormals { get; internal set; }
        public int InactiveNormalDefaults { get; internal set; }
        public bool IsEp6 => Version == 444;
        public bool HasEmbeddedSkeleton => InverseBindMatrices.Count > 0;
        public List<Matrix4x4> InverseBindMatrices { get; } = new List<Matrix4x4>();
        public List<Legacy3dcVertex> Vertices { get; } = new List<Legacy3dcVertex>();
        public List<LegacyTriangle> Faces { get; } = new List<LegacyTriangle>();
    }
    public struct Legacy3dcVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public float Weight1, Weight2, Weight3;
        public byte Bone1, Bone2, Bone3, Unknown;
    }
    public struct LegacyTriangle { public ushort A, B, C; }

    public static class Legacy3dcParser
    {
        private const int MaxBones = 1024;
        private const int MaxVertices = 5_000_000;
        private const int MaxFaces = 5_000_000;
        private const int MaxEmbeddedTextureNameBytes = 260;

        public static Legacy3dcFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("3DC path is required.", nameof(path));
            return Parse(File.ReadAllBytes(path));
        }
        public static Legacy3dcFile Parse(byte[] bytes)
        {
            var sections = ParseMany(bytes);
            if (sections.Count != 1) throw new InvalidDataException(
                "3DC contains " + sections.Count + " concatenated sections. Use ParseMany for this resource.");
            return sections[0];
        }
        public static IReadOnlyList<Legacy3dcFile> ParseMany(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("3DC path is required.", nameof(path));
            return ParseMany(File.ReadAllBytes(path));
        }
        /// <summary>Explicit MON compatibility: repair only invalid normals after topology validation.</summary>
        public static Legacy3dcFile ParseWithTopologyNormals(byte[] bytes)
        {
            var sections = ParseMany(bytes, true);
            if (sections.Count != 1) throw new InvalidDataException("Expected one MON mesh section.");
            return sections[0];
        }
        public static IReadOnlyList<Legacy3dcFile> ParseMany(byte[] bytes) { return ParseMany(bytes, false); }
        private static IReadOnlyList<Legacy3dcFile> ParseMany(byte[] bytes, bool repairNormals)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var sections = new List<Legacy3dcFile>();
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    if (OnlyZeroPaddingRemains(reader)) break;
                    long start = reader.BaseStream.Position;
                    Legacy3dcFile section = ParseSection(reader, start, repairNormals);
                    if (repairNormals) LegacyMeshNormalRepair.Apply(section);
                    sections.Add(section);
                    if (reader.BaseStream.Position <= start) throw new InvalidDataException("3DC parser made no forward progress.");
                }
            }
            if (sections.Count == 0) throw new InvalidDataException("3DC contains no mesh section.");
            return sections;
        }
        private static Legacy3dcFile ParseSection(BinaryReader reader, long start, bool repairNormals)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 4);
            int marker = reader.ReadInt32(); reader.BaseStream.Position = start;
            Exception standardFailure = null;
            if (marker == 0 || marker == 444)
            {
                try { return ReadStandard(reader, repairNormals); }
                catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException)
                { standardFailure = ex; reader.BaseStream.Position = start; }
                if (marker == 0)
                {
                    try { return ReadSkeletonless(reader, repairNormals); }
                    catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException)
                    {
                        reader.BaseStream.Position = start;
                        throw new InvalidDataException("3DC section is neither standard nor skeletonless.", standardFailure ?? ex);
                    }
                }
            }
            if (marker > 0 && marker <= MaxEmbeddedTextureNameBytes)
            {
                try { return ReadTexturePrefixed(reader, repairNormals); }
                catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException)
                {
                    reader.BaseStream.Position = start;
                    throw new InvalidDataException("Unsupported texture-prefixed 3DC section.", ex);
                }
            }
            throw new InvalidDataException("Unsupported 3DC section marker " + marker + ".");
        }
        private static Legacy3dcFile ReadStandard(BinaryReader reader, bool repairNormals)
        {
            int version = reader.ReadInt32();
            if (version != 0 && version != 444) throw new InvalidDataException("Unsupported standard 3DC version " + version + ".");
            var result = new Legacy3dcFile { Version = version, Layout = Legacy3dcLayout.Standard };
            int count = LegacyFormatPrimitives.ReadCount(reader, "3DC bone", MaxBones);
            ReadBones(reader, result, count);
            int vertices = LegacyFormatPrimitives.ReadCount(reader, "3DC vertex", MaxVertices);
            ReadVertices(reader, result, vertices, count, true, repairNormals);
            ReadFaces(reader, result, vertices);
            return result;
        }
        private static Legacy3dcFile ReadSkeletonless(BinaryReader reader, bool repairNormals)
        {
            if (reader.ReadInt32() != 0) throw new InvalidDataException("Skeletonless 3DC requires version marker 0.");
            var result = new Legacy3dcFile { Version = 0, Layout = Legacy3dcLayout.Skeletonless };
            int vertices = LegacyFormatPrimitives.ReadCount(reader, "skeletonless 3DC vertex", MaxVertices);
            ReadVertices(reader, result, vertices, 0, false, repairNormals);
            ReadFaces(reader, result, vertices);
            return result;
        }
        private static Legacy3dcFile ReadTexturePrefixed(BinaryReader reader, bool repairNormals)
        {
            int bytes = reader.ReadInt32();
            if (bytes <= 0 || bytes > MaxEmbeddedTextureNameBytes)
                throw new InvalidDataException("Invalid embedded 3DC texture name length " + bytes + ".");
            LegacyFormatPrimitives.EnsureRemaining(reader, bytes + 4L);
            string name = Encoding.ASCII.GetString(reader.ReadBytes(bytes)).TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Texture-prefixed 3DC has an empty texture name.");
            var result = new Legacy3dcFile { Version = 0, Layout = Legacy3dcLayout.TexturePrefixed, EmbeddedTextureName = name };
            int count = LegacyFormatPrimitives.ReadCount(reader, "texture-prefixed 3DC bone", MaxBones);
            ReadBones(reader, result, count);
            int vertices = LegacyFormatPrimitives.ReadCount(reader, "texture-prefixed 3DC vertex", MaxVertices);
            ReadVertices(reader, result, vertices, count, true, repairNormals);
            ReadFaces(reader, result, vertices);
            return result;
        }
        private static void ReadBones(BinaryReader reader, Legacy3dcFile result, int count)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * 64 + 4);
            for (int i = 0; i < count; i++) result.InverseBindMatrices.Add(LegacyFormatPrimitives.ReadMatrix4x4(reader));
        }
        private static void ReadVertices(BinaryReader reader, Legacy3dcFile result, int count,
            int boneCount, bool validateBones, bool repairNormals)
        {
            int stride = result.IsEp6 ? 48 : 40;
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * stride + 4);
            for (int i = 0; i < count; i++)
            {
                var v = new Legacy3dcVertex { Position = LegacyFormatPrimitives.ReadVector3(reader),
                    Weight1 = LegacyFormatPrimitives.ReadFiniteSingle(reader) };
                if (result.IsEp6)
                { v.Weight2 = LegacyFormatPrimitives.ReadFiniteSingle(reader); v.Weight3 = LegacyFormatPrimitives.ReadFiniteSingle(reader); }
                else { v.Weight2 = 1 - v.Weight1; v.Weight3 = 0; }
                LegacyFormatPrimitives.EnsureRemaining(reader, 4);
                v.Bone1 = reader.ReadByte(); v.Bone2 = reader.ReadByte(); v.Bone3 = reader.ReadByte(); v.Unknown = reader.ReadByte();
                v.Normal = repairNormals ? new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) :
                    LegacyFormatPrimitives.ReadVector3(reader);
                v.UV = LegacyFormatPrimitives.ReadVector2(reader);
                ValidateVertex(v, boneCount, result.IsEp6, validateBones, i);
                result.Vertices.Add(v);
            }
        }
        private static void ReadFaces(BinaryReader reader, Legacy3dcFile result, int vertexCount)
        {
            int count = LegacyFormatPrimitives.ReadCount(reader, "3DC face", MaxFaces);
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * 6);
            for (int i = 0; i < count; i++)
            {
                var face = new LegacyTriangle { A = reader.ReadUInt16(), B = reader.ReadUInt16(), C = reader.ReadUInt16() };
                if (face.A >= vertexCount || face.B >= vertexCount || face.C >= vertexCount)
                    throw new InvalidDataException("3DC face " + i + " references a vertex outside the mesh.");
                result.Faces.Add(face);
            }
        }
        private static void ValidateVertex(Legacy3dcVertex v, int boneCount, bool ep6, bool validateBones, int ordinal)
        {
            if (v.Unknown != 0) throw new InvalidDataException("3DC vertex " + ordinal + " has unexpected marker " + v.Unknown + ".");
            if (validateBones && (v.Bone1 >= boneCount || (v.Weight2 > 0.000001f && v.Bone2 >= boneCount) ||
                (ep6 && v.Weight3 > 0.000001f && v.Bone3 >= boneCount)))
                throw new InvalidDataException("3DC vertex " + ordinal + " references an invalid bone.");
            float total = v.Weight1 + v.Weight2 + v.Weight3;
            if (total < 0.999f || total > 1.001f)
                throw new InvalidDataException("3DC vertex " + ordinal + " has invalid skin weight sum " + total + ".");
        }
        private static bool OnlyZeroPaddingRemains(BinaryReader reader)
        {
            long start = reader.BaseStream.Position, remaining = reader.BaseStream.Length - start;
            if (remaining <= 0) return true;
            if (remaining > 64) return false;
            byte[] tail = reader.ReadBytes((int)remaining); reader.BaseStream.Position = start;
            foreach (byte b in tail) if (b != 0) return false;
            reader.BaseStream.Position = reader.BaseStream.Length;
            return true;
        }
    }
}
