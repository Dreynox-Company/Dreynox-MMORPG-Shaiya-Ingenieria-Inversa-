using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyAniFile
    {
        public const float FramesPerSecond = 30f;
        public bool IsV2 { get; internal set; }
        public uint StartKeyframe { get; internal set; }
        public uint EndKeyframe { get; internal set; }
        // Known signed pre-roll -1 is shifted with all keys to a zero-based timeline.
        public int FrameOffset { get; internal set; }
        public List<LegacyAniBone> Bones { get; } = new List<LegacyAniBone>();
        public float DurationSeconds => (EndKeyframe - StartKeyframe) / FramesPerSecond;
    }
    public sealed class LegacyAniBone
    {
        public int ParentBoneIndex;
        public Matrix4x4 AbsoluteBaseMatrix;
        public List<LegacyAniRotationFrame> Rotations { get; } = new List<LegacyAniRotationFrame>();
        public List<LegacyAniTranslationFrame> Translations { get; } = new List<LegacyAniTranslationFrame>();
    }
    public struct LegacyAniRotationFrame { public uint Frame; public Quaternion Rotation; }
    public struct LegacyAniTranslationFrame { public uint Frame; public Vector3 Translation; }

    public static class LegacyAniParser
    {
        private const int MaxBones = 1024;
        private const int MaxFramesPerChannel = 1_000_000;
        public static LegacyAniFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("ANI path is required.", nameof(path));
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream)) return Parse(reader);
            }
            catch (InvalidDataException ex)
            { throw new InvalidDataException("ANI '" + path + "': " + ex.Message, ex); }
        }
        public static LegacyAniFile Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream)) return Parse(reader);
        }
        private static LegacyAniFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 10);
            long origin = reader.BaseStream.Position;
            bool v2 = Encoding.ASCII.GetString(reader.ReadBytes(6)) == "ANI_V2";
            if (!v2) reader.BaseStream.Position = origin;
            LegacyFormatPrimitives.EnsureRemaining(reader, 10);
            uint rawStart = reader.ReadUInt32();
            uint rawEnd = reader.ReadUInt32();
            int frameOffset = rawStart == uint.MaxValue ? 1 : 0;
            if (frameOffset != 0 && rawEnd > int.MaxValue)
                throw new InvalidDataException("Unsupported signed ANI time range.");
            var result = new LegacyAniFile {
                IsV2 = v2, FrameOffset = frameOffset,
                StartKeyframe = frameOffset == 0 ? rawStart : 0u,
                EndKeyframe = frameOffset == 0 ? rawEnd : rawEnd + 1u };
            if (result.EndKeyframe < result.StartKeyframe)
                throw new InvalidDataException("ANI end keyframe precedes start keyframe.");
            ushort boneCount = reader.ReadUInt16();
            if (boneCount > MaxBones)
                throw new InvalidDataException("ANI bone count is outside the supported range: " + boneCount + ".");
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                LegacyFormatPrimitives.EnsureRemaining(reader, 68);
                var bone = new LegacyAniBone {
                    ParentBoneIndex = reader.ReadInt32(),
                    AbsoluteBaseMatrix = LegacyFormatPrimitives.ReadMatrix4x4(reader) };
                if (bone.ParentBoneIndex >= boneCount || bone.ParentBoneIndex == boneIndex || bone.ParentBoneIndex < -1)
                    throw new InvalidDataException("ANI bone " + boneIndex + " has invalid parent " + bone.ParentBoneIndex + ".");
                int rotationCount = LegacyFormatPrimitives.ReadCount(reader, "ANI rotation", MaxFramesPerChannel);
                LegacyFormatPrimitives.EnsureRemaining(reader, (long)rotationCount * 20L + 4L);
                uint previous = 0;
                for (int i = 0; i < rotationCount; i++)
                {
                    uint frame = ReadFrame(reader, frameOffset, rawEnd);
                    Quaternion rotation = LegacyFormatPrimitives.ReadQuaternion(reader);
                    if (i > 0 && frame < previous) throw new InvalidDataException("ANI rotation frames are not sorted.");
                    previous = frame;
                    bone.Rotations.Add(new LegacyAniRotationFrame { Frame = frame, Rotation = rotation });
                }
                int translationCount = LegacyFormatPrimitives.ReadCount(reader, "ANI translation", MaxFramesPerChannel);
                LegacyFormatPrimitives.EnsureRemaining(reader, (long)translationCount * 16L);
                previous = 0;
                for (int i = 0; i < translationCount; i++)
                {
                    uint frame = ReadFrame(reader, frameOffset, rawEnd);
                    Vector3 translation = LegacyFormatPrimitives.ReadVector3(reader);
                    if (i > 0 && frame < previous) throw new InvalidDataException("ANI translation frames are not sorted.");
                    previous = frame;
                    bone.Translations.Add(new LegacyAniTranslationFrame { Frame = frame, Translation = translation });
                }
                result.Bones.Add(bone);
            }
            ValidateHierarchy(result.Bones);
            LegacyFormatPrimitives.EnsureFullyConsumed(reader, "ANI");
            return result;
        }
        private static uint ReadFrame(BinaryReader reader, int offset, uint rawEnd)
        {
            uint raw = reader.ReadUInt32();
            if (offset == 0) return raw;
            if (raw == uint.MaxValue) return 0;
            if (raw > rawEnd) throw new InvalidDataException("ANI key exceeds the signed source timeline.");
            return raw + 1u;
        }
        private static void ValidateHierarchy(IReadOnlyList<LegacyAniBone> bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                int cursor = i, steps = 0;
                while (cursor >= 0)
                {
                    cursor = bones[cursor].ParentBoneIndex;
                    if (++steps > bones.Count) throw new InvalidDataException("ANI hierarchy contains a parent cycle.");
                }
            }
        }
    }
}
