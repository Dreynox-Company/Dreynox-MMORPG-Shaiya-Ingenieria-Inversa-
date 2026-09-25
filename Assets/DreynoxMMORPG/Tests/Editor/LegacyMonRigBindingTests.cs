using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyMonRigBindingTests
    {
        [Test]
        public void ALongIdleDoesNotOverrideTheAuthoredBodyRigOrDropAliases()
        {
            var body = Ani(3); var longIdle = Ani(5);
            var clips = new List<LegacyMonClipBinding> {
                Clip("idle", longIdle), Clip("breath", body), Clip("walk", body) };
            var plan = LegacyMonRigBinding.Prepare(new[] { Mesh(3, 2) }, clips, null);
            Assert.AreSame(body, plan.Reference);
            Assert.AreEqual(3, plan.Clips.Count);
            Assert.AreEqual(3, plan.Clips[0].Bound.Bones.Count);
            Assert.AreEqual(5, longIdle.Bones.Count, "Source ANI must not be modified.");
            Assert.AreEqual(2, plan.Clips[0].ExtraTracks);
            Assert.AreEqual("walk", plan.Clips[2].Semantic);
        }
        [Test]
        public void APartWithTheCompleteBindTableCanFollowShorterParts()
        {
            var ani = Ani(5);
            var plan = LegacyMonRigBinding.Prepare(new[] { Mesh(3, 2), Mesh(5, 4) },
                new List<LegacyMonClipBinding> { Clip("idle", ani) }, null);
            Assert.AreEqual(1, plan.ReferencePart);
            Assert.AreEqual(5, plan.Reference.Bones.Count);
        }
        [Test]
        public void MissingWeightedBonesAndMissingAnimationTracksRemainErrors()
        {
            Assert.Throws<InvalidDataException>(() => LegacyMonRigBinding.Prepare(
                new[] { Mesh(5, 4) }, new List<LegacyMonClipBinding> { Clip("idle", Ani(3)) }, null));
            Assert.Throws<InvalidDataException>(() => LegacyMonRigBinding.BindClip(Ani(2), Ani(3), out _));
        }
        [Test]
        public void ReparentedTrackPreservesWorldPositionAtAuthoredFrames()
        {
            var source = Ani(3); var target = Ani(3);
            source.Bones[1].ParentBoneIndex = 0;
            source.Bones[2].ParentBoneIndex = 0;
            target.Bones[1].ParentBoneIndex = 0;
            target.Bones[2].ParentBoneIndex = 1;
            source.Bones[1].AbsoluteBaseMatrix = Matrix4x4.Translate(new Vector3(2, 0, 0));
            source.Bones[2].AbsoluteBaseMatrix = Matrix4x4.Translate(new Vector3(3, 0, 0));
            source.Bones[1].Translations.Add(new LegacyAniTranslationFrame { Frame = 0, Translation = new Vector3(2, 0, 0) });
            source.Bones[1].Translations.Add(new LegacyAniTranslationFrame { Frame = 30, Translation = new Vector3(5, 0, 0) });
            var bound = LegacyMonRigBinding.BindClip(source, target, out int changed);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(31, bound.Bones[2].Translations.Count);
            Assert.AreEqual(1, bound.Bones[2].ParentBoneIndex);
            Assert.AreEqual(1f, bound.Bones[2].Translations[0].Translation.x, 0.0001f);
            Assert.AreEqual(-2f, bound.Bones[2].Translations[30].Translation.x, 0.0001f);
            Assert.AreSame(source.Bones[1], bound.Bones[1], "Unchanged channels keep their source keys.");
            Assert.AreEqual(0, source.Bones[2].Translations.Count);
        }
        [Test]
        public void NormalRepairIsExplicitAndDoesNotRelaxPositionOrUvValidation()
        {
            byte[] bytes = MeshBytes(float.NaN, 0, 0);
            Assert.Throws<InvalidDataException>(() => Legacy3dcParser.Parse(bytes));
            var mesh = Legacy3dcParser.ParseWithTopologyNormals(bytes);
            Assert.AreEqual(1, mesh.ReconstructedNormals);
            Assert.AreEqual(Vector3.forward, mesh.Vertices[0].Normal);
            Assert.AreEqual(Vector3.up, mesh.Vertices[1].Normal);
            Assert.Throws<InvalidDataException>(() => Legacy3dcParser.ParseWithTopologyNormals(MeshBytes(0, float.NaN, 0)));
            Assert.Throws<InvalidDataException>(() => Legacy3dcParser.ParseWithTopologyNormals(MeshBytes(0, 0, float.NaN)));
        }
        [Test]
        public void All52Map0MonsterModelsPreflightBeforeExpensiveSceneImport()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Canonical ps0032 content required.");
            var svmap = LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/0.svmap"));
            var db = LegacyDbMonsterDataParser.ParseData(corpus.Resolve("DATA_Español/binarysdata/DBMonsterData.SData"));
            var mon = LegacyMonParser.Parse(corpus.Resolve("DATA_Español/monster/monster.mon"));
            var ids = new SortedSet<int>();
            foreach (var spawn in svmap.MonsterAreas.SelectMany(a => a.Monsters).Where(s => s.Count > 0))
            {
                Assert.IsTrue(db.TryGet(spawn.MobId, out var row));
                ids.Add(checked((int)row.Image));
            }
            Assert.AreEqual(52, ids.Count);
            int slots = 0, tails = 0, reparented = 0, repaired = 0;
            foreach (int id in ids)
            {
                var plan = LegacyMonPrefabImporter.ValidateRigOnly(corpus, LegacyMonCatalogKind.Monster, mon.Records[id]);
                slots += plan.Clips.Count;
                tails += plan.Clips.Count(c => c.ExtraTracks > 0);
                reparented += plan.Clips.Count(c => c.ReparentedBones > 0);
                repaired += plan.Parts.Sum(p => p.ReconstructedNormals + p.InactiveNormalDefaults);
                Assert.IsTrue(plan.Clips.Any(c => c.Semantic == "dead"), mon.Records[id].Name);
                if (id == 372) Assert.AreEqual(79, plan.Reference.Bones.Count);
                if (id == 722) Assert.AreEqual(58, plan.Reference.Bones.Count);
            }
            Assert.AreEqual(441, slots);
            Assert.AreEqual(6, tails);
            Assert.AreEqual(4, reparented);
            Assert.AreEqual(1, repaired);
        }
        private static LegacyMonClipBinding Clip(string semantic, LegacyAniFile source)
            => new LegacyMonClipBinding { Semantic = semantic, FileName = semantic + ".ani", Source = source };
        private static LegacyAniFile Ani(int count)
        {
            var a = new LegacyAniFile { EndKeyframe = 30 };
            for (int i = 0; i < count; i++) a.Bones.Add(new LegacyAniBone { ParentBoneIndex = -1, AbsoluteBaseMatrix = Matrix4x4.identity });
            return a;
        }
        private static Legacy3dcFile Mesh(int count, byte weightedBone)
        {
            var m = new Legacy3dcFile();
            for (int i = 0; i < count; i++) m.InverseBindMatrices.Add(Matrix4x4.identity);
            m.Vertices.Add(new Legacy3dcVertex { Bone1 = weightedBone, Weight1 = 1 });
            return m;
        }
        private static byte[] MeshBytes(float normalX, float positionX, float uvX)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
            {
                w.Write(0); w.Write(1);
                for (int i = 0; i < 16; i++) w.Write(i % 5 == 0 ? 1f : 0f);
                w.Write(3);
                var positions = new[] { Vector3.zero, Vector3.right, Vector3.up };
                for (int i = 0; i < 3; i++)
                {
                    w.Write(i == 0 ? positionX : positions[i].x); w.Write(positions[i].y); w.Write(positions[i].z);
                    w.Write(1f); w.Write(0);
                    w.Write(i == 0 ? normalX : 0f); w.Write(1f); w.Write(0f);
                    w.Write(i == 0 ? uvX : 0f); w.Write(0f);
                }
                w.Write(1); w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)2);
                return s.ToArray();
            }
        }
    }
}
