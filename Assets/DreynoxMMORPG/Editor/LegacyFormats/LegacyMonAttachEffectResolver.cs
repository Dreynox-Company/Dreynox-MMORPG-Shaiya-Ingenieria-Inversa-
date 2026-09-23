using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Vfx;
using Dreynox.Mmorpg.World;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyMonAttachEffectIndexMode
    {
        None,
        Sequence,
        RawEffect,
        Ambiguous
    }

    public sealed class LegacyMonAttachEffectCatalogAnalysis
    {
        public LegacyMonAttachEffectIndexMode Mode;
        public int RecordsWithBindings;
        public int ResolvedLibraries;
        public int SequenceExclusiveEvidence;
        public int RawEffectExclusiveEvidence;
        public int AmbiguousEvidence;
    }

    public static class LegacyMonAttachEffectResolver
    {
        public static LegacyMonAttachEffectCatalogAnalysis Analyze(
            CanonicalClientCorpus corpus,
            LegacyMonFile mon)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));
            if (mon == null)
                throw new ArgumentNullException(nameof(mon));

            var result =
                new LegacyMonAttachEffectCatalogAnalysis();

            for (int recordIndex = 0;
                 recordIndex < mon.Records.Count;
                 recordIndex++)
            {
                LegacyMonRecord record =
                    mon.Records[recordIndex];

                if (record == null ||
                    record.Effects.Count == 0)
                    continue;

                result.RecordsWithBindings++;

                string libraryName =
                    LegacyMonEntityDescriptor
                        .NormalizeResourceName(
                            record.AttachEffect);

                if (string.IsNullOrWhiteSpace(
                        libraryName))
                {
                    throw new InvalidDataException(
                        "MON record " + recordIndex +
                        " ('" + record.Name +
                        "') declares " +
                        record.Effects.Count +
                        " attached effects but has no AttachEffect library.");
                }

                LegacyEftFile library =
                    LegacyEftPrefabImporter
                        .ParseCanonical(
                            corpus,
                            libraryName);

                if (library == null)
                {
                    throw new FileNotFoundException(
                        "MON record " + recordIndex +
                        " ('" + record.Name +
                        "') references missing attached effect library '" +
                        libraryName + "'.");
                }

                result.ResolvedLibraries++;

                bool fitsSequences =
                    Fits(
                        record,
                        library.Sequences.Count);

                bool fitsRawEffects =
                    Fits(
                        record,
                        library.Effects.Count);

                if (!fitsSequences &&
                    !fitsRawEffects)
                {
                    throw new InvalidDataException(
                        "MON record " + recordIndex +
                        " ('" + record.Name +
                        "') contains attached EffectId values outside both " +
                        "EFT sequences (" +
                        library.Sequences.Count +
                        ") and raw effects (" +
                        library.Effects.Count +
                        ") in '" + libraryName + "'.");
                }

                if (fitsSequences &&
                    !fitsRawEffects)
                {
                    result
                        .SequenceExclusiveEvidence++;
                }
                else if (fitsRawEffects &&
                         !fitsSequences)
                {
                    result
                        .RawEffectExclusiveEvidence++;
                }
                else
                {
                    result.AmbiguousEvidence++;
                }
            }

            if (result.RecordsWithBindings == 0)
            {
                result.Mode =
                    LegacyMonAttachEffectIndexMode.None;

                return result;
            }

            if (result.SequenceExclusiveEvidence > 0 &&
                result.RawEffectExclusiveEvidence > 0)
            {
                throw new InvalidDataException(
                    "MON attached-effect semantics are inconsistent across " +
                    "the catalog: " +
                    result.SequenceExclusiveEvidence +
                    " sequence-exclusive records and " +
                    result.RawEffectExclusiveEvidence +
                    " raw-effect-exclusive records.");
            }

            if (result.SequenceExclusiveEvidence > 0)
            {
                result.Mode =
                    LegacyMonAttachEffectIndexMode.Sequence;
            }
            else if (result.RawEffectExclusiveEvidence > 0)
            {
                result.Mode =
                    LegacyMonAttachEffectIndexMode.RawEffect;
            }
            else
            {
                result.Mode =
                    LegacyMonAttachEffectIndexMode.Ambiguous;
            }

            return result;
        }

        public static LegacyEftInvocationKind ResolveInvocationKind(
            LegacyMonRecord record,
            LegacyEftFile library,
            LegacyMonAttachEffectIndexMode catalogMode)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            bool fitsSequences =
                Fits(
                    record,
                    library.Sequences.Count);

            bool fitsRawEffects =
                Fits(
                    record,
                    library.Effects.Count);

            if (fitsSequences &&
                !fitsRawEffects)
            {
                return LegacyEftInvocationKind.Sequence;
            }

            if (fitsRawEffects &&
                !fitsSequences)
            {
                return LegacyEftInvocationKind.RawEffect;
            }

            if (!fitsSequences &&
                !fitsRawEffects)
            {
                throw new InvalidDataException(
                    "Attached effect IDs do not fit this EFT library.");
            }

            switch (catalogMode)
            {
                case LegacyMonAttachEffectIndexMode.Sequence:
                    return LegacyEftInvocationKind.Sequence;

                case LegacyMonAttachEffectIndexMode.RawEffect:
                    return LegacyEftInvocationKind.RawEffect;

                default:
                    throw new InvalidDataException(
                        "Attached EffectId is ambiguous for MON record '" +
                        record.Name +
                        "'. The catalog has not established whether ids " +
                        "index EFT sequences or raw components.");
            }
        }

        private static bool Fits(
            LegacyMonRecord record,
            int count)
        {
            if (count <= 0)
                return false;

            for (int i = 0;
                 i < record.Effects.Count;
                 i++)
            {
                int id =
                    record.Effects[i].EffectId;

                if (id < 0 ||
                    id >= count)
                    return false;
            }

            return true;
        }
    }
}
