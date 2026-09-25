using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dreynox.Mmorpg.Editor.Build
{
    public static class WorldRenderPipelineSetup
    {
        private const string Folder = "Assets/DreynoxMMORPG/GeneratedRendering";
        private const string AssetPath = Folder + "/WorldURP.asset";

        public static void EnsureConfigured()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "GeneratedRendering");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create();
                pipeline.name = "Dreynox_World_URP";
                AssetDatabase.CreateAsset(pipeline, AssetPath);
                // Unity's public factory initializes renderer shaders/postprocess resources.
                // This generated renderer belongs to this build workspace, not to DATA.
                var renderer = pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer);
                if (renderer == null) throw new InvalidOperationException("URP renderer creation failed.");
                AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(renderer), Folder + "/WorldRenderer.asset");
            }
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 110f;
            pipeline.shadowCascadeCount = 4;
            pipeline.cascade4Split = new Vector3(0.08f, 0.25f, 0.55f);
            pipeline.cascadeBorder = 0.2f;
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.supportsHDR = true;
            pipeline.renderScale = 1f;
            pipeline.useSRPBatcher = true;
            // URP 17 exposes these settings read-only publicly. Fail on a package
            // schema change rather than claiming soft shadows are enabled.
            var settings = new SerializedObject(pipeline);
            var soft = settings.FindProperty("m_SoftShadowsSupported");
            var shadows = settings.FindProperty("m_MainLightShadowsSupported");
            if (soft == null || shadows == null)
                throw new InvalidOperationException("URP shadow schema changed; requalify the rendering profile.");
            soft.boolValue = true;
            shadows.boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (!pipeline.supportsSoftShadows || !pipeline.supportsMainLightShadows)
                throw new InvalidOperationException("Enhanced shadow configuration was not applied.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // All generated quality tiers must use the same configured renderer.
            int previous = QualitySettings.GetQualityLevel();
            for (int i=0;i<QualitySettings.names.Length;i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(previous, false);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            if (GraphicsSettings.currentRenderPipeline != pipeline)
                throw new InvalidOperationException("World URP selection did not persist.");
            Debug.Log("DREYNOX_WORLD_URP_CONFIGURED " + AssetPath);
        }
    }
}
