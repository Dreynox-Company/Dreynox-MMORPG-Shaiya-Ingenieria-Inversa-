using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public static class LegacyCharacterAppearanceUiCore
    {
        public const int GroupCount = 8;
        public const int VariantCount = 5;

        public static int ResolveGroupIndex(
            int family,
            int sex)
        {
            ValidateFamilySex(
                family,
                sex);

            return family * 2 + sex;
        }

        public static string ResolveGroupKey(
            int family,
            int sex)
        {
            int index =
                ResolveGroupIndex(
                    family,
                    sex);

            switch (index)
            {
                case 0: return "hum";
                case 1: return "huf";
                case 2: return "elm";
                case 3: return "elf";
                case 4: return "dem";
                case 5: return "def";
                case 6: return "vim";
                case 7: return "vif";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(index));
            }
        }

        public static string ResolveThumbnailFileName(
            int family,
            int sex,
            bool face,
            int variant)
        {
            if (variant < 0 ||
                variant >= VariantCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(variant));
            }

            return
                "create_appearance_" +
                ResolveGroupKey(
                    family,
                    sex) +
                "_" +
                (face ? "face" : "hair") +
                (variant + 1)
                    .ToString("D2") +
                ".tga";
        }

        public static int ResolveTextureIndex(
            int family,
            int sex,
            int variant)
        {
            if (variant < 0 ||
                variant >= VariantCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(variant));
            }

            return
                ResolveGroupIndex(
                    family,
                    sex) *
                VariantCount +
                variant;
        }

        private static void ValidateFamilySex(
            int family,
            int sex)
        {
            if (family < 0 ||
                family > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family));
            }

            if (sex < 0 ||
                sex > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sex));
            }
        }
    }
}
