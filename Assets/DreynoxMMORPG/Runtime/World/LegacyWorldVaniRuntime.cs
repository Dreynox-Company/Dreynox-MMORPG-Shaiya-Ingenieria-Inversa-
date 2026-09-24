using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyVaniMeshFrames
    {
        public Material material;
        public Mesh[] frames =
            Array.Empty<Mesh>();
    }

    [Serializable]
    public sealed class LegacyVaniInstanceBatch
    {
        public Bounds bounds;
        public Matrix4x4[] matrices =
            Array.Empty<Matrix4x4>();

        public int Count =>
            matrices != null
                ? matrices.Length
                : 0;
    }

    [Serializable]
    public sealed class LegacyVaniResourceRuntime
    {
        public string resourceName =
            string.Empty;

        public int frameCount;

        public int frameIntervalMilliseconds;

        public bool frameTimingCalibrated;

        public LegacyVaniMeshFrames[] meshParts =
            Array.Empty<LegacyVaniMeshFrames>();

        public LegacyVaniInstanceBatch[] batches =
            Array.Empty<LegacyVaniInstanceBatch>();

        public int logicalPlacementCount;
    }

    public sealed class LegacyWorldVaniRuntime : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        [SerializeField] private Transform observer;
        [SerializeField] private Camera renderCamera;

        [Header("Culling")]
        [SerializeField, Min(32f)] private float drawDistance = 900f;
        [SerializeField] private bool drawDistanceCalibrated;

        [Header("Rendering")]
        [SerializeField] private bool receiveShadows;
        [SerializeField] private ShadowCastingMode shadowCasting =
            ShadowCastingMode.Off;

        [SerializeField] private List<LegacyVaniResourceRuntime> resources =
            new List<LegacyVaniResourceRuntime>();

        private readonly Plane[] _frustumPlanes =
            new Plane[6];

        public IReadOnlyList<LegacyVaniResourceRuntime> Resources =>
            resources;

        public int LogicalPlacementCount { get; private set; }
        public int RenderedBatchCount { get; private set; }
        public int RenderedInstanceCount { get; private set; }
        public bool DrawDistanceCalibrated => drawDistanceCalibrated;
        public float DrawDistance => drawDistance;

        public void Configure(
            Transform playerObserver,
            Camera camera,
            IEnumerable<LegacyVaniResourceRuntime> source,
            float maximumDistance,
            bool distanceCalibrated)
        {
            observer = playerObserver;
            renderCamera = camera;
            drawDistance =
                Mathf.Max(
                    32f,
                    maximumDistance);
            drawDistanceCalibrated =
                distanceCalibrated;

            resources.Clear();
            LogicalPlacementCount = 0;

            if (source == null)
                return;

            foreach (LegacyVaniResourceRuntime resource in source)
            {
                ValidateResource(resource);

                resources.Add(resource);

                LogicalPlacementCount +=
                    resource.logicalPlacementCount;

                for (int partIndex = 0;
                     partIndex < resource.meshParts.Length;
                     partIndex++)
                {
                    LegacyVaniMeshFrames part =
                        resource.meshParts[partIndex];

                    if (part.material != null)
                        part.material.enableInstancing = true;
                }
            }
        }

        public void SetObserver(
            Transform playerObserver,
            Camera camera)
        {
            observer = playerObserver;
            renderCamera = camera;
        }

        public void SetDrawDistance(
            float maximumDistance,
            bool calibrated)
        {
            drawDistance =
                Mathf.Max(
                    32f,
                    maximumDistance);

            drawDistanceCalibrated =
                calibrated;
        }

        private void LateUpdate()
        {
            RenderedBatchCount = 0;
            RenderedInstanceCount = 0;

            if (observer == null ||
                resources.Count == 0)
                return;

            Camera camera =
                renderCamera != null
                    ? renderCamera
                    : Camera.main;

            if (camera == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(
                camera,
                _frustumPlanes);

            float distanceSqr =
                drawDistance *
                drawDistance;

            Vector3 observerPosition =
                observer.position;

            double time =
                Time.timeAsDouble;

            for (int resourceIndex = 0;
                 resourceIndex < resources.Count;
                 resourceIndex++)
            {
                LegacyVaniResourceRuntime resource =
                    resources[resourceIndex];

                int frame =
                    LegacyVaniAnimationCore
                        .ResolveFrameIndex(
                            time,
                            resource.frameCount,
                            resource.frameIntervalMilliseconds);

                for (int batchIndex = 0;
                     batchIndex < resource.batches.Length;
                     batchIndex++)
                {
                    LegacyVaniInstanceBatch batch =
                        resource.batches[batchIndex];

                    if (batch == null ||
                        batch.matrices == null ||
                        batch.matrices.Length == 0)
                        continue;

                    if (batch.bounds.SqrDistance(
                            observerPosition) >
                        distanceSqr)
                        continue;

                    if (!GeometryUtility.TestPlanesAABB(
                            _frustumPlanes,
                            batch.bounds))
                        continue;

                    for (int partIndex = 0;
                         partIndex < resource.meshParts.Length;
                         partIndex++)
                    {
                        LegacyVaniMeshFrames part =
                            resource.meshParts[partIndex];

                        Mesh mesh =
                            part.frames[frame];

                        Graphics.DrawMeshInstanced(
                            mesh,
                            0,
                            part.material,
                            batch.matrices,
                            batch.matrices.Length,
                            null,
                            shadowCasting,
                            receiveShadows,
                            gameObject.layer,
                            camera,
                            LightProbeUsage.Off,
                            null);

                        RenderedBatchCount++;
                    }

                    RenderedInstanceCount +=
                        batch.matrices.Length;
                }
            }
        }

        private static void ValidateResource(
            LegacyVaniResourceRuntime resource)
        {
            if (resource == null)
                throw new ArgumentNullException(
                    nameof(resource));

            if (resource.frameCount <= 0)
                throw new ArgumentException(
                    "VANI resource has no frames.");

            LegacyVaniAnimationCore
                .FrameDurationSeconds(
                    resource.frameIntervalMilliseconds);

            if (resource.meshParts == null ||
                resource.meshParts.Length == 0)
            {
                throw new ArgumentException(
                    "VANI resource has no renderable mesh parts.");
            }

            for (int partIndex = 0;
                 partIndex < resource.meshParts.Length;
                 partIndex++)
            {
                LegacyVaniMeshFrames part =
                    resource.meshParts[partIndex];

                if (part == null ||
                    part.material == null ||
                    part.frames == null ||
                    part.frames.Length !=
                        resource.frameCount)
                {
                    throw new ArgumentException(
                        "VANI mesh part is incomplete.");
                }

                for (int frame = 0;
                     frame < part.frames.Length;
                     frame++)
                {
                    if (part.frames[frame] == null)
                    {
                        throw new ArgumentException(
                            "VANI frame mesh is null.");
                    }
                }
            }

            if (resource.batches == null)
            {
                throw new ArgumentException(
                    "VANI resource batches are null.");
            }

            int placements = 0;

            for (int i = 0;
                 i < resource.batches.Length;
                 i++)
            {
                LegacyVaniInstanceBatch batch =
                    resource.batches[i];

                if (batch == null ||
                    batch.matrices == null ||
                    batch.matrices.Length == 0)
                    continue;

                if (batch.matrices.Length >
                    MaxInstancesPerDraw)
                {
                    throw new ArgumentException(
                        "VANI batch contains " +
                        batch.matrices.Length +
                        " matrices; Unity instancing limit is " +
                        MaxInstancesPerDraw + ".");
                }

                placements +=
                    batch.matrices.Length;
            }

            if (placements !=
                resource.logicalPlacementCount)
            {
                throw new ArgumentException(
                    "VANI logical placement count mismatch.");
            }
        }
    }
}
