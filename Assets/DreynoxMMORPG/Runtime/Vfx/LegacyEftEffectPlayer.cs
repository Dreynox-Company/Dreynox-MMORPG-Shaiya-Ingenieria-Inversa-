using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    [Serializable]
    public struct LegacyEftRotationKey
    {
        public Quaternion rotation;
        public float time;
    }

    [Serializable]
    public struct LegacyEftOpacityKey
    {
        public float opacity;
        public float time;
    }

    public sealed class LegacyEftEffectPlayer : MonoBehaviour
    {
        [SerializeField] private LegacyVertexEffectPlayer vertexPlayer;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Vector3 baseLocalPosition;
        [SerializeField] private Quaternion baseLocalRotation = Quaternion.identity;
        [SerializeField] private LegacyEftRotationKey[] rotationKeys =
            Array.Empty<LegacyEftRotationKey>();
        [SerializeField] private LegacyEftOpacityKey[] opacityKeys =
            Array.Empty<LegacyEftOpacityKey>();
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private bool loop;

        private MaterialPropertyBlock _properties;
        private float _time;
        private bool _playing;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        public bool IsPlaying => _playing;
        public float Duration => duration;

        public void Configure(
            LegacyVertexEffectPlayer vertex,
            Renderer renderer,
            Vector3 localPosition,
            Quaternion localRotation,
            LegacyEftRotationKey[] rotations,
            LegacyEftOpacityKey[] opacity,
            float effectDuration,
            bool shouldLoop)
        {
            vertexPlayer = vertex;
            targetRenderer = renderer;
            baseLocalPosition = localPosition;
            baseLocalRotation = localRotation;
            rotationKeys = rotations ?? Array.Empty<LegacyEftRotationKey>();
            opacityKeys = opacity ?? Array.Empty<LegacyEftOpacityKey>();
            duration = Mathf.Max(0f, effectDuration);
            loop = shouldLoop;

            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
        }

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();

            if (vertexPlayer == null)
                vertexPlayer = GetComponent<LegacyVertexEffectPlayer>();

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (!_playing)
                return;

            _time += Time.deltaTime;

            float resolvedDuration =
                Mathf.Max(0.0001f, duration);

            if (loop)
            {
                _time %= resolvedDuration;
            }
            else if (_time >= resolvedDuration)
            {
                _time = resolvedDuration;
                Evaluate(_time);
                _playing = false;
                return;
            }

            Evaluate(_time);
        }

        public void Play()
        {
            _time = 0f;
            _playing = true;
            gameObject.SetActive(true);

            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;

            if (vertexPlayer != null)
                vertexPlayer.Play();

            Evaluate(0f);
        }

        public void Stop(bool deactivate = false)
        {
            _playing = false;
            _time = 0f;

            if (vertexPlayer != null)
                vertexPlayer.Stop();

            ApplyOpacity(0f);

            if (deactivate)
                gameObject.SetActive(false);
        }

        private void Evaluate(float time)
        {
            transform.localRotation =
                EvaluateRotation(time);

            ApplyOpacity(
                EvaluateOpacity(time));
        }

        private Quaternion EvaluateRotation(float time)
        {
            if (rotationKeys == null ||
                rotationKeys.Length == 0)
                return baseLocalRotation;

            int right = 0;

            while (right < rotationKeys.Length &&
                   rotationKeys[right].time < time)
            {
                right++;
            }

            int left =
                Mathf.Clamp(
                    right - 1,
                    0,
                    rotationKeys.Length - 1);

            right =
                Mathf.Clamp(
                    right,
                    0,
                    rotationKeys.Length - 1);

            LegacyEftRotationKey a =
                rotationKeys[left];

            LegacyEftRotationKey b =
                rotationKeys[right];

            if (left == right ||
                b.time <= a.time)
            {
                return baseLocalRotation * a.rotation;
            }

            float t =
                Mathf.InverseLerp(
                    a.time,
                    b.time,
                    time);

            return baseLocalRotation *
                   Quaternion.SlerpUnclamped(
                       a.rotation,
                       b.rotation,
                       t);
        }

        private float EvaluateOpacity(float time)
        {
            if (opacityKeys == null ||
                opacityKeys.Length == 0)
                return 1f;

            int right = 0;

            while (right < opacityKeys.Length &&
                   opacityKeys[right].time < time)
            {
                right++;
            }

            int left =
                Mathf.Clamp(
                    right - 1,
                    0,
                    opacityKeys.Length - 1);

            right =
                Mathf.Clamp(
                    right,
                    0,
                    opacityKeys.Length - 1);

            LegacyEftOpacityKey a =
                opacityKeys[left];

            LegacyEftOpacityKey b =
                opacityKeys[right];

            if (left == right ||
                b.time <= a.time)
            {
                return Mathf.Clamp01(a.opacity);
            }

            float t =
                Mathf.InverseLerp(
                    a.time,
                    b.time,
                    time);

            return Mathf.Clamp01(
                Mathf.LerpUnclamped(
                    a.opacity,
                    b.opacity,
                    t));
        }

        private void ApplyOpacity(float opacity)
        {
            if (targetRenderer == null)
                return;

            if (_properties == null)
                _properties = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(_properties);

            Color color = Color.white;

            Material material =
                targetRenderer.sharedMaterial;

            if (material != null)
            {
                if (material.HasProperty(BaseColorId))
                    color = material.GetColor(BaseColorId);
                else if (material.HasProperty(ColorId))
                    color = material.GetColor(ColorId);
            }

            color.a *= opacity;

            if (material != null &&
                material.HasProperty(BaseColorId))
            {
                _properties.SetColor(
                    BaseColorId,
                    color);
            }

            if (material != null &&
                material.HasProperty(ColorId))
            {
                _properties.SetColor(
                    ColorId,
                    color);
            }

            targetRenderer.SetPropertyBlock(_properties);
        }
    }
}
