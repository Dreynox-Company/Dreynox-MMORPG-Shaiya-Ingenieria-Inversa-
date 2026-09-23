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
                : maxKeyframe / framesPerSecond;

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
            }
        }
    }
}
