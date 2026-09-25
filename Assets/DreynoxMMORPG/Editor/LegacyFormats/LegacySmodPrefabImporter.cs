using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacySmodPrefabImporter
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Static";

        private static readonly Dictionary<string, GameObject> PrefabCache =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Material> MaterialCache =
            new Dictionary<string, Material>(
                StringComparer.OrdinalIgnoreCase);

        public static GameObject Import(
            CanonicalClientCorpus corpus,
            string category,
            string resourceName,
            bool alphaClip)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException(
                    "SMOD category is required.",
                    nameof(category));

            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException(
                    "SMOD resource name is required.",
                    nameof(resourceName));

            string cacheKey =
                category + "/" + resourceName +
                (alphaClip ? "#alpha" : "#opaque");

            GameObject cached;
            if (PrefabCache.TryGetValue(cacheKey, out cached) &&
                cached != null)
            {
                return cached;
            }

            string sourcePath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/entity/" +
                    category + "/" +
                    resourceName);

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    "WLD references a missing SMOD resource: " +
                    category + "/" + resourceName,
                    sourcePath);
            }

            LegacySmodFile smod =
                LegacySmodParser.Parse(sourcePath);

            string safe =
                SanitizeAssetName(
                    Path.GetFileNameWithoutExtension(resourceName));

            string folder =
                OutputRoot + "/" +
                SanitizeAssetName(category);

            EnsureFolder(OutputRoot);
            EnsureFolder(folder);
            EnsureFolder(folder + "/Meshes");
            EnsureFolder(folder + "/Prefabs");
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/Materials");

            GameObject root =
                new GameObject(safe);

            try
            {
                for (int i = 0; i < smod.Meshes.Count; i++)
                {
                    LegacySmodMesh sourceMesh =
                        smod.Meshes[i];

                    if (string.IsNullOrWhiteSpace(
                            sourceMesh.TextureName))
                    {
                        continue;
                    }

                    Mesh mesh =
                        BuildRenderMesh(
                            safe + "_Render_" + i.ToString("D2"),
                            sourceMesh);

                    string meshPath =
                        folder + "/Meshes/" +
                        safe + "_Render_" +
                        i.ToString("D2") + ".asset";

                    ReplaceAsset(meshPath, mesh);

                    Material material =
                        ImportMaterial(
                            corpus,
                            sourceMesh.TextureName,
                            alphaClip);

                    GameObject child =
                        new GameObject(
                            "Render_" + i.ToString("D2"));

                    child.transform.SetParent(
                        root.transform,
                        false);

                    MeshFilter filter =
                        child.AddComponent<MeshFilter>();

                    filter.sharedMesh =
                        AssetDatabase.LoadAssetAtPath<Mesh>(
                            meshPath);

                    MeshRenderer renderer =
                        child.AddComponent<MeshRenderer>();

                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode =
                        ShadowCastingMode.On;

                    renderer.receiveShadows = true;
                }

                for (int i = 0;
                     i < smod.CollisionMeshes.Count;
                     i++)
                {
                    LegacySmodCollisionMesh sourceCollision =
                        smod.CollisionMeshes[i];

                    Mesh collisionMesh =
                        BuildCollisionMesh(
                            safe + "_Collision_" +
                            i.ToString("D2"),
                            sourceCollision);

                    string collisionPath =
                        folder + "/Meshes/" +
                        safe + "_Collision_" +
                        i.ToString("D2") + ".asset";

                    ReplaceAsset(
                        collisionPath,
                        collisionMesh);

                    GameObject collisionObject =
                        new GameObject(
                            "Collision_" +
                            i.ToString("D2"));

                    collisionObject.transform.SetParent(
                        root.transform,
                        false);

                    MeshCollider collider =
                        collisionObject.AddComponent<MeshCollider>();

                    collider.sharedMesh =
                        AssetDatabase.LoadAssetAtPath<Mesh>(
                            collisionPath);

                    collider.convex = false;
                }

                string prefabPath =
                    folder + "/Prefabs/" +
                    safe + ".prefab";

                AssetDatabase.DeleteAsset(prefabPath);

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        prefabPath);

                PrefabCache[cacheKey] = prefab;
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void ClearSessionCache()
        {
            PrefabCache.Clear();
            MaterialCache.Clear();
        }

        private static Mesh BuildRenderMesh(
            string name,
            LegacySmodMesh source)
        {
            Vector3[] vertices =
                new Vector3[source.Vertices.Count];

            Vector3[] normals =
                new Vector3[source.Vertices.Count];

            Vector2[] uv =
                new Vector2[source.Vertices.Count];

            for (int i = 0; i < source.Vertices.Count; i++)
            {
                LegacySmodVertex vertex =
                    source.Vertices[i];

                vertices[i] = vertex.Position;
                normals[i] =
                    vertex.Normal.sqrMagnitude > 0.000001f
                        ? vertex.Normal.normalized
                        : Vector3.up;

                uv[i] = vertex.UV;
            }

            int[] triangles =
                BuildTriangles(source.Faces);

            Mesh mesh =
                new Mesh
                {
                    name = name,
                    indexFormat =
                        vertices.Length > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCollisionMesh(
            string name,
            LegacySmodCollisionMesh source)
        {
            Vector3[] vertices =
                source.Vertices.ToArray();

            int[] triangles =
                BuildTriangles(source.Faces);

            Mesh mesh =
                new Mesh
                {
                    name = name,
                    indexFormat =
                        vertices.Length > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int[] BuildTriangles(
            IReadOnlyList<LegacyTriangle> faces)
        {
            int[] triangles =
                new int[faces.Count * 3];

            for (int i = 0; i < faces.Count; i++)
            {
                LegacyTriangle face = faces[i];

                // SMOD/WLD are already in the Direct3D-style left-handed
                // world convention used by Unity. Preserve winding.
                triangles[i * 3] = face.A;
                triangles[i * 3 + 1] = face.B;
                triangles[i * 3 + 2] = face.C;
            }

            return triangles;
        }

        private static Material ImportMaterial(
            CanonicalClientCorpus corpus,
            string legacyTextureName,
            bool alphaClip)
        {
            string normalizedName =
                Path.GetFileNameWithoutExtension(
                    legacyTextureName) +
                ".dds";

            string key =
                normalizedName +
                (alphaClip ? "#alpha" : "#opaque");

            Material cached;
            if (MaterialCache.TryGetValue(key, out cached) &&
                cached != null)
            {
                return cached;
            }

            string source =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/entity/texture/" +
                    normalizedName);

            if (!File.Exists(source))
            {
                throw new FileNotFoundException(
                    "SMOD texture was not found: " +
                    normalizedName,
                    source);
            }

            string safe =
                SanitizeAssetName(
                    Path.GetFileNameWithoutExtension(
                        normalizedName));

            string texturePath =
                OutputRoot + "/Textures/" +
                safe + ".dds";

            string absoluteDestination =
                Path.GetFullPath(texturePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    absoluteDestination));

            File.Copy(
                source,
                absoluteDestination,
                true);

            AssetDatabase.ImportAsset(
                texturePath,
                ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer =
                AssetImporter.GetAtPath(texturePath)
                as TextureImporter;

            if (importer != null)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode =
                    TextureWrapMode.Repeat;

                importer.filterMode =
                    FilterMode.Bilinear;

                importer.alphaSource =
                    TextureImporterAlphaSource.FromInput;

                importer.textureCompression =
                    TextureImporterCompression.CompressedHQ;

                importer.SaveAndReimport();
            }

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    texturePath);

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                throw new InvalidOperationException(
                    "No compatible lit shader is available.");

            Material material =
                new Material(shader)
                {
                    name =
                        safe +
                        (alphaClip
                            ? "_Alpha"
                            : "_Opaque"),
                    enableInstancing = true
                };

            if (material.HasProperty("_BaseMap"))
                material.SetTexture(
                    "_BaseMap",
                    texture);
            else
                material.mainTexture = texture;

            if (alphaClip)
            {
                if (material.HasProperty("_AlphaClip"))
                    material.SetFloat("_AlphaClip", 1f);

                if (material.HasProperty("_Cutoff"))
                    material.SetFloat("_Cutoff", 0.45f);

                material.EnableKeyword("_ALPHATEST_ON");
            }

            string materialPath =
                OutputRoot + "/Materials/" +
                safe +
                (alphaClip
                    ? "_Alpha.mat"
                    : "_Opaque.mat");

            AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(
                material,
                materialPath);

            Material asset =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);

            MaterialCache[key] = asset;
            return asset;
        }

        private static void ReplaceAsset(
            string path,
            UnityEngine.Object value)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(
                value,
                path);
        }

        private static string ResolveCaseInsensitive(
            string root,
            string relativePath)
        {
            return CanonicalCorpusPaths.Resolve(root, relativePath);
        }

        private static string SanitizeAssetName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] invalid =
                Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(
                    invalid[i],
                    '_');

            return value
                .Replace(' ', '_')
                .Trim();
        }

        private static void EnsureFolder(
            string path)
        {
            string[] parts =
                path.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }
    }
}
