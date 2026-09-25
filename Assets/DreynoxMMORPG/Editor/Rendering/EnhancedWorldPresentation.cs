using System;
using System.IO;
using Dreynox.Mmorpg.Rendering;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dreynox.Mmorpg.Editor.Rendering
{
    /// <summary>Explicit PC enhancement values. They are art direction, not claimed ps0032 constants.</summary>
    public static class EnhancedWorldPresentation
    {
        public const string Revision = "enhanced-pc-01";
        public const string SkyShader = "Dreynox/Enhanced/LegacyAtmosphere";
        [MenuItem("Dreynox MMORPG/Rendering/Apply enhanced presentation to generated world")]
        public static void ApplyToCurrentWorld()
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("World camera is missing.");
            ConfigureCamera(camera);
            PrepareColorBatch();
            NormalizeGeneratedMaterials();
            ReplaceLegacySkyPlanes();
            foreach (Light sun in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (sun.type == LightType.Directional && sun.gameObject.scene == camera.gameObject.scene)
                    ConfigureLighting(sun);
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != camera.gameObject.scene) continue;
                foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
                {
                    if (layer == null || layer.diffuseTexture == null)
                        throw new InvalidOperationException("World terrain has an untextured layer.");
                    string path = AssetDatabase.GetAssetPath(layer.diffuseTexture);
                    if (string.Equals(System.IO.Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
                        layer.diffuseTexture = LegacyColorTextureImporter.Import(path, path);
                    ConfigureTerrainLayer(layer);
                    EditorUtility.SetDirty(layer);
                }
            }
            CreateToneMappingVolume();
            Debug.Log("DREYNOX_ENHANCED_PRESENTATION " + Revision + " HDR=on MSAA=4 neutral-tone-map soft-shadows");
        }
        private static void PrepareColorBatch()
        {
            LegacyColorTextureImporter.ClearSessionCache();
            var requests = new System.Collections.Generic.List<LegacyColorTextureImporter.Request>();
            const string root = "Assets/DreynoxMMORPG/LocalLegacyGenerated";
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") continue;
                if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f) continue;
                string path = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"));
                if (string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
                    requests.Add(new LegacyColorTextureImporter.Request(path,path));
            }
            foreach (Terrain terrain in Terrain.activeTerrains)
                foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
                {
                    if (layer == null || layer.diffuseTexture == null) continue;
                    string path = AssetDatabase.GetAssetPath(layer.diffuseTexture);
                    if (string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
                        requests.Add(new LegacyColorTextureImporter.Request(path,path));
                }
            LegacyColorTextureImporter.Prepare(requests);
        }

        private static void NormalizeGeneratedMaterials()
        {
            const string root = "Assets/DreynoxMMORPG/LocalLegacyGenerated";
            if (!AssetDatabase.IsValidFolder(root)) throw new InvalidOperationException("Generated original content is missing.");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") continue;
                // Effect blend modes and linear lightmaps are not albedo and stay untouched.
                if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f) continue;
                Texture source = material.GetTexture("_BaseMap");
                string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
                if (string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
                    material.SetTexture("_BaseMap", LegacyColorTextureImporter.Import(path, path));
                bool cutout = material.IsKeywordEnabled("_ALPHATEST_ON") || material.GetFloat("_AlphaClip") > 0.5f;
                ConfigureOpaque(material, cutout);
                EditorUtility.SetDirty(material); count++;
            }
            Debug.Log("DREYNOX_ENHANCED_ALBEDO materials=" + count + " linear-lightmaps=unchanged");
        }
        private static void ReplaceLegacySkyPlanes()
        {
            foreach (var legacy in UnityEngine.Object.FindObjectsByType<LegacySkyCloudRuntime>(FindObjectsSortMode.None))
            {
                Renderer sky = null, lower = null, upper = null;
                foreach (Renderer renderer in legacy.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.StartsWith("LegacySky_", StringComparison.Ordinal)) sky = renderer;
                    else if (renderer.name == "CloudLayer_1") lower = renderer;
                    else if (renderer.name == "CloudLayer_2") upper = renderer;
                }
                if (sky == null) throw new InvalidOperationException("Authored sky renderer was not generated.");
                Texture2D skyTexture = SkyTexture(sky, TextureWrapMode.Clamp);
                Texture2D lowerTexture = SkyTexture(lower, TextureWrapMode.Repeat);
                Texture2D upperTexture = SkyTexture(upper, TextureWrapMode.Repeat);
                var shader = Shader.Find(SkyShader);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Enhanced atmosphere shader failed validation.");
                var atmosphere = new Material(shader) { name = "Dreynox_AuthoredAtmosphere" };
                atmosphere.SetTexture("_SkyTex", skyTexture);
                atmosphere.SetTexture("_Cloud1", lowerTexture);
                atmosphere.SetTexture("_Cloud2", upperTexture);
                if (lowerTexture == null) atmosphere.SetFloat("_CloudOpacity1", 0f);
                if (upperTexture == null) atmosphere.SetFloat("_CloudOpacity2", 0f);
                string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sky.sharedMaterial)).Replace('\\', '/');
                string assetPath = folder + "/EnhancedAtmosphere.mat";
                AssetDatabase.DeleteAsset(assetPath); AssetDatabase.CreateAsset(atmosphere, assetPath);
                legacy.gameObject.AddComponent<LegacyAtmosphereRuntime>().Configure(atmosphere);
                // These are generated presentation primitives, not original map objects.
                UnityEngine.Object.DestroyImmediate(sky.gameObject);
                if (lower != null) UnityEngine.Object.DestroyImmediate(lower.gameObject);
                if (upper != null) UnityEngine.Object.DestroyImmediate(upper.gameObject);
                UnityEngine.Object.DestroyImmediate(legacy);
            }
        }
        private static Texture2D SkyTexture(Renderer renderer, TextureWrapMode wrap)
        {
            if (renderer == null) return null;
            string path = AssetDatabase.GetAssetPath(renderer.sharedMaterial.GetTexture("_BaseMap"));
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Authored atmosphere texture missing.");
            return LegacyColorTextureImporter.Import(path, path, wrap);
        }
        public static void ConfigureLighting(Light sun)
        {
            if (sun == null) throw new ArgumentNullException(nameof(sun));
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.965f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.3f;
            sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.52f, 0.63f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.33f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.14f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fogColor = new Color(0.48f, 0.56f, 0.64f);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }
        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.08f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // MSAA comes from the URP asset, no extra blur.
        }
        public static void ConfigureOpaque(Material material, bool alphaClip)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            Set(material, "_Surface", 0f); Set(material, "_ZWrite", 1f);
            Set(material, "_SrcBlend", (float)BlendMode.One);
            Set(material, "_DstBlend", (float)BlendMode.Zero);
            Set(material, "_Metallic", 0f);
            Set(material, "_Smoothness", 0.18f);
            Set(material, "_SmoothnessTextureChannel", 0f);
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            Set(material, "_AlphaClip", alphaClip ? 1f : 0f);
            Set(material, "_Cutoff", 0.45f);
            if (alphaClip) material.EnableKeyword("_ALPHATEST_ON");
            else material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.enableInstancing = true;
        }
        public static void ConfigureTerrainLayer(TerrainLayer layer)
        {
            layer.metallic = 0f;
            layer.smoothness = 0.1f;
            // Source diffuse alpha is coverage, never a physically authored smoothness map.
            layer.diffuseRemapMin = new Vector4(0, 0, 0, 1);
            layer.diffuseRemapMax = new Vector4(1, 1, 1, 1);
        }
        public static void CreateToneMappingVolume()
        {
            const string folder = "Assets/DreynoxMMORPG/GeneratedRendering";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "GeneratedRendering");
            const string path = folder + "/EnhancedWorldVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet(out Tonemapping tone))
            {
                tone = profile.Add<Tonemapping>(true);
                AssetDatabase.AddObjectToAsset(tone, profile);
            }
            tone.mode.Override(TonemappingMode.Neutral);
            tone.active = true;
            EditorUtility.SetDirty(tone); EditorUtility.SetDirty(profile);
            GameObject existing = GameObject.Find("Dreynox_EnhancedPresentation");
            var volume = existing != null ? existing.GetComponent<Volume>() : null;
            if (volume == null) volume = new GameObject("Dreynox_EnhancedPresentation").AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 5f; volume.sharedProfile = profile;
        }
        private static void Set(Material material, string name, float value)
        { if (material.HasProperty(name)) material.SetFloat(name, value); }
    }
}
