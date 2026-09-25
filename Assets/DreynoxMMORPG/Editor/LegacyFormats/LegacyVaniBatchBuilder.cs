using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyVaniBatchBuilder
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Map000/VAni";

        private const float CellSize = 160f;
        private const int MaxInstancesPerBatch = 1023;
        private const float InitialDrawDistance = 950f;

        private sealed class ResourceBuildResult
        {
            public string Name;
            public LegacyVaniFile Source;
            public LegacyVaniMeshFrames[] MeshParts;
            public Bounds LocalBounds;
        }

        private readonly struct CellKey : IEquatable<CellKey>
        {
            public readonly int X;
            public readonly int Z;

            public CellKey(
                int x,
                int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(
                CellKey other)
            {
                return
                    X == other.X &&
                    Z == other.Z;
            }

            public override bool Equals(
                object obj)
            {
                return
                    obj is CellKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (X * 397) ^
                        Z;
                }
            }
        }

        public static LegacyWorldVaniRuntime Create(
            CanonicalClientCorpus corpus,
            LegacyWldNameCoordinateGroup firstGroup,
            LegacyWldNameCoordinateGroup secondGroup,
            Transform observer,
            Camera camera)
        {
            if (corpus == null)
                throw new ArgumentNullException(
                    nameof(corpus));

            if (firstGroup == null)
                throw new ArgumentNullException(
                    nameof(firstGroup));

            if (secondGroup == null)
                throw new ArgumentNullException(
                    nameof(secondGroup));

            if (observer == null)
                throw new ArgumentNullException(
                    nameof(observer));

            if (camera == null)
                throw new ArgumentNullException(
                    nameof(camera));

            EnsureFolder(OutputRoot);

            var resources =
                new List<
                    LegacyVaniResourceRuntime>();

            AppendGroup(
                corpus,
                firstGroup,
                resources,
                "VAni1");

            AppendGroup(
                corpus,
                secondGroup,
                resources,
                "VAni2");

            GameObject root =
                new GameObject(
                    "WLD_VAni_GPU");

            LegacyWorldVaniRuntime runtime =
                root.AddComponent<
                    LegacyWorldVaniRuntime>();

            runtime.Configure(
                observer,
                camera,
                resources,
                InitialDrawDistance,
                false);

            return runtime;
        }

        private static void AppendGroup(
            CanonicalClientCorpus corpus,
            LegacyWldNameCoordinateGroup group,
            ICollection<LegacyVaniResourceRuntime> target,
            string groupLabel)
        {
            if (group.Names.Count == 0)
            {
                if (group.Coordinates.Count != 0)
                {
                    throw new InvalidDataException(
                        groupLabel +
                        " has placements but no resource names.");
                }

                return;
            }

            var coordinatesByResource =
                new List<
                    LegacyWldCoordinate>[
                        group.Names.Count];

            for (int i = 0;
                 i < coordinatesByResource.Length;
                 i++)
            {
                coordinatesByResource[i] =
                    new List<
                        LegacyWldCoordinate>();
            }

            for (int i = 0;
                 i < group.Coordinates.Count;
                 i++)
            {
                LegacyWldCoordinate coordinate =
                    group.Coordinates[i];

                if (coordinate.Id < 0 ||
                    coordinate.Id >=
                    group.Names.Count)
                {
                    throw new InvalidDataException(
                        groupLabel +
                        " placement " +
                        i +
                        " references invalid resource " +
                        coordinate.Id +
                        ".");
                }

                coordinatesByResource[
                    coordinate.Id]
                    .Add(coordinate);
            }

            for (int resourceIndex = 0;
                 resourceIndex <
                    group.Names.Count;
                 resourceIndex++)
            {
                string resourceName =
                    group.Names[
                        resourceIndex];

                List<LegacyWldCoordinate> placements =
                    coordinatesByResource[
                        resourceIndex];

                if (placements.Count == 0)
                    continue;

                ResourceBuildResult built =
                    BuildResource(
                        corpus,
                        resourceName);

                LegacyVaniInstanceBatch[] batches =
                    BuildBatches(
                        placements,
                        built.LocalBounds);

                int logicalCount = 0;

                for (int i = 0;
                     i < batches.Length;
                     i++)
                {
                    logicalCount +=
                        batches[i].Count;
                }

                if (logicalCount !=
                    placements.Count)
                {
                    throw new InvalidDataException(
                        groupLabel +
                        " resource '" +
                        resourceName +
                        "' batch count mismatch. " +
                        logicalCount +
                        " != " +
                        placements.Count +
                        ".");
                }

                target.Add(
                    new LegacyVaniResourceRuntime
                    {
                        resourceName =
                            groupLabel +
                            "/" +
                            resourceName,
                        frameCount =
                            built.Source.FrameCount,
                        frameIntervalMilliseconds =
                            built.Source.Unknown1,
                        frameTimingCalibrated =
                            false,
                        meshParts =
                            built.MeshParts,
                        batches =
                            batches,
                        logicalPlacementCount =
                            logicalCount
                    });
            }
        }

        private static ResourceBuildResult BuildResource(
            CanonicalClientCorpus corpus,
            string resourceName)
        {
            string vaniRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/entity/vani");

            string sourcePath =
                CanonicalResourceIndex
                    .FindUnique(
                        vaniRoot,
                        Path.GetFileName(
                            resourceName));

            if (sourcePath == null ||
                !File.Exists(
                    sourcePath))
            {
                throw new FileNotFoundException(
                    "VANI resource not found: " +
                    resourceName,
                    sourcePath);
            }

            LegacyVaniFile source =
                LegacyVaniParser.Parse(
                    sourcePath);

            if (source.Unknown1 <= 0 ||
                source.Unknown1 > 10000)
            {
                throw new InvalidDataException(
                    "VANI '" +
                    resourceName +
                    "' has invalid frame interval field " +
                    source.Unknown1 +
                    ".");
            }

            string safeName =
                SanitizeAssetName(
                    Path.GetFileNameWithoutExtension(
                        resourceName));

            string resourceRoot =
                OutputRoot +
                "/" +
                safeName;

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                resourceRoot);

            EnsureFolder(resourceRoot);
            EnsureFolder(resourceRoot + "/Meshes");
            EnsureFolder(resourceRoot + "/Materials");
            EnsureFolder(resourceRoot + "/Textures");

            LegacyVaniMeshFrames[] meshParts =
                new LegacyVaniMeshFrames[
                    source.Meshes.Count];

            for (int meshIndex = 0;
                 meshIndex <
                    source.Meshes.Count;
                 meshIndex++)
            {
                LegacyVaniMesh sourceMesh =
                    source.Meshes[
                        meshIndex];

                Material material =
                    ImportMaterial(
                        corpus,
                        sourceMesh.TextureName,
                        resourceRoot,
                        meshIndex);

                Mesh[] frames =
                    new Mesh[
                        source.FrameCount];

                for (int frame = 0;
                     frame <
                        source.FrameCount;
                     frame++)
                {
                    Mesh mesh =
                        BuildFrameMesh(
                            safeName,
                            sourceMesh,
                            frame);

                    string meshPath =
                        resourceRoot +
                        "/Meshes/Mesh_" +
                        meshIndex.ToString("D2") +
                        "_Frame_" +
                        frame.ToString("D3") +
                        ".asset";

                    Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                        meshPath);

                    Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                        mesh,
                        meshPath);

                    frames[frame] =
                        AssetDatabase
                            .LoadAssetAtPath<
                                Mesh>(
                                    meshPath);
                }

                meshParts[meshIndex] =
                    new LegacyVaniMeshFrames
                    {
                        material =
                            material,
                        frames =
                            frames
                    };
            }

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAssets();

            return new ResourceBuildResult
            {
                Name =
                    resourceName,
                Source =
                    source,
                MeshParts =
                    meshParts,
                LocalBounds =
                    MergeBounds(
                        source.BoundingBox,
                        source.BoundingBox2)
            };
        }

        private static Mesh BuildFrameMesh(
            string resourceName,
            LegacyVaniMesh source,
            int frame)
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

            for (int vertexIndex = 0;
                 vertexIndex <
                    vertexCount;
                 vertexIndex++)
            {
                LegacyVaniVertex vertex =
                    source.Vertices[
                        vertexIndex];

                if (frame < 0 ||
                    frame >=
                    vertex.Frames.Count)
                {
                    throw new InvalidDataException(
                        "VANI frame index " +
                        frame +
                        " is invalid for vertex " +
                        vertexIndex +
                        ".");
                }

                LegacyVaniVertexFrame value =
                    vertex.Frames[
                        frame];

                if (value.BoneId != -1)
                {
                    throw new InvalidDataException(
                        "VANI '" +
                        resourceName +
                        "' frame " +
                        frame +
                        " contains unexpected BoneId " +
                        value.BoneId +
                        ".");
                }

                positions[vertexIndex] =
                    value.Position;

                normals[vertexIndex] =
                    value.Normal.sqrMagnitude >
                    0.000001f
                        ? value.Normal.normalized
                        : Vector3.up;

                uv[vertexIndex] =
                    value.UV;
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
                    face.B;

                triangles[i * 3 + 2] =
                    face.C;
            }

            Mesh mesh =
                new Mesh
                {
                    name =
                        resourceName +
                        "_Frame_" +
                        frame.ToString("D3"),
                    indexFormat =
                        vertexCount >
                        65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices =
                positions;

            mesh.normals =
                normals;

            mesh.uv =
                uv;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();

            return mesh;
        }

        private static Material ImportMaterial(
            CanonicalClientCorpus corpus,
            string textureName,
            string resourceRoot,
            int meshIndex)
        {
            string textureRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/entity/texture");

            string baseName =
                Path.GetFileNameWithoutExtension(
                    textureName);

            string sourcePath =
                CanonicalResourceIndex
                    .FindUnique(
                        textureRoot,
                        baseName +
                        ".dds");

            if (sourcePath == null)
            {
                sourcePath =
                    CanonicalResourceIndex
                        .FindUnique(
                            textureRoot,
                            Path.GetFileName(
                                textureName));
            }

            if (sourcePath == null ||
                !File.Exists(
                    sourcePath))
            {
                throw new FileNotFoundException(
                    "VANI texture not found: " +
                    textureName,
                    sourcePath);
            }

            string assetPath =
                resourceRoot +
                "/Textures/" +
                meshIndex.ToString("D2") +
                "_" +
                Path.GetFileName(
                    sourcePath)
                    .ToLowerInvariant();

            CopyTexture(
                sourcePath,
                assetPath);

            Texture2D texture =
                AssetDatabase
                    .LoadAssetAtPath<
                        Texture2D>(
                            assetPath);

            if (texture == null)
            {
                throw new InvalidDataException(
                    "Unity did not import VANI texture '" +
                    assetPath +
                    "'.");
            }

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Standard");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible lit shader is available.");
            }

            Material material =
                new Material(shader)
                {
                    name =
                        "VANI_Material_" +
                        meshIndex.ToString("D2"),
                    enableInstancing =
                        true
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

            ConfigureAlphaClip(
                material);

            string materialPath =
                resourceRoot +
                "/Materials/Material_" +
                meshIndex.ToString("D2") +
                ".mat";

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                materialPath);

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                material,
                materialPath);

            return AssetDatabase
                .LoadAssetAtPath<
                    Material>(
                        materialPath);
        }

        private static void ConfigureAlphaClip(
            Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty(
                    "_AlphaClip"))
            {
                material.SetFloat(
                    "_AlphaClip",
                    1f);
            }

            if (material.HasProperty(
                    "_Cutoff"))
            {
                material.SetFloat(
                    "_Cutoff",
                    0.45f);
            }

            material.EnableKeyword(
                "_ALPHATEST_ON");

            material.renderQueue =
                (int)RenderQueue.AlphaTest;
        }

        private static LegacyVaniInstanceBatch[] BuildBatches(
            IReadOnlyList<LegacyWldCoordinate> coordinates,
            Bounds localBounds)
        {
            var cells =
                new Dictionary<
                    CellKey,
                    List<Matrix4x4>>();

            for (int i = 0;
                 i < coordinates.Count;
                 i++)
            {
                LegacyWldCoordinate coordinate =
                    coordinates[i];

                Quaternion rotation =
                    RotationFromBasis(
                        coordinate.Forward,
                        coordinate.Up,
                        i);

                Matrix4x4 matrix =
                    Matrix4x4.TRS(
                        coordinate.Position,
                        rotation,
                        Vector3.one);

                int cellX =
                    Mathf.FloorToInt(
                        coordinate.Position.x /
                        CellSize);

                int cellZ =
                    Mathf.FloorToInt(
                        coordinate.Position.z /
                        CellSize);

                var key =
                    new CellKey(
                        cellX,
                        cellZ);

                List<Matrix4x4> list;

                if (!cells.TryGetValue(
                        key,
                        out list))
                {
                    list =
                        new List<Matrix4x4>();

                    cells.Add(
                        key,
                        list);
                }

                list.Add(matrix);
            }

            var result =
                new List<
                    LegacyVaniInstanceBatch>();

            foreach (KeyValuePair<
                         CellKey,
                         List<Matrix4x4>>
                     pair in cells)
            {
                List<Matrix4x4> source =
                    pair.Value;

                for (int offset = 0;
                     offset <
                        source.Count;
                     offset +=
                        MaxInstancesPerBatch)
                {
                    int count =
                        Math.Min(
                            MaxInstancesPerBatch,
                            source.Count -
                            offset);

                    Matrix4x4[] matrices =
                        new Matrix4x4[
                            count];

                    source.CopyTo(
                        offset,
                        matrices,
                        0,
                        count);

                    result.Add(
                        new LegacyVaniInstanceBatch
                        {
                            matrices =
                                matrices,
                            bounds =
                                CalculateBounds(
                                    localBounds,
                                    matrices)
                        });
                }
            }

            return result.ToArray();
        }

        private static Quaternion RotationFromBasis(
            Vector3 forward,
            Vector3 up,
            int ordinal)
        {
            if (forward.sqrMagnitude <
                    0.000001f ||
                up.sqrMagnitude <
                    0.000001f)
            {
                throw new InvalidDataException(
                    "VANI coordinate " +
                    ordinal +
                    " has a degenerate basis.");
            }

            forward.Normalize();
            up.Normalize();

            if (Mathf.Abs(
                    Vector3.Dot(
                        forward,
                        up)) >
                0.01f)
            {
                Vector3 right =
                    Vector3.Cross(
                        up,
                        forward)
                    .normalized;

                if (right.sqrMagnitude <
                    0.999f)
                {
                    throw new InvalidDataException(
                        "VANI coordinate " +
                        ordinal +
                        " has an unusable basis.");
                }

                up =
                    Vector3.Cross(
                        forward,
                        right)
                    .normalized;
            }

            return Quaternion.LookRotation(
                forward,
                up);
        }

        private static Bounds MergeBounds(
            LegacyBounds first,
            LegacyBounds second)
        {
            Vector3 min =
                Vector3.Min(
                    Vector3.Min(
                        first.Lower,
                        first.Upper),
                    Vector3.Min(
                        second.Lower,
                        second.Upper));

            Vector3 max =
                Vector3.Max(
                    Vector3.Max(
                        first.Lower,
                        first.Upper),
                    Vector3.Max(
                        second.Lower,
                        second.Upper));

            Bounds result =
                new Bounds();

            result.SetMinMax(
                min,
                max);

            return result;
        }

        private static Bounds CalculateBounds(
            Bounds localBounds,
            IReadOnlyList<Matrix4x4> matrices)
        {
            if (matrices == null ||
                matrices.Count == 0)
                return new Bounds();

            Bounds result =
                TransformBounds(
                    localBounds,
                    matrices[0]);

            for (int i = 1;
                 i < matrices.Count;
                 i++)
            {
                Bounds transformed =
                    TransformBounds(
                        localBounds,
                        matrices[i]);

                result.Encapsulate(
                    transformed.min);

                result.Encapsulate(
                    transformed.max);
            }

            return result;
        }

        private static Bounds TransformBounds(
            Bounds source,
            Matrix4x4 matrix)
        {
            Vector3 center =
                matrix.MultiplyPoint3x4(
                    source.center);

            Vector3 extents =
                source.extents;

            Vector3 axisX =
                matrix.MultiplyVector(
                    new Vector3(
                        extents.x,
                        0f,
                        0f));

            Vector3 axisY =
                matrix.MultiplyVector(
                    new Vector3(
                        0f,
                        extents.y,
                        0f));

            Vector3 axisZ =
                matrix.MultiplyVector(
                    new Vector3(
                        0f,
                        0f,
                        extents.z));

            Vector3 worldExtents =
                Abs(axisX) +
                Abs(axisY) +
                Abs(axisZ);

            return new Bounds(
                center,
                worldExtents *
                2f);
        }

        private static Vector3 Abs(
            Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(
                    value.x),
                Mathf.Abs(
                    value.y),
                Mathf.Abs(
                    value.z));
        }

        private static void CopyTexture(
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

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    assetPath)
                as TextureImporter;

            if (importer == null)
                return;

            importer.textureType =
                TextureImporterType.Default;

            importer.sRGBTexture =
                true;

            importer.mipmapEnabled =
                true;

            importer.alphaIsTransparency =
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

        private static string SanitizeAssetName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
                return "Unnamed";

            foreach (char invalid in
                     Path.GetInvalidFileNameChars())
            {
                value =
                    value.Replace(
                        invalid,
                        '_');
            }

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
                    StringSplitOptions
                        .RemoveEmptyEntries);

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
