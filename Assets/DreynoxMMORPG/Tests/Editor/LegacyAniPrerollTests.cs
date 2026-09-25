using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyAniPrerollTests
    {
        [Test]
        public void SignedMinusOneMovesTheWholeTimelineWithoutDroppingKeys()
        {
            var clip = LegacyAniParser.Parse(Fixture(uint.MaxValue, 1));
            Assert.AreEqual(1, clip.FrameOffset);
            Assert.AreEqual(0u, clip.StartKeyframe);
            Assert.AreEqual(2u, clip.EndKeyframe);
            CollectionAssert.AreEqual(new uint[] { 0, 2 }, clip.Bones[0].Rotations.Select(k => k.Frame));
            Assert.AreEqual(2f / 30f, clip.DurationSeconds, 0.000001f);
            Assert.Throws<InvalidDataException>(() => LegacyAniParser.Parse(Fixture(uint.MaxValue - 1, 1)));
        }
        [Test]
        public void PrerollDoesNotAcceptOutOfRangeOrUnsortedKeys()
        {
            Assert.Throws<InvalidDataException>(() => LegacyAniParser.Parse(Fixture(uint.MaxValue, 2)));
            Assert.Throws<InvalidDataException>(() => LegacyAniParser.Parse(Fixture(uint.MaxValue, uint.MaxValue - 1)));
        }
        [TestCase("Mob_Orc4_Die.ANI", 59, 57, 1306, 146, 55)]
        [TestCase("Mob_Zomb_01_Att1.ANI", 36, 52, 705, 127, 31)]
        public void ActualMobAttackAndDeathRetainEveryAuthoredKey(string name, int bones, int end,
            int rotationKeys, int translationKeys, int prerollKeys)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Canonical ps0032 corpus required.");
            var clip = LegacyAniParser.Parse(corpus.Resolve("DATA_Español/monster/ani/" + name));
            Assert.AreEqual(bones, clip.Bones.Count);
            Assert.AreEqual(1, clip.FrameOffset);
            Assert.AreEqual(0u, clip.StartKeyframe);
            Assert.AreEqual((uint)end, clip.EndKeyframe);
            Assert.AreEqual(rotationKeys, clip.Bones.Sum(b => b.Rotations.Count));
            Assert.AreEqual(translationKeys, clip.Bones.Sum(b => b.Translations.Count));
            Assert.AreEqual(prerollKeys, clip.Bones.Sum(b => b.Rotations.Count(k => k.Frame == 0) + b.Translations.Count(k => k.Frame == 0)));
        }
        private static byte[] Fixture(uint start, uint secondFrame)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s))
            {
                w.Write(start); w.Write(1u); w.Write((ushort)1); w.Write(-1);
                for (int i = 0; i < 16; i++) w.Write(i % 5 == 0 ? 1f : 0f);
                w.Write(2);
                foreach (uint frame in new[] { uint.MaxValue, secondFrame })
                { w.Write(frame); w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f); }
                w.Write(0);
                return s.ToArray();
            }
        }
    }
}
