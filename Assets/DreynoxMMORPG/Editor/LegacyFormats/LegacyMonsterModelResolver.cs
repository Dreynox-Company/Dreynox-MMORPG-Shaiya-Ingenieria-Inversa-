using System;
using System.Collections.Generic;
using System.IO;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyMonsterModelIndexMode
    {
        Direct,
        OneBased
    }

    public static class LegacyMonsterModelResolver
    {
        public static LegacyMonsterModelIndexMode Detect(
            IReadOnlyList<LegacyMonsterRecord> records,
            int monRecordCount)
        {
            if (records == null)
                throw new ArgumentNullException(nameof(records));
            if (monRecordCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(monRecordCount));

            int directInvalid = 0;
            int oneBasedInvalid = 0;
            int zeroModels = 0;
            int meaningful = 0;

            for (int i = 0; i < records.Count; i++)
            {
                LegacyMonsterRecord record = records[i];
                if (record == null)
                    continue;

                // Empty rows are common in client tables and should not influence
                // the index-base decision.
                bool hasMeaning =
                    !string.IsNullOrWhiteSpace(record.MobName) ||
                    record.Hp > 0 ||
                    record.Level > 0;

                if (!hasMeaning)
                    continue;

                meaningful++;

                int modelId = record.ModelId;
                if (modelId == 0)
                    zeroModels++;

                if (modelId < 0 || modelId >= monRecordCount)
                    directInvalid++;

                int oneBased = modelId - 1;
                if (oneBased < 0 || oneBased >= monRecordCount)
                    oneBasedInvalid++;
            }

            if (meaningful == 0)
                throw new InvalidDataException(
                    "Monster.SData contains no meaningful rows.");

            if (directInvalid < oneBasedInvalid)
                return LegacyMonsterModelIndexMode.Direct;

            if (oneBasedInvalid < directInvalid)
                return LegacyMonsterModelIndexMode.OneBased;

            // If both modes have the same structural coverage, a meaningful
            // zero model can only be represented by direct indexing.
            if (zeroModels > 0)
                return LegacyMonsterModelIndexMode.Direct;

            throw new InvalidDataException(
                "Monster.SData model index base is ambiguous. " +
                "Direct invalid=" + directInvalid +
                ", one-based invalid=" + oneBasedInvalid +
                ". Add corpus evidence before importing monsters.");
        }

        public static int Resolve(
            LegacyMonsterRecord record,
            LegacyMonsterModelIndexMode mode,
            int monRecordCount)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (monRecordCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(monRecordCount));

            int index =
                mode == LegacyMonsterModelIndexMode.OneBased
                    ? record.ModelId - 1
                    : record.ModelId;

            if (index < 0 || index >= monRecordCount)
            {
                throw new InvalidDataException(
                    "MobId " + record.MobId +
                    " resolves ModelId " + record.ModelId +
                    " to MON index " + index +
                    " outside 0.." + (monRecordCount - 1) + ".");
            }

            return index;
        }
    }
}
