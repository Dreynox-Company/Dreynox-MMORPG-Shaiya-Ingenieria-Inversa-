using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    public sealed class WingAttachmentPoseApplier : MonoBehaviour
    {
        [SerializeField] private Transform wingRoot;

        [Header("Legacy identity")]
        [SerializeField, Range(0, 3)] private int family;
        [SerializeField, Range(0, 5)] private int job;
        [SerializeField, Range(0, 1)] private int sex;

        [Header("Coordinate bridge")]
        [Tooltip("Unity local X receives Shaiya WING_LEFT_RIGHT.")]
        [SerializeField] private float leftRightSign = 1f;
        [Tooltip("Unity local Y receives Shaiya WING_UP_DOWN.")]
        [SerializeField] private float upDownSign = 1f;
        [Tooltip("Unity local Z receives Shaiya WING_FRONT_BACK.")]
        [SerializeField] private float frontBackSign = 1f;

        public int Family => family;
        public int Job => job;
        public int Sex => sex;
        public LegacyWingPose CurrentPose { get; private set; }

        public void Configure(
            Transform root,
            int characterFamily,
            int characterJob,
            int characterSex)
        {
            wingRoot = root;
            family = characterFamily;
            job = characterJob;
            sex = characterSex;
            ApplyVerifiedPose();
        }

        public bool ApplyVerifiedPose()
        {
            if (wingRoot == null)
                return false;

            LegacyWingPose pose;
            if (!LegacyWingPoseCore.TryResolve(
                    family,
                    job,
                    sex,
                    out pose))
                return false;

            CurrentPose = pose;

            wingRoot.localPosition = new Vector3(
                (float)pose.LeftRight * leftRightSign,
                (float)pose.UpDown * upDownSign,
                (float)pose.FrontBack * frontBackSign);

            wingRoot.localRotation = Quaternion.Euler(
                (float)pose.RotX,
                (float)pose.RotY,
                (float)pose.RotZ);

            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && wingRoot != null)
                ApplyVerifiedPose();
        }
#endif
    }
}
