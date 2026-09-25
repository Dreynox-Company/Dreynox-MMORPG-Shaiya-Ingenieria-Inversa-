using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public struct LegacySmodVertex
    {
        public Vector3 Position, Normal;
        public int BoneId;
        public Vector2 UV;
    }
    public sealed class LegacySmodMesh
    {
        public string TextureName;
        public List<LegacySmodVertex> Vertices { get; } = new List<LegacySmodVertex>();
        public List<LegacyTriangle> Faces { get; } = new List<LegacyTriangle>();
        public int ReconstructedNormals { get; internal set; }
        public int InactiveNormalDefaults { get; internal set; }
        public int UnreferencedUvDefaults { get; internal set; }
    }
    public sealed class LegacySmodCollisionMesh
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<LegacyTriangle> Faces { get; } = new List<LegacyTriangle>();
    }
    public sealed class LegacySmodFile
    {
        public Vector3 Center;
        public float DistanceToCenter;
        public LegacyBounds ViewBox, CollisionBox;
        public List<LegacySmodMesh> Meshes { get; } = new List<LegacySmodMesh>();
        public List<LegacySmodCollisionMesh> CollisionMeshes { get; } = new List<LegacySmodCollisionMesh>();
    }
    public static class LegacySmodParser
    {
        private const int MaxMeshes = 10000, MaxVertices = 5000000, MaxFaces = 5000000;
        public static LegacySmodFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("SMOD path is required.", nameof(path));
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream)) return Parse(reader);
            }
            catch (InvalidDataException ex) { throw new InvalidDataException("SMOD '" + path + "': " + ex.Message, ex); }
        }
        public static LegacySmodFile Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream)) return Parse(reader);
        }
        private static LegacySmodFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 44);
            var result = new LegacySmodFile
            {
                Center = LegacyFormatPrimitives.ReadVector3(reader),
                DistanceToCenter = LegacyFormatPrimitives.ReadFiniteSingle(reader),
                ViewBox = Bounds(reader)
            };
            int count = Count(reader, "textured mesh", MaxMeshes);
            for (int i = 0; i < count; i++) result.Meshes.Add(ReadMesh(reader, i));
            result.CollisionBox = Bounds(reader);
            count = Count(reader, "collision mesh", MaxMeshes);
            for (int i = 0; i < count; i++) result.CollisionMeshes.Add(ReadCollision(reader, i));
            LegacyFormatPrimitives.EnsureFullyConsumed(reader, "SMOD");
            return result;
        }
        private static LegacySmodMesh ReadMesh(BinaryReader reader, int ordinal)
        {
            var mesh = new LegacySmodMesh { TextureName = ReadString(reader) };
            int count = Count(reader, "vertex", MaxVertices);
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * 36 + 4);
            for (int i = 0; i < count; i++)
            {
                // Positions remain strict. Normal/UV validity needs face topology:
                // the canonical corpus contains NaN normals and unused UV slots.
                mesh.Vertices.Add(new LegacySmodVertex
                {
                    Position = LegacyFormatPrimitives.ReadVector3(reader),
                    Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    BoneId = reader.ReadInt32(),
                    UV = new Vector2(reader.ReadSingle(), reader.ReadSingle())
                });
            }
            ReadFaces(reader, mesh.Faces, count, "mesh " + ordinal);
            RepairDerivedAttributes(mesh, ordinal);
            return mesh;
        }
        private static void RepairDerivedAttributes(LegacySmodMesh mesh, int ordinal)
        {
            int count = mesh.Vertices.Count;
            bool needsRepair = false;
            for (int i = 0; i < count; i++)
                needsRepair |= !Finite(mesh.Vertices[i].Normal) || !Finite(mesh.Vertices[i].UV);
            if (!needsRepair) return;

            var used = new bool[count];
            var sums = new Vector3[count];
            var strongest = new Vector3[count];
            foreach (LegacyTriangle face in mesh.Faces)
            {
                int a = face.A, b = face.B, c = face.C;
                used[a] = used[b] = used[c] = true;
                Vector3 normal = Vector3.Cross(mesh.Vertices[b].Position - mesh.Vertices[a].Position,
                    mesh.Vertices[c].Position - mesh.Vertices[a].Position);
                if (!Finite(normal) || !Finite(normal.sqrMagnitude))
                    throw new InvalidDataException("SMOD face geometry overflow in mesh " + ordinal + ".");
                Accumulate(a, normal, sums, strongest);
                Accumulate(b, normal, sums, strongest);
                Accumulate(c, normal, sums, strongest);
            }
            for (int i = 0; i < count; i++)
            {
                LegacySmodVertex vertex = mesh.Vertices[i];
                if (!Finite(vertex.UV))
                {
                    if (used[i]) throw new InvalidDataException("SMOD mesh " + ordinal + " has non-finite UV on referenced vertex " + i + ".");
                    vertex.UV = Vector2.zero;
                    mesh.UnreferencedUvDefaults++;
                }
                if (!Finite(vertex.Normal))
                {
                    if (!Finite(sums[i]) || !Finite(sums[i].sqrMagnitude))
                        throw new InvalidDataException("SMOD normal accumulation overflow.");
                    Vector3 derived = sums[i].sqrMagnitude > 1e-20f ? sums[i] : strongest[i];
                    if (derived.sqrMagnitude > 1e-20f)
                    {
                        // Derive only invalid normals; preserve all valid authored normals.
                        vertex.Normal = derived / Mathf.Sqrt(derived.sqrMagnitude);
                        mesh.ReconstructedNormals++;
                    }
                    else
                    {
                        // Unreferenced vertices / zero-area triangles do not render.
                        vertex.Normal = Vector3.up;
                        mesh.InactiveNormalDefaults++;
                    }
                }
                mesh.Vertices[i] = vertex;
            }
        }
        private static void Accumulate(int i, Vector3 normal, Vector3[] sums, Vector3[] strongest)
        {
            sums[i] += normal;
            if (normal.sqrMagnitude > strongest[i].sqrMagnitude) strongest[i] = normal;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
        private static bool Finite(Vector2 v) { return Finite(v.x) && Finite(v.y); }
        private static LegacySmodCollisionMesh ReadCollision(BinaryReader reader, int ordinal)
        {
            var mesh = new LegacySmodCollisionMesh();
            int count = Count(reader, "collision vertex", MaxVertices);
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * 12 + 4);
            for (int i = 0; i < count; i++) mesh.Vertices.Add(LegacyFormatPrimitives.ReadVector3(reader));
            ReadFaces(reader, mesh.Faces, count, "collision " + ordinal);
            return mesh;
        }
        private static void ReadFaces(BinaryReader reader, List<LegacyTriangle> output, int vertices, string label)
        {
            int count = Count(reader, "face", MaxFaces);
            LegacyFormatPrimitives.EnsureRemaining(reader, (long)count * 6);
            for (int i = 0; i < count; i++)
            {
                var face = new LegacyTriangle { A = reader.ReadUInt16(), B = reader.ReadUInt16(), C = reader.ReadUInt16() };
                if (face.A >= vertices || face.B >= vertices || face.C >= vertices)
                    throw new InvalidDataException("SMOD " + label + " face " + i + " references a missing vertex.");
                output.Add(face);
            }
        }
        private static int Count(BinaryReader reader, string label, int max)
        { return LegacyFormatPrimitives.ReadCount(reader, "SMOD " + label, max); }
        private static string ReadString(BinaryReader reader)
        {
            int length = Count(reader, "texture name bytes", 4096);
            LegacyFormatPrimitives.EnsureRemaining(reader, length);
            byte[] bytes = reader.ReadBytes(length);
            int end = Array.IndexOf(bytes, (byte)0);
            return Encoding.ASCII.GetString(bytes, 0, end < 0 ? bytes.Length : end).Trim();
        }
        private static LegacyBounds Bounds(BinaryReader reader)
        {
            return new LegacyBounds { Lower = LegacyFormatPrimitives.ReadVector3(reader), Upper = LegacyFormatPrimitives.ReadVector3(reader) };
        }
    }
}
