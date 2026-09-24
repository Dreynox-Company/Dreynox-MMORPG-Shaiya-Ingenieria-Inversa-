using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Vfx;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyMonAttachEffectTests
    {
        [Test]
        public void ResolverSelectsSequenceWhenOnlySequenceIndexFits()
        {
            LegacyMonRecord record =
                RecordWithEffectId(2);

            LegacyEftFile library =
                Library(
                    rawEffects: 1,
                    sequences: 3);

            Assert.AreEqual(
                LegacyEftInvocationKind.Sequence,
                LegacyMonAttachEffectResolver
                    .ResolveInvocationKind(
                        record,
                        library,
                        LegacyMonAttachEffectIndexMode.Sequence));
        }

        [Test]
        public void ResolverSelectsRawEffectWhenOnlyRawIndexFits()
        {
            LegacyMonRecord record =
                RecordWithEffectId(2);

            LegacyEftFile library =
                Library(
                    rawEffects: 3,
                    sequences: 1);

            Assert.AreEqual(
                LegacyEftInvocationKind.RawEffect,
                LegacyMonAttachEffectResolver
                    .ResolveInvocationKind(
                        record,
                        library,
                        LegacyMonAttachEffectIndexMode.RawEffect));
        }

        [Test]
        public void AmbiguousRecordUsesCatalogEvidence()
        {
            LegacyMonRecord record =
                RecordWithEffectId(0);

            LegacyEftFile library =
                Library(
                    rawEffects: 2,
                    sequences: 2);

            Assert.AreEqual(
                LegacyEftInvocationKind.Sequence,
                LegacyMonAttachEffectResolver
                    .ResolveInvocationKind(
                        record,
                        library,
                        LegacyMonAttachEffectIndexMode.Sequence));

            Assert.AreEqual(
                LegacyEftInvocationKind.RawEffect,
                LegacyMonAttachEffectResolver
                    .ResolveInvocationKind(
                        record,
                        library,
                        LegacyMonAttachEffectIndexMode.RawEffect));
        }

        [Test]
        public void AmbiguousRecordWithoutCatalogEvidenceFailsClosed()
        {
            LegacyMonRecord record =
                RecordWithEffectId(0);

            LegacyEftFile library =
                Library(
                    rawEffects: 2,
                    sequences: 2);

            Assert.Throws<InvalidDataException>(
                () =>
                    LegacyMonAttachEffectResolver
                        .ResolveInvocationKind(
                            record,
                            library,
                            LegacyMonAttachEffectIndexMode.Ambiguous));
        }

        [Test]
        public void CanonicalMonAttachEffectSemanticsResolveWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            AssertCatalog(
                corpus,
                "DATA_Español/monster/monster.mon");

            AssertCatalog(
                corpus,
                "DATA_Español/npc/npc.mon");

            AssertCatalog(
                corpus,
                "DATA_Español/character/wing/wing.mon");
        }

        private static void AssertCatalog(
            CanonicalClientCorpus corpus,
            string relativePath)
        {
            LegacyMonFile mon =
                LegacyMonParser.Parse(
                    corpus.Resolve(relativePath));

            LegacyMonAttachEffectCatalogAnalysis analysis =
                LegacyMonAttachEffectResolver.Analyze(
                    corpus,
                    mon);

            if (analysis.RecordsWithBindings == 0)
            {
                Assert.AreEqual(
                    LegacyMonAttachEffectIndexMode.None,
                    analysis.Mode);

                return;
            }

            Assert.AreEqual(
                analysis.RecordsWithBindings,
                analysis.ResolvedLibraries);

            Assert.AreNotEqual(
                LegacyMonAttachEffectIndexMode.Ambiguous,
                analysis.Mode,
                relativePath +
                " has only ambiguous attached EffectId evidence.");

            Assert.IsFalse(
                analysis.SequenceExclusiveEvidence > 0 &&
                analysis.RawEffectExclusiveEvidence > 0);

            for (int i = 0;
                 i < mon.Records.Count;
                 i++)
            {
                LegacyMonRecord record =
                    mon.Records[i];

                if (record == null ||
                    record.Effects.Count == 0)
                    continue;

                LegacyEftFile library =
                    LegacyEftPrefabImporter.ParseCanonical(
                        corpus,
                        record.AttachEffect);

                Assert.IsNotNull(
                    library,
                    record.Name);

                Assert.DoesNotThrow(
                    () =>
                        LegacyMonAttachEffectResolver
                            .ResolveInvocationKind(
                                record,
                                library,
                                analysis.Mode),
                    record.Name);
            }
        }

        private static LegacyMonRecord RecordWithEffectId(
            int effectId)
        {
            var record =
                new LegacyMonRecord
                {
                    Name = "Fixture",
                    AttachEffect = "fixture.eft"
                };

            record.Effects.Add(
                new LegacyMonEffect
                {
                    BoneId = 4,
                    EffectId = effectId
                });

            return record;
        }

        private static LegacyEftFile Library(
            int rawEffects,
            int sequences)
        {
            var result =
                new LegacyEftFile();

            for (int i = 0;
                 i < rawEffects;
                 i++)
            {
                result.Effects.Add(
                    new LegacyEftEffect());
            }

            for (int i = 0;
                 i < sequences;
                 i++)
            {
                result.Sequences.Add(
                    new LegacyEftSequence
                    {
                        Name =
                            "Sequence_" + i
                    });
            }

            return result;
        }
    }
}
