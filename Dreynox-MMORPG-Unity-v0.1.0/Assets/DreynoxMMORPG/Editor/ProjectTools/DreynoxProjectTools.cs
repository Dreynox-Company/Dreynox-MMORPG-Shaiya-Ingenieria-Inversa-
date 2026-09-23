using Dreynox.Mmorpg.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.ProjectTools
{
    public static class DreynoxProjectTools
    {
        private const string ScenePath = "Assets/DreynoxMMORPG/Scenes/Bootstrap.unity";

        [MenuItem("Dreynox MMORPG/Project/Create Refresh Bootstrap Scene")]
        public static void CreateBootstrapScene()
        {
            EnsureFolder("Assets/DreynoxMMORPG/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("DreynoxRuntime");
            root.AddComponent<DreynoxBootstrap>();

            GameObject camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            Camera cam = camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 6f, -10f);
            camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.farClipPlane = 1000f;

            GameObject light = new GameObject("Sun");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Bootstrap Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeObject = root;
            Debug.Log("Dreynox MMORPG: Bootstrap scene creada en " + ScenePath);
        }

        [MenuItem("Dreynox MMORPG/Rendering/Prepare Selected For Instancing + Occlusion")]
        public static void PrepareSelectedRenderers()
        {
            int renderers = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null) { material.enableInstancing = true; EditorUtility.SetDirty(material); }
                    }
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    flags |= StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                    renderers++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Dreynox MMORPG: {renderers} renderers preparados para GPU Instancing/Occlusion.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
