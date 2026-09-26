using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyGpuInstanceBatch
    {
        public Mesh mesh;
        public Material material;
        public Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
    }

    public sealed class LegacyGpuInstanceRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        [SerializeField] private List<LegacyGpuInstanceBatch> batches =
            new List<LegacyGpuInstanceBatch>();

        public IReadOnlyList<LegacyGpuInstanceBatch> Batches => batches;

        public int InstanceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < batches.Count; i++)
                    count += batches[i].matrices?.Length ?? 0;
                return count;
            }
        }

        public void ReplaceBatches(
            IEnumerable<LegacyGpuInstanceBatch> source)
        {
            batches.Clear();

            if (source == null)
                return;

            foreach (LegacyGpuInstanceBatch batch in source)
            {
                if (batch == null ||
                    batch.mesh == null ||
                    batch.material == null ||
                    batch.matrices == null ||
                    batch.matrices.Length == 0)
                {
                    continue;
                }

                if (batch.matrices.Length > MaxInstancesPerDraw)
                {
                    throw new ArgumentException(
                        "GPU instance batches must contain at most " +
                        MaxInstancesPerDraw + " matrices.");
                }

                batch.material.enableInstancing = true;
                batches.Add(batch);
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                LegacyGpuInstanceBatch batch = batches[i];

                if (batch.mesh == null ||
                    batch.material == null ||
                    batch.matrices == null ||
                    batch.matrices.Length == 0)
                {
                    continue;
                }

#pragma warning disable CS0618
                Graphics.DrawMeshInstanced(
                    batch.mesh,
                    0,
                    batch.material,
                    batch.matrices,
                    batch.matrices.Length);
#pragma warning restore CS0618
            }
        }
    }
}
