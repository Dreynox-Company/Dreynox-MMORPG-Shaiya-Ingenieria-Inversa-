using UnityEngine;

namespace Dreynox.Mmorpg.Rendering
{
    /// <summary>Skybox presentation only; no huge meshes, scene depth, colliders or gameplay changes.</summary>
    public sealed class LegacyAtmosphereRuntime : MonoBehaviour
    {
        [SerializeField] private Material template;
        [Tooltip("Art direction in UV/second, not a recovered native timing.")]
        [SerializeField] private Vector2 lowerDrift = new Vector2(0.0015f, 0.0006f);
        [SerializeField] private Vector2 upperDrift = new Vector2(-0.0007f, 0.0004f);
        private Material instance, previous;
        private bool ownsSetting;
        private static readonly int Offset1 = Shader.PropertyToID("_CloudOffset1");
        private static readonly int Offset2 = Shader.PropertyToID("_CloudOffset2");
        public Material Template => template;
        public void Configure(Material material)
        {
            Release();
            if (instance != null) Destroy(instance);
            instance = null; template = material;
            if (Application.isPlaying && isActiveAndEnabled) Bind();
            else RenderSettings.skybox = template;
        }
        private void OnEnable() { if (Application.isPlaying) Bind(); }
        private void Bind()
        {
            if (template == null || ownsSetting) return;
            if (instance == null) instance = new Material(template) { name = template.name + "_Runtime" };
            previous = RenderSettings.skybox;
            RenderSettings.skybox = instance;
            ownsSetting = true;
        }
        private void LateUpdate()
        {
            if (!ownsSetting || instance == null) return;
            double time = Time.timeAsDouble;
            Vector2 a = Offset(lowerDrift, time), b = Offset(upperDrift, time);
            instance.SetVector(Offset1, new Vector4(a.x, a.y, 0, 0));
            instance.SetVector(Offset2, new Vector4(b.x, b.y, 0, 0));
        }
        private static Vector2 Offset(Vector2 speed, double time) =>
            new Vector2((float)((speed.x * time) % 1.0), (float)((speed.y * time) % 1.0));
        private void OnDisable() { Release(); }
        private void Release()
        {
            if (ownsSetting && RenderSettings.skybox == instance) RenderSettings.skybox = previous;
            ownsSetting = false;
        }
        private void OnDestroy() { Release(); if (instance != null) Destroy(instance); }
    }
}
