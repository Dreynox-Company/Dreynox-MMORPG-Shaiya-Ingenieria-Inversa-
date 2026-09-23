using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct LegacyWingPose
    {
        public readonly int Family;
        public readonly int Job;
        public readonly int Sex;
        public readonly int BoneIndex;
        public readonly double RotX;
        public readonly double RotY;
        public readonly double RotZ;
        public readonly double UpDown;
        public readonly double FrontBack;
        public readonly double LeftRight;

        public LegacyWingPose(
            int family,
            int job,
            int sex,
            int boneIndex,
            double rotX,
            double rotY,
            double rotZ,
            double upDown,
            double frontBack,
            double leftRight)
        {
            Family = family;
            Job = job;
            Sex = sex;
            BoneIndex = boneIndex;
            RotX = rotX;
            RotY = rotY;
            RotZ = rotZ;
            UpDown = upDown;
            FrontBack = frontBack;
            LeftRight = leftRight;
        }
    }

    public static class LegacyWingPoseCore
    {
        public const string SourcePath = "DATA_Español/excelxml/wingposition.xml";
        public const string SourceSha256 =
            "8a2c376c898bb025550b5fe34b92a40dbbbb9e39063619cfee4756006908cd03";

        private static readonly LegacyWingPose[] Profiles =
        {
            P(0,0,0,4,170,0,90,0.05,-0.18,0),
            P(0,0,1,4,170,0,90,0.05,-0.11,0),
            P(0,1,0,4,170,0,90,0.05,-0.18,0),
            P(0,1,1,4,170,0,90,0.05,-0.11,0),
            P(0,2,0,4,0,0,90,0,0,0),
            P(0,2,1,4,0,0,90,0,0,0),
            P(0,3,0,4,0,0,90,0,0,0),
            P(0,3,1,4,0,0,90,0,0,0),
            P(0,4,0,4,0,0,90,0,0,0),
            P(0,4,1,4,0,0,90,0,0,0),
            P(0,5,0,4,168,0,90,0.05,-0.14,0),
            P(0,5,1,4,168,0,90,0.04,-0.11,0),

            P(1,0,0,4,0,0,90,0,0,0),
            P(1,0,1,4,0,0,90,0,0,0),
            P(1,1,0,4,0,0,90,0,0,0),
            P(1,1,1,4,0,0,90,0,0,0),
            P(1,2,0,4,180,0,90,-0.04,-0.12,0),
            P(1,2,1,4,175,0,90,0.03,-0.12,0),
            P(1,3,0,4,180,0,90,-0.04,-0.12,0),
            P(1,3,1,4,175,0,90,0.03,-0.12,0),
            P(1,4,0,4,183,0,90,0.01,-0.13,0),
            P(1,4,1,4,175,0,90,0.02,-0.10,0),
            P(1,5,0,4,0,0,90,0,0,0),
            P(1,5,1,4,0,0,90,0,0,0),

            P(2,0,0,4,0,0,90,0,0,0),
            P(2,0,1,4,0,0,90,0,0,0),
            P(2,1,0,4,0,0,90,0,0,0),
            P(2,1,1,4,0,0,90,0,0,0),
            P(2,2,0,4,180,0,90,0.04,-0.12,0),
            P(2,2,1,4,173,0,90,0.05,-0.11,0),
            P(2,3,0,4,0,0,90,0,0,0),
            P(2,3,1,4,0,0,90,0,0,0),
            P(2,4,0,4,178,0,90,0.06,-0.11,0),
            P(2,4,1,4,175,0,90,0.04,-0.11,0),
            P(2,5,0,4,178,0,90,0.06,-0.11,0),
            P(2,5,1,4,175,0,90,0.04,-0.11,0),

            P(3,0,0,4,192,0,90,0.10,-0.19,0),
            P(3,0,1,4,165,0,90,0.04,-0.17,0),
            P(3,1,0,4,192,0,90,0.10,-0.19,0),
            P(3,1,1,4,165,0,90,0.04,-0.17,0),
            P(3,2,0,4,0,0,90,0,0,0),
            P(3,2,1,4,0,0,90,0,0,0),
            P(3,3,0,4,185,0,90,0.15,-0.14,0),
            P(3,3,1,4,170,0,90,0,-0.19,0),
            P(3,4,0,4,0,0,90,0,0,0),
            P(3,4,1,4,0,0,90,0,0,0),
            P(3,5,0,4,0,0,90,0,0,0),
            P(3,5,1,4,0,0,90,0,0,0),
        };

        private static readonly Dictionary<int, LegacyWingPose> Lookup = BuildLookup();

        public static int Count => Profiles.Length;
        public static IReadOnlyList<LegacyWingPose> All => Profiles;

        public static bool TryResolve(
            int family,
            int job,
            int sex,
            out LegacyWingPose pose)
        {
            if (family < 0 || family > 3 ||
                job < 0 || job > 5 ||
                sex < 0 || sex > 1)
            {
                pose = default;
                return false;
            }

            return Lookup.TryGetValue(Key(family, job, sex), out pose);
        }

        public static LegacyWingPose Resolve(int family, int job, int sex)
        {
            LegacyWingPose pose;
            if (!TryResolve(family, job, sex, out pose))
                throw new ArgumentOutOfRangeException(
                    "No verified WingPosition profile for " +
                    "family=" + family + ", job=" + job + ", sex=" + sex + ".");
            return pose;
        }

        private static LegacyWingPose P(
            int family,
            int job,
            int sex,
            int boneIndex,
            double rx,
            double ry,
            double rz,
            double upDown,
            double frontBack,
            double leftRight)
        {
            return new LegacyWingPose(
                family,
                job,
                sex,
                boneIndex,
                rx,
                ry,
                rz,
                upDown,
                frontBack,
                leftRight);
        }

        private static Dictionary<int, LegacyWingPose> BuildLookup()
        {
            var result = new Dictionary<int, LegacyWingPose>();
            for (int i = 0; i < Profiles.Length; i++)
            {
                LegacyWingPose pose = Profiles[i];
                int key = Key(pose.Family, pose.Job, pose.Sex);
                if (result.ContainsKey(key))
                    throw new InvalidOperationException(
                        "Duplicate verified WingPosition profile: " + key);
                result.Add(key, pose);
            }
            return result;
        }

        private static int Key(int family, int job, int sex)
        {
            return family * 100 + job * 10 + sex;
        }
    }
}
