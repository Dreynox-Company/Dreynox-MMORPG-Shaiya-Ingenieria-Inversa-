using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.ProjectTools
{
    public static class ClientParitySceneBuilder
    {
        public const string ScenePath = "Assets/DreynoxMMORPG/Scenes/ClientParitySandbox.unity";

        [MenuItem("Dreynox MMORPG/Client Parity/Create Refresh Sandbox")]
        public static void CreateSandbox()
        {
            EnsureFolder("Assets/DreynoxMMORPG/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject runtime = new GameObject("DreynoxRuntime");
            runtime.AddComponent<Dreynox.Mmorpg.App.DreynoxBootstrap>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ParityGround";
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_ParityActor";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.42f;
            player.transform.position = new Vector3(0f, 1f, 0f);
            ShaiyaClientActor actor = player.AddComponent<ShaiyaClientActor>();

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            ShaiyaThirdPersonCamera cameraController = cameraGo.AddComponent<ShaiyaThirdPersonCamera>();
            cameraController.SetTarget(player.transform);
            actor.SetCameraReference(cameraGo.transform);
            cameraGo.transform.position = new Vector3(0f, 4f, -7f);

            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            ShaiyaCombatInteraction combat = player.AddComponent<ShaiyaCombatInteraction>();
            combat.Bind(actor, camera);
            ParitySandboxSetup setup = player.AddComponent<ParitySandboxSetup>();
            setup.Bind(actor);

            for (int i = 0; i < 3; i++)
            {
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = "Opponent_" + (i + 1);
                dummy.transform.position = new Vector3(-4f + i * 4f, 1f, 7f);
                ShaiyaCombatTarget target = dummy.AddComponent<ShaiyaCombatTarget>();
                target.Configure(100 + i, 1000 + i * 250);
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CameraCollisionWall";
            wall.transform.position = new Vector3(5f, 1.5f, 1f);
            wall.transform.localScale = new Vector3(1f, 3f, 8f);

            GameObject hud = new GameObject("ParityHUD");
            ParityDebugHud debugHud = hud.AddComponent<ParityDebugHud>();
            debugHud.Bind(actor);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeObject = player;
            Debug.Log("Dreynox MMORPG: sandbox de paridad creado en " + ScenePath);
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
