using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class ParityScenarioBootstrap : MonoBehaviour
    {
        private static bool _created;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_created)
                return;

            _created = true;

            GameObject go =
                new GameObject(
                    "DreynoxParityAutomation");

            DontDestroyOnLoad(go);

            go.AddComponent<
                ParityScenarioBootstrap>();
        }

        private IEnumerator Start()
        {
            string[] args =
                Environment.GetCommandLineArgs();

            string scenario =
                ValueAfter(
                    args,
                    "--parity-scene");

            string targetScene =
                ResolveSceneName(
                    scenario);

            if (!string.IsNullOrWhiteSpace(
                    targetScene) &&
                !string.Equals(
                    SceneManager.GetActiveScene().name,
                    targetScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        targetScene,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    Debug.LogError(
                        "Dreynox parity automation could not load scene '" +
                        targetScene +
                        "'.");

                    yield break;
                }

                while (!operation.isDone)
                    yield return null;

                yield return null;
            }

            ParityScreenshotCapture existing =
                FindFirstObjectByType<
                    ParityScreenshotCapture>();

            if (existing == null)
            {
                gameObject.AddComponent<
                    ParityScreenshotCapture>();
            }
        }

        public static string ResolveSceneName(
            string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(
                    scenarioId))
                return string.Empty;

            switch (
                scenarioId.Trim()
                    .ToLowerInvariant())
            {
                case "login":
                    return "LegacyLoginParity";

                case "character-selection":
                case "character-select":
                    return "LegacyCharacterSelectParity";

                case "character-editor":
                case "mode-selected":
                case "character-make":
                    return "LegacyCharacterMakeParity";

                case "world-entry":
                case "world-loaded":
                case "after-movement":
                case "map0":
                    return "CanonicalMap000World";

                default:
                    return scenarioId.Trim();
            }
        }

        private static string ValueAfter(
            string[] args,
            string key)
        {
            if (args == null)
                return string.Empty;

            for (int i = 0;
                 i < args.Length - 1;
                 i++)
            {
                if (string.Equals(
                        args[i],
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }
    }
}
