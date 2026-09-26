using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyCharacterBodyClipTests
    {
        [TestCase(0)] [TestCase(2)] [TestCase(36)]
        public void ExtraTracksDoNotInventBodyBonesOrMutateTheParsedClip(int extras)
        {
            var reference = Fixture(36); var source = Fixture(36 + extras);
            LegacyAniFile bound = LegacyAniRigBinding.BodyClip(source, reference);
            Assert.AreEqual(36, bound.Bones.Count);
            Assert.AreEqual(36 + extras, source.Bones.Count);
            Assert.AreSame(source.Bones[35], bound.Bones[35]);
            Assert.AreEqual(source.EndKeyframe, bound.EndKeyframe);
        }
        [Test]
        public void ChangedHierarchyIsRejectedEvenWhenBoneCountsMatch()
        {
            var reference = Fixture(36); var source = Fixture(36);
            source.Bones[5].ParentBoneIndex = -1;
            Assert.Throws<InvalidDataException>(() => LegacyAniRigBinding.BodyClip(source, reference));
        }
        [Test]
        public void MissingBodyBonesAreRejectedInsteadOfTruncatingSkinInfluences()
        {
            Assert.Throws<InvalidDataException>(() => LegacyAniRigBinding.BodyClip(Fixture(35), Fixture(36)));
        }
        [Test]
        public void CanonicalHumanFighterAll28ClipsBindToTheSameBodyHierarchy()
        {
            CanonicalClientCorpus corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Canonical ps0032 corpus is required.");
            var reference = LegacyCharacterImporter.ReadClip(corpus, LegacyCharacterImporter.Clips[0]);
            Assert.AreEqual(36, reference.Bones.Count);
            int extraClips = 0;
            foreach (var spec in LegacyCharacterImporter.Clips)
            {
                var source = LegacyCharacterImporter.ReadClip(corpus, spec);
                var body = LegacyAniRigBinding.BodyClip(source, reference);
                Assert.AreEqual(36, body.Bones.Count, spec.File);
                Assert.Greater(body.DurationSeconds, 0, spec.File);
                if (source.Bones.Count > 36) extraClips++;
            }
            Assert.AreEqual(28, LegacyCharacterImporter.Clips.Length);
            Assert.AreEqual(14, extraClips);
        }
        private static LegacyAniFile Fixture(int count)
        {
            var value = new LegacyAniFile { StartKeyframe = 0, EndKeyframe = 30 };
            for (int i = 0; i < count; i++) value.Bones.Add(new LegacyAniBone
                { ParentBoneIndex = i < 36 ? i - 1 : -1, AbsoluteBaseMatrix = Matrix4x4.identity });
            return value;
        }
    }
}
