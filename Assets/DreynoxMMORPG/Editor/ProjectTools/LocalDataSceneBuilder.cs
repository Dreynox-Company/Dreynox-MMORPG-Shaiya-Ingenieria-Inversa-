using System.IO;
using Dreynox.Mmorpg.LocalData;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ProjectTools
{
    public static class LocalDataSceneBuilder
    {
        public const string ScenePath = "Assets/DreynoxMMORPG/LocalDataGenerated/LocalData.unity";
        [MenuItem("Dreynox MMORPG/Client Parity/Build Local DATA Screen")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string folder = "Assets/DreynoxMMORPG/LocalDataGenerated";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "LocalDataGenerated");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.10f, 0.13f);
            cameraObject.transform.position = new Vector3(0, 1.3f, -5); cameraObject.transform.LookAt(new Vector3(0, 1.1f, 0));
            var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(40, -25, 0);
            var root = new GameObject("LocalCharacterSession", typeof(LocalCharacterLoader), typeof(LocalDataScreen));
            string matPath = folder + "/LocalDataLit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException("URP Lit missing.");
                mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath);
            }
            root.GetComponent<LocalCharacterLoader>().MaterialTemplate = mat;
            root.AddComponent<Dreynox.Mmorpg.Parity.ParityScreenshotCapture>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
