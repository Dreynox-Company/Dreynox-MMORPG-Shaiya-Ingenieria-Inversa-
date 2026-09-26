using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>
    /// Some MON resources append invalid, independent, unweighted helper roots.
    /// Trim only a proven-unused suffix before strict readers see the payload.
    /// Retained matrices, channels, vertices and topology stay byte-for-byte exact.
    /// </summary>
    internal static class LegacyMonUnusedBoneTail
    {
        internal sealed class Result
        {
            public byte[][] Meshes, Animations;
            public int RetainedBones, RemovedBones;
        }
        private sealed class MeshInfo { public int Bones, End, VertexCount, FirstBad = int.MaxValue, Stride; }
        private sealed class AniInfo
        {
            public int Header, CountOffset, Bones, FirstBad = int.MaxValue;
            public int[] Parent, Begin, End, Rotations, Translations;
        }
        public static Result Normalize(byte[][] meshes, byte[][] animations, IReadOnlyList<LegacyMonEffect> effects)
        {
            if (meshes == null || animations == null) throw new ArgumentNullException();
            var result = new Result { Meshes = meshes, Animations = animations };
            var meshInfo = meshes.Select(ScanMesh).ToArray();
            var aniInfo = animations.Select(ScanAni).ToArray();
            int cut = int.MaxValue;
            foreach (var info in meshInfo) if (info != null) cut = Math.Min(cut, info.FirstBad);
            foreach (var info in aniInfo) cut = Math.Min(cut, info.FirstBad);
            if (cut == int.MaxValue) return result;
            if (cut < 1) throw new InvalidDataException("MON invalid root cannot be discarded.");
            if (effects != null && effects.Any(e => e.BoneId >= cut || e.BoneId < 0))
                throw new InvalidDataException("MON invalid suffix contains an attached effect anchor.");

            foreach (var info in aniInfo)
            {
                if (info.Bones < cut) throw new InvalidDataException("MON suffix pruning would lose an authored body hierarchy.");
                for (int i = 0; i < info.Bones; i++)
                {
                    int parent = info.Parent[i];
                    if (parent < -1 || parent >= i) throw new InvalidDataException("MON hierarchy is not parent-first.");
                    if (i < cut && parent >= cut) throw new InvalidDataException("Body depends on invalid helper roots.");
                    if (i >= cut && ((parent >= 0 && parent < cut) || info.Rotations[i] != 0 || info.Translations[i] != 0))
                        throw new InvalidDataException("MON suffix is attached to the body or has authored animation; cannot prune.");
                }
            }
            for (int i = 0; i < meshInfo.Length; i++)
            {
                var info = meshInfo[i];
                if (info == null || info.Bones < cut)
                    throw new InvalidDataException("MON unused-tail compatibility requires a standard, matching mesh skeleton.");
                ValidateInfluences(meshes[i], info, cut);
            }
            result.Meshes = new byte[meshes.Length][];
            result.Animations = new byte[animations.Length][];
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshInfo info = meshInfo[i];
                byte[] raw = meshes[i];
                var bytes = new byte[checked(raw.Length - (info.Bones - cut) * 64)];
                Buffer.BlockCopy(raw, 0, bytes, 0, 8 + cut * 64);
                Buffer.BlockCopy(BitConverter.GetBytes(cut), 0, bytes, 4, 4);
                Buffer.BlockCopy(raw, info.End, bytes, 8 + cut * 64, raw.Length - info.End);
                result.Meshes[i] = bytes;
                result.RemovedBones = Math.Max(result.RemovedBones, info.Bones - cut);
            }
            for (int i = 0; i < animations.Length; i++)
            {
                AniInfo info = aniInfo[i];
                byte[] raw = animations[i];
                int end = info.End[cut - 1];
                byte[] bytes = new byte[end];
                Buffer.BlockCopy(raw, 0, bytes, 0, end);
                Buffer.BlockCopy(BitConverter.GetBytes(checked((ushort)cut)), 0, bytes, info.CountOffset, 2);
                result.Animations[i] = bytes;
                result.RemovedBones = Math.Max(result.RemovedBones, info.Bones - cut);
            }
            result.RetainedBones = cut;
            return result;
        }
        private static MeshInfo ScanMesh(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) throw new InvalidDataException("Truncated MON mesh.");
            using (var r = new BinaryReader(new MemoryStream(bytes, false)))
            {
                int version = r.ReadInt32();
                if (version != 0 && version != 444) return null;
                int count = r.ReadInt32();
                if (count < 1 || count > 1024 || count * 64L + 4 > r.BaseStream.Length - r.BaseStream.Position) return null;
                var info = new MeshInfo { Bones = count, Stride = version == 444 ? 48 : 40 };
                for (int i = 0; i < count; i++)
                    for (int n = 0; n < 16; n++)
                        if (!Finite(r.ReadSingle())) info.FirstBad = Math.Min(info.FirstBad, i);
                info.End = checked((int)r.BaseStream.Position);
                info.VertexCount = Count(r, 5000000);
                Need(r, info.VertexCount * (long)info.Stride + 4);
                return info;
            }
        }
        private static AniInfo ScanAni(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 10) throw new InvalidDataException("Truncated MON ANI.");
            using (var r = new BinaryReader(new MemoryStream(bytes, false)))
            {
                bool v2 = bytes.Length >= 16 && System.Text.Encoding.ASCII.GetString(bytes, 0, 6) == "ANI_V2";
                int prefix = v2 ? 6 : 0;
                r.BaseStream.Position = prefix + 8;
                int count = r.ReadUInt16();
                if (count < 1 || count > 1024) throw new InvalidDataException("Invalid MON ANI bone count.");
                var info = new AniInfo { Header = prefix + 10, CountOffset = prefix + 8, Bones = count,
                    Parent = new int[count], Begin = new int[count], End = new int[count],
                    Rotations = new int[count], Translations = new int[count] };
                for (int i = 0; i < count; i++)
                {
                    info.Begin[i] = checked((int)r.BaseStream.Position);
                    Need(r, 68);
                    info.Parent[i] = r.ReadInt32();
                    for (int n = 0; n < 16; n++)
                        if (!Finite(r.ReadSingle())) info.FirstBad = Math.Min(info.FirstBad, i);
                    info.Rotations[i] = Count(r, 1000000);
                    Skip(r, info.Rotations[i] * 20L);
                    info.Translations[i] = Count(r, 1000000);
                    Skip(r, info.Translations[i] * 16L);
                    info.End[i] = checked((int)r.BaseStream.Position);
                }
                while (r.BaseStream.Position < r.BaseStream.Length)
                    if (r.ReadByte() != 0) throw new InvalidDataException("MON ANI contains unparsed nonzero bytes.");
                return info;
            }
        }
        private static void ValidateInfluences(byte[] bytes, MeshInfo info, int cut)
        {
            using (var r = new BinaryReader(new MemoryStream(bytes, false)))
            {
                for (int i = 0; i < info.VertexCount; i++)
                {
                    r.BaseStream.Position = info.End + 4L + i * (long)info.Stride + 12;
                    float a = r.ReadSingle(), b = info.Stride == 48 ? r.ReadSingle() : 1f - a;
                    float c = info.Stride == 48 ? r.ReadSingle() : 0f;
                    float d = info.Stride == 48 ? 1f - a - b - c : 0f;
                    float[] weights = { a, b, c, d };
                    for (int slot = 0; slot < 4; slot++)
                    {
                        int index = r.ReadByte(); float weight = weights[slot];
                        if (!Finite(weight) || weight < -0.00001f || (weight > 0.000001f && index >= cut))
                            throw new InvalidDataException("MON invalid suffix is weighted or the skin weights are invalid.");
                    }
                }
            }
        }
        private static int Count(BinaryReader r, int max)
        {
            Need(r, 4); int count = r.ReadInt32();
            if (count < 0 || count > max) throw new InvalidDataException("MON resource count is invalid.");
            return count;
        }
        private static void Skip(BinaryReader r, long count) { Need(r, count); r.BaseStream.Position += count; }
        private static void Need(BinaryReader r, long bytes)
        {
            if (bytes < 0 || bytes > r.BaseStream.Length - r.BaseStream.Position)
                throw new EndOfStreamException("MON resource is truncated.");
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
