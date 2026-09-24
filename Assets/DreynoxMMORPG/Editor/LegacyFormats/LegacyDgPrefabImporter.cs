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
        public Texture2D[] Lightmaps =
            Array.Empty<Texture2D>();
        public string PrefabPath =
            string.Empty;
    }

    public static class LegacyDgPrefabImporter
    {
        public static LegacyDgImportResult ImportCanonical(
            CanonicalClientCorpus corpus,
            string authoredDgName,
            string outputRoot)
        {
            if (corpus == null)
                throw new ArgumentNullException(
                    nameof(corpus));

            if (string.IsNullOrWhiteSpace(
                    authoredDgName))
            {
                throw new ArgumentException(
                    "DG file name is required.",
                    nameof(authoredDgName));
            }

            if (string.IsNullOrWhiteSpace(
                    outputRoot) ||
                !outputRoot.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "DG output root must be below Assets/.",
                    nameof(outputRoot));
            }

            string dungeonRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/world/dungeon");

            string dgPath =
                CanonicalResourceIndex
                    .FindUnique(
                        dungeonRoot,
                        Path.GetFileName(
                            authoredDgName));

            if (dgPath == null ||
                !File.Exists(dgPath))
            {
                throw new FileNotFoundException(
                    "Canonical DG not found: " +
                    authoredDgName,
                    dgPath);
            }

            LegacyDgFile source =
                LegacyDgParser.Parse(
                    dgPath);

            EnsureFolder(outputRoot);
            EnsureFolder(outputRoot + "/Meshes");
            EnsureFolder(outputRoot + "/Materials");
            EnsureFolder(outputRoot + "/Textures");
            EnsureFolder(outputRoot + "/Lightmaps");
            EnsureFolder(outputRoot + "/Prefabs");

            Material[] materials =
                ImportMaterials(
                    corpus,
                    source,
                    outputRoot);

            Texture2D[] lightmaps =
                ImportLightmaps(
                    dungeonRoot,
                    dgPath,
                    source.LightmapCount,
                    outputRoot);

            string stem =
                Path.GetFileNameWithoutExtension(
                    dgPath);

            GameObject root =
                new GameObject(
                    stem + "_DG");

            try
            {
                int nodeOrdinal = 0;
                int meshOrdinal = 0;
                int collisionOrdinal = 0;

                BuildNode(
                    source.RootNode,
                    root.transform,
                    source,
                    materials,
                    outputRoot,
                    ref nodeOrdinal,
                    ref meshOrdinal,
                    ref collisionOrdinal);

                string prefabPath =
                    outputRoot +
                    "/Prefabs/" +
                    stem +
                    "_DG.prefab";

                AssetDatabase.DeleteAsset(
                    prefabPath);

                GameObject prefab =
                    PrefabUtility
                        .SaveAsPrefabAsset(
                            root,
                            prefabPath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Unity could not save DG prefab '" +
                        prefabPath +
                        "'.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new LegacyDgImportResult
                {
                    Source = source,
                    Prefab = prefab,
                    Lightmaps = lightmaps,
                    PrefabPath = prefabPath
                };
            }
            finally
            {
                UnityEngine.Object
                    .DestroyImmediate(root);
            }
        }

        public static void ApplySceneLightmaps(
            IReadOnlyList<Texture2D> textures)
        {
            if (textures == null ||
                textures.Count == 0)
            {
                LightmapSettings.lightmaps =
                    Array.Empty<LightmapData>();

                return;
            }

            var data =
                new LightmapData[
                    textures.Count];

            for (int i = 0;
                 i < textures.Count;
                 i++)
            {
                if (textures[i] == null)
                {
                    throw new InvalidDataException(
                        "DG lightmap " +
                        i +
                        " is null.");
                }

                data[i] =
                    new LightmapData
                    {
                        lightmapColor =
                            textures[i]
                    };
            }

            LightmapSettings.lightmapsMode =
                LightmapsMode.NonDirectional;

            LightmapSettings.lightmaps =
                data;
        }

        private static void BuildNode(
            LegacyDgNode node,
            Transform parent,
            LegacyDgFile source,
            IReadOnlyList<Material> materials,
            string outputRoot,
            ref int nodeOrdinal,
            ref int meshOrdinal,
            ref int collisionOrdinal)
        {
            if (node == null)
                return;

            int currentNode =
                nodeOrdinal++;

            GameObject nodeObject =
                new GameObject(
                    "DG_Node_" +
                    currentNode.ToString("D4"));

            nodeObject.transform.SetParent(
                parent,
                false);

            for (int groupIndex = 0;
                 groupIndex <
                 node.MeshGroups.Count;
                 groupIndex++)
            {
                LegacyDgMeshGroup group =
                    node.MeshGroups[groupIndex];

                if (group.TextureIndex < 0 ||
                    group.TextureIndex >=
                    materials.Count)
                {
                    throw new InvalidDataException(
                        "DG group references invalid texture " +
                        group.TextureIndex +
                        ".");
                }

                for (int meshIndex = 0;
                     meshIndex <
                     group.Meshes.Count;
                     meshIndex++)
                {
                    LegacyDgMesh sourceMesh =
                        group.Meshes[meshIndex];

                    Mesh mesh =
                        BuildVisualMesh(
                            sourceMesh);

                    string meshPath =
                        outputRoot +
                        "/Meshes/Mesh_" +
                        meshOrdinal
                            .ToString("D4") +
                        ".asset";

                    AssetDatabase.DeleteAsset(
                        meshPath);

                    AssetDatabase.CreateAsset(
                        mesh,
                        meshPath);

                    GameObject visual =
                        new GameObject(
                            "Mesh_" +
                            meshOrdinal
                                .ToString("D4"));

                    visual.transform.SetParent(
                        nodeObject.transform,
                        false);

                    MeshFilter filter =
                        visual.AddComponent<
                            MeshFilter>();

                    filter.sharedMesh =
                        mesh;

                    MeshRenderer renderer =
                        visual.AddComponent<
                            MeshRenderer>();

                    renderer.sharedMaterial =
                        materials[
                            group.TextureIndex];

                    renderer.shadowCastingMode =
                        ShadowCastingMode.On;

                    renderer.receiveShadows =
                        true;

                    if (sourceMesh.LightmapIndex >= 0)
                    {
                        renderer.lightmapIndex =
                            sourceMesh.LightmapIndex;

                        renderer.lightmapScaleOffset =
                            new Vector4(
                                1f,
                                1f,
                                0f,
                                0f);
                    }

                    GameObjectUtility
                        .SetStaticEditorFlags(
                            visual,
                            StaticEditorFlags.BatchingStatic |
                            StaticEditorFlags.OccluderStatic |
                            StaticEditorFlags.OccludeeStatic |
                            StaticEditorFlags.NavigationStatic);

                    meshOrdinal++;
                }
            }

            if (node.CollisionType ==
                    LegacyDgCollisionType.Collision &&
                node.CollisionMesh != null &&
                node.CollisionMesh.Vertices.Count >
                    0)
            {
                Mesh collision =
                    BuildCollisionMesh(
                        node.CollisionMesh);

                string collisionPath =
                    outputRoot +
                    "/Meshes/Collision_" +
                    collisionOrdinal
                        .ToString("D4") +
                    ".asset";

                AssetDatabase.DeleteAsset(
                    collisionPath);

                AssetDatabase.CreateAsset(
                    collision,
                    collisionPath);

                GameObject colliderObject =
                    new GameObject(
                        "Collision_" +
                        collisionOrdinal
                            .ToString("D4"));

                colliderObject.transform.SetParent(
                    nodeObject.transform,
                    false);

                MeshCollider collider =
                    colliderObject.AddComponent<
                        MeshCollider>();

                collider.sharedMesh =
                    collision;

                collisionOrdinal++;
            }

            for (int i = 0;
                 i < node.Children.Count;
                 i++)
            {
                BuildNode(
                    node.Children[i],
                    nodeObject.transform,
                    source,
                    materials,
                    outputRoot,
                    ref nodeOrdinal,
                    ref meshOrdinal,
                    ref collisionOrdinal);
            }
        }

        private static Mesh BuildVisualMesh(
            LegacyDgMesh source)
        {
            int vertexCount =
                source.Vertices.Count;

            Vector3[] positions =
                new Vector3[
                    vertexCount];

            Vector3[] normals =
                new Vector3[
                    vertexCount];

            Vector2[] uv =
                new Vector2[
                    vertexCount];

            Vector2[] uv2 =
                new Vector2[
                    vertexCount];

            for (int i = 0;
                 i < vertexCount;
                 i++)
            {
                LegacyDgMeshVertex vertex =
                    source.Vertices[i];

                positions[i] =
                    LegacyCoordinateBridge
                        .Position(
                            vertex.Position);

                normals[i] =
                    LegacyCoordinateBridge
                        .Direction(
                            vertex.Normal)
                        .normalized;

                uv[i] =
                    vertex.UV;

                uv2[i] =
                    vertex.LightmapUV;
            }

            int[] triangles =
                new int[
                    source.Faces.Count *
                    3];

            for (int i = 0;
                 i < source.Faces.Count;
                 i++)
            {
                LegacyTriangle face =
                    source.Faces[i];

                // Z reflection changes handedness.
                triangles[i * 3] =
                    face.A;

                triangles[i * 3 + 1] =
                    face.C;

                triangles[i * 3 + 2] =
                    face.B;
            }

            Mesh mesh =
                new Mesh
                {
                    name =
                        "LegacyDG",
                    indexFormat =
                        vertexCount > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices =
                positions;

            mesh.normals =
                normals;

            mesh.uv =
                uv;

            mesh.uv2 =
                uv2;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();

            try
            {
                mesh.RecalculateTangents();
            }
            catch
            {
                // Some very old DG fixtures can be tangent-degenerate.
            }

            return mesh;
        }

        private static Mesh BuildCollisionMesh(
            LegacyDgCollisionMesh source)
        {
            Vector3[] positions =
                new Vector3[
                    source.Vertices.Count];

            for (int i = 0;
                 i < positions.Length;
                 i++)
            {
                positions[i] =
                    LegacyCoordinateBridge
                        .Position(
                            source.Vertices[i]);
            }

            int[] triangles =
                new int[
                    source.Faces.Count *
                    3];

            for (int i = 0;
                 i < source.Faces.Count;
                 i++)
            {
                LegacyTriangle face =
                    source.Faces[i];

                triangles[i * 3] =
                    face.A;

                triangles[i * 3 + 1] =
                    face.C;

                triangles[i * 3 + 2] =
                    face.B;
            }

            Mesh mesh =
                new Mesh
                {
                    name =
                        "LegacyDG_Collision",
                    indexFormat =
                        positions.Length >
                        65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices =
                positions;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();

            return mesh;
        }

        private static Material[] ImportMaterials(
            CanonicalClientCorpus corpus,
            LegacyDgFile source,
            string outputRoot)
        {
            string textureRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/entity/texture");

            var result =
                new Material[
                    source.TextureNames.Count];

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader =
                    Shader.Find("Standard");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible lit shader is available.");
            }

            for (int i = 0;
                 i < source.TextureNames.Count;
                 i++)
            {
                string authored =
                    source.TextureNames[i];

                string ddsName =
                    Path.GetFileNameWithoutExtension(
                        authored) +
                    ".dds";

                string sourcePath =
                    CanonicalResourceIndex
                        .FindUnique(
                            textureRoot,
                            ddsName);

                if (sourcePath == null ||
                    !File.Exists(sourcePath))
                {
                    throw new FileNotFoundException(
                        "DG texture could not be resolved: " +
                        authored +
                        " -> " +
                        ddsName,
                        sourcePath);
                }

                string textureAssetPath =
                    outputRoot +
                    "/Textures/" +
                    i.ToString("D2") +
                    "_" +
                    Path.GetFileName(
                        sourcePath)
                        .ToLowerInvariant();

                CopyAsset(
                    sourcePath,
                    textureAssetPath);

                TextureImporter importer =
                    AssetImporter.GetAtPath(
                        textureAssetPath)
                    as TextureImporter;

                if (importer != null)
                {
                    importer.textureType =
                        TextureImporterType.Default;

                    importer.sRGBTexture =
                        true;

                    importer.mipmapEnabled =
                        true;

                    importer.wrapMode =
                        TextureWrapMode.Repeat;

                    importer.filterMode =
                        FilterMode.Bilinear;

                    importer.textureCompression =
                        TextureImporterCompression
                            .CompressedHQ;

                    importer.SaveAndReimport();
                }

                Texture2D texture =
                    AssetDatabase
                        .LoadAssetAtPath<
                            Texture2D>(
                                textureAssetPath);

                if (texture == null)
                {
                    throw new InvalidDataException(
                        "Unity did not import DG texture '" +
                        textureAssetPath +
                        "'.");
                }

                Material material =
                    new Material(shader)
                    {
                        name =
                            "DG_Material_" +
                            i.ToString("D2")
                    };

                if (material.HasProperty(
                        "_BaseMap"))
                {
                    material.SetTexture(
                        "_BaseMap",
                        texture);
                }
                else
                {
                    material.mainTexture =
                        texture;
                }

                if (material.HasProperty(
                        "_Smoothness"))
                {
                    material.SetFloat(
                        "_Smoothness",
                        0f);
                }

                string materialPath =
                    outputRoot +
                    "/Materials/Material_" +
                    i.ToString("D2") +
                    ".mat";

                AssetDatabase.DeleteAsset(
                    materialPath);

                AssetDatabase.CreateAsset(
                    material,
                    materialPath);

                result[i] =
                    material;
            }

            return result;
        }

        private static Texture2D[] ImportLightmaps(
            string dungeonRoot,
            string dgPath,
            int count,
            string outputRoot)
        {
            if (count == 0)
                return Array.Empty<Texture2D>();

            string stem =
                Path.GetFileNameWithoutExtension(
                    dgPath);

            string lightmapRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        dungeonRoot,
                        stem);

            if (!Directory.Exists(
                    lightmapRoot))
            {
                throw new DirectoryNotFoundException(
                    "DG lightmap directory not found: " +
                    lightmapRoot);
            }

            var result =
                new Texture2D[
                    count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                string fileName =
                    stem +
                    "_l" +
                    i +
                    ".dds";

                string sourcePath =
                    LegacyUiAssetImporter
                        .ResolveCaseInsensitive(
                            lightmapRoot,
                            fileName);

                if (!File.Exists(
                        sourcePath))
                {
                    throw new FileNotFoundException(
                        "DG lightmap not found: " +
                        fileName,
                        sourcePath);
                }

                string assetPath =
                    outputRoot +
                    "/Lightmaps/" +
                    i.ToString("D2") +
                    "_" +
                    Path.GetFileName(
                        sourcePath)
                        .ToLowerInvariant();

                CopyAsset(
                    sourcePath,
                    assetPath);

                TextureImporter importer =
                    AssetImporter.GetAtPath(
                        assetPath)
                    as TextureImporter;

                if (importer != null)
                {
                    importer.textureType =
                        TextureImporterType.Lightmap;

                    importer.sRGBTexture =
                        false;

                    importer.mipmapEnabled =
                        true;

                    importer.wrapMode =
                        TextureWrapMode.Clamp;

                    importer.filterMode =
                        FilterMode.Bilinear;

                    importer.textureCompression =
                        TextureImporterCompression
                            .CompressedHQ;

                    importer.SaveAndReimport();
                }

                result[i] =
                    AssetDatabase
                        .LoadAssetAtPath<
                            Texture2D>(
                                assetPath);

                if (result[i] == null)
                {
                    throw new InvalidDataException(
                        "Unity did not import DG lightmap '" +
                        assetPath +
                        "'.");
                }
            }

            return result;
        }

        private static void CopyAsset(
            string source,
            string assetPath)
        {
            string destination =
                Path.GetFullPath(
                    assetPath);

            string directory =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            File.Copy(
                source,
                destination,
                true);

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions
                    .ForceSynchronousImport);
        }

        private static void EnsureFolder(
            string assetPath)
        {
            string[] parts =
                assetPath.Split('/');

            string current =
                parts[0];

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    current +
                    "/" +
                    parts[i];

                if (!AssetDatabase
                        .IsValidFolder(
                            next))
                {
                    AssetDatabase
                        .CreateFolder(
                            current,
                            parts[i]);
                }

                current =
                    next;
            }
        }
    }
}
