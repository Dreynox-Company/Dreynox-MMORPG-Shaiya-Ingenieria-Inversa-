using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyNpcQuestTranslationTests
    {
        [Test]
        public void SyntheticTranslationBindsByNpcGroupOrder()
        {
            LegacyNpcQuestHeaderFile definitions =
                LegacyNpcQuestHeaderParser.ParsePlain(
                    BuildDefinitions());

            byte[] translations =
                BuildTranslations();

            LegacyNpcQuestTranslationHeaderFile parsed =
                LegacyNpcQuestTranslationParser.ParsePlain(
                    translations,
                    definitions);

            Assert.AreEqual(3, parsed.Translations.Count);
            Assert.AreEqual(7, parsed.QuestTranslationCount);

            Assert.IsTrue(
                parsed.TryGet(
                    1,
                    1,
                    out LegacyNpcTranslation merchant));

            Assert.AreEqual("Señor Mercader", merchant.Name);
            Assert.AreEqual("¡Buenos días!", merchant.WelcomeMessage);

            Assert.IsTrue(
                parsed.TryGet(
                    2,
                    1,
                    out LegacyNpcTranslation gate));

            Assert.AreEqual("Guardiana", gate.Name);
            CollectionAssert.AreEqual(
                new[] { "Destino A", "Destino B", "Destino C" },
                gate.TeleportNames);

            Assert.IsTrue(
                parsed.TryGet(
                    8,
                    26,
                    out LegacyNpcTranslation guard));

            Assert.AreEqual("Joel", guard.Name);
        }

        [Test]
        public void CanonicalSpanishNpcTranslationsMatchPs0032WhenCorpusIsConfigured()
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

            LegacyNpcQuestTranslationHeaderFile translations =
                LegacyNpcQuestTranslationParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/npc/npcquesttrans_spain.sdata"),
                    definitions);

            CollectionAssert.AreEqual(
                definitions.GroupCounts,
                translations.GroupCounts);

            Assert.AreEqual(2394, translations.Translations.Count);
            Assert.AreEqual(219081, translations.NpcTranslationBytesConsumed);
            Assert.AreEqual(4085, translations.QuestTranslationCount);
            Assert.AreEqual(4224036, translations.QuestTranslationPayloadBytes);

            AssertTranslation(
                translations,
                1,
                1,
                "Erina Probicio");

            Assert.IsTrue(
                translations.TryGet(
                    2,
                    1,
                    out LegacyNpcTranslation gate));

            Assert.AreEqual("Geneaa Irwen", gate.Name);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Villa Arktuis",
                    "Baluarte Huigronn",
                    "Apulune"
                },
                gate.TeleportNames);

            AssertTranslation(
                translations,
                3,
                1,
                "Luisan Xiphos");

            AssertTranslation(
                translations,
                8,
                1,
                "Joel Deian");

            AssertTranslation(
                translations,
                9,
                1,
                "Vaca");

            AssertTranslation(
                translations,
                11,
                1,
                "Maestro de la Luz del Gremio");

            AssertTranslation(
                translations,
                13,
                1,
                "Comandante de Combate de la Luz");
        }

        private static void AssertTranslation(
            LegacyNpcQuestTranslationHeaderFile file,
            int type,
            int id,
            string expectedName)
        {
            Assert.IsTrue(
                file.TryGet(
                    type,
                    id,
                    out LegacyNpcTranslation translation));

            Assert.AreEqual(
                expectedName,
                translation.Name);
        }

        private static byte[] BuildDefinitions()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                WriteMerchant(writer, 1);

                writer.Write(1);
                WriteGateKeeper(writer, 1);

                for (int group = 3; group <= 7; group++)
                    writer.Write(0);

                writer.Write(1);
                WriteStandard(
                    writer,
                    (byte)LegacyNpcType.Guard,
                    26,
                    9);

                for (int group = 9; group <= 13; group++)
                    writer.Write(0);

                return stream.ToArray();
            }
        }

        private static byte[] BuildTranslations()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                WriteCp1252String(writer, "Señor Mercader");
                WriteCp1252String(writer, "¡Buenos días!");

                writer.Write(1);
                WriteCp1252String(writer, "Guardiana");
                WriteCp1252String(writer, "Viaja con cuidado.");
                WriteCp1252String(writer, "Destino A");
                WriteCp1252String(writer, "Destino B");
                WriteCp1252String(writer, "Destino C");

                for (int group = 3; group <= 7; group++)
                    writer.Write(0);

                writer.Write(1);
                WriteCp1252String(writer, "Joel");
                WriteCp1252String(writer, "Mantén el orden.");

                for (int group = 9; group <= 13; group++)
                    writer.Write(0);

                writer.Write(7);
                writer.Write(new byte[12]);

                return stream.ToArray();
            }
        }

        private static void WriteMerchant(
            BinaryWriter writer,
            short typeId)
        {
            writer.Write((byte)LegacyNpcType.Merchant);
            writer.Write(typeId);
            writer.Write((byte)1);

            WriteSecond(writer, 42);

            writer.Write(0);
            WriteThird(writer);
        }

        private static void WriteGateKeeper(
            BinaryWriter writer,
            short typeId)
        {
            writer.Write((byte)LegacyNpcType.GateKeeper);
            writer.Write(typeId);

            WriteSecond(writer, 42);

            for (int i = 0; i < 3; i++)
            {
                writer.Write((short)(19 + i));
                writer.Write(10f + i);
                writer.Write(20f + i);
                writer.Write(30f + i);
                writer.Write(1000 + i);
            }

            WriteThird(writer);
        }

        private static void WriteStandard(
            BinaryWriter writer,
            byte type,
            short typeId,
            int model)
        {
            writer.Write(type);
            writer.Write(typeId);
            WriteSecond(writer, model);
            WriteThird(writer);
        }

        private static void WriteSecond(
            BinaryWriter writer,
            int model)
        {
            writer.Write(model);
            writer.Write(16);
            writer.Write(8000);
            writer.Write(1);
        }

        private static void WriteThird(
            BinaryWriter writer)
        {
            writer.Write(0);
            writer.Write(0);
        }

        private static void WriteCp1252String(
            BinaryWriter writer,
            string value)
        {
            byte[] bytes = EncodeCp1252(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static byte[] EncodeCp1252(
            string value)
        {
            byte[] bytes =
                new byte[value.Length];

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c <= 0xFF)
                {
                    bytes[i] = (byte)c;
                    continue;
                }

                throw new InvalidOperationException(
                    "Synthetic CP1252 fixture contains unsupported char U+" +
                    ((int)c).ToString("X4") + ".");
            }

            return bytes;
        }
    }
}
