using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyNpcQuestHeaderTests
    {
        [Test]
        public void SyntheticHeaderParsesMerchantGatekeeperAndGuard()
        {
            byte[] payload;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Merchant
                writer.Write(1);
                WriteMerchant(
                    writer,
                    typeId: 1,
                    merchantType: 2,
                    model: 42,
                    faction: 1);

                // GateKeeper
                writer.Write(1);
                WriteGateKeeper(
                    writer,
                    typeId: 1,
                    model: 42,
                    faction: 1);

                // Blacksmith .. Normal = zero counts
                for (int group = 3; group <= 7; group++)
                    writer.Write(0);

                // Guard
                writer.Write(1);
                WriteStandard(
                    writer,
                    (byte)LegacyNpcType.Guard,
                    typeId: 26,
                    model: 9,
                    faction: 1);

                // Animal .. CombatCommander = zero counts
                for (int group = 9; group <= 13; group++)
                    writer.Write(0);

                // Tail intentionally left unparsed.
                writer.Write(new byte[32]);

                payload = stream.ToArray();
            }

            LegacyNpcQuestHeaderFile parsed =
                LegacyNpcQuestHeaderParser.ParsePlain(payload);

            Assert.AreEqual(13, parsed.GroupCounts.Count);
            Assert.AreEqual(3, parsed.Definitions.Count);
            Assert.AreEqual(32, parsed.UnparsedTailBytes);

            Assert.IsTrue(
                parsed.TryGet(
                    (int)LegacyNpcType.Merchant,
                    1,
                    out LegacyNpcDefinition merchant));

            Assert.AreEqual(42, merchant.Model);
            Assert.AreEqual(2, merchant.MerchantType.Value);
            Assert.AreEqual(2, merchant.SaleItems.Count);

            Assert.IsTrue(
                parsed.TryGet(
                    (int)LegacyNpcType.GateKeeper,
                    1,
                    out LegacyNpcDefinition gate));

            Assert.AreEqual(3, gate.GateTargets.Count);
            Assert.AreEqual(19, gate.GateTargets[0].MapId);
            Assert.AreEqual(8600, gate.GateTargets[0].Cost);

            Assert.IsTrue(
                parsed.TryGet(
                    (int)LegacyNpcType.Guard,
                    26,
                    out LegacyNpcDefinition guard));

            Assert.AreEqual(9, guard.Model);
        }

        [Test]
        public void CanonicalNpcHeaderMatchesOfflineMapZeroWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyNpcQuestHeaderFile definitions =
                LegacyNpcQuestHeaderParser.ParseEncrypted(
                    corpus.Resolve(
                        "DATA_Español/npc/npcquest.sdata"),
                    validateChecksum: true);

            CollectionAssert.AreEqual(
                new[]
                {
                    455, 147, 69, 2, 8, 45, 1354,
                    168, 27, 89, 6, 20, 4
                },
                definitions.GroupCounts);

            Assert.AreEqual(2394, definitions.Definitions.Count);
            Assert.AreEqual(98637, definitions.HeaderBytesConsumed);
            Assert.AreEqual(1701067, definitions.UnparsedTailBytes);

            Assert.IsTrue(
                definitions.TryGet(
                    1,
                    1,
                    out LegacyNpcDefinition merchant));

            Assert.AreEqual(42, merchant.Model);
            Assert.AreEqual(25, merchant.SaleItems.Count);

            Assert.IsTrue(
                definitions.TryGet(
                    2,
                    1,
                    out LegacyNpcDefinition gate));

            Assert.AreEqual(42, gate.Model);
            Assert.AreEqual(19, gate.GateTargets[0].MapId);
            Assert.AreEqual(28, gate.GateTargets[1].MapId);
            Assert.AreEqual(35, gate.GateTargets[2].MapId);

            Assert.IsTrue(
                definitions.TryGet(
                    8,
                    26,
                    out LegacyNpcDefinition guard));

            Assert.AreEqual(9, guard.Model);

            LegacyMonFile npcModels =
                LegacyMonParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/npc/npc.mon"));

            Assert.AreEqual(264, npcModels.Records.Count);

            LegacySvmapFile map =
                LegacySvmapParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/0.svmap"));

            int unresolvedDefinitions = 0;
            int unresolvedPositions = 0;
            int resolvedDefinitions = 0;
            int resolvedPositions = 0;
            var models = new HashSet<int>();

            for (int i = 0; i < map.Npcs.Count; i++)
            {
                LegacySvmapNpc npc =
                    map.Npcs[i];

                if (!definitions.TryGet(
                        npc.NpcType,
                        npc.NpcId,
                        out LegacyNpcDefinition definition))
                {
                    unresolvedDefinitions++;
                    unresolvedPositions +=
                        npc.Positions.Count;

                    Assert.AreEqual(0, npc.NpcType);
                    Assert.AreEqual(0, npc.NpcId);
                    continue;
                }

                resolvedDefinitions++;
                resolvedPositions +=
                    npc.Positions.Count;

                Assert.GreaterOrEqual(
                    definition.Model,
                    0);

                Assert.Less(
                    definition.Model,
                    npcModels.Records.Count);

                models.Add(definition.Model);
            }

            Assert.AreEqual(150, map.Npcs.Count);
            Assert.AreEqual(9, unresolvedDefinitions);
            Assert.AreEqual(18, unresolvedPositions);
            Assert.AreEqual(141, resolvedDefinitions);
            Assert.AreEqual(180, resolvedPositions);
            Assert.AreEqual(41, models.Count);
        }

        private static void WriteMerchant(
            BinaryWriter writer,
            short typeId,
            byte merchantType,
            int model,
            int faction)
        {
            WriteFirst(
                writer,
                (byte)LegacyNpcType.Merchant,
                typeId);

            writer.Write(merchantType);
            WriteSecond(writer, model, faction);

            writer.Write(2);
            writer.Write((byte)1);
            writer.Write((byte)10);
            writer.Write((byte)2);
            writer.Write((byte)20);

            WriteThird(writer);
        }

        private static void WriteGateKeeper(
            BinaryWriter writer,
            short typeId,
            int model,
            int faction)
        {
            WriteFirst(
                writer,
                (byte)LegacyNpcType.GateKeeper,
                typeId);

            WriteSecond(writer, model, faction);

            WriteTarget(writer, 19, 1f, 2f, 3f, 8600);
            WriteTarget(writer, 28, 4f, 5f, 6f, 32700);
            WriteTarget(writer, 35, 7f, 8f, 9f, 20900);

            WriteThird(writer);
        }

        private static void WriteStandard(
            BinaryWriter writer,
            byte type,
            short typeId,
            int model,
            int faction)
        {
            WriteFirst(writer, type, typeId);
            WriteSecond(writer, model, faction);
            WriteThird(writer);
        }

        private static void WriteFirst(
            BinaryWriter writer,
            byte type,
            short typeId)
        {
            writer.Write(type);
            writer.Write(typeId);
        }

        private static void WriteSecond(
            BinaryWriter writer,
            int model,
            int faction)
        {
            writer.Write(model);
            writer.Write(16);
            writer.Write(8000);
            writer.Write(faction);
        }

        private static void WriteThird(
            BinaryWriter writer)
        {
            writer.Write(0);
            writer.Write(0);
        }

        private static void WriteTarget(
            BinaryWriter writer,
            short mapId,
            float x,
            float y,
            float z,
            int cost)
        {
            writer.Write(mapId);
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
            writer.Write(cost);
        }
    }
}
