using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyMonParserTests
    {
        [Test]
        public void Mo4SyntheticRecordParsesCompleteLayout()
        {
            byte[] bytes;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("MO4"));
                writer.Write(1);

                WriteString(writer, "TestMob");
                writer.Write((byte)255);

                WriteString(writer, "walk.ani");
                WriteString(writer, "run.ani");
                WriteString(writer, "attack1.ani");
                WriteString(writer, "attack2.ani");
                WriteString(writer, "attack3.ani");
                WriteString(writer, "die.ani");
                WriteString(writer, "breath.ani");
                WriteString(writer, "damage.ani");
                WriteString(writer, "idle.ani");

                WriteString(writer, "a1.wav");
                WriteString(writer, "a2.wav");
                WriteString(writer, "a3.wav");
                WriteString(writer, "die.wav");

                WriteString(writer, "a1.eft");
                WriteString(writer, "a2.eft");
                WriteString(writer, "a3.eft");
                WriteString(writer, "die.eft");
                WriteString(writer, "attach.eft");

                writer.Write(2);
                WriteString(writer, "body_a.3dc");
                WriteString(writer, "body.dds");
                WriteString(writer, "body_b.3dc");
                WriteString(writer, "body.dds");

                writer.Write(1.75f);

                writer.Write(2);
                writer.Write(4);
                writer.Write(11);
                writer.Write(8);
                writer.Write(22);

                bytes = stream.ToArray();
            }

            LegacyMonFile parsed =
                LegacyMonParser.Parse(bytes);

            Assert.AreEqual("MO4", parsed.Signature);
            Assert.AreEqual(LegacyMonFormat.MO4, parsed.Format);
            Assert.AreEqual(1, parsed.Records.Count);

            LegacyMonRecord record = parsed.Records[0];

            Assert.AreEqual("TestMob", record.Name);
            Assert.AreEqual(255, record.Unknown);
            Assert.AreEqual("walk.ani", record.WalkAnimation);
            Assert.AreEqual("attach.eft", record.AttachEffect);
            Assert.AreEqual(2, record.Objects.Count);
            Assert.AreEqual("body_a.3dc", record.Objects[0].MeshName);
            Assert.AreEqual("body.dds", record.Objects[1].TextureName);
            Assert.AreEqual(1.75f, record.Height, 0.000001f);
            Assert.AreEqual(2, record.Effects.Count);
            Assert.AreEqual(4, record.Effects[0].BoneId);
            Assert.AreEqual(11, record.Effects[0].EffectId);
            Assert.AreEqual(8, record.Effects[1].BoneId);
            Assert.AreEqual(22, record.Effects[1].EffectId);
        }

        [Test]
        public void Mo2DoesNotReadAttachEffect()
        {
            byte[] bytes;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("MO2"));
                writer.Write(1);

                WriteString(writer, "Legacy");
                writer.Write((byte)255);

                for (int i = 0; i < 17; i++)
                    WriteString(writer, "LOAD");

                writer.Write(0);
                writer.Write(1f);
                writer.Write(0);

                bytes = stream.ToArray();
            }

            LegacyMonFile parsed =
                LegacyMonParser.Parse(bytes);

            Assert.AreEqual(LegacyMonFormat.MO2, parsed.Format);
            Assert.AreEqual(string.Empty, parsed.Records[0].AttachEffect);
        }

        [Test]
        public void CanonicalMonCatalogsMatchPs0032WhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null || !corpus.Validate().IsCanonical)
                Assert.Ignore("Canonical ps0032 corpus is not configured on this machine.");

            LegacyMonFile monsters =
                LegacyMonParser.Parse(
                    corpus.Resolve("DATA_Español/monster/monster.mon"));

            Assert.AreEqual(LegacyMonFormat.MO4, monsters.Format);
            Assert.AreEqual(863, monsters.Records.Count);
            Assert.AreEqual("Mob_Ape_01", monsters.Records[0].Name);
            Assert.AreEqual(2, monsters.Records[0].Objects.Count);
            Assert.AreEqual(
                "Mob_Ape_01_A.3DC",
                monsters.Records[0].Objects[0].MeshName);
            Assert.AreEqual(
                "Mob_Ape_01.dds",
                monsters.Records[0].Objects[0].TextureName);
            Assert.AreEqual(
                "Mob_Ape_01_Walk.ANI",
                monsters.Records[0].WalkAnimation);
            Assert.AreEqual(
                1.4603692f,
                monsters.Records[0].Height,
                0.00001f);

            LegacyMonFile npcs =
                LegacyMonParser.Parse(
                    corpus.Resolve("DATA_Español/npc/npc.mon"));

            Assert.AreEqual(264, npcs.Records.Count);
            Assert.AreEqual("Ctl_Cow_01", npcs.Records[0].Name);
            Assert.AreEqual(
                "Ctl_Cow_01.3DC",
                npcs.Records[0].Objects[0].MeshName);
            Assert.AreEqual(
                "Ctl_Cow_01_Idle.ANI",
                npcs.Records[0].IdleAnimation);

            LegacyMonFile wings =
                LegacyMonParser.Parse(
                    corpus.Resolve("DATA_Español/character/wing/wing.mon"));

            Assert.AreEqual(29, wings.Records.Count);
            Assert.AreEqual("Wing_01", wings.Records[0].Name);
            Assert.AreEqual(
                "Wing_01.3DC",
                wings.Records[0].Objects[0].MeshName);
            Assert.AreEqual(
                "Wing_01.ANI",
                wings.Records[0].WalkAnimation);
        }

        private static void WriteString(
            BinaryWriter writer,
            string value)
        {
            byte[] bytes =
                Encoding.ASCII.GetBytes(value ?? string.Empty);

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
