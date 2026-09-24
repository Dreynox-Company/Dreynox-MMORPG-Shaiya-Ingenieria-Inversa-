using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public static class LegacyCharacterMakeLayoutCore
    {
        // Native CharacterMake layout is not ordered by the protocol/SData
        // job id. ps0032 renders:
        //
        //   Fighter   Defender   Priest
        //   Ranger    Archer     Mage
        //
        // Canonical job ids are:
        // Fighter=0, Defender=1, Ranger=2, Archer=3, Mage=4, Priest=5.
        private static readonly int[] JobByVisualSlot =
        {
            0, 1, 5,
            2, 3, 4
        };

        public static int VisualSlotCount =>
            JobByVisualSlot.Length;

        public static int JobForVisualSlot(
            int visualSlot)
        {
            if (visualSlot < 0 ||
                visualSlot >=
                    JobByVisualSlot.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualSlot));
            }

            return
                JobByVisualSlot[
                    visualSlot];
        }

        public static int VisualSlotForJob(
            int job)
        {
            for (int slot = 0;
                 slot <
                    JobByVisualSlot.Length;
                 slot++)
            {
                if (JobByVisualSlot[slot] ==
                    job)
                    return slot;
            }

            throw new ArgumentOutOfRangeException(
                nameof(job));
        }
    }
}
