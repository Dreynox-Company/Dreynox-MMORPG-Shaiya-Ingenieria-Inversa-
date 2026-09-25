using System;
using System.Collections.Generic;
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
        public const string Ps0032BindingLibrary = "monster.EFT";

        public static string ResolveBindingLibraryName(LegacyMonRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            // ps0032 0x521D73 reads binding EffectId and 0x521D78 selects
            // global 0x8E9498, initialized with data/Effect/monster.EFT at 0x4C4847.
            // The named AttachEffect field is NOT the library for this raw binding array.
            return Ps0032BindingLibrary;
        }

        public static LegacyMonAttachEffectCatalogAnalysis Analyze(
            CanonicalClientCorpus corpus, LegacyMonFile mon)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));
            if (mon == null) throw new ArgumentNullException(nameof(mon));
            var result = new LegacyMonAttachEffectCatalogAnalysis();
            LegacyEftFile library = null;
            foreach (LegacyMonRecord record in mon.Records)
            {
                if (record == null || record.Effects.Count == 0) continue;
                result.RecordsWithBindings++;
                if (library == null)
                    library = LegacyEftPrefabImporter.ParseCanonical(corpus, Ps0032BindingLibrary);
                if (library == null)
                    throw new FileNotFoundException("Missing ps0032 global attached-effect library: " + Ps0032BindingLibrary);
                if (!Fits(record, library.Effects.Count))
                    throw new InvalidDataException("MON binding outside global monster.EFT raw table: " + record.Name);
                result.ResolvedLibraries++;
                result.RawEffectExclusiveEvidence++;
            }
            result.Mode = result.RecordsWithBindings == 0
                ? LegacyMonAttachEffectIndexMode.None : LegacyMonAttachEffectIndexMode.RawEffect;
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
