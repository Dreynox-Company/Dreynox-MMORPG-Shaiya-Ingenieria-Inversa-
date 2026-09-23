using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using Parsec.Cryptography;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacySDataTests
    {
        [Test]
        public void SeedSDataRoundTripValidatesChecksum()
        {
            byte[] plaintext =
                Encoding.UTF8.GetBytes(
                    "Dreynox MMORPG ps0032 SData parity fixture");

            byte[] encrypted =
                EncryptRegularForTest(plaintext);

            Assert.IsTrue(
                LegacySDataDecryptor.IsEncrypted(encrypted));

            LegacySDataDecryptionResult result =
                LegacySDataDecryptor.Decrypt(
                    encrypted,
                    validateChecksum: true);

            CollectionAssert.AreEqual(
                plaintext,
                result.Plaintext);

            Assert.AreEqual(
                (uint)plaintext.Length,
                result.RealSize);

            Assert.IsFalse(result.BinaryHeader);
        }

        [Test]
        public void MonsterSDataPlainParserPreservesModelAndCombatFields()
        {
            byte[] plaintext;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(2);

                WriteMonster(
                    writer,
                    "Wolf",
                    modelId: 12,
                    level: 7,
                    ai: 3,
                    hp: 450,
                    normalTime: 2500,
                    normalStep: 4,
                    chaseTime: 5000,
                    chaseStep: 7,
                    attackType1: 1,
                    attackAni1: 2,
                    attackType2: 3,
                    attackAni2: 4,
                    attackType3: 5,
                    attackAni3: 6,
                    attackPlus3: 9,
                    questItemId: 77);

                WriteMonster(
                    writer,
                    "Bear",
                    modelId: 24,
                    level: 12,
                    ai: 6,
                    hp: 900,
                    normalTime: 3000,
                    normalStep: 5,
                    chaseTime: 7000,
                    chaseStep: 8,
                    attackType1: 2,
                    attackAni1: 1,
                    attackType2: 0,
                    attackAni2: 0,
                    attackType3: 0,
                    attackAni3: 0,
                    attackPlus3: 0,
                    questItemId: 0);

                plaintext = stream.ToArray();
            }

            LegacyMonsterSDataFile parsed =
                LegacyMonsterSDataParser.ParsePlain(plaintext);

            Assert.AreEqual(2, parsed.Records.Count);

            LegacyMonsterRecord wolf =
                parsed.Records[0];

            Assert.AreEqual(0u, wolf.MobId);
            Assert.AreEqual("Wolf", wolf.MobName);
            Assert.AreEqual(12, wolf.ModelId);
            Assert.AreEqual(7, wolf.Level);
            Assert.AreEqual(3, wolf.Ai);
            Assert.AreEqual(450, wolf.Hp);
            Assert.AreEqual(2500, wolf.NormalTime);
            Assert.AreEqual(7, wolf.ChaseStep);
            Assert.AreEqual(6, wolf.AttackAni3);
            Assert.AreEqual(77, wolf.QuestItemId);

            Assert.IsTrue(
                parsed.TryGet(
                    1,
                    out LegacyMonsterRecord bear));

            Assert.AreEqual("Bear", bear.MobName);
            Assert.AreEqual(24, bear.ModelId);
            Assert.AreEqual(900, bear.Hp);
        }

        [Test]
        public void CanonicalMonsterSDataResolvesEveryMapZeroMobWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyMonsterSDataFile monsters =
                LegacyMonsterSDataParser.ParseEncrypted(
                    corpus.Resolve(
                        "DATA_Español/monster/monster.sdata"),
                    validateChecksum: true);

            LegacyMonFile models =
                LegacyMonParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/monster/monster.mon"));

            LegacySvmapFile map =
                LegacySvmapParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/0.svmap"));

            Assert.Greater(
                monsters.Records.Count,
                4984,
                "Map 0 references MobId values through 4984.");

            var uniqueMobIds =
                new HashSet<uint>();

            for (int areaIndex = 0;
                 areaIndex < map.MonsterAreas.Count;
                 areaIndex++)
            {
                LegacySvmapMonsterArea area =
                    map.MonsterAreas[areaIndex];

                for (int spawnIndex = 0;
                     spawnIndex < area.Monsters.Count;
                     spawnIndex++)
                {
                    uniqueMobIds.Add(
                        area.Monsters[spawnIndex].MobId);
                }
            }

            foreach (uint mobId in uniqueMobIds)
            {
                Assert.IsTrue(
                    monsters.TryGet(
                        mobId,
                        out LegacyMonsterRecord record),
                    "Missing Monster.SData record " + mobId + ".");

                Assert.GreaterOrEqual(
                    record.ModelId,
                    0,
                    "Negative model for MobId " + mobId + ".");

                Assert.Less(
                    record.ModelId,
                    models.Records.Count,
                    "MON model index out of range for MobId " +
                    mobId + ".");
            }
        }

        private static byte[] EncryptRegularForTest(
            byte[] plaintext)
        {
            uint checksum =
                LegacySDataDecryptor.CalculateChecksum(
                    plaintext);

            int aligned =
                ((plaintext.Length + 15) / 16) * 16;

            byte[] padded =
                new byte[aligned];

            Buffer.BlockCopy(
                plaintext,
                0,
                padded,
                0,
                plaintext.Length);

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(
                    Encoding.ASCII.GetBytes(
                        LegacySDataDecryptor.SeedSignature));

                writer.Write(checksum);
                writer.Write((uint)plaintext.Length);
                writer.Write(new byte[16]);

                for (int offset = 0;
                     offset < padded.Length;
                     offset += 16)
                {
                    byte[] input =
                        new byte[16];

                    Buffer.BlockCopy(
                        padded,
                        offset,
                        input,
                        0,
                        16);

                    byte[] encrypted;
                    Seed.EncryptChunk(
                        input,
                        out encrypted);

                    writer.Write(encrypted);
                }

                return stream.ToArray();
            }
        }

        private static void WriteMonster(
            BinaryWriter writer,
            string name,
            short modelId,
            short level,
            byte ai,
            int hp,
            int normalTime,
            byte normalStep,
            int chaseTime,
            byte chaseStep,
            byte attackType1,
            byte attackAni1,
            byte attackType2,
            byte attackAni2,
            byte attackType3,
            byte attackAni3,
            byte attackPlus3,
            short questItemId)
        {
            byte[] nameBytes =
                Encoding.UTF8.GetBytes(name);

            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);

            writer.Write(modelId);
            writer.Write(level);
            writer.Write(ai);
            writer.Write(hp);
            writer.Write((byte)0);
            writer.Write((byte)100);
            writer.Write((byte)0);
            writer.Write(normalTime);
            writer.Write(normalStep);
            writer.Write(chaseTime);
            writer.Write(chaseStep);
            writer.Write(attackType1);
            writer.Write(attackAni1);
            writer.Write(attackType2);
            writer.Write(attackAni2);
            writer.Write(attackType3);
            writer.Write(attackAni3);
            writer.Write(attackPlus3);
            writer.Write(questItemId);
        }
        [Test]
        public void MonsterModelResolverDetectsDirectIndexing()
        {
            var records =
                new List<LegacyMonsterRecord>
                {
                    new LegacyMonsterRecord
                    {
                        MobId = 0,
                        MobName = "A",
                        ModelId = 0,
                        Level = 1,
                        Hp = 10
                    },
                    new LegacyMonsterRecord
                    {
                        MobId = 1,
                        MobName = "B",
                        ModelId = 2,
                        Level = 2,
                        Hp = 20
                    }
                };

            LegacyMonsterModelIndexMode mode =
                LegacyMonsterModelResolver.Detect(
                    records,
                    monRecordCount: 3);

            Assert.AreEqual(
                LegacyMonsterModelIndexMode.Direct,
                mode);

            Assert.AreEqual(
                2,
                LegacyMonsterModelResolver.Resolve(
                    records[1],
                    mode,
                    3));
        }

        [Test]
        public void MonsterModelResolverDetectsOneBasedIndexing()
        {
            var records =
                new List<LegacyMonsterRecord>
                {
                    new LegacyMonsterRecord
                    {
                        MobId = 1,
                        MobName = "A",
                        ModelId = 1,
                        Level = 1,
                        Hp = 10
                    },
                    new LegacyMonsterRecord
                    {
                        MobId = 2,
                        MobName = "B",
                        ModelId = 3,
                        Level = 2,
                        Hp = 20
                    }
                };

            LegacyMonsterModelIndexMode mode =
                LegacyMonsterModelResolver.Detect(
                    records,
                    monRecordCount: 3);

            Assert.AreEqual(
                LegacyMonsterModelIndexMode.OneBased,
                mode);

            Assert.AreEqual(
                2,
                LegacyMonsterModelResolver.Resolve(
                    records[1],
                    mode,
                    3));
        }

    }
}
