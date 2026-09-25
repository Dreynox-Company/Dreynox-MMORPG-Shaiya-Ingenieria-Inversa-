using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using UnityEngine;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>Runtime-only ANI evaluator; does not call Editor AnimationUtility or construct non-legacy curves.</summary>
    public sealed class LocalAniPlayer : MonoBehaviour
    {
        private LegacyAniFile animation;
        private Transform[] bones;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private double elapsed;
        public bool Paused { get; set; }
        public double TimeSeconds => elapsed;
        public void Configure(LegacyAniFile source, Transform[] skeleton)
        {
            if (source == null || skeleton == null || source.Bones.Count != skeleton.Length) throw new ArgumentException("ANI/skeleton mismatch.");
            animation = source; bones = skeleton; elapsed = 0;
            restPositions = new Vector3[bones.Length]; restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = source.Bones[i];
                Matrix4x4 world = LegacyCoordinateBridge.Matrix(bone.AbsoluteBaseMatrix);
                Matrix4x4 local = bone.ParentBoneIndex < 0 ? world : LegacyCoordinateBridge.Matrix(source.Bones[bone.ParentBoneIndex].AbsoluteBaseMatrix).inverse * world;
                restPositions[i] = local.GetColumn(3); restRotations[i] = local.rotation.normalized;
            }
            Sample(0);
        }
        private void LateUpdate()
        {
            if (animation == null || Paused) return;
            elapsed += Time.deltaTime;
            Sample(elapsed);
        }
        public void Sample(double timeSeconds)
        {
            if (animation == null) return;
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds)) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            double span = (double)animation.EndKeyframe - animation.StartKeyframe;
            double relative = Math.Max(0, timeSeconds) * LegacyAniFile.FramesPerSecond;
            double frame = animation.StartKeyframe + (span > 0 ? relative % span : 0);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = animation.Bones[i];
                bones[i].localPosition = PositionAt(b.Translations, frame, restPositions[i]);
                bones[i].localRotation = RotationAt(b.Rotations, frame, restRotations[i]);
            }
        }
        public static Vector3 PositionAt(IReadOnlyList<LegacyAniTranslationFrame> keys, double frame, Vector3 fallback)
        {
            if (keys.Count == 0) return fallback;
            int a = FloorTranslation(keys, frame), b = Math.Min(keys.Count - 1, a + 1);
            float t = Blend(keys[a].Frame, keys[b].Frame, frame);
            return LegacyCoordinateBridge.Position(Vector3.LerpUnclamped(keys[a].Translation, keys[b].Translation, t));
        }
        public static Quaternion RotationAt(IReadOnlyList<LegacyAniRotationFrame> keys, double frame, Quaternion fallback)
        {
            if (keys.Count == 0) return fallback;
            int a = FloorRotation(keys, frame), b = Math.Min(keys.Count - 1, a + 1);
            float t = Blend(keys[a].Frame, keys[b].Frame, frame);
            // Shortest-path spherical sampling; compare to game.exe before certifying exact interpolation.
            return LegacyCoordinateBridge.Rotation(Quaternion.Slerp(keys[a].Rotation, keys[b].Rotation, t));
        }
        private static int FloorTranslation(IReadOnlyList<LegacyAniTranslationFrame> keys, double frame)
        {
            int lo = 0, hi = keys.Count;
            while (lo < hi) { int m = lo + (hi - lo) / 2; if (keys[m].Frame <= frame) lo = m + 1; else hi = m; }
            return Math.Max(0, lo - 1);
        }
        private static int FloorRotation(IReadOnlyList<LegacyAniRotationFrame> keys, double frame)
        {
            int lo = 0, hi = keys.Count;
            while (lo < hi) { int m = lo + (hi - lo) / 2; if (keys[m].Frame <= frame) lo = m + 1; else hi = m; }
            return Math.Max(0, lo - 1);
        }
        private static float Blend(uint start, uint end, double frame) => end <= start ? 0 : Mathf.Clamp01((float)((frame - start) / (end - start)));
    }
}
