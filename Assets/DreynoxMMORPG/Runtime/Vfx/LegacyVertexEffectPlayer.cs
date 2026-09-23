using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class LegacyVertexEffectPlayer : MonoBehaviour
    {
        [SerializeField] private LegacyVertexEffectClip clip;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop;

        private MeshFilter _filter;
        private Mesh _runtimeMesh;
        private Vector3[] _positions;
        private Vector2[] _uvs;
        private float _time;
        private bool _playing;

        public LegacyVertexEffectClip Clip => clip;
        public bool IsPlaying => _playing;

        public void Configure(
            LegacyVertexEffectClip value,
            bool shouldLoop)
        {
            clip = value;
            loop = shouldLoop;
            RebuildRuntimeMesh();
        }

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            RebuildRuntimeMesh();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        private void OnDestroy()
        {
            if (_runtimeMesh != null)
                Destroy(_runtimeMesh);
        }

        private void Update()
        {
            if (!_playing ||
                clip == null ||
                clip.Frames.Count == 0 ||
                _runtimeMesh == null)
                return;

            _time += Time.deltaTime;

            float duration =
                Mathf.Max(
                    0.0001f,
                    clip.DurationSeconds);

            if (loop)
            {
                _time %= duration;
            }
            else if (_time >= duration)
            {
                _time = duration;
                _playing = false;
            }

            Evaluate(_time);
        }

        public void Play()
        {
            if (clip == null)
                return;

            _time = 0f;
            _playing = true;
            Evaluate(0f);
        }

        public void Stop()
        {
            _playing = false;
            _time = 0f;
            Evaluate(0f);
        }

        private void RebuildRuntimeMesh()
        {
            if (_filter == null)
                _filter = GetComponent<MeshFilter>();

            if (_runtimeMesh != null)
                DestroyImmediateSafe(_runtimeMesh);

            if (clip == null ||
                clip.BaseMesh == null)
            {
                if (_filter != null)
                    _filter.sharedMesh = null;
                return;
            }

            _runtimeMesh =
                Instantiate(clip.BaseMesh);

            _runtimeMesh.name =
                clip.BaseMesh.name + "_Runtime";

            _runtimeMesh.MarkDynamic();

            _positions =
                new Vector3[_runtimeMesh.vertexCount];

            _uvs =
                new Vector2[_runtimeMesh.vertexCount];

            _filter.sharedMesh = _runtimeMesh;
        }

        private void Evaluate(float seconds)
        {
            if (clip == null ||
                clip.Frames.Count == 0 ||
                _runtimeMesh == null)
                return;

            float key =
                seconds * clip.FramesPerSecond;

            int right = 0;

            while (right < clip.Frames.Count &&
                   clip.Frames[right].keyframe < key)
            {
                right++;
            }

            int left =
                Mathf.Clamp(
                    right - 1,
                    0,
                    clip.Frames.Count - 1);

            right =
                Mathf.Clamp(
                    right,
                    0,
                    clip.Frames.Count - 1);

            LegacyVertexEffectFrame a =
                clip.Frames[left];

            LegacyVertexEffectFrame b =
                clip.Frames[right];

            float t;

            if (left == right ||
                b.keyframe <= a.keyframe)
            {
                t = 0f;
            }
            else
            {
                t =
                    Mathf.InverseLerp(
                        a.keyframe,
                        b.keyframe,
                        key);
            }

            for (int i = 0;
                 i < _positions.Length;
                 i++)
            {
                _positions[i] =
                    Vector3.LerpUnclamped(
                        a.positions[i],
                        b.positions[i],
                        t);

                _uvs[i] =
                    Vector2.LerpUnclamped(
                        a.uvs[i],
                        b.uvs[i],
                        t);
            }

            _runtimeMesh.vertices = _positions;
            _runtimeMesh.uv = _uvs;
            _runtimeMesh.RecalculateBounds();
        }

        private static void DestroyImmediateSafe(
            UnityEngine.Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
