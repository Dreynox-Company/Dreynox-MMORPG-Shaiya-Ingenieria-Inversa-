using Dreynox.Mmorpg.Editor.Build;
using Dreynox.Mmorpg.Editor.ProjectTools;
using Dreynox.Mmorpg.LocalData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LocalDataBuildGuardTests
    {
        private static readonly string[] ShippingScenes = { "Assets/Game/World.unity" };
        private static readonly string[] DeveloperScenes = { LocalDataSceneBuilder.ScenePath };

        [Test]
        public void ReleaseAcceptsPreconvertedSceneList()
        {
            Assert.DoesNotThrow(() => LocalDataBuildGuard.ValidateRequest(
                ShippingScenes, BuildOptions.CompressWithLz4HC, false, "client-release"));
        }

        [Test]
        public void DeveloperLoaderRequiresExplicitOptInAndDevelopmentBuild()
        {
            Assert.DoesNotThrow(() => LocalDataBuildGuard.ValidateRequest(
                DeveloperScenes, BuildOptions.Development, true, "local-data-character-qualification"));
            Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateRequest(
                DeveloperScenes, BuildOptions.None, true, "local-data-character-qualification"));
            Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateRequest(
                DeveloperScenes, BuildOptions.Development, false, "parity-lab"));
        }

        [Test]
        public void ReleaseRejectsDeveloperBuildEvenWithOrdinaryScenes()
        {
            Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateRequest(
                ShippingScenes, BuildOptions.Development, false, "client-release"));
            Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateRequest(
                DeveloperScenes, BuildOptions.Development, true, "client-release"));
        }

        [Test]
        public void EmptySceneListCannotSilentlyBuildTheCurrentEditorScene()
        {
            Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateRequest(
                new string[0], BuildOptions.None, false, "client-release"));
        }

        [Test]
        public void InactiveDeveloperComponentIsStillRejectedInRelease()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Inactive developer loader");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<LocalCharacterLoader>();
            go.SetActive(false);
            try
            {
                Assert.Throws<BuildFailedException>(() => LocalDataBuildGuard.ValidateScene(scene, false));
                Assert.DoesNotThrow(() => LocalDataBuildGuard.ValidateScene(scene, true));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
