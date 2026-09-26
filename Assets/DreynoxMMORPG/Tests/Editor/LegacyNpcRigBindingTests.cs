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
    public sealed class LegacyNpcRigBindingTests
    {
        [Test]
        public void AllLongClipsCanUseAClosedAuthoredBodyPrefix()
        {
            var mesh = new Legacy3dcFile();
            for (int i = 0; i < 3; i++) mesh.InverseBindMatrices.Add(Matrix4x4.identity);
            mesh.Vertices.Add(new Legacy3dcVertex { Bone1 = 2, Weight1 = 1 });
            var source = new LegacyAniFile { EndKeyframe = 30 };
            for (int i = 0; i < 5; i++) source.Bones.Add(new LegacyAniBone {
                ParentBoneIndex = -1, AbsoluteBaseMatrix = Matrix4x4.identity });
            var plan = LegacyMonRigBinding.Prepare(new[] { mesh }, new List<LegacyMonClipBinding> {
                new LegacyMonClipBinding { Semantic = "idle", Source = source } }, null);
            Assert.AreEqual(3, plan.Reference.Bones.Count);
            Assert.AreEqual(3, plan.Clips[0].Bound.Bones.Count);
            Assert.AreEqual(5, source.Bones.Count);
            Assert.AreEqual(2, plan.Clips[0].ExtraTracks);
            Assert.Throws<InvalidDataException>(() => LegacyMonRigBinding.Prepare(new[] { mesh },
                new List<LegacyMonClipBinding> { new LegacyMonClipBinding { Source = source } },
                new[] { new LegacyMonEffect { BoneId = 4, EffectId = 0 } }));
        }
        [Test]
        public void EveryResolvedMap0NpcModelHasAllDeclaredAnimations()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Canonical ps0032 corpus required.");
            var svmap = LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/0.svmap"));
            var definitions = LegacyNpcQuestHeaderParser.ParseEncrypted(corpus.Resolve("DATA_Español/npc/NpcQuest.SData"));
            var mon = LegacyMonParser.Parse(corpus.Resolve("DATA_Español/npc/npc.mon"));
            var ids = new SortedSet<int>();
            foreach (var npc in svmap.Npcs)
            {
                if (definitions.TryGet(npc.NpcType, npc.NpcId, out var row)) ids.Add(row.Model);
                else Assert.IsTrue(npc.NpcType == 0 && npc.NpcId == 0, "Unexpected unresolved NPC.");
            }
            Assert.AreEqual(41, ids.Count);
            int slots = 0, tails = 0, changedParents = 0, invalidNormals = 0;
            foreach (int id in ids)
            {
                var plan = LegacyMonPrefabImporter.ValidateRigOnly(corpus, LegacyMonCatalogKind.Npc, mon.Records[id]);
                slots += plan.Clips.Count;
                tails += plan.Clips.Count(c => c.ExtraTracks > 0);
                changedParents += plan.Clips.Count(c => c.ReparentedBones > 0);
                invalidNormals += plan.Parts.Sum(p => p.ReconstructedNormals + p.InactiveNormalDefaults);
                if (id == 186) Assert.AreEqual(79, plan.Reference.Bones.Count);
            }
            Assert.AreEqual(135, slots);
            Assert.AreEqual(3, tails);
            Assert.AreEqual(1, changedParents);
            Assert.AreEqual(486, invalidNormals);
        }
    }
}
