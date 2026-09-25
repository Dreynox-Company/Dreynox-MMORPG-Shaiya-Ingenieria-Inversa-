using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.LocalData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class SkinnedDdsImportTests
    {
        private string folder;
        [SetUp] public void SetUp()
        {
            const string root = "Assets/DreynoxMMORPG/LocalLegacyGenerated";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/DreynoxMMORPG", "LocalLegacyGenerated");
            string name = "SkinnedColorTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(root, name); folder = root + "/" + name;
            LegacyColorTextureImporter.ClearSessionCache();
        }
        [TearDown] public void TearDown()
        {
            LegacyColorTextureImporter.ClearSessionCache();
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }
        [Test]
        public void SkinnedMaterialNeverCopiesDdsIntoTheNativeImportPipeline()
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dds");
            byte[] bytes = Fixture(); File.WriteAllBytes(source, bytes);
            try
            {
                Material material = LegacySkinnedAssetBuilder.ImportLitMaterial(
                    source, folder + "/color.dds", folder + "/color.mat", "fixture", true);
                Texture texture = material.GetTexture("_BaseMap");
                Assert.IsNotNull(texture);
                Assert.AreEqual(folder + "/color.png", AssetDatabase.GetAssetPath(texture));
                Assert.IsFalse(File.Exists(folder + "/color.dds"), "Legacy DDS must never reach IHVImageFormatImporter.");
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                Assert.IsNotNull(importer); Assert.IsTrue(importer.sRGBTexture); Assert.IsTrue(importer.mipmapEnabled);
                Assert.AreEqual(FilterMode.Trilinear, importer.filterMode); Assert.AreEqual(8, importer.anisoLevel);
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHATEST_ON"));
                Assert.AreEqual(0.45f, material.GetFloat("_Cutoff"));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
            }
            finally { File.Delete(source); }
        }
        [Test]
        public void TruncatedDdsFailsManagedValidationInsteadOfNativeImport()
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dds");
            byte[] bytes = Fixture(); Array.Resize(ref bytes, 130); File.WriteAllBytes(source, bytes);
            try
            {
                Assert.Throws<EndOfStreamException>(() => LegacySkinnedAssetBuilder.ImportLitMaterial(
                    source, folder + "/bad.dds", folder + "/bad.mat", "invalid", false));
                Assert.IsFalse(File.Exists(folder + "/bad.dds"));
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(folder + "/bad.mat"));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
            }
            finally { File.Delete(source); }
        }
        [Test]
        public void OriginalMap1Npc137TextureDecodesWithoutNativeDdsImport()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            string source = corpus.Resolve("DATA_Español/npc/dds/elmr_torso010_2.dds");
            const string originalHash = "b089857babb7ab69bd24ca456b82a3af902e5304a1eb32be51b4bcf336ddd014";
            Assert.AreEqual(originalHash, FileFingerprint.Sha256(source));
            byte[] bytes = File.ReadAllBytes(source);
            Assert.AreEqual(349680, bytes.Length);
            DecodedDds expected = LegacyDdsDecoder.Decode(bytes);
            Assert.AreEqual(512, expected.Width); Assert.AreEqual(512, expected.Height);
            Material material = LegacySkinnedAssetBuilder.ImportLitMaterial(
                source, folder + "/elmr.dds", folder + "/elmr.mat", "Original NPC texture", true);
            Assert.AreEqual(folder + "/elmr.png", AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")));
            Assert.IsFalse(File.Exists(folder + "/elmr.dds"));
            var decodedPng = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(decodedPng.LoadImage(File.ReadAllBytes(folder + "/elmr.png")));
                var actual = decodedPng.GetPixels32();
                Assert.AreEqual(expected.Width * expected.Height, actual.Length);
                for (int i = 0; i < actual.Length; i++)
                {
                    if (actual[i].r != expected.Pixels[i*4] || actual[i].g != expected.Pixels[i*4+1] ||
                        actual[i].b != expected.Pixels[i*4+2] || actual[i].a != expected.Pixels[i*4+3])
                        Assert.Fail("Converted original pixel or alpha changed at " + i);
                }
            }
            finally { Object.DestroyImmediate(decodedPng); }
            Assert.AreEqual(originalHash, FileFingerprint.Sha256(source));
        }
        private static byte[] Fixture()
        {
            byte[] bytes = new byte[144];
            void U32(int offset, uint value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4); }
            U32(0,0x20534444);U32(4,124);U32(8,0x81007);U32(12,4);U32(16,4);U32(20,16);
            U32(76,32);U32(80,4);U32(84,0x33545844);U32(88,256);U32(108,0x1000);
            U32(116,0x58534444);U32(120,0xffffffff);U32(124,0xffffffff);
            for(int i=128;i<136;i++) bytes[i]=0xf1;
            bytes[136]=0;bytes[137]=0xf8;bytes[138]=0xe0;bytes[139]=7;
            return bytes;
        }
    }
}
