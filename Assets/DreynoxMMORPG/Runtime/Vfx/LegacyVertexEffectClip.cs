using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    [Serializable]
    public sealed class LegacyVertexEffectFrame
    {
        public int keyframe;
        public Vector3[] positions = Array.Empty<Vector3>();
        public Vector2[] uvs = Array.Empty<Vector2>();
    }

    public sealed class LegacyVertexEffectClip : ScriptableObject
    {
        [SerializeField] private Mesh baseMesh;
        [SerializeField] private Vector3[] basePositions =
            Array.Empty<Vector3>();
        [SerializeField] private Vector2[] baseUvs =
            Array.Empty<Vector2>();
        [SerializeField, Min(0)] private int maxKeyframe;
        [SerializeField, Min(1f)] private float framesPerSecond = 30f;
        [SerializeField] private LegacyVertexEffectFrame[] frames =
            Array.Empty<LegacyVertexEffectFrame>();

        public Mesh BaseMesh => baseMesh;
        public int MaxKeyframe => maxKeyframe;
        public float FramesPerSecond => framesPerSecond;
        public IReadOnlyList<LegacyVertexEffectFrame> Frames => frames;

        public float DurationSeconds =>
            maxKeyframe <= 0
                ? 0f
                : (maxKeyframe + 1f) / framesPerSecond;

        public void Configure(
            Mesh mesh,
            int maximumKeyframe,
            float fps,
            LegacyVertexEffectFrame[] sourceFrames)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (maximumKeyframe < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumKeyframe));
            if (fps <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fps));

            baseMesh = mesh;
            basePositions = mesh.vertices;
            baseUvs = mesh.uv;
            maxKeyframe = maximumKeyframe;
            framesPerSecond = fps;
            frames = sourceFrames ?? Array.Empty<LegacyVertexEffectFrame>();

            for (int i = 0; i < frames.Length; i++)
            {
                LegacyVertexEffectFrame frame = frames[i];

                if (frame == null)
                    throw new InvalidOperationException(
                        "3DE runtime frame cannot be null.");

                if (frame.keyframe < 0 ||
                    frame.keyframe > maxKeyframe)
                {
                    throw new InvalidOperationException(
                        "3DE frame key is outside the clip range.");
                }

                if (frame.positions == null ||
                    frame.positions.Length != mesh.vertexCount)
                {
                    throw new InvalidOperationException(
                        "3DE frame vertex count does not match base mesh.");
                }

                if (frame.uvs == null ||
                    frame.uvs.Length != mesh.vertexCount)
                {
                    throw new InvalidOperationException(
                        "3DE frame UV count does not match base mesh.");
                }

                if (i > 0 &&
                    frames[i - 1].keyframe >
                    frame.keyframe)
                {
                    throw new InvalidOperationException(
                        "3DE frames must be sorted.");
                }
            }
        }

        public Vector3 SampleVertexAtTick(
            int vertexIndex,
            float tick)
        {
            if (vertexIndex < 0 ||
                vertexIndex >= basePositions.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vertexIndex));
            }

            if (frames == null ||
                frames.Length == 0)
            {
                return basePositions[vertexIndex];
            }

            ResolveFrameSample(
                tick,
                out int current,
                out int next,
                out float amount);

            if (current < 0)
                return basePositions[vertexIndex];

            return Vector3.LerpUnclamped(
                frames[current].positions[vertexIndex],
                frames[next].positions[vertexIndex],
                amount);
        }

        public void EvaluateAtTick(
            float tick,
            Vector3[] positions,
            Vector2[] uvs)
        {
            if (positions == null ||
                positions.Length != basePositions.Length)
            {
                throw new ArgumentException(
                    "Output position buffer has the wrong size.",
                    nameof(positions));
            }

            if (uvs == null ||
                uvs.Length != baseUvs.Length)
            {
                throw new ArgumentException(
                    "Output UV buffer has the wrong size.",
                    nameof(uvs));
            }

            if (frames == null ||
                frames.Length == 0)
            {
                Array.Copy(
                    basePositions,
                    positions,
                    basePositions.Length);

                Array.Copy(
                    baseUvs,
                    uvs,
                    baseUvs.Length);

                return;
            }

            ResolveFrameSample(
                tick,
                out int current,
                out int next,
                out float amount);

            if (current < 0)
            {
                Array.Copy(
                    basePositions,
                    positions,
                    basePositions.Length);

                Array.Copy(
                    baseUvs,
                    uvs,
                    baseUvs.Length);

                return;
            }

            LegacyVertexEffectFrame a =
                frames[current];

            LegacyVertexEffectFrame b =
                frames[next];

            for (int i = 0;
                 i < positions.Length;
                 i++)
            {
                positions[i] =
                    Vector3.LerpUnclamped(
                        a.positions[i],
                        b.positions[i],
                        amount);

                uvs[i] =
                    Vector2.LerpUnclamped(
                        a.uvs[i],
                        b.uvs[i],
                        amount);
            }
        }

        private void ResolveFrameSample(
            float tick,
            out int current,
            out int next,
            out float amount)
        {
            current = -1;
            next = -1;
            amount = 0f;

            if (frames == null ||
                frames.Length == 0)
                return;

            if (frames.Length == 1 ||
                maxKeyframe == 0)
            {
                current = 0;
                next = 0;
                return;
            }

            float period =
                maxKeyframe + 1f;

            float localTick =
                PositiveModulo(
                    Mathf.Max(0f, tick),
                    period);

            float firstKey =
                frames[0].keyframe;

            if (localTick < firstKey)
            {
                current =
                    frames.Length - 1;

                next = 0;

                amount =
                    InverseRange(
                        frames[current].keyframe -
                        period,
                        firstKey,
                        localTick);

                return;
            }

            for (int i = 1;
                 i < frames.Length;
                 i++)
            {
                float nextKey =
                    frames[i].keyframe;

                if (localTick < nextKey)
                {
                    current = i - 1;
                    next = i;
                    amount =
                        InverseRange(
                            frames[current].keyframe,
                            nextKey,
                            localTick);
                    return;
                }
            }

            current =
                frames.Length - 1;

            next = 0;

            amount =
                InverseRange(
                    frames[current].keyframe,
                    firstKey + period,
                    localTick);
        }

        private static float PositiveModulo(
            float value,
            float divisor)
        {
            return Mathf.Repeat(
                value,
                divisor);
        }

        private static float InverseRange(
            float min,
            float max,
            float value)
        {
            if (Mathf.Abs(max - min) <=
                0.0001f)
                return 0f;

            return Mathf.Clamp01(
                (value - min) /
                (max - min));
        }
    }
}
