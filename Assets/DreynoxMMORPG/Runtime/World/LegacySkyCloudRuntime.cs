using System;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public sealed class LegacySkyCloudRuntime : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField] private Renderer skyRenderer;
        [SerializeField] private Renderer cloudLayer1;
        [SerializeField] private Renderer cloudLayer2;

        [Header("Cloud placement")]
        [SerializeField, Min(10f)] private float cloudHeight1 = 420f;
        [SerializeField, Min(10f)] private float cloudHeight2 = 470f;

        [Header("Parity calibration")]
        [SerializeField] private bool scrollTimingCalibrated;
        [SerializeField] private Vector2 cloudScroll1;
        [SerializeField] private Vector2 cloudScroll2;

        private MaterialPropertyBlock _skyProperties;
        private MaterialPropertyBlock _cloud1Properties;
        private MaterialPropertyBlock _cloud2Properties;

        private static readonly int BaseMapStId =
            Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId =
            Shader.PropertyToID("_MainTex_ST");

        public bool ScrollTimingCalibrated =>
            scrollTimingCalibrated;

        public void Configure(
            Transform followObserver,
            Renderer sky,
            Renderer firstCloudLayer,
            Renderer secondCloudLayer,
            Vector2 firstScroll,
            Vector2 secondScroll,
            bool isScrollTimingCalibrated)
        {
            observer = followObserver;
            skyRenderer = sky;
            cloudLayer1 = firstCloudLayer;
            cloudLayer2 = secondCloudLayer;
            cloudScroll1 = firstScroll;
            cloudScroll2 = secondScroll;
            scrollTimingCalibrated =
                isScrollTimingCalibrated;

            EnsureBlocks();
            FollowObserver();
            ApplyCloudOffsets();
        }

        private void Awake()
        {
            EnsureBlocks();
            FollowObserver();
            ApplyCloudOffsets();
        }

        private void LateUpdate()
        {
            FollowObserver();

            if (scrollTimingCalibrated)
                ApplyCloudOffsets();
        }

        public void SetCalibratedScroll(
            Vector2 firstLayerUnitsPerSecond,
            Vector2 secondLayerUnitsPerSecond)
        {
            cloudScroll1 = firstLayerUnitsPerSecond;
            cloudScroll2 = secondLayerUnitsPerSecond;
            scrollTimingCalibrated = true;
            ApplyCloudOffsets();
        }

        private void FollowObserver()
        {
            if (observer == null)
                return;

            Vector3 center =
                observer.position;

            if (skyRenderer != null)
            {
                Transform sky =
                    skyRenderer.transform;

                sky.position = center;
            }

            if (cloudLayer1 != null)
            {
                Transform cloud =
                    cloudLayer1.transform;

                cloud.position =
                    new Vector3(
                        center.x,
                        cloudHeight1,
                        center.z);
            }

            if (cloudLayer2 != null)
            {
                Transform cloud =
                    cloudLayer2.transform;

                cloud.position =
                    new Vector3(
                        center.x,
                        cloudHeight2,
                        center.z);
            }
        }

        private void ApplyCloudOffsets()
        {
            float time =
                scrollTimingCalibrated
                    ? Time.time
                    : 0f;

            ApplyOffset(
                cloudLayer1,
                ref _cloud1Properties,
                cloudScroll1 * time);

            ApplyOffset(
                cloudLayer2,
                ref _cloud2Properties,
                cloudScroll2 * time);
        }

        private static void ApplyOffset(
            Renderer renderer,
            ref MaterialPropertyBlock block,
            Vector2 offset)
        {
            if (renderer == null)
                return;

            if (block == null)
                block =
                    new MaterialPropertyBlock();

            renderer.GetPropertyBlock(block);

            Vector4 st =
                new Vector4(
                    1f,
                    1f,
                    offset.x,
                    offset.y);

            Material material =
                renderer.sharedMaterial;

            if (material != null &&
                material.HasProperty(BaseMapStId))
            {
                block.SetVector(
                    BaseMapStId,
                    st);
            }

            if (material != null &&
                material.HasProperty(MainTexStId))
            {
                block.SetVector(
                    MainTexStId,
                    st);
            }

            renderer.SetPropertyBlock(block);
        }

        private void EnsureBlocks()
        {
            if (_skyProperties == null)
                _skyProperties =
                    new MaterialPropertyBlock();

            if (_cloud1Properties == null)
                _cloud1Properties =
                    new MaterialPropertyBlock();

            if (_cloud2Properties == null)
                _cloud2Properties =
                    new MaterialPropertyBlock();
        }
    }
}
