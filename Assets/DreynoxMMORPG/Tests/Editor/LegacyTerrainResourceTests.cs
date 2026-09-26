using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyTerrainResourceTests
    {
        [Test]
        public void MissingAuthoredTgaResolvesOnlyItsExactDdsCounterpart()
        {
            string root = Path.Combine(Path.GetTempPath(), "dx-terrain-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "DATA", "terrain", "detail");
            Directory.CreateDirectory(folder);
            try
            {
                string dds = Path.Combine(folder, "a1_grass_earth.dds");
                File.WriteAllBytes(dds, new byte[] { 1 });
                Assert.AreEqual(dds, LegacyTerrainLayerImporter.ResolveTexture(root, "A1_Grass_Earth.tga"));
                string tga = Path.Combine(folder, "a1_grass_earth.tga");
                File.WriteAllBytes(tga, new byte[] { 2 });
                Assert.AreEqual(tga, LegacyTerrainLayerImporter.ResolveTexture(root, "A1_Grass_Earth.tga"));
                Assert.Throws<FileNotFoundException>(() => LegacyTerrainLayerImporter.ResolveTexture(root, "unrelated.tga"));
                Assert.Throws<ArgumentException>(() => LegacyTerrainLayerImporter.ResolveTexture(root, "../../outside.tga"));
                CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(dds));
                CollectionAssert.AreEqual(new byte[] { 2 }, File.ReadAllBytes(tga));
            }
            finally { Directory.Delete(root, true); }
        }
        [Test]
        public void EveryNativeStartingMapTerrainLayerHasItsOriginalResource()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Canonical original DATA required.");
            var wld = LegacyWldTerrainParser.Parse(corpus.Resolve("DATA_Español/world/1.wld"));
            Assert.Greater(wld.Textures.Count, 0);
            foreach (var layer in wld.Textures)
            {
                string path = LegacyTerrainLayerImporter.ResolveTexture(corpus.RootPath, layer.TextureName);
                Assert.IsTrue(File.Exists(path), layer.TextureName);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(layer.TextureName).ToLowerInvariant(),
                    Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            }
        }
    }
}
