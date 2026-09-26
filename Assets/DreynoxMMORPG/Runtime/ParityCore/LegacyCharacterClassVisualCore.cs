using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum LegacyCharacterWeaponKind
    {
        OneHandSword = 0,
        TwoHandSword = 1,
        DualSword = 2,
        Spear = 3,
        OneHandBlunt = 4,
        TwoHandBlunt = 5,
        Shield = 6,
        OneHandAxe = 7,
        TwoHandAxe = 8,
        DualAxe = 9,
        ReversedSword = 10,
        Dagger = 11,
        Knuckle = 12,
        Bow = 13,
        Crossbow = 14,
        ThrowingWeapon = 15,
        Staff = 16
    }

    public readonly struct LegacyCharacterClassVisualProfile
    {
        private readonly LegacyCharacterWeaponKind[] _weapons;

        public readonly int Family;
        public readonly int Job;
        public readonly int VisualSlot;
        public readonly string ClassInfoKey;
        public readonly string DisplayJob;
        public readonly string Explanation;

        public LegacyCharacterClassVisualProfile(
            int family,
            int job,
            int visualSlot,
            string classInfoKey,
            string displayJob,
            string explanation,
            LegacyCharacterWeaponKind[] weapons)
        {
            Family = family;
            Job = job;
            VisualSlot = visualSlot;
            ClassInfoKey = classInfoKey ?? string.Empty;
            DisplayJob = displayJob ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            _weapons =
                weapons ??
                Array.Empty<LegacyCharacterWeaponKind>();
        }

        public IReadOnlyList<LegacyCharacterWeaponKind> Weapons =>
            _weapons ??
            Array.Empty<LegacyCharacterWeaponKind>();
    }

    public static class LegacyCharacterClassVisualCore
    {
        public static LegacyCharacterClassVisualProfile Resolve(
            int family,
            int job)
        {
            if (!LegacyCharacterRigCore.IsJobAllowed(
                    family,
                    job))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(job),
                    "Class visual profile is not valid for the selected family.");
            }

            return new LegacyCharacterClassVisualProfile(
                family,
                job,
                LegacyCharacterMakeLayoutCore.VisualSlotForJob(job),
                ResolveClassInfoKey(job),
                LegacyCharacterRigCore.ResolveDisplayJob(family, job),
                ResolveExplanation(family, job),
                ResolveWeapons(family, job));
        }

        public static string ResolveClassInfoKey(
            int job)
        {
            switch (job)
            {
                case 0: return "character.make.classInfo.fighterBars";
                case 1: return "character.make.classInfo.defenderBars";
                case 2: return "character.make.classInfo.rangerBars";
                case 3: return "character.make.classInfo.archerBars";
                case 4: return "character.make.classInfo.mageBars";
                case 5: return "character.make.classInfo.priestBars";
                default:
                    throw new ArgumentOutOfRangeException(nameof(job));
            }
        }

        public static LegacyCharacterWeaponKind[] ResolveWeapons(
            int family,
            int job)
        {
            bool fury =
                family >=
                (int)LegacyCharacterFamily.DeathEater;

            switch (job)
            {
                case 0:
                    return fury
                        ? new[]
                        {
                            LegacyCharacterWeaponKind.OneHandAxe,
                            LegacyCharacterWeaponKind.TwoHandAxe,
                            LegacyCharacterWeaponKind.DualAxe,
                            LegacyCharacterWeaponKind.Spear,
                            LegacyCharacterWeaponKind.OneHandBlunt,
                            LegacyCharacterWeaponKind.TwoHandBlunt,
                            LegacyCharacterWeaponKind.Shield
                        }
                        : new[]
                        {
                            LegacyCharacterWeaponKind.OneHandSword,
                            LegacyCharacterWeaponKind.TwoHandSword,
                            LegacyCharacterWeaponKind.DualSword,
                            LegacyCharacterWeaponKind.Spear,
                            LegacyCharacterWeaponKind.OneHandBlunt,
                            LegacyCharacterWeaponKind.TwoHandBlunt,
                            LegacyCharacterWeaponKind.Shield
                        };

                case 1:
                    return fury
                        ? new[]
                        {
                            LegacyCharacterWeaponKind.OneHandAxe,
                            LegacyCharacterWeaponKind.TwoHandAxe,
                            LegacyCharacterWeaponKind.OneHandBlunt,
                            LegacyCharacterWeaponKind.TwoHandBlunt,
                            LegacyCharacterWeaponKind.Shield
                        }
                        : new[]
                        {
                            LegacyCharacterWeaponKind.OneHandSword,
                            LegacyCharacterWeaponKind.TwoHandSword,
                            LegacyCharacterWeaponKind.OneHandBlunt,
                            LegacyCharacterWeaponKind.TwoHandBlunt,
                            LegacyCharacterWeaponKind.Shield
                        };

                case 2:
                    return new[]
                    {
                        LegacyCharacterWeaponKind.ReversedSword,
                        LegacyCharacterWeaponKind.Dagger,
                        LegacyCharacterWeaponKind.Knuckle
                    };

                case 3:
                    return fury
                        ? new[]
                        {
                            LegacyCharacterWeaponKind.Bow,
                            LegacyCharacterWeaponKind.ThrowingWeapon
                        }
                        : new[]
                        {
                            LegacyCharacterWeaponKind.Bow,
                            LegacyCharacterWeaponKind.Crossbow
                        };

                case 4:
                case 5:
                    return new[]
                    {
                        LegacyCharacterWeaponKind.Dagger,
                        LegacyCharacterWeaponKind.Staff
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(job));
            }
        }

        public static string ResolveExplanation(
            int family,
            int job)
        {
            string name =
                LegacyCharacterRigCore.ResolveDisplayJob(
                    family,
                    job);

            bool fury =
                family >=
                (int)LegacyCharacterFamily.DeathEater;

            switch (job)
            {
                case 0:
                    if (!fury)
                    {
                        return
                            "The Fighter is your standard melee combatant. Up close\n" +
                            "and personal is how the Fighter prefers confrontation.\n" +
                            "Physical attack power is the focus of the Fighter, but don't\n" +
                            "be fooled. A certain amount of Magical Points (MP) is\n" +
                            "needed to power the Fighter's devastating Special Skills.\n\n" +
                            "Characteristics:\n" +
                            "· Wide range of available weapons\n" +
                            "· Powerful physical attacks";
                    }

                    return
                        "The " + name +
                        " is a front-line melee combatant focused on physical attack power.\n" +
                        "Its native weapon set replaces swords with axes while preserving spear and heavy-weapon options.";

                case 1:
                    return
                        "The " + name +
                        " is a defensive front-line class built around durability, protection and control.\n" +
                        "It trades raw damage for survivability and group utility.";

                case 2:
                    return
                        "The " + name +
                        " is an agile close-range class that relies on speed, evasion and fast weapon combinations.";

                case 3:
                    return
                        "The " + name +
                        " attacks from range and emphasizes accuracy, reach and sustained pressure.";

                case 4:
                    return
                        "The " + name +
                        " is an offensive magic class focused on intelligence and destructive ranged attacks.";

                case 5:
                    return
                        "The " + name +
                        " is a wisdom-focused support caster specializing in healing and group utility.";

                default:
                    throw new ArgumentOutOfRangeException(nameof(job));
            }
        }
    }
}
