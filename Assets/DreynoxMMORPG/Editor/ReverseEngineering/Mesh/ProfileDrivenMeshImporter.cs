using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Meshes
{
    public static class ProfileDrivenMeshImporter
    {
        public static Mesh Import(string sourcePath, LegacyMeshLayoutProfile profile, string outputAssetPath)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!File.Exists(sourcePath)) throw new FileNotFoundException(sourcePath);
            using (FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int vertexCount = profile.ResolveVertexCount(reader);
                if (vertexCount <= 0 || vertexCount > 20_000_000) throw new InvalidDataException("Conteo de vértices inválido: " + vertexCount);
                long vertexEnd = checked(profile.vertexDataOffset + (long)vertexCount * profile.vertexStride);
                if (profile.vertexDataOffset < 0 || profile.vertexStride < 12 || vertexEnd > stream.Length)
                    throw new EndOfStreamException("Layout de vértices fuera del archivo.");

                Vector3[] vertices = new Vector3[vertexCount];
                Vector2[] uv = profile.hasUv ? new Vector2[vertexCount] : null;
                for (int i = 0; i < vertexCount; i++)
                {
                    long baseOffset = profile.vertexDataOffset + (long)i * profile.vertexStride;
                    float x = ReadFloat(reader, baseOffset + profile.positionXOffset);
                    float y = ReadFloat(reader, baseOffset + profile.positionYOffset);
                    float z = ReadFloat(reader, baseOffset + profile.positionZOffset);
                    if (profile.swapYAndZ) { float tmp = y; y = z; z = tmp; }
                    if (profile.negateX) x = -x;
                    if (profile.negateZ) z = -z;
                    vertices[i] = new Vector3(x, y, z) * profile.positionScale;
                    if (uv != null)
                    {
                        float u = ReadFloat(reader, baseOffset + profile.uvUOffset);
                        float v = ReadFloat(reader, baseOffset + profile.uvVOffset);
                        uv[i] = new Vector2(u, profile.flipV ? 1f - v : v);
                    }
                }

                int[] triangles;
                if (profile.indexed)
                {
                    int indexCount = profile.ResolveIndexCount(reader);
                    if (indexCount <= 0 || indexCount % 3 != 0 || indexCount > 60_000_000) throw new InvalidDataException("Conteo de índices inválido: " + indexCount);
                    int indexSize = profile.indicesAre32Bit ? 4 : 2;
                    long indexEnd = checked(profile.indexDataOffset + (long)indexCount * indexSize);
                    if (profile.indexDataOffset < 0 || indexEnd > stream.Length) throw new EndOfStreamException("Layout de índices fuera del archivo.");
                    triangles = new int[indexCount];
                    reader.BaseStream.Position = profile.indexDataOffset;
                    for (int i = 0; i < indexCount; i++)
                    {
                        int idx = profile.indicesAre32Bit ? reader.ReadInt32() : reader.ReadUInt16();
                        if (idx < 0 || idx >= vertexCount) throw new InvalidDataException($"Índice {idx} fuera de 0..{vertexCount - 1}.");
                        triangles[i] = idx;
                    }
                }
                else
                {
                    int usable = vertexCount - (vertexCount % 3);
                    triangles = new int[usable];
                    for (int i = 0; i < usable; i++) triangles[i] = i;
                }

                Mesh mesh = new Mesh { name = Path.GetFileNameWithoutExtension(sourcePath) };
                if (vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                if (uv != null) mesh.uv = uv;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();

                string dir = Path.GetDirectoryName(outputAssetPath)?.Replace('\\', '/');
                EnsureFolder(dir);
                AssetDatabase.CreateAsset(mesh, outputAssetPath);
                AssetDatabase.SaveAssets();
                return mesh;
            }
        }

        private static float ReadFloat(BinaryReader reader, long offset)
        {
            if (offset < 0 || offset + 4 > reader.BaseStream.Length) throw new EndOfStreamException("Float fuera del archivo en offset " + offset);
            reader.BaseStream.Position = offset;
            return reader.ReadSingle();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
