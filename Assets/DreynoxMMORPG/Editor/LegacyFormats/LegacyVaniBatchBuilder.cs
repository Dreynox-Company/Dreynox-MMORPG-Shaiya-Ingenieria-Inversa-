using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyVaniBatchBuilder
    {
        private const float CellSize = 160f;
        private const int MaxInstancesPerBatch = 1023;
        private const float InitialDrawDistance = 950f;

        private sealed class ResourceBuildResult
        {
            public LegacyVaniFile Source;
            public LegacyVaniMeshFrames[] MeshParts;
            public Bounds LocalBounds;
        }
        private readonly struct CellKey : IEquatable<CellKey>
        {
            public readonly int X, Z;
            public CellKey(int x, int z) { X = x; Z = z; }
            public bool Equals(CellKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is CellKey other && Equals(other);
            public override int GetHashCode() { unchecked { return (X * 397) ^ Z; } }
        }

        public static LegacyWorldVaniRuntime Create(
            CanonicalClientCorpus corpus, LegacyWldNameCoordinateGroup firstGroup,
            LegacyWldNameCoordinateGroup secondGroup, Transform observer, Camera camera,
            int mapId = -1)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));
            if (firstGroup == null) throw new ArgumentNullException(nameof(firstGroup));
            if (secondGroup == null) throw new ArgumentNullException(nameof(secondGroup));
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            int selectedMap = mapId == -1 ? LegacyWorldTerrainImporter.CurrentMapId : mapId;
            if (selectedMap < 0 || selectedMap > 9999) throw new ArgumentOutOfRangeException(nameof(mapId));
            string outputRoot = "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Map" + selectedMap.ToString("D3") + "/VAni";
            EnsureFolder(outputRoot);
            // One resource can occur in both WLD groups. Rebuilding/deleting it
            // twice would invalidate the first group's material and frame references.
            var cache = new Dictionary<string, ResourceBuildResult>(StringComparer.OrdinalIgnoreCase);
            var resources = new List<LegacyVaniResourceRuntime>();
            GameObject root = null;
            try
            {
                AppendGroup(corpus, firstGroup, resources, "VAni1", outputRoot, cache);
                AppendGroup(corpus, secondGroup, resources, "VAni2", outputRoot, cache);
                LegacyAssetWriteBatch.Flush();
                root = new GameObject("WLD_VAni_GPU");
                var runtime = root.AddComponent<LegacyWorldVaniRuntime>();
                runtime.Configure(observer, camera, resources, InitialDrawDistance, false);
                LegacyAssetWriteBatch.SaveAssets();
                return runtime;
            }
            catch
            {
                LegacyAssetWriteBatch.Abort();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void AppendGroup(CanonicalClientCorpus corpus,
            LegacyWldNameCoordinateGroup group, ICollection<LegacyVaniResourceRuntime> target,
            string groupLabel, string outputRoot, Dictionary<string, ResourceBuildResult> cache)
        {
            if (group.Names.Count == 0)
            {
                if (group.Coordinates.Count != 0)
                    throw new InvalidDataException(groupLabel + " has placements but no resource names.");
                return;
            }
            var coordinatesByResource = new List<LegacyWldCoordinate>[group.Names.Count];
            for (int i = 0; i < coordinatesByResource.Length; i++)
                coordinatesByResource[i] = new List<LegacyWldCoordinate>();
            for (int i = 0; i < group.Coordinates.Count; i++)
            {
                var coordinate = group.Coordinates[i];
                if (coordinate.Id < 0 || coordinate.Id >= group.Names.Count)
                    throw new InvalidDataException(groupLabel + " placement " + i + " references invalid resource " + coordinate.Id + ".");
                coordinatesByResource[coordinate.Id].Add(coordinate);
            }
            for (int resourceIndex = 0; resourceIndex < group.Names.Count; resourceIndex++)
            {
                string resourceName = group.Names[resourceIndex];
                var placements = coordinatesByResource[resourceIndex];
                if (placements.Count == 0) continue;
                string key = Path.GetFileName(resourceName);
                if (!cache.TryGetValue(key, out ResourceBuildResult built))
                {
                    built = BuildResource(corpus, resourceName, outputRoot);
                    cache.Add(key, built);
                }
                var batches = BuildBatches(placements, built.LocalBounds);
                int logicalCount = 0;
                foreach (var batch in batches) logicalCount += batch.Count;
                if (logicalCount != placements.Count)
                    throw new InvalidDataException(groupLabel + " resource '" + resourceName + "' batch count mismatch.");
                target.Add(new LegacyVaniResourceRuntime
                {
                    resourceName = groupLabel + "/" + resourceName,
                    frameCount = built.Source.FrameCount,
                    frameIntervalMilliseconds = built.Source.Unknown1,
                    frameTimingCalibrated = false,
                    meshParts = built.MeshParts,
                    batches = batches,
                    logicalPlacementCount = logicalCount
                });
            }
        }

        private static ResourceBuildResult BuildResource(CanonicalClientCorpus corpus, string resourceName, string outputRoot)
        {
            string vaniRoot = LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, "DATA_Español/entity/vani");
            string sourcePath = CanonicalResourceIndex.FindUnique(vaniRoot, Path.GetFileName(resourceName));
            if (sourcePath == null || !File.Exists(sourcePath))
                throw new FileNotFoundException("VANI resource not found: " + resourceName, sourcePath);
            var source = LegacyVaniParser.Parse(sourcePath);
            if (source.Unknown1 <= 0 || source.Unknown1 > 10000)
                throw new InvalidDataException("VANI '" + resourceName + "' has invalid frame interval field " + source.Unknown1 + ".");
            string safeName = SanitizeAssetName(Path.GetFileNameWithoutExtension(resourceName));
            string resourceRoot = outputRoot + "/" + safeName;
            LegacyAssetWriteBatch.DeleteAsset(resourceRoot);
            EnsureFolder(resourceRoot);
            EnsureFolder(resourceRoot + "/Meshes");
            EnsureFolder(resourceRoot + "/Materials");
            EnsureFolder(resourceRoot + "/Textures");
            var meshParts = new LegacyVaniMeshFrames[source.Meshes.Count];
            for (int meshIndex = 0; meshIndex < source.Meshes.Count; meshIndex++)
            {
                var sourceMesh = source.Meshes[meshIndex];
                var material = ImportMaterial(corpus, sourceMesh.TextureName, resourceRoot, meshIndex);
                var frames = new Mesh[source.FrameCount];
                for (int frame = 0; frame < source.FrameCount; frame++)
                {
                    Mesh mesh = BuildFrameMesh(safeName, sourceMesh, frame);
                    string meshPath = resourceRoot + "/Meshes/Mesh_" + meshIndex.ToString("D2") + "_Frame_" + frame.ToString("D3") + ".asset";
                    LegacyAssetWriteBatch.DeleteAsset(meshPath);
                    LegacyAssetWriteBatch.CreateAsset(mesh, meshPath);
                    // Keep the staged object, not a disk lookup of an unflushed asset.
                    frames[frame] = mesh;
                }
                meshParts[meshIndex] = new LegacyVaniMeshFrames { material = material, frames = frames };
            }
            return new ResourceBuildResult
            {
                Source = source, MeshParts = meshParts,
                LocalBounds = MergeBounds(source.BoundingBox, source.BoundingBox2)
            };
        }

        private static Mesh BuildFrameMesh(string resourceName, LegacyVaniMesh source, int frame)
        {
            int vertexCount = source.Vertices.Count;
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var diffuse = new Color32[vertexCount];
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                var vertex = source.Vertices[vertexIndex];
                if (frame < 0 || frame >= vertex.Frames.Count)
                    throw new InvalidDataException("VANI frame index " + frame + " is invalid for vertex " + vertexIndex + ".");
                var value = vertex.Frames[frame];
                diffuse[vertexIndex] = value.DiffuseColor;
                positions[vertexIndex] = value.Position;
                normals[vertexIndex] = value.Normal.sqrMagnitude > 0.000001f ? value.Normal.normalized : Vector3.up;
                uv[vertexIndex] = value.UV;
            }
            var triangles = new int[source.Faces.Count * 3];
            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];
                triangles[i * 3] = face.A; triangles[i * 3 + 1] = face.B; triangles[i * 3 + 2] = face.C;
            }
            var mesh = new Mesh
            {
                name = resourceName + "_Frame_" + frame.ToString("D3"),
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = positions, normals = normals, uv = uv, colors32 = diffuse, triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material ImportMaterial(CanonicalClientCorpus corpus, string textureName, string resourceRoot, int meshIndex)
        {
            string textureRoot = LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, "DATA_Español/entity/texture");
            string sourcePath = CanonicalResourceIndex.FindUnique(textureRoot, Path.GetFileNameWithoutExtension(textureName) + ".dds");
            if (sourcePath == null) sourcePath = CanonicalResourceIndex.FindUnique(textureRoot, Path.GetFileName(textureName));
            if (sourcePath == null || !File.Exists(sourcePath))
                throw new FileNotFoundException("VANI texture not found: " + textureName, sourcePath);
            string assetPath = resourceRoot + "/Textures/" + meshIndex.ToString("D2") + "_" + Path.GetFileName(sourcePath).ToLowerInvariant();
            // This is albedo, not a normal map/lightmap. Never feed raw DDS to IHV import.
            Texture2D texture = LegacyColorTextureImporter.Import(sourcePath, assetPath, TextureWrapMode.Repeat);
            if (texture == null) throw new InvalidDataException("Unity did not import VANI color texture: " + assetPath);
            Shader shader = Shader.Find("Dreynox/Enhanced/LegacyVaniDiffuse");
            if (shader == null) throw new InvalidOperationException("The VANI vertex-diffuse shader is required; a shader ignoring original colors is not a valid fallback.");
            var material = new Material(shader) { name = "VANI_Material_" + meshIndex.ToString("D2"), enableInstancing = true };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            string materialPath = resourceRoot + "/Materials/Material_" + meshIndex.ToString("D2") + ".mat";
            LegacyAssetWriteBatch.DeleteAsset(materialPath);
            LegacyAssetWriteBatch.CreateAsset(material, materialPath);
            return material;
        }

        private static LegacyVaniInstanceBatch[] BuildBatches(IReadOnlyList<LegacyWldCoordinate> coordinates, Bounds localBounds)
        {
            var cells = new Dictionary<CellKey, List<Matrix4x4>>();
            for (int i = 0; i < coordinates.Count; i++)
            {
                var coordinate = coordinates[i];
                Quaternion rotation = RotationFromBasis(coordinate.Forward, coordinate.Up, i);
                Matrix4x4 matrix = Matrix4x4.TRS(coordinate.Position, rotation, Vector3.one);
                var key = new CellKey(Mathf.FloorToInt(coordinate.Position.x / CellSize), Mathf.FloorToInt(coordinate.Position.z / CellSize));
                if (!cells.TryGetValue(key, out List<Matrix4x4> list)) { list = new List<Matrix4x4>(); cells.Add(key, list); }
                list.Add(matrix);
            }
            var result = new List<LegacyVaniInstanceBatch>();
            foreach (var pair in cells)
            {
                var source = pair.Value;
                for (int offset = 0; offset < source.Count; offset += MaxInstancesPerBatch)
                {
                    int count = Math.Min(MaxInstancesPerBatch, source.Count - offset);
                    var matrices = new Matrix4x4[count]; source.CopyTo(offset, matrices, 0, count);
                    result.Add(new LegacyVaniInstanceBatch { matrices = matrices, bounds = CalculateBounds(localBounds, matrices) });
                }
            }
            return result.ToArray();
        }
        private static Quaternion RotationFromBasis(Vector3 forward, Vector3 up, int ordinal)
        {
            if (forward.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000001f)
                throw new InvalidDataException("VANI coordinate " + ordinal + " has a degenerate basis.");
            forward.Normalize(); up.Normalize();
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.01f)
            {
                Vector3 right = Vector3.Cross(up, forward).normalized;
                if (right.sqrMagnitude < 0.999f) throw new InvalidDataException("VANI coordinate " + ordinal + " has an unusable basis.");
                up = Vector3.Cross(forward, right).normalized;
            }
            return Quaternion.LookRotation(forward, up);
        }
        private static Bounds MergeBounds(LegacyBounds first, LegacyBounds second)
        {
            Vector3 min = Vector3.Min(Vector3.Min(first.Lower, first.Upper), Vector3.Min(second.Lower, second.Upper));
            Vector3 max = Vector3.Max(Vector3.Max(first.Lower, first.Upper), Vector3.Max(second.Lower, second.Upper));
            var result = new Bounds(); result.SetMinMax(min, max); return result;
        }
        private static Bounds CalculateBounds(Bounds localBounds, IReadOnlyList<Matrix4x4> matrices)
        {
            if (matrices == null || matrices.Count == 0) return new Bounds();
            Bounds result = TransformBounds(localBounds, matrices[0]);
            for (int i = 1; i < matrices.Count; i++)
            {
                Bounds transformed = TransformBounds(localBounds, matrices[i]);
                result.Encapsulate(transformed.min); result.Encapsulate(transformed.max);
            }
            return result;
        }
        private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 extents = source.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(source.center), (Abs(axisX) + Abs(axisY) + Abs(axisZ)) * 2f);
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_').Trim();
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
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
