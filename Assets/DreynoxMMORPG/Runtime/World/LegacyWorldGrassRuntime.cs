using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyGrassInstanceBatch
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        public Bounds bounds;
        public Matrix4x4[] matrices =
            Array.Empty<Matrix4x4>();

        public int Count =>
            matrices != null
                ? matrices.Length
                : 0;
    }

    public sealed class LegacyWorldGrassRuntime : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        [SerializeField] private Transform observer;
        [SerializeField] private Camera renderCamera;

        [Header("Culling")]
        [SerializeField, Min(32f)] private float drawDistance = 650f;
        [SerializeField] private bool drawDistanceCalibrated;

        [Header("Rendering")]
        [SerializeField] private bool receiveShadows = true;
        [SerializeField] private ShadowCastingMode shadowCasting =
            ShadowCastingMode.Off;
        [SerializeField] private List<LegacyGrassInstanceBatch> batches =
            new List<LegacyGrassInstanceBatch>();
        [SerializeField, Min(0)] private int logicalPlacementCount;

        private readonly Plane[] _frustumPlanes =
            new Plane[6];

        public IReadOnlyList<LegacyGrassInstanceBatch> Batches =>
            batches;

        public int LogicalPlacementCount =>
            logicalPlacementCount;

        public bool DrawDistanceCalibrated =>
            drawDistanceCalibrated;

        public float DrawDistance =>
            drawDistance;

        public int RenderedBatchCount { get; private set; }
        public int RenderedInstanceCount { get; private set; }

        public void Configure(
            Transform playerObserver,
            Camera camera,
            IEnumerable<LegacyGrassInstanceBatch> source,
            int placementCount,
            float maximumDistance,
            bool distanceCalibrated)
        {
            observer = playerObserver;
            renderCamera = camera;
            logicalPlacementCount =
                Mathf.Max(
                    0,
                    placementCount);

            drawDistance =
                Mathf.Max(
                    32f,
                    maximumDistance);

            drawDistanceCalibrated =
                distanceCalibrated;

            batches.Clear();

            if (source == null)
                return;

            foreach (LegacyGrassInstanceBatch batch in source)
            {
                if (batch == null ||
                    batch.mesh == null ||
                    batch.material == null ||
                    batch.matrices == null ||
                    batch.matrices.Length == 0)
                    continue;

                if (batch.matrices.Length >
                    MaxInstancesPerDraw)
                {
                    throw new ArgumentException(
                        "Grass batch contains " +
                        batch.matrices.Length +
                        " matrices; Unity instancing limit is " +
                        MaxInstancesPerDraw + ".");
                }

                batch.material.enableInstancing =
                    true;

                batches.Add(batch);
            }
        }

        public void SetObserver(
            Transform playerObserver,
            Camera camera)
        {
            observer =
                playerObserver;

            renderCamera =
                camera;
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
                batches.Count == 0)
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

            for (int i = 0;
                 i < batches.Count;
                 i++)
            {
                LegacyGrassInstanceBatch batch =
                    batches[i];

                if (batch == null ||
                    batch.mesh == null ||
                    batch.material == null ||
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

                Graphics.DrawMeshInstanced(
                    batch.mesh,
                    batch.submeshIndex,
                    batch.material,
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
                RenderedInstanceCount +=
                    batch.matrices.Length;
            }
        }
    }
}
