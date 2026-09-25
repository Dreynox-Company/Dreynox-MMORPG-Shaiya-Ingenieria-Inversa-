using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>Authored transform in original coordinates. Slots beyond 15 in pandaIT2 are deliberately unnamed.</summary>
    public readonly struct LegacyItemBoneTransform
    {
        public readonly int Bone;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public LegacyItemBoneTransform(int bone, Vector3 position, Quaternion rotation)
        { Bone = bone; Position = position; Rotation = rotation; }
    }
    public sealed class LegacyItemVisualRecord
    {
        public int MeshIndex, TextureIndex, BlendMode, Unknown1, RecordFormat, Unknown2, Unknown3;
        public uint Rgba;
        public float Rotation, Scale;
        public LegacyItemBoneTransform[] Primary, Secondary;
    }
    public sealed class LegacyItemVisualFile
    {
        public string Signature;
        public int ArchetypeCount;
        public readonly List<string> MeshNames = new List<string>();
        public readonly List<string> TextureNames = new List<string>();
        public readonly List<LegacyItemVisualRecord> Records = new List<LegacyItemVisualRecord>();
    }
    public readonly struct Legacy3doVertex
    {
        public readonly Vector3 Position, Normal;
        public readonly Vector2 UV;
        public Legacy3doVertex(Vector3 position, Vector3 normal, Vector2 uv)
        { Position = position; Normal = normal; UV = uv; }
    }
    public sealed class Legacy3doFile
    {
        public string EmbeddedTexture;
        public Legacy3doVertex[] Vertices;
        public LegacyTriangle[] Faces;
    }
    /// <summary>
    /// Read-only, bounded ITM/IT2/pandaIT2 and 3DO readers. Table record ordinals
    /// are visual indices, not item TypeId. These readers do not grant/equip an item.
    /// Format cross-check: matigramirez/Parsec Itm and 3do, e5cbc6d7.
    /// pandaIT2's 24 transform pairs were independently verified against the supplied file to exact EOF.
    /// </summary>
    public static class LegacyItemVisualParser
    {
        private const int MaximumBytes = 64 * 1024 * 1024;
        public static LegacyItemVisualFile ParseItm(string path) => ParseItm(ReadBounded(path));
        public static Legacy3doFile Parse3do(string path) => Parse3do(ReadBounded(path));
        public static LegacyItemVisualFile ParseItm(byte[] bytes)
        {
            ValidateBuffer(bytes);
            using (var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.ASCII))
            {
                LegacyFormatPrimitives.EnsureRemaining(reader, 3);
                string signature = Encoding.ASCII.GetString(reader.ReadBytes(3));
                int archetypes;
                if (signature == "ITM") archetypes = 0;
                else if (signature == "IT2") archetypes = 16;
                else if (signature == "pan")
                {
                    LegacyFormatPrimitives.EnsureRemaining(reader, 5);
                    signature += Encoding.ASCII.GetString(reader.ReadBytes(5));
                    if (signature != "pandaIT2") throw new InvalidDataException("Unsupported item visual signature.");
                    archetypes = 24;
                }
                else throw new InvalidDataException("Unsupported item visual signature: " + signature);
                var result = new LegacyItemVisualFile { Signature = signature, ArchetypeCount = archetypes };
                ReadNames(reader, result.MeshNames, ".3do");
                ReadNames(reader, result.TextureNames, ".dds");
                int count = LegacyFormatPrimitives.ReadCount(reader, "Item visual record", 20000);
                LegacyFormatPrimitives.EnsureRemaining(reader, checked((long)count * (24 + archetypes * 64)));
                for (int i = 0; i < count; i++)
                {
                    LegacyFormatPrimitives.EnsureRemaining(reader, 24);
                    var record = new LegacyItemVisualRecord
                    {
                        MeshIndex = reader.ReadInt32(), TextureIndex = reader.ReadInt32(),
                        BlendMode = reader.ReadInt32(), Unknown1 = reader.ReadInt32(),
                        RecordFormat = reader.ReadInt32(), Unknown2 = reader.ReadInt32(),
                        Primary = new LegacyItemBoneTransform[archetypes], Secondary = new LegacyItemBoneTransform[archetypes]
                    };
                    if (record.MeshIndex < 0 || record.MeshIndex >= result.MeshNames.Count ||
                        record.TextureIndex < 0 || record.TextureIndex >= result.TextureNames.Count)
                        throw new InvalidDataException("Item visual record " + i + " references a missing mesh/texture name.");
                    if (record.RecordFormat != 0 && record.RecordFormat != 1)
                        throw new InvalidDataException("Unsupported item record format at ordinal " + i + ".");
                    if (record.RecordFormat == 1)
                    {
                        LegacyFormatPrimitives.EnsureRemaining(reader, 16);
                        record.Rgba = reader.ReadUInt32();
                        record.Rotation = LegacyFormatPrimitives.ReadFiniteSingle(reader);
                        record.Scale = LegacyFormatPrimitives.ReadFiniteSingle(reader);
                        record.Unknown3 = reader.ReadInt32();
                    }
                    for (int a = 0; a < archetypes; a++)
                    { record.Primary[a] = ReadTransform(reader); record.Secondary[a] = ReadTransform(reader); }
                    result.Records.Add(record);
                }
                // These authored descriptors end exactly after their records. Do not hide an unknown extension.
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("Unparsed trailing item descriptor bytes.");
                return result;
            }
        }
        public static Legacy3doFile Parse3do(byte[] bytes)
        {
            ValidateBuffer(bytes);
            using (var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.ASCII))
            {
                string texture = ReadName(reader, ".dds", true);
                int vertices = LegacyFormatPrimitives.ReadCount(reader, "3DO vertex", 65536);
                if (vertices == 0) throw new InvalidDataException("An item mesh must have vertices.");
                LegacyFormatPrimitives.EnsureRemaining(reader, checked((long)vertices * 32 + 4));
                var result = new Legacy3doFile { EmbeddedTexture = texture, Vertices = new Legacy3doVertex[vertices] };
                for (int i = 0; i < vertices; i++)
                    result.Vertices[i] = new Legacy3doVertex(LegacyFormatPrimitives.ReadVector3(reader),
                        LegacyFormatPrimitives.ReadVector3(reader), LegacyFormatPrimitives.ReadVector2(reader));
                int faces = LegacyFormatPrimitives.ReadCount(reader, "3DO triangle", 1000000);
                if (faces == 0) throw new InvalidDataException("An item mesh must have triangles.");
                LegacyFormatPrimitives.EnsureRemaining(reader, checked((long)faces * 6));
                result.Faces = new LegacyTriangle[faces];
                for (int i = 0; i < faces; i++)
                {
                    var face = new LegacyTriangle { A = reader.ReadUInt16(), B = reader.ReadUInt16(), C = reader.ReadUInt16() };
                    if (face.A >= vertices || face.B >= vertices || face.C >= vertices)
                        throw new InvalidDataException("3DO triangle " + i + " references a missing vertex.");
                    result.Faces[i] = face;
                }
                // Supplied 01001.3do has eight zero trailer bytes. Nonzero extensions and excessive tails stay explicit.
                if (reader.BaseStream.Length - reader.BaseStream.Position > 16)
                    throw new InvalidDataException("Unsupported 3DO trailer.");
                LegacyFormatPrimitives.EnsureFullyConsumed(reader, "3DO");
                return result;
            }
        }
        private static LegacyItemBoneTransform ReadTransform(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 32);
            int bone = reader.ReadInt32();
            if (bone < 0 || bone > 4095) throw new InvalidDataException("Item bone index outside supported bounds.");
            Vector3 position = LegacyFormatPrimitives.ReadVector3(reader);
            // Preserve authored components, including tiny normalization differences. Normalize only at application time.
            var rotation = new Quaternion(LegacyFormatPrimitives.ReadFiniteSingle(reader), LegacyFormatPrimitives.ReadFiniteSingle(reader),
                LegacyFormatPrimitives.ReadFiniteSingle(reader), LegacyFormatPrimitives.ReadFiniteSingle(reader));
            double norm = (double)rotation.x * rotation.x + (double)rotation.y * rotation.y +
                (double)rotation.z * rotation.z + (double)rotation.w * rotation.w;
            if (norm < 0.000000000001 || norm > 1000000) throw new InvalidDataException("Invalid authored item rotation.");
            return new LegacyItemBoneTransform(bone, position, rotation);
        }
        private static void ReadNames(BinaryReader reader, ICollection<string> target, string extension)
        {
            int count = LegacyFormatPrimitives.ReadCount(reader, "Item filename", 20000);
            for (int i = 0; i < count; i++) target.Add(ReadName(reader, extension, false));
        }
        private static string ReadName(BinaryReader reader, string extension, bool allowEmpty)
        {
            int size = LegacyFormatPrimitives.ReadCount(reader, "Item filename byte", 256);
            if (size == 0 && allowEmpty) return string.Empty;
            if (size == 0) throw new InvalidDataException("Empty item filename.");
            LegacyFormatPrimitives.EnsureRemaining(reader, size);
            byte[] data = reader.ReadBytes(size);
            int length = data[size - 1] == 0 ? size - 1 : size;
            for (int i = 0; i < length; i++)
                if (data[i] < 32 || data[i] > 126) throw new InvalidDataException("Unsupported item filename encoding.");
            string name = Encoding.ASCII.GetString(data, 0, length);
            if (name.Length == 0 || name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0 ||
                !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || name == "." || name == "..")
                throw new InvalidDataException("Unsafe or unexpected item filename: " + name);
            return name;
        }
        private static byte[] ReadBounded(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Item source path required.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumBytes) throw new InvalidDataException("Item resource exceeds its read budget.");
                using (var reader = new BinaryReader(stream))
                {
                    byte[] bytes = reader.ReadBytes(checked((int)stream.Length));
                    if (bytes.Length != stream.Length) throw new EndOfStreamException("Truncated item source.");
                    return bytes;
                }
            }
        }
        private static void ValidateBuffer(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Item resource exceeds its read budget.");
        }
    }
}
