using System;
using Dreynox.Mmorpg.Editor.ProjectTools;
using Dreynox.Mmorpg.LocalData;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.Build
{
    /// <summary>Keep optional raw-DATA diagnostics out of the shipped client.</summary>
    [BuildCallbackVersion(1)]
    public sealed class LocalDataBuildGuard : IProcessSceneWithReport
    {
        public const string DeveloperDefine = "DREYNOX_DEV_DATA";
        public int callbackOrder => -1000;

        public static void ValidateRequest(
            string[] scenes, BuildOptions flags, bool developerDataTools, string buildKind)
        {
            if (scenes == null || scenes.Length == 0)
                throw new BuildFailedException("An explicit scene list is required.");

            bool development = (flags & BuildOptions.Development) != 0;
            bool hasLocalScene = Array.Exists(scenes, IsLocalDataScene);
            if (developerDataTools && (!development || !hasLocalScene))
                throw new BuildFailedException(
                    "Local DATA diagnostics require Development Build and the dedicated LocalData scene.");
            if (hasLocalScene && !developerDataTools)
                throw new BuildFailedException(
                    "The LocalData scene is developer-only. It cannot be included in a regular Player.");
            if (string.Equals(buildKind, "client-release", StringComparison.Ordinal) &&
                (development || developerDataTools || hasLocalScene))
                throw new BuildFailedException(
                    "Client Release must use preconverted content without developer DATA tools.");
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Unity also invokes scene callbacks when entering Play Mode.
            if (report == null) return;
            ValidateScene(scene, (report.summary.options & BuildOptions.Development) != 0);
        }

        public static void ValidateScene(Scene scene, bool development)
        {
            if (development) return;
            if (IsLocalDataScene(scene.path))
                throw new BuildFailedException("LocalData diagnostic scene is forbidden in a release build.");
            foreach (var root in scene.GetRootGameObjects())
            {
                // Include inactive objects; merely hiding a developer panel is not removal.
                if (root.GetComponentInChildren<LocalDataScreen>(true) != null ||
                    root.GetComponentInChildren<LocalCharacterLoader>(true) != null)
                    throw new BuildFailedException(
                        "Developer DATA components found in release scene: " + scene.path);
            }
        }

        private static bool IsLocalDataScene(string path)
        {
            return string.Equals(path?.Replace('\\', '/'),
                LocalDataSceneBuilder.ScenePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
