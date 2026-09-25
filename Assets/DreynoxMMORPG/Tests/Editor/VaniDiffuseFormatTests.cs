using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class VaniDiffuseFormatTests
    {
        [TestCase(0xffc5c5c5u, 197, 197, 197, 255)]
        [TestCase(0x80123456u, 18, 52, 86, 128)]
        [TestCase(0x00123456u, 18, 52, 86, 0)]
        [TestCase(0xffffffffu, 255, 255, 255, 255)]
        public void NativeDiffuseDwordIsArgbNotABone(uint packed, int r, int g, int b, int a)
        {
            var frame = new LegacyVaniVertexFrame { BoneId = unchecked((int)packed) };
            Assert.AreEqual(packed, frame.DiffuseArgb);
            Assert.AreEqual(new Color32((byte)r, (byte)g, (byte)b, (byte)a), frame.DiffuseColor);
        }
        [Test]
        public void ExactOriginalFishKeepsAllDiffuseValues()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            string path = corpus.Resolve("DATA_Español/entity/vani/fish_02.vani");
            const string digest = "027f2f1c73820ef10eb82e5f4eb2eccaa36dfe475a26714802703111395ea1da";
            Assert.AreEqual(digest, FileFingerprint.Sha256(path));
            var data = LegacyVaniParser.Parse(path);
            Assert.AreEqual(54, data.FrameCount);
            Assert.AreEqual(150, data.TotalVertexCount);
            var values = data.Meshes.SelectMany(m => m.Vertices).SelectMany(v => v.Frames)
                .GroupBy(v => v.DiffuseArgb).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(3, values.Count);
            Assert.AreEqual(6696, values[0xffffffff]);
            Assert.AreEqual(54, values[0xffc5c5c5]);
            Assert.AreEqual(1350, values[0xff8b8b8b]);
            Assert.AreEqual(digest, FileFingerprint.Sha256(path));
        }
        [Test]
        public void EveryPlacedMap1VaniIsDecodedBeforeWorldPreparation()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original ps0032 DATA.");
            var world = LegacyWldTerrainParser.Parse(corpus.Resolve("DATA_Español/world/1.wld"));
            var names = new HashSet<string>();
            int vertices = 0, colors = 0;
            foreach (var group in new[] { world.VAni1, world.VAni2 })
                foreach (var placement in group.Coordinates)
                {
                    Assert.That(placement.Id, Is.InRange(0, group.Names.Count - 1));
                    string name = group.Names[placement.Id];
                    if (!names.Add(name)) continue;
                    var data = LegacyVaniParser.Parse(corpus.Resolve("DATA_Español/entity/vani/" + Path.GetFileName(name)));
                    Assert.Greater(data.Unknown1, 0);
                    foreach (var frame in data.Meshes.SelectMany(m => m.Vertices).SelectMany(v => v.Frames))
                    {
                        vertices++;
                        if (frame.DiffuseArgb != uint.MaxValue) colors++;
                    }
                }
            Assert.Greater(names.Count, 0); Assert.Greater(colors, 0);
            TestContext.WriteLine("Original placed VANI resources=" + names.Count + " frame vertices=" + vertices + " nonwhite diffuse=" + colors);
        }
        [Test]
        public void VertexDiffuseShaderHasForwardDepthAndInstancingSupport()
        {
            Shader shader = Shader.Find("Dreynox/Enhanced/LegacyVaniDiffuse");
            Assert.IsNotNull(shader); Assert.IsTrue(shader.isSupported);
            var material = new Material(shader) { enableInstancing = true };
            try
            {
                Assert.GreaterOrEqual(material.FindPass("VaniForward"), 0);
                Assert.GreaterOrEqual(material.FindPass("VaniDepth"), 0);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
                Assert.IsTrue(material.HasProperty("_BaseMap"));
                Assert.IsTrue(material.HasProperty("_AlphaClip"));
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
