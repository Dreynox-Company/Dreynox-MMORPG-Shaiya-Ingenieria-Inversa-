using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyDgImportResult
    {
        public LegacyDgFile Source;
        public GameObject Prefab;
        public Texture2D[] Lightmaps = Array.Empty<Texture2D>();
        public string PrefabPath = string.Empty;
    }

    public static class LegacyDgPrefabImporter
    {
        public static LegacyDgImportResult ImportCanonical(
            CanonicalClientCorpus corpus, string authoredDgName, string outputRoot)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));
            if (string.IsNullOrWhiteSpace(authoredDgName))
                throw new ArgumentException("DG file name is required.", nameof(authoredDgName));
            if (string.IsNullOrWhiteSpace(outputRoot) || !outputRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("DG output root must be below Assets/.", nameof(outputRoot));
            string dungeonRoot = LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, "DATA_Español/world/dungeon");
            string dgPath = CanonicalResourceIndex.FindUnique(dungeonRoot, Path.GetFileName(authoredDgName));
            if (dgPath == null || !File.Exists(dgPath))
                throw new FileNotFoundException("Canonical DG not found: " + authoredDgName, dgPath);
            LegacyDgFile source = LegacyDgParser.Parse(dgPath);
            EnsureFolder(outputRoot);
            foreach (string folder in new[] { "Meshes", "Materials", "Textures", "Lightmaps", "Prefabs" })
                EnsureFolder(outputRoot + "/" + folder);
            Material[] materials = ImportMaterials(corpus, source, outputRoot);
            Texture2D[] lightmaps = ImportLightmaps(dungeonRoot, dgPath, source.LightmapCount, outputRoot);
            string stem = Path.GetFileNameWithoutExtension(dgPath);
            GameObject root = new GameObject(stem + "_DG");
            try
            {
                int nodeOrdinal = 0, meshOrdinal = 0, collisionOrdinal = 0;
                BuildNode(source.RootNode, root.transform, source, materials, outputRoot,
                    ref nodeOrdinal, ref meshOrdinal, ref collisionOrdinal);
                string prefabPath = outputRoot + "/Prefabs/" + stem + "_DG.prefab";
                AssetDatabase.DeleteAsset(prefabPath);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Unity could not save DG prefab '" + prefabPath + "'.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new LegacyDgImportResult { Source = source, Prefab = prefab, Lightmaps = lightmaps, PrefabPath = prefabPath };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void ApplySceneLightmaps(IReadOnlyList<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
            { LightmapSettings.lightmaps = Array.Empty<LightmapData>(); return; }
            var data = new LightmapData[textures.Count];
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] == null) throw new InvalidDataException("DG lightmap " + i + " is null.");
                data[i] = new LightmapData { lightmapColor = textures[i] };
            }
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = data;
        }
        public static int AppendSceneLightmaps(IReadOnlyList<Texture2D> textures)
        {
            LightmapData[] existing = LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
            int offset = existing.Length;
            if (textures == null || textures.Count == 0) return offset;
            var combined = new LightmapData[existing.Length + textures.Count];
            Array.Copy(existing, combined, existing.Length);
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] == null) throw new InvalidDataException("DG lightmap " + i + " is null.");
                combined[offset + i] = new LightmapData { lightmapColor = textures[i] };
            }
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = combined;
            return offset;
        }
        public static void OffsetInstanceLightmapIndices(GameObject instance, int offset, int localLightmapCount)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (localLightmapCount < 0) throw new ArgumentOutOfRangeException(nameof(localLightmapCount));
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                int local = renderer.lightmapIndex;
                if (local >= 0 && local < localLightmapCount) renderer.lightmapIndex = offset + local;
            }
        }
        private static void BuildNode(LegacyDgNode node, Transform parent, LegacyDgFile source,
            IReadOnlyList<Material> materials, string outputRoot,
            ref int nodeOrdinal, ref int meshOrdinal, ref int collisionOrdinal)
        {
            if (node == null) return;
            var nodeObject = new GameObject("DG_Node_" + (nodeOrdinal++).ToString("D4"));
            nodeObject.transform.SetParent(parent, false);
            foreach (LegacyDgMeshGroup group in node.MeshGroups)
            {
                if (group.TextureIndex < 0 || group.TextureIndex >= materials.Count)
                    throw new InvalidDataException("DG group references invalid texture " + group.TextureIndex + ".");
                foreach (LegacyDgMesh sourceMesh in group.Meshes)
                {
                    Mesh mesh = BuildVisualMesh(sourceMesh);
                    string path = outputRoot + "/Meshes/Mesh_" + meshOrdinal.ToString("D4") + ".asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(mesh, path);
                    var visual = new GameObject("Mesh_" + meshOrdinal.ToString("D4"));
                    visual.transform.SetParent(nodeObject.transform, false);
                    visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = visual.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[group.TextureIndex];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (sourceMesh.LightmapIndex >= 0)
                    {
                        renderer.lightmapIndex = sourceMesh.LightmapIndex;
                        renderer.lightmapScaleOffset = new Vector4(1, 1, 0, 0);
                    }
                    GameObjectUtility.SetStaticEditorFlags(visual,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
                    meshOrdinal++;
                }
            }
            if (node.CollisionType == LegacyDgCollisionType.Collision && node.CollisionMesh != null && node.CollisionMesh.Vertices.Count > 0)
            {
                Mesh collision = BuildCollisionMesh(node.CollisionMesh);
                string path = outputRoot + "/Meshes/Collision_" + collisionOrdinal.ToString("D4") + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(collision, path);
                var colliderObject = new GameObject("Collision_" + collisionOrdinal.ToString("D4"));
                colliderObject.transform.SetParent(nodeObject.transform, false);
                colliderObject.AddComponent<MeshCollider>().sharedMesh = collision;
                collisionOrdinal++;
            }
            foreach (LegacyDgNode child in node.Children)
                BuildNode(child, nodeObject.transform, source, materials, outputRoot,
                    ref nodeOrdinal, ref meshOrdinal, ref collisionOrdinal);
        }
        private static Mesh BuildVisualMesh(LegacyDgMesh source)
        {
            int count = source.Vertices.Count;
            var positions = new Vector3[count]; var normals = new Vector3[count];
            var uv = new Vector2[count]; var uv2 = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                LegacyDgMeshVertex vertex = source.Vertices[i];
                positions[i] = vertex.Position;
                normals[i] = vertex.Normal.sqrMagnitude > 0.000001f ? vertex.Normal.normalized : Vector3.up;
                uv[i] = vertex.UV; uv2[i] = vertex.LightmapUV;
            }
            var triangles = new int[source.Faces.Count * 3];
            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];
                // Preserve DG world-space convention shared by WLD/SMOD.
                triangles[i * 3] = face.A; triangles[i * 3 + 1] = face.B; triangles[i * 3 + 2] = face.C;
            }
            var mesh = new Mesh { name = "LegacyDG", indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.normals = normals; mesh.uv = uv; mesh.uv2 = uv2; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            try { mesh.RecalculateTangents(); }
            catch { /* Existing compatibility for tangent-degenerate legacy geometry. */ }
            return mesh;
        }
        private static Mesh BuildCollisionMesh(LegacyDgCollisionMesh source)
        {
            var positions = new Vector3[source.Vertices.Count];
            for (int i = 0; i < positions.Length; i++) positions[i] = source.Vertices[i];
            var triangles = new int[source.Faces.Count * 3];
            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];
                triangles[i * 3] = face.A; triangles[i * 3 + 1] = face.B; triangles[i * 3 + 2] = face.C;
            }
            var mesh = new Mesh { name = "LegacyDG_Collision", indexFormat = positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.triangles = triangles; mesh.RecalculateBounds();
            return mesh;
        }
        private static Material[] ImportMaterials(CanonicalClientCorpus corpus, LegacyDgFile source, string outputRoot)
        {
            string textureRoot = LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, "DATA_Español/entity/texture");
            var result = new Material[source.TextureNames.Count];
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible lit shader is available.");
            for (int i = 0; i < result.Length; i++)
            {
                string authored = source.TextureNames[i];
                string materialPath = outputRoot + "/Materials/Material_" + i.ToString("D2") + ".mat";
                if (string.IsNullOrWhiteSpace(authored))
                {
                    var untextured = new Material(shader) { name = "DG_AuthoredEmptyTexture_" + i };
                    if (untextured.HasProperty("_Smoothness")) untextured.SetFloat("_Smoothness", 0);
                    AssetDatabase.DeleteAsset(materialPath); AssetDatabase.CreateAsset(untextured, materialPath);
                    result[i] = untextured;
                    Debug.LogWarning("DG authored empty texture slot " + i + " retained as untextured/lightmapped. Native fallback shading is not yet calibrated.");
                    continue;
                }
                string ddsName = Path.GetFileNameWithoutExtension(authored) + ".dds";
                string sourcePath = CanonicalResourceIndex.FindUnique(textureRoot, ddsName);
                if (sourcePath == null || !File.Exists(sourcePath))
                    throw new FileNotFoundException("DG texture could not be resolved: " + authored + " -> " + ddsName, sourcePath);
                // File extension in legacy DATA is not always the actual image format.
                string textureAssetPath = outputRoot + "/Textures/" + i.ToString("D2") + "_" + LegacyImageSignature.ImportedFileName(sourcePath);
                CopyAsset(sourcePath, textureAssetPath);
                var importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                    importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                if (texture == null) throw new InvalidDataException("Unity did not import DG texture '" + textureAssetPath + "'.");
                var material = new Material(shader) { name = "DG_Material_" + i.ToString("D2") };
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                else material.mainTexture = texture;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
                AssetDatabase.DeleteAsset(materialPath); AssetDatabase.CreateAsset(material, materialPath);
                result[i] = material;
            }
            return result;
        }
        private static Texture2D[] ImportLightmaps(string dungeonRoot, string dgPath, int count, string outputRoot)
        {
            if (count == 0) return Array.Empty<Texture2D>();
            string stem = Path.GetFileNameWithoutExtension(dgPath);
            string lightmapRoot = LegacyUiAssetImporter.ResolveCaseInsensitive(dungeonRoot, stem);
            if (!Directory.Exists(lightmapRoot)) throw new DirectoryNotFoundException("DG lightmap directory not found: " + lightmapRoot);
            var result = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                string fileName = stem + "_l" + i + ".dds";
                string sourcePath = LegacyUiAssetImporter.ResolveCaseInsensitive(lightmapRoot, fileName);
                if (!File.Exists(sourcePath)) throw new FileNotFoundException("DG lightmap not found: " + fileName, sourcePath);
                // Fortress lightmaps are bitmaps named .dds in original DATA.
                // Copy their bytes unchanged, but use the detected extension so Unity chooses the right importer.
                string assetPath = outputRoot + "/Lightmaps/" + i.ToString("D2") + "_" + LegacyImageSignature.ImportedFileName(sourcePath);
                CopyAsset(sourcePath, assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Lightmap; importer.sRGBTexture = false;
                    importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
                result[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (result[i] == null) throw new InvalidDataException("Unity did not import DG lightmap '" + assetPath + "'.");
            }
            return result;
        }
        private static void CopyAsset(string source, string assetPath)
        {
            string destination = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }
        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
