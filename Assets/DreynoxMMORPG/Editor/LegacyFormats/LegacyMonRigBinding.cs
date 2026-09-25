using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.LocalData;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    internal sealed class LegacyMonClipBinding
    {
        public string Semantic, FileName;
        public bool Loop;
        public LegacyAniFile Source, Bound;
        public int ExtraTracks, ReparentedBones;
    }
    internal sealed class LegacyMonRigPlan
    {
        public Legacy3dcFile[] Parts;
        public LegacyAniFile Reference;
        public int ReferencePart;
        public List<LegacyMonClipBinding> Clips;
    }

    /// <summary>Bind every MON part and semantic slot before assets are written.</summary>
    internal static class LegacyMonRigBinding
    {
        public static LegacyMonRigPlan Prepare(Legacy3dcFile[] parts,
            List<LegacyMonClipBinding> clips, IReadOnlyList<LegacyMonEffect> effects)
        {
            if (parts == null || parts.Length == 0 || parts.Any(p => p == null))
                throw new InvalidDataException("MON requires parsed mesh parts.");
            if (clips == null || clips.Count == 0)
                throw new InvalidDataException("MON has no authored animation.");
            int referencePart = 0;
            for (int i = 1; i < parts.Length; i++)
                if (parts[i].InverseBindMatrices.Count > parts[referencePart].InverseBindMatrices.Count)
                    referencePart = i;
            LegacyAniFile reference = null;
            foreach (var candidate in clips)
            {
                if (candidate.Source == null) throw new InvalidDataException("Missing parsed MON animation.");
                int count = candidate.Source.Bones.Count;
                if (count == 0 || count > parts[referencePart].InverseBindMatrices.Count) continue;
                try
                {
                    foreach (var part in parts) LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(part, count);
                    if (effects != null && effects.Any(e => e.BoneId < 0 || e.BoneId >= count)) continue;
                    reference = candidate.Source;
                    break;
                }
                catch (InvalidDataException) { /* Try the next authored hierarchy, never fabricate bones. */ }
            }
            if (reference == null)
            {
                // All NPC clips may carry independent, unused tracks beyond the
                // mesh bind table. Select its closed authored prefix, not fake bones.
                int count = parts[referencePart].InverseBindMatrices.Count;
                foreach (var candidate in clips)
                {
                    if (count == 0 || candidate.Source.Bones.Count < count) continue;
                    try
                    {
                        ValidateParents(candidate.Source, count);
                        foreach (var part in parts) LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(part, count);
                        if (effects != null && effects.Any(e => e.BoneId < 0 || e.BoneId >= count)) continue;
                        reference = new LegacyAniFile { IsV2 = candidate.Source.IsV2,
                            StartKeyframe = candidate.Source.StartKeyframe, EndKeyframe = candidate.Source.EndKeyframe };
                        for (int i = 0; i < count; i++) reference.Bones.Add(candidate.Source.Bones[i]);
                        break;
                    }
                    catch (InvalidDataException) { /* No compatible closed prefix; try next authored clip. */ }
                }
            }
            if (reference == null)
                throw new InvalidDataException("No authored ANI hierarchy covers every weighted MON bone and effect anchor.");
            foreach (var clip in clips)
            {
                clip.ExtraTracks = clip.Source.Bones.Count - reference.Bones.Count;
                clip.Bound = BindClip(clip.Source, reference, out clip.ReparentedBones);
            }
            return new LegacyMonRigPlan { Parts = parts, Reference = reference,
                ReferencePart = referencePart, Clips = clips };
        }

        public static LegacyAniFile BindClip(LegacyAniFile source, LegacyAniFile reference, out int reparented)
        {
            if (source == null || reference == null) throw new ArgumentNullException();
            int count = reference.Bones.Count;
            ValidateParents(reference, count);
            ValidateParents(source, count);
            reparented = 0;
            for (int i = 0; i < count; i++)
                if (source.Bones[i].ParentBoneIndex != reference.Bones[i].ParentBoneIndex) reparented++;
            if (reparented == 0) return LegacyAniRigBinding.BodyClip(source, reference);

            // Convert changed-parent local channels into the selected hierarchy
            // while preserving evaluated world poses at authored frame times.
            uint span = source.EndKeyframe - source.StartKeyframe;
            if (span > 10000) throw new InvalidDataException("MON hierarchy rebake exceeds the sample budget.");
            var result = new LegacyAniFile { IsV2 = source.IsV2,
                StartKeyframe = source.StartKeyframe, EndKeyframe = source.EndKeyframe };
            var restPosition = new Vector3[count];
            var restRotation = new Quaternion[count];
            var world = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                var bone = source.Bones[i];
                int parent = bone.ParentBoneIndex;
                Matrix4x4 absolute = LegacyCoordinateBridge.Matrix(bone.AbsoluteBaseMatrix);
                Matrix4x4 local = parent < 0 ? absolute :
                    LegacyCoordinateBridge.Matrix(source.Bones[parent].AbsoluteBaseMatrix).inverse * absolute;
                restPosition[i] = local.GetColumn(3);
                restRotation[i] = local.rotation.normalized;
                if (parent == reference.Bones[i].ParentBoneIndex) result.Bones.Add(bone);
                else result.Bones.Add(new LegacyAniBone {
                    ParentBoneIndex = reference.Bones[i].ParentBoneIndex,
                    AbsoluteBaseMatrix = bone.AbsoluteBaseMatrix });
            }
            for (uint relative = 0; relative <= span; relative++)
            {
                uint frame = source.StartKeyframe + relative;
                for (int i = 0; i < count; i++)
                {
                    var bone = source.Bones[i];
                    Matrix4x4 local = Matrix4x4.TRS(
                        LocalAniPlayer.PositionAt(bone.Translations, frame, restPosition[i]),
                        LocalAniPlayer.RotationAt(bone.Rotations, frame, restRotation[i]), Vector3.one);
                    world[i] = bone.ParentBoneIndex < 0 ? local : world[bone.ParentBoneIndex] * local;
                }
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(result.Bones[i], source.Bones[i])) continue;
                    int parent = result.Bones[i].ParentBoneIndex;
                    Matrix4x4 local = parent < 0 ? world[i] : world[parent].inverse * world[i];
                    Vector3 position = local.GetColumn(3);
                    Quaternion rotation = local.rotation.normalized;
                    Matrix4x4 rebuilt = Matrix4x4.TRS(position, rotation, Vector3.one);
                    for (int c = 0; c < 16; c++)
                        if (float.IsNaN(local[c]) || float.IsInfinity(local[c]) || Mathf.Abs(local[c] - rebuilt[c]) > 0.002f)
                            throw new InvalidDataException("MON reparenting would discard scale/shear or non-finite geometry.");
                    result.Bones[i].Translations.Add(new LegacyAniTranslationFrame {
                        Frame = frame, Translation = LegacyCoordinateBridge.Position(position) });
                    result.Bones[i].Rotations.Add(new LegacyAniRotationFrame {
                        Frame = frame, Rotation = LegacyCoordinateBridge.Rotation(rotation) });
                }
            }
            return result;
        }
        private static void ValidateParents(LegacyAniFile source, int count)
        {
            if (count == 0 || source.Bones.Count < count)
                throw new InvalidDataException("MON clip is missing required body tracks.");
            if (source.EndKeyframe < source.StartKeyframe)
                throw new InvalidDataException("Invalid ANI time range.");
            for (int i = 0; i < count; i++)
                if (source.Bones[i].ParentBoneIndex < -1 || source.Bones[i].ParentBoneIndex >= i)
                    throw new InvalidDataException("MON hierarchy must be parent-before-child and closed over its body tracks.");
        }
    }
}
