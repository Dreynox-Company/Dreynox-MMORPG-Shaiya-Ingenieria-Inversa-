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
        public void CanonicalMonsterExt5AndDbMonsterTablesMatchPs0032WhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyMonsterSDataFile legacyClient =
                LegacyMonsterSDataParser.ParseEncrypted(
                    corpus.Resolve(
                        "DATA_Español/monster/monster.sdata"),
                    validateChecksum: true);

            Assert.AreEqual(4774, legacyClient.Records.Count);
            Assert.AreEqual("Error Monster", legacyClient.Records[0].MobName);
            CollectionAssert.AreEqual(
                new byte[] { 1, 0, 0, 50, 0 },
                legacyClient.Records[0].Extension5);
            Assert.AreEqual(217, legacyClient.Records[1].ModelId);

            LegacyDbMonsterDataFile db =
                LegacyDbMonsterDataParser.ParseData(
                    corpus.Resolve(
                        "DATA_Español/binarysdata/dbmonsterdata.sdata"));

            LegacyDbMonsterTextFile textTable =
                LegacyDbMonsterDataParser.ParseText(
                    corpus.Resolve(
                        "DATA_Español/binarysdata/dbmonstertext_spn.sdata"));

            LegacyMonFile models =
                LegacyMonParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/monster/monster.mon"));

            LegacySvmapFile map =
                LegacySvmapParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/0.svmap"));

            Assert.AreEqual(5008, db.Records.Count);
            Assert.AreEqual(5008, textTable.Count);
            Assert.AreEqual(863, models.Records.Count);

            Assert.IsTrue(
                db.TryGet(
                    1,
                    out LegacyDbMonsterDataRecord first));

            Assert.AreEqual(217, first.Image);
            Assert.AreEqual(38, first.Level);
            Assert.AreEqual(2765, first.Hp);

            Assert.IsTrue(
                db.TryGet(
                    4984,
                    out LegacyDbMonsterDataRecord tideWitch));

            Assert.AreEqual(262, tideWitch.Image);
            Assert.AreEqual(80, tideWitch.Level);
            Assert.AreEqual(130230, tideWitch.Hp);

            Assert.IsTrue(
                textTable.TryGetName(
                    4984,
                    out string tideWitchName));

            Assert.AreEqual(
                "Bruja de la marea",
                tideWitchName);

            var uniqueMobIds =
                new HashSet<uint>();

            int zeroPlaceholderRows = 0;
            long zeroPlaceholderInstances = 0;

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
                    LegacySvmapMonsterSpawn spawn =
                        area.Monsters[spawnIndex];

                    if (spawn.MobId == 0)
                    {
                        zeroPlaceholderRows++;
                        zeroPlaceholderInstances += spawn.Count;
                        continue;
                    }

                    if (spawn.Count > 0)
                        uniqueMobIds.Add(spawn.MobId);
                }
            }

            Assert.AreEqual(5, zeroPlaceholderRows);
            Assert.AreEqual(0, zeroPlaceholderInstances);
            Assert.AreEqual(64, uniqueMobIds.Count);
            Assert.IsFalse(uniqueMobIds.Contains(0));
            Assert.IsTrue(uniqueMobIds.Contains(4984));

            foreach (uint mobId in uniqueMobIds)
            {
                Assert.IsTrue(
                    db.TryGet(
                        mobId,
                        out LegacyDbMonsterDataRecord record),
                    "Missing DBMonsterData id " + mobId + ".");

                Assert.GreaterOrEqual(record.Image, 0);
                Assert.Less(
                    record.Image,
                    models.Records.Count,
                    "MON image out of range for MobId " + mobId + ".");

                Assert.IsTrue(
                    textTable.TryGetName(
                        mobId,
                        out string name),
                    "Missing Spanish DBMonsterText id " + mobId + ".");

                Assert.IsNotNull(name);
            }
        }

        [Test]
        public void BinaryMonsterDataParsesExplicitIdsAndSpanishNames()
        {
            string[] fields =
            {
                "id", "image", "level", "ai", "hp", "size", "attrib",
                "normaltime", "normalstep", "chasetime", "chasestep",
                "chaserange", "attackani1", "attacktype1", "attacktime1",
                "attackrange1", "attack1", "attackplus1", "attackattrib1",
                "attackspecial1", "attackok1", "attackani2", "attacktype2",
                "attacktime2", "attackrange2", "attack2", "attackplus2",
                "attackattrib2", "attackspecial2", "attackok2", "attackani3",
                "attacktype3", "attacktime3", "attackrange3", "attack3",
                "attackplus3", "attackattrib3", "attackspecial3", "attackok3"
            };

            LegacyDbMonsterDataFile db =
                LegacyDbMonsterDataParser.ParseDataPlain(
                    BuildBinaryMonsterDataFixture(fields));

            Assert.AreEqual(1, db.Records.Count);

            LegacyDbMonsterDataRecord record =
                db.Records[0];

            Assert.AreEqual(4984, record.Id);
            Assert.AreEqual(262, record.Image);
            Assert.AreEqual(80, record.Level);
            Assert.AreEqual(130230, record.Hp);
            Assert.AreEqual(2, record.Size);
            Assert.AreEqual(4, record.Element);

            LegacyDbMonsterTextFile names =
                LegacyDbMonsterDataParser.ParseTextPlain(
                    BuildBinaryMonsterTextFixture(
                        4984,
                        "Bruja de la marea"));

            Assert.IsTrue(
                names.TryGetName(
                    4984,
                    out string value));

            Assert.AreEqual(
                "Bruja de la marea",
                value);
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
            writer.Write(new byte[] { 1, 0, 0, 50, 0 });
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

        private static byte[] BuildBinaryMonsterDataFixture(
            IReadOnlyList<string> fields)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.Unicode))
            {
                writer.Write(new byte[128]);
                writer.Write(fields.Count);

                for (int i = 0; i < fields.Count; i++)
                {
                    writer.Write((byte)fields[i].Length);
                    writer.Write(
                        Encoding.Unicode.GetBytes(
                            fields[i]));
                }

                writer.Write(1);

                for (int i = 0; i < fields.Count; i++)
                {
                    long value = 0;

                    switch (fields[i])
                    {
                        case "id": value = 4984; break;
                        case "image": value = 262; break;
                        case "level": value = 80; break;
                        case "ai": value = 1; break;
                        case "hp": value = 130230; break;
                        case "size": value = 2; break;
                        case "attrib": value = 4; break;
                        case "normaltime": value = 4000; break;
                        case "normalstep": value = 6; break;
                        case "chasetime": value = 1800; break;
                        case "chasestep": value = 9; break;
                        case "chaserange": value = 9; break;
                    }

                    writer.Write(value);
                }

                return stream.ToArray();
            }
        }

        private static byte[] BuildBinaryMonsterTextFixture(
            long id,
            string name)
        {
            byte[] bytes = new byte[name.Length];
            for (int i = 0; i < name.Length; i++)
                bytes[i] = (byte)name[i];

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(new byte[128]);
                writer.Write(2);

                writer.Write((byte)2);
                writer.Write(Encoding.Unicode.GetBytes("id"));

                writer.Write((byte)4);
                writer.Write(Encoding.Unicode.GetBytes("name"));

                writer.Write(1);
                writer.Write(id);
                writer.Write(bytes.Length);
                writer.Write(bytes);

                return stream.ToArray();
            }
        }

    }
}
