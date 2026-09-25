using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyMonUnusedBoneTailTests
    {
        [Test]
        public void UnusedInvalidRootSuffixIsRemovedWithoutChangingBodyOrSourceBytes()
        {
            byte[] mesh = MeshFixture(), ani = AniFixture();
            byte[] meshCopy = (byte[])mesh.Clone(), aniCopy = (byte[])ani.Clone();
            Assert.Throws<InvalidDataException>(() => Legacy3dcParser.Parse(mesh));
            Assert.Throws<InvalidDataException>(() => LegacyAniParser.Parse(ani));
            var prepared = LegacyMonUnusedBoneTail.Normalize(new[] { mesh }, new[] { ani }, null);
            Assert.AreEqual(2, prepared.RetainedBones); Assert.AreEqual(2, prepared.RemovedBones);
            var resultMesh = Legacy3dcParser.Parse(prepared.Meshes[0]);
            var resultAni = LegacyAniParser.Parse(prepared.Animations[0]);
            Assert.AreEqual(2, resultMesh.InverseBindMatrices.Count);
            Assert.AreEqual(2, resultAni.Bones.Count);
            Assert.AreEqual(3, resultMesh.Vertices.Count); Assert.AreEqual(1, resultMesh.Faces.Count);
            Assert.AreEqual(Vector3.right, resultMesh.Vertices[1].Position);
            CollectionAssert.AreEqual(meshCopy, mesh); CollectionAssert.AreEqual(aniCopy, ani);
            byte[] before = new byte[mesh.Length - 8 - 4 * 64];
            byte[] after = new byte[prepared.Meshes[0].Length - 8 - 2 * 64];
            Buffer.BlockCopy(mesh, 8 + 4 * 64, before, 0, before.Length);
            Buffer.BlockCopy(prepared.Meshes[0], 8 + 2 * 64, after, 0, after.Length);
            CollectionAssert.AreEqual(before, after);
        }
        [Test]
        public void ReferencedOrAnimatedSuffixCannotBeDiscarded()
        {
            Assert.Throws<InvalidDataException>(() => LegacyMonUnusedBoneTail.Normalize(
                new[] { MeshFixture(weightedTail: true) }, new[] { AniFixture() }, null));
            Assert.Throws<InvalidDataException>(() => LegacyMonUnusedBoneTail.Normalize(
                new[] { MeshFixture() }, new[] { AniFixture(animatedTail: true) }, null));
            Assert.Throws<InvalidDataException>(() => LegacyMonUnusedBoneTail.Normalize(
                new[] { MeshFixture() }, new[] { AniFixture(bodyAttachedTail: true) }, null));
            Assert.Throws<InvalidDataException>(() => LegacyMonUnusedBoneTail.Normalize(
                new[] { MeshFixture() }, new[] { AniFixture() },
                new[] { new LegacyMonEffect { BoneId = 2, EffectId = 0 } }));
        }
        [Test]
        public void AlreadyValidRigIsNotRewrittenOrReduced()
        {
            byte[][] meshes = { MeshFixture(bad: false) }, animations = { AniFixture(bad: false) };
            var result = LegacyMonUnusedBoneTail.Normalize(meshes, animations, null);
            Assert.AreSame(meshes, result.Meshes); Assert.AreSame(animations, result.Animations);
            Assert.AreEqual(0, result.RemovedBones);
        }
        [TestCase(67)] [TestCase(69)] [TestCase(228)]
        public void OriginalMap1NpcWithUnusedHelpersRetainsAllFourBodyPartsAndThreeClips(int id)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Original ps0032 corpus required.");
            var mon = LegacyMonParser.Parse(corpus.Resolve("DATA_Español/npc/npc.mon"));
            var plan = LegacyMonPrefabImporter.ValidateRigOnly(corpus, LegacyMonCatalogKind.Npc, mon.Records[id]);
            Assert.AreEqual(57, plan.Reference.Bones.Count);
            Assert.AreEqual(4, plan.Parts.Length);
            Assert.AreEqual(3, plan.Clips.Count);
            foreach (var part in plan.Parts) Assert.AreEqual(57, part.InverseBindMatrices.Count);
            foreach (var clip in plan.Clips) Assert.AreEqual(57, clip.Bound.Bones.Count);
        }
        private static void Matrix(BinaryWriter w, bool bad)
        {
            for (int i = 0; i < 16; i++) w.Write(bad && i == 12 ? float.NaN : i % 5 == 0 ? 1f : 0f);
        }
        private static byte[] MeshFixture(bool bad = true, bool weightedTail = false)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
            {
                w.Write(0); w.Write(4); for (int i = 0; i < 4; i++) Matrix(w, bad && i >= 2);
                w.Write(3);
                for (int i = 0; i < 3; i++)
                {
                    w.Write(i == 1 ? 1f : 0f); w.Write(i == 2 ? 1f : 0f); w.Write(0f);
                    w.Write(1f); w.Write((byte)(weightedTail ? 2 : 0)); w.Write((byte)1); w.Write((byte)0); w.Write((byte)0);
                    w.Write(0f); w.Write(0f); w.Write(1f); w.Write(0f); w.Write(0f);
                }
                w.Write(1); w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)2);
                return s.ToArray();
            }
        }
        private static byte[] AniFixture(bool bad = true, bool animatedTail = false, bool bodyAttachedTail = false)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
            {
                w.Write(0u); w.Write(30u); w.Write((ushort)4);
                for (int i = 0; i < 4; i++)
                {
                    int parent = i == 0 || i == 2 ? -1 : i - 1;
                    if (i == 2 && bodyAttachedTail) parent = 1;
                    w.Write(parent); Matrix(w, bad && i >= 2);
                    bool key = animatedTail && i == 2;
                    w.Write(key ? 1 : 0);
                    if (key) { w.Write(0u); w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f); }
                    w.Write(0);
                }
                return s.ToArray();
            }
        }
    }
}
