using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Importing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyBatchOrderingTests
    {
        private string folder;
        [SetUp] public void SetUp()
        {
            const string parent = "Assets/DreynoxMMORPG/LocalLegacyGenerated";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "LocalLegacyGenerated");
            string name = "BatchOrder_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(parent, name); folder = parent + "/" + name;
        }
        [TearDown] public void TearDown() { if (folder != null) AssetDatabase.DeleteAsset(folder); }
        [Test]
        public void MissingFolderDeleteCannotDeleteARecreatedFolderOrItsImportedTexture()
        {
            string resource = folder + "/eagle";
            using (LegacyAssetWriteBatch.Begin())
            {
                Assert.IsFalse(LegacyAssetWriteBatch.DeleteAsset(resource));
                AssetDatabase.CreateFolder(folder, "eagle");
                AssetDatabase.CreateFolder(resource, "Meshes");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try { File.WriteAllBytes(resource + "/color.png", texture.EncodeToPNG()); }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(resource + "/color.png", ImportAssetOptions.ForceSynchronousImport);
                var mesh = Triangle();
                LegacyAssetWriteBatch.CreateAsset(mesh, resource + "/Meshes/frame.asset");
                LegacyAssetWriteBatch.Flush();
                Assert.IsTrue(AssetDatabase.IsValidFolder(resource + "/Meshes"));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(resource + "/color.png"));
                Assert.IsTrue(AssetDatabase.Contains(mesh));
            }
        }
        [Test]
        public void ExistingFolderDeletionRunsBeforeRecreationAndPreservesOtherPendingAssets()
        {
            string resource = folder + "/eagle";
            AssetDatabase.CreateFolder(folder, "eagle");
            AssetDatabase.CreateAsset(Triangle(), resource + "/old.asset");
            using (LegacyAssetWriteBatch.Begin())
            {
                LegacyAssetWriteBatch.CreateAsset(Triangle(), folder + "/unrelated.asset");
                Assert.IsTrue(LegacyAssetWriteBatch.DeleteAsset(resource));
                Assert.IsFalse(Directory.Exists(resource));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/unrelated.asset"));
                AssetDatabase.CreateFolder(folder, "eagle");
                LegacyAssetWriteBatch.CreateAsset(Triangle(), resource + "/new.asset");
                LegacyAssetWriteBatch.Flush();
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<Mesh>(resource + "/old.asset"));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(resource + "/new.asset"));
            }
        }
        [Test]
        public void DeletingOnlyStagedFileDoesNotScheduleADeletionAfterRecreation()
        {
            string path = folder + "/frame.asset";
            var discarded = Triangle();
            try
            {
                using (LegacyAssetWriteBatch.Begin())
                {
                    LegacyAssetWriteBatch.CreateAsset(discarded, path);
                    Assert.IsTrue(LegacyAssetWriteBatch.DeleteAsset(path));
                    var replacement = Triangle();
                    LegacyAssetWriteBatch.CreateAsset(replacement, path);
                    LegacyAssetWriteBatch.Flush();
                    Assert.AreSame(replacement, AssetDatabase.LoadAssetAtPath<Mesh>(path));
                }
            }
            finally { if (!AssetDatabase.Contains(discarded)) Object.DestroyImmediate(discarded); }
        }
        [Test]
        public void AbortedPreparationPreservesItsOriginalExceptionAndDoesNotPersistPendingWrites()
        {
            string path = folder + "/uncommitted.asset";
            var mesh = Triangle();
            var original = new InvalidDataException("Authored resource failed validation.");
            try
            {
                Exception actual = Assert.Throws<InvalidDataException>(() =>
                {
                    using (LegacyAssetWriteBatch.Begin())
                    {
                        LegacyAssetWriteBatch.CreateAsset(mesh, path);
                        try { throw original; }
                        catch { LegacyAssetWriteBatch.Abort(); throw; }
                    }
                });
                Assert.AreSame(original, actual);
                Assert.IsFalse(File.Exists(path));
                using (LegacyAssetWriteBatch.Begin()) LegacyAssetWriteBatch.CreateAsset(Triangle(), folder + "/recovered.asset");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/recovered.asset"));
            }
            finally { if (!AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh); }
        }
        [TestCase("../escape.asset")]
        [TestCase("nested/../../escape.asset")]
        [TestCase("nested//escape.asset")]
        public void NoncanonicalPathsAreRejectedBeforeMutation(string suffix)
        {
            using (LegacyAssetWriteBatch.Begin())
                Assert.Throws<InvalidOperationException>(() => LegacyAssetWriteBatch.DeleteAsset(folder + "/" + suffix));
        }
        private static Mesh Triangle()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateNormals(); return mesh;
        }
    }
}
