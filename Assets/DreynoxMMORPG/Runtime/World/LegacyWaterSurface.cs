using System;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public sealed class LegacyWaterSurface : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Texture2D[] frames =
            Array.Empty<Texture2D>();
        [SerializeField, Min(0.01f)] private float tileSize = 64f;

        [Header("Parity calibration")]
        [SerializeField, Min(0f)] private float calibratedFramesPerSecond;
        [SerializeField] private bool timingCalibrated;

        private MaterialPropertyBlock _properties;
        private int _lastFrame = -1;

        private static readonly int BaseMapId =
            Shader.PropertyToID("_BaseMap");

        private static readonly int MainTexId =
            Shader.PropertyToID("_MainTex");

        private static readonly int BaseMapStId =
            Shader.PropertyToID("_BaseMap_ST");

        private static readonly int MainTexStId =
            Shader.PropertyToID("_MainTex_ST");

        public float TileSize => tileSize;
        public int FrameCount =>
            frames != null ? frames.Length : 0;
        public bool TimingCalibrated =>
            timingCalibrated;
        public float CalibratedFramesPerSecond =>
            calibratedFramesPerSecond;

        public void Configure(
            Renderer renderer,
            Texture2D[] sourceFrames,
            float waterTileSize,
            float framesPerSecond = 0f,
            bool isTimingCalibrated = false)
        {
            targetRenderer = renderer;
            frames =
                sourceFrames ??
                Array.Empty<Texture2D>();

            tileSize =
                Mathf.Max(
                    0.01f,
                    waterTileSize);

            calibratedFramesPerSecond =
                Mathf.Max(
                    0f,
                    framesPerSecond);

            timingCalibrated =
                isTimingCalibrated &&
                calibratedFramesPerSecond > 0f;

            _lastFrame = -1;
            ApplyFrame(0);
            ApplyTiling();
        }

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            _properties =
                new MaterialPropertyBlock();

            ApplyFrame(0);
            ApplyTiling();
        }

        private void Update()
        {
            if (!timingCalibrated ||
                calibratedFramesPerSecond <= 0f ||
                frames == null ||
                frames.Length <= 1)
                return;

            int frame =
                Mathf.FloorToInt(
                    Time.time *
                    calibratedFramesPerSecond) %
                frames.Length;

            ApplyFrame(frame);
        }

        public void SetCalibratedTiming(
            float framesPerSecond)
        {
            calibratedFramesPerSecond =
                Mathf.Max(
                    0f,
                    framesPerSecond);

            timingCalibrated =
                calibratedFramesPerSecond > 0f;
        }

        private void ApplyFrame(int index)
        {
            if (targetRenderer == null ||
                frames == null ||
                frames.Length == 0)
                return;

            index =
                Mathf.Clamp(
                    index,
                    0,
                    frames.Length - 1);

            if (_lastFrame == index)
                return;

            Texture2D texture =
                frames[index];

            if (texture == null)
                return;

            if (_properties == null)
                _properties =
                    new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(
                _properties);

            Material material =
                targetRenderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(BaseMapId))
            {
                _properties.SetTexture(
                    BaseMapId,
                    texture);
            }

            if (material != null &&
                material.HasProperty(MainTexId))
            {
                _properties.SetTexture(
                    MainTexId,
                    texture);
            }

            targetRenderer.SetPropertyBlock(
                _properties);

            _lastFrame = index;
        }

        private void ApplyTiling()
        {
            if (targetRenderer == null)
                return;

            Bounds bounds =
                targetRenderer.bounds;

            float width =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(bounds.size.x));

            float depth =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(bounds.size.z));

            Vector4 st =
                new Vector4(
                    Mathf.Max(
                        1f,
                        width / tileSize),
                    Mathf.Max(
                        1f,
                        depth / tileSize),
                    0f,
                    0f);

            if (_properties == null)
                _properties =
                    new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(
                _properties);

            Material material =
                targetRenderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(BaseMapStId))
            {
                _properties.SetVector(
                    BaseMapStId,
                    st);
            }

            if (material != null &&
                material.HasProperty(MainTexStId))
            {
                _properties.SetVector(
                    MainTexStId,
                    st);
            }

            targetRenderer.SetPropertyBlock(
                _properties);
        }
    }
}
