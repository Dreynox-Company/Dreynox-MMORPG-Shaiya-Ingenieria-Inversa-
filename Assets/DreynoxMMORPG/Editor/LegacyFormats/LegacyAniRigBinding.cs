using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dreynox.Mmorpg.EditorTests")]

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>
    /// Bind the explicitly matching body prefix of an ANI to a mesh rig.
    /// ps0032 humf contains 36-body-bone clips with 0, 2 or 36 extra tracks.
    /// Extra tracks remain in the source file; they are not invented as skin bones.
    /// This is index/hierarchy compatibility, not a claim of visual equivalence.
    /// </summary>
    internal static class LegacyAniRigBinding
    {
        public static LegacyAniFile BodyClip(LegacyAniFile source, LegacyAniFile reference)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            int count = reference.Bones.Count;
            if (count == 0 || source.Bones.Count < count)
                throw new InvalidDataException("ANI is missing required body bones.");

            for (int i = 0; i < count; i++)
            {
                int expectedParent = reference.Bones[i].ParentBoneIndex;
                int actualParent = source.Bones[i].ParentBoneIndex;
                if (expectedParent < -1 || expectedParent >= count || expectedParent == i ||
                    actualParent != expectedParent)
                    throw new InvalidDataException("ANI body hierarchy differs at bone " + i +
                        ": expected parent " + expectedParent + ", actual " + actualParent + ".");
                int cursor = i;
                for (int steps = 0; cursor >= 0; steps++)
                {
                    if (steps >= count) throw new InvalidDataException("ANI body hierarchy is cyclic.");
                    cursor = reference.Bones[cursor].ParentBoneIndex;
                    if (cursor < -1 || cursor >= count)
                        throw new InvalidDataException("ANI body parent escapes the target skeleton.");
                }
            }

            var result = new LegacyAniFile
            {
                IsV2 = source.IsV2,
                StartKeyframe = source.StartKeyframe,
                EndKeyframe = source.EndKeyframe
            };
            // Read-only view of already parsed channels. Do not mutate source data.
            for (int i = 0; i < count; i++) result.Bones.Add(source.Bones[i]);
            return result;
        }
    }
}
