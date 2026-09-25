using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class OriginalVaniPersistenceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OriginalEagleSurvivesBatchPersistenceSharedGroupsAndPrefabReload(bool batched)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            const int testMap = 9999;
            const string output = "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Map9999";
            Assert.IsFalse(Directory.Exists(output), "Refusing to overwrite pre-existing test output.");
            var observer = new GameObject("VaniTestObserver");
            var cameraObject = new GameObject("VaniTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            LegacyWorldVaniRuntime runtime = null;
            try
            {
                var group = new LegacyWldNameCoordinateGroup();
                group.Names.Add("eagle.vani");
                group.Coordinates.Add(new LegacyWldCoordinate
                { Id = 0, Position = new Vector3(5, 9, 7), Forward = Vector3.forward, Up = Vector3.up });
                if (batched)
                {
                    using (LegacyAssetWriteBatch.Begin())
                        runtime = LegacyVaniBatchBuilder.Create(corpus, group, group, observer.transform, camera, testMap);
                }
                else runtime = LegacyVaniBatchBuilder.Create(corpus, group, group, observer.transform, camera, testMap);
                Assert.AreEqual(2, runtime.LogicalPlacementCount);
                Assert.AreEqual(2, runtime.Resources.Count);
                var first = runtime.Resources[0];
                var second = runtime.Resources[1];
                Assert.AreSame(first.meshParts[0].material, second.meshParts[0].material);
                Assert.AreSame(first.meshParts[0].frames[0], second.meshParts[0].frames[0]);
                Assert.AreEqual(64, first.frameCount);
                Assert.AreEqual(66, first.frameIntervalMilliseconds);
                Assert.IsFalse(first.frameTimingCalibrated);
                var authored = LegacyVaniParser.Parse(corpus.Resolve("DATA_Español/entity/vani/eagle.vani"));
                for (int f = 0; f < first.frameCount; f++)
                {
                    var mesh = first.meshParts[0].frames[f];
                    Assert.IsNotNull(mesh); Assert.IsTrue(AssetDatabase.Contains(mesh));
                    Assert.IsTrue(AssetDatabase.GetAssetPath(mesh).StartsWith(output + "/VAni/"));
                    var vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                        Assert.AreEqual(authored.Meshes[0].Vertices[v].Frames[f].Position, vertices[v]);
                }
                var material = first.meshParts[0].material;
                Assert.IsTrue(AssetDatabase.Contains(material));
                Assert.IsTrue(AssetDatabase.GetAssetPath(material.mainTexture).EndsWith(".png"));
                Assert.IsEmpty(Directory.GetFiles(output, "*.dds", SearchOption.AllDirectories));
                string prefabPath = output + "/VaniEvidence.prefab";
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(runtime.gameObject, prefabPath));
                Object.DestroyImmediate(runtime.gameObject); runtime = null;
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<LegacyWorldVaniRuntime>();
                foreach (var resource in loaded.Resources)
                {
                    Assert.AreEqual(64, resource.meshParts[0].frames.Length);
                    Assert.IsNotNull(resource.meshParts[0].material);
                    foreach (var mesh in resource.meshParts[0].frames) Assert.IsNotNull(mesh);
                }
            }
            finally
            {
                if (runtime != null) Object.DestroyImmediate(runtime.gameObject);
                Object.DestroyImmediate(observer); Object.DestroyImmediate(cameraObject);
                AssetDatabase.DeleteAsset(output);
            }
        }
    }
}
