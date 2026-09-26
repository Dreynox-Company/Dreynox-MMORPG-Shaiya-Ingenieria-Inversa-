using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyGrassBatchBuilder
    {
        private const float CellSize = 128f;
        private const int MaxInstancesPerBatch = 1023;
        private const float InitialDrawDistance = 650f;

        private sealed class RenderPart
        {
            public Mesh Mesh;
            public Material Material;
            public int SubmeshIndex;
            public Matrix4x4 LocalMatrix;
        }

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly int CellX;
            public readonly int CellZ;
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly int SubmeshIndex;

            public BatchKey(
                int cellX,
                int cellZ,
                Mesh mesh,
                Material material,
                int submeshIndex)
            {
                CellX = cellX;
                CellZ = cellZ;
                Mesh = mesh;
                Material = material;
                SubmeshIndex = submeshIndex;
            }

            public bool Equals(BatchKey other)
            {
                return
                    CellX == other.CellX &&
                    CellZ == other.CellZ &&
                    Mesh == other.Mesh &&
                    Material == other.Material &&
                    SubmeshIndex == other.SubmeshIndex;
            }

            public override bool Equals(object obj)
            {
                return
                    obj is BatchKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + CellX;
                    hash = hash * 31 + CellZ;
                    hash = hash * 31 +
                        (Mesh != null
                            ? Mesh.GetInstanceID()
                            : 0);
                    hash = hash * 31 +
                        (Material != null
                            ? Material.GetInstanceID()
                            : 0);
                    hash = hash * 31 + SubmeshIndex;
                    return hash;
                }
            }
        }

        public static LegacyWorldGrassRuntime Create(
            CanonicalClientCorpus corpus,
            LegacyWldNameCoordinateGroup group,
            Transform observer,
            Camera camera)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            GameObject root =
                new GameObject(
                    "WLD_Grass_GPU");

            LegacyWorldGrassRuntime runtime =
                root.AddComponent<
                    LegacyWorldGrassRuntime>();

            if (group.Coordinates.Count == 0)
            {
                runtime.Configure(
                    observer,
                    camera,
                    Array.Empty<
                        LegacyGrassInstanceBatch>(),
                    0,
                    InitialDrawDistance,
                    false);

                return runtime;
            }

            GameObject[] prefabs =
                new GameObject[
                    group.Names.Count];

            List<RenderPart>[] parts =
                new List<RenderPart>[
                    group.Names.Count];

            for (int resourceIndex = 0;
                 resourceIndex < group.Names.Count;
                 resourceIndex++)
            {
                prefabs[resourceIndex] =
                    LegacySmodPrefabImporter.Import(
                        corpus,
                        "grass",
                        group.Names[resourceIndex],
                        alphaClip: true);

                if (prefabs[resourceIndex] == null)
                {
                    throw new InvalidDataException(
                        "Grass SMOD import returned null for " +
                        group.Names[resourceIndex] + ".");
                }

                parts[resourceIndex] =
                    ExtractRenderParts(
                        prefabs[resourceIndex]);

                if (parts[resourceIndex].Count == 0)
                {
                    throw new InvalidDataException(
                        "Grass SMOD has no renderable mesh: " +
                        group.Names[resourceIndex] + ".");
                }
            }

            var matrices =
                new Dictionary<
                    BatchKey,
                    List<Matrix4x4>>();

            for (int coordinateIndex = 0;
                 coordinateIndex <
                    group.Coordinates.Count;
                 coordinateIndex++)
            {
                LegacyWldCoordinate coordinate =
                    group.Coordinates[
                        coordinateIndex];

                if (coordinate.Id < 0 ||
                    coordinate.Id >=
                    parts.Length)
                {
                    throw new InvalidDataException(
                        "Grass coordinate " +
                        coordinateIndex +
                        " references invalid resource " +
                        coordinate.Id + ".");
                }

                Quaternion rotation =
                    RotationFromBasis(
                        coordinate.Forward,
                        coordinate.Up,
                        coordinateIndex);

                Matrix4x4 world =
                    Matrix4x4.TRS(
                        coordinate.Position,
                        rotation,
                        Vector3.one);

                List<RenderPart> resourceParts =
                    parts[coordinate.Id];

                for (int partIndex = 0;
                     partIndex <
                        resourceParts.Count;
                     partIndex++)
                {
                    RenderPart part =
                        resourceParts[partIndex];

                    Matrix4x4 matrix =
                        world *
                        part.LocalMatrix;

                    Vector3 position =
                        matrix.GetColumn(3);

                    int cellX =
                        Mathf.FloorToInt(
                            position.x /
                            CellSize);

                    int cellZ =
                        Mathf.FloorToInt(
                            position.z /
                            CellSize);

                    var key =
                        new BatchKey(
                            cellX,
                            cellZ,
                            part.Mesh,
                            part.Material,
                            part.SubmeshIndex);

                    List<Matrix4x4> list;

                    if (!matrices.TryGetValue(
                            key,
                            out list))
                    {
                        list =
                            new List<Matrix4x4>();

                        matrices.Add(
                            key,
                            list);
                    }

                    list.Add(matrix);
                }
            }

            var batches =
                new List<
                    LegacyGrassInstanceBatch>();

            foreach (KeyValuePair<
                         BatchKey,
                         List<Matrix4x4>>
                     pair in matrices)
            {
                BatchKey key =
                    pair.Key;

                List<Matrix4x4> source =
                    pair.Value;

                key.Material.enableInstancing =
                    true;

                EditorUtility.SetDirty(
                    key.Material);

                for (int offset = 0;
                     offset < source.Count;
                     offset += MaxInstancesPerBatch)
                {
                    int count =
                        Math.Min(
                            MaxInstancesPerBatch,
                            source.Count - offset);

                    Matrix4x4[] chunk =
                        new Matrix4x4[count];

                    source.CopyTo(
                        offset,
                        chunk,
                        0,
                        count);

                    batches.Add(
                        new LegacyGrassInstanceBatch
                        {
                            mesh =
                                key.Mesh,
                            material =
                                key.Material,
                            submeshIndex =
                                key.SubmeshIndex,
                            matrices =
                                chunk,
                            bounds =
                                CalculateBounds(
                                    key.Mesh.bounds,
                                    chunk)
                        });
                }
            }

            runtime.Configure(
                observer,
                camera,
                batches,
                group.Coordinates.Count,
                InitialDrawDistance,
                false);

            return runtime;
        }

        private static List<RenderPart> ExtractRenderParts(
            GameObject prefab)
        {
            var result =
                new List<RenderPart>();

            MeshFilter[] filters =
                prefab.GetComponentsInChildren<
                    MeshFilter>(
                        true);

            for (int i = 0;
                 i < filters.Length;
                 i++)
            {
                MeshFilter filter =
                    filters[i];

                Mesh mesh =
                    filter.sharedMesh;

                MeshRenderer renderer =
                    filter.GetComponent<
                        MeshRenderer>();

                if (mesh == null ||
                    renderer == null ||
                    !renderer.enabled)
                    continue;

                Material[] materials =
                    renderer.sharedMaterials;

                if (materials == null ||
                    materials.Length == 0)
                    continue;

                Matrix4x4 local =
                    RelativeMatrix(
                        filter.transform,
                        prefab.transform);

                for (int submesh = 0;
                     submesh <
                        mesh.subMeshCount;
                     submesh++)
                {
                    Material material =
                        materials[
                            Mathf.Min(
                                submesh,
                                materials.Length - 1)];

                    if (material == null)
                        continue;

                    result.Add(
                        new RenderPart
                        {
                            Mesh =
                                mesh,
                            Material =
                                material,
                            SubmeshIndex =
                                submesh,
                            LocalMatrix =
                                local
                        });
                }
            }

            return result;
        }

        private static Matrix4x4 RelativeMatrix(
            Transform value,
            Transform root)
        {
            if (value == root)
                return Matrix4x4.identity;

            var chain =
                new List<Transform>();

            Transform current =
                value;

            while (current != null &&
                   current != root)
            {
                chain.Add(current);
                current = current.parent;
            }

            if (current != root)
            {
                throw new InvalidDataException(
                    "Grass mesh transform is not below its prefab root.");
            }

            Matrix4x4 result =
                Matrix4x4.identity;

            for (int i = chain.Count - 1;
                 i >= 0;
                 i--)
            {
                Transform transform =
                    chain[i];

                result *=
                    Matrix4x4.TRS(
                        transform.localPosition,
                        transform.localRotation,
                        transform.localScale);
            }

            return result;
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
                    "Grass coordinate " +
                    ordinal +
                    " has a degenerate orientation basis.");
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
                        "Grass coordinate " +
                        ordinal +
                        " has an unusable orientation basis.");
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
                worldExtents * 2f);
        }

        private static Vector3 Abs(
            Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }
    }
}
