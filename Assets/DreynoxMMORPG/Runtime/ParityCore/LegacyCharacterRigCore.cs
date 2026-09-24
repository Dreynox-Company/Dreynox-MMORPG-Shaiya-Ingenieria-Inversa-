using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum LegacyCharacterFamily
    {
        Human = 0,
        Elf = 1,
        DeathEater = 2,
        Vile = 3
    }

    public enum LegacyCharacterJob
    {
        Fighter = 0,
        Defender = 1,
        Ranger = 2,
        Archer = 3,
        Mage = 4,
        Priest = 5
    }

    public readonly struct LegacyCharacterRigSelection
    {
        public readonly int Family;
        public readonly int Job;
        public readonly int Sex;
        public readonly int Archetype;
        public readonly int NativeRigIndex;
        public readonly string Prefix;
        public readonly string FamilyFolder;
        public readonly string DisplayJob;

        public LegacyCharacterRigSelection(
            int family,
            int job,
            int sex,
            int archetype,
            string prefix,
            string familyFolder,
            string displayJob)
        {
            Family = family;
            Job = job;
            Sex = sex;
            Archetype = archetype;
            NativeRigIndex =
                archetype +
                2 * sex +
                4 * family;
            Prefix = prefix ?? string.Empty;
            FamilyFolder = familyFolder ?? string.Empty;
            DisplayJob = displayJob ?? string.Empty;
        }
    }

    public static class LegacyCharacterRigCore
    {
        // game.exe ps0032 x86:
        // 0x408BC1 reads family byte at +0x0C (0..3)
        // 0x408BD5..0x408CA8 branches on +0x0D and +0x0E.
        // 0x5B23B0 computes:
        // rigIndex = archetype + 2 * sex + 4 * family.
        //
        // +0x0D: 0 = male family prefix (hum/elm/dem/vim),
        //        1 = female family prefix (huw/elw/dew/viw).
        // +0x0E: class-archetype selector described below.
        //
        // Job order is the canonical SData order:
        // Fighter, Defender, Ranger, Archer, Mage, Priest.

        public static bool IsJobAllowed(
            int family,
            int job)
        {
            switch (family)
            {
                case (int)LegacyCharacterFamily.Human:
                    return job == 0 ||
                           job == 1 ||
                           job == 5;

                case (int)LegacyCharacterFamily.Elf:
                    return job == 2 ||
                           job == 3 ||
                           job == 4;

                case (int)LegacyCharacterFamily.DeathEater:
                    return job == 0 ||
                           job == 1 ||
                           job == 3;

                case (int)LegacyCharacterFamily.Vile:
                    return job == 2 ||
                           job == 4 ||
                           job == 5;

                default:
                    return false;
            }
        }

        public static int ResolveArchetype(
            int family,
            int job)
        {
            if (!IsJobAllowed(
                    family,
                    job))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(job),
                    "Job " +
                    job +
                    " is not available for family " +
                    family +
                    " in ps0032.");
            }

            switch (family)
            {
                case (int)LegacyCharacterFamily.Human:
                    // Fighter/Defender use hum?f; Priest uses hum?m.
                    return job == 5
                        ? 1
                        : 0;

                case (int)LegacyCharacterFamily.Elf:
                    // Ranger/Archer use el?r; Mage uses el?m.
                    return job == 4
                        ? 1
                        : 0;

                case (int)LegacyCharacterFamily.DeathEater:
                    // Warrior/Guardian use de?f; Hunter uses de?r.
                    return job == 3
                        ? 1
                        : 0;

                case (int)LegacyCharacterFamily.Vile:
                    // Assassin uses vi?r; Pagan/Oracle use vi?m.
                    return job == 2
                        ? 0
                        : 1;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(family));
            }
        }

        public static LegacyCharacterRigSelection Resolve(
            int family,
            int job,
            int sex)
        {
            if (family < 0 ||
                family > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family));
            }

            if (job < 0 ||
                job > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(job));
            }

            if (sex < 0 ||
                sex > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sex));
            }

            int archetype =
                ResolveArchetype(
                    family,
                    job);

            return new LegacyCharacterRigSelection(
                family,
                job,
                sex,
                archetype,
                ResolvePrefix(
                    family,
                    sex,
                    archetype),
                ResolveFamilyFolder(
                    family),
                ResolveDisplayJob(
                    family,
                    job));
        }

        public static string ResolveGlobalJobName(
            int job)
        {
            switch (job)
            {
                case 0: return "Fighter";
                case 1: return "Defender";
                case 2: return "Ranger";
                case 3: return "Archer";
                case 4: return "Mage";
                case 5: return "Priest";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(job));
            }
        }

        public static string ResolveDisplayJob(
            int family,
            int job)
        {
            if (!IsJobAllowed(
                    family,
                    job))
            {
                return ResolveGlobalJobName(
                    job);
            }

            if (family <=
                (int)LegacyCharacterFamily.Elf)
            {
                return ResolveGlobalJobName(
                    job);
            }

            switch (job)
            {
                case 0: return "Warrior";
                case 1: return "Guardian";
                case 2: return "Assassin";
                case 3: return "Hunter";
                case 4: return "Pagan";
                case 5: return "Oracle";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(job));
            }
        }

        private static string ResolvePrefix(
            int family,
            int sex,
            int archetype)
        {
            int index =
                archetype +
                2 * sex +
                4 * family;

            switch (index)
            {
                case 0: return "humf";
                case 1: return "humm";
                case 2: return "huwf";
                case 3: return "huwm";

                case 4: return "elmr";
                case 5: return "elmm";
                case 6: return "elwr";
                case 7: return "elwm";

                case 8: return "demf";
                case 9: return "demr";
                case 10: return "dewf";
                case 11: return "dewr";

                case 12: return "vimr";
                case 13: return "vimm";
                case 14: return "viwr";
                case 15: return "viwm";

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(index));
            }
        }

        private static string ResolveFamilyFolder(
            int family)
        {
            switch (family)
            {
                case 0: return "human";
                case 1: return "elf";
                case 2: return "deatheater";
                case 3: return "vile";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(family));
            }
        }
    }
}
