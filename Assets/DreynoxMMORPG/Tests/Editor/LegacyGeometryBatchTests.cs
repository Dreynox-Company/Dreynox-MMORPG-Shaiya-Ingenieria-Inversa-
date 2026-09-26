using System;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyGeometryBatchTests
    {
        private string folder;
        [SetUp] public void Setup()
        {
            const string parent = "Assets/DreynoxMMORPG/LocalLegacyGenerated";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "LocalLegacyGenerated");
            string name = "GeometryBatchTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(parent, name); folder = parent + "/" + name;
        }
        [TearDown] public void Cleanup() { if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder); }
        [Test]
        public void ImportedMeshClipsMaterialAndCatalogArePersistentBeforePrefabSave()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            var root = new GameObject("persistent batch fixture");
            var mesh = Triangle(1);
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            var material = new Material(shader);
            var catalog = ScriptableObject.CreateInstance<AnimationStateCatalog>();
            catalog.ReplaceEntries(new[] { new AnimationStateCatalog.Entry { semanticState = "walk", clip = clip, playbackSpeed = 1 } });
            try
            {
                using (LegacyAssetWriteBatch.Begin())
                {
                    LegacyAssetWriteBatch.CreateAsset(material, folder + "/surface.mat");
                    LegacyAssetWriteBatch.CreateAsset(catalog, folder + "/catalog.asset");
                    LegacyAssetWriteBatch.CreateAsset(mesh, folder + "/mesh.asset");
                    LegacyAssetWriteBatch.CreateAsset(clip, folder + "/walk.anim");
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterial = material;
                    string prefabPath = folder + "/model.prefab";
                    Assert.IsNotNull(LegacyAssetWriteBatch.SaveAsPrefabAsset(root, prefabPath));
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Assert.AreEqual(3, saved.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                    Assert.IsTrue(AssetDatabase.Contains(saved.GetComponent<MeshRenderer>().sharedMaterial));
                    var savedCatalog = AssetDatabase.LoadAssetAtPath<AnimationStateCatalog>(folder + "/catalog.asset");
                    Assert.IsTrue(savedCatalog.TryGet("walk", out AnimationClip actual));
                    Assert.AreEqual(folder + "/walk.anim", AssetDatabase.GetAssetPath(actual));
                    Assert.AreEqual(1f, actual.length, 0.001f);
                }
                Assert.IsTrue(AssetDatabase.Contains(mesh));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void ReplacedGeometryIsImportedBeforeBeingReturnedToCaller()
        {
            string path = folder + "/mesh.asset";
            AssetDatabase.CreateAsset(Triangle(1), path);
            using (LegacyAssetWriteBatch.Begin())
            {
                var replacement = Triangle(2);
                LegacyAssetWriteBatch.DeleteAsset(path);
                LegacyAssetWriteBatch.CreateAsset(replacement, path);
                Assert.AreSame(replacement, LegacyAssetWriteBatch.LoadAssetAtPath<Mesh>(path));
                LegacyAssetWriteBatch.Flush();
                var loaded = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.AreEqual(2f, loaded.vertices[1].x);
                Assert.IsTrue(AssetDatabase.Contains(loaded));
            }
        }
        [Test]
        public void FailedStagingDoesNotReplayItsWritesOnDisposeOrLeaveDatabasePaused()
        {
            var batch = LegacyAssetWriteBatch.Begin();
            var mesh = Triangle(1);
            LegacyAssetWriteBatch.CreateAsset(mesh, folder + "/destroyed.asset");
            Object.DestroyImmediate(mesh);
            try
            {
                Assert.Throws<InvalidOperationException>(() => LegacyAssetWriteBatch.Flush());
                Assert.DoesNotThrow(() => batch.Dispose());
                using (LegacyAssetWriteBatch.Begin())
                    LegacyAssetWriteBatch.CreateAsset(Triangle(1), folder + "/recovery.asset");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/recovery.asset"));
            }
            finally { batch.Dispose(); }
        }
        private static Mesh Triangle(float size)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right * size, Vector3.up * size };
            mesh.triangles = new[] { 0, 1, 2 }; mesh.RecalculateNormals();
            return mesh;
        }
    }
}
