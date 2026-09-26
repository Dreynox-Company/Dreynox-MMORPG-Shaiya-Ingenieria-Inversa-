using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyCharacterPreviewSceneBuilder
    {
        public const string ScenePath =
            "Assets/DreynoxMMORPG/Game/Scenes/Generated/" +
            "CanonicalHumanMale003Preview.unity";

        private const string PrefabPath =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/" +
            "Characters/HumanMale003/Prefabs/" +
            "HumanMale003_Canonical.prefab";

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Human Male Preview")]
        public static void Build()
        {
            LegacyCharacterImporter.ImportCanonicalHumanMale003();

            GameObject prefab =
                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
                throw new System.InvalidOperationException(
                    "Canonical character prefab was not generated.");

            EnsureFolder(
                "Assets/DreynoxMMORPG/Game/Scenes/Generated");

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            GameObject runtime =
                new GameObject("DreynoxRuntime");

            runtime.AddComponent<
                Dreynox.Mmorpg.App.DreynoxBootstrap>();

            runtime.AddComponent<ParityScreenshotCapture>();

            GameObject actor =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (actor == null)
                throw new System.InvalidOperationException(
                    "Could not instantiate canonical character prefab.");

            actor.name = "Player_HumanMale003";
            actor.transform.position = Vector3.zero;

            ShaiyaClientActor clientActor =
                actor.GetComponent<ShaiyaClientActor>();

            if (clientActor == null)
                throw new System.InvalidOperationException(
                    "Canonical actor does not contain ShaiyaClientActor.");

            GameObject ground =
                GameObject.CreatePrimitive(PrimitiveType.Plane);

            ground.name = "ParityGround";
            ground.transform.localScale =
                new Vector3(12f, 1f, 12f);

            Material groundMaterial =
                CreatePreviewMaterial(
                    "GroundPreviewMaterial",
                    new Color(0.20f, 0.31f, 0.16f, 1f));

            ground.GetComponent<Renderer>().sharedMaterial =
                groundMaterial;

            GameObject cameraObject =
                new GameObject("Main Camera");

            cameraObject.tag = "MainCamera";

            Camera camera =
                cameraObject.AddComponent<Camera>();

            cameraObject.AddComponent<AudioListener>();

            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1200f;

            ShaiyaThirdPersonCamera cameraController =
                cameraObject.AddComponent<ShaiyaThirdPersonCamera>();

            cameraController.SetTarget(actor.transform);
            cameraObject.transform.position =
                new Vector3(0f, 3.2f, -6.5f);

            clientActor.SetCameraReference(
                cameraObject.transform);

            GameObject sun =
                new GameObject("Sun");

            Light light =
                sun.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;

            sun.transform.rotation =
                Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Trilight;

            RenderSettings.ambientIntensity = 1f;

            GameObject hud =
                new GameObject("ParityHUD");

            ParityDebugHud debugHud =
                hud.AddComponent<ParityDebugHud>();

            debugHud.Bind(clientActor);

            EditorSceneManager.SaveScene(
                scene,
                ScenePath);

            Selection.activeObject = actor;

            Debug.Log(
                "Dreynox MMORPG: real 3DC/ANI character preview " +
                "generated at " + ScenePath + ".");
        }

        private static Material CreatePreviewMaterial(
            string name,
            Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            Material material =
                new Material(shader)
                {
                    name = name,
                    color = color
                };

            return material;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
