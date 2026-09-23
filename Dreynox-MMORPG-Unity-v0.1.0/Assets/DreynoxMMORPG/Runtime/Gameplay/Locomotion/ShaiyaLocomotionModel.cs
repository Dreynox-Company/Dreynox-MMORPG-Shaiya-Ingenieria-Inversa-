using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Locomotion
{
    public enum ShaiyaLocomotionState
    {
        Idle,
        Walk,
        Run,
        Jump,
        Airborne,
        MountedIdle,
        MountedWalk,
        MountedRun,
        Hover,
        Flight
    }

    public static class ShaiyaLocomotionModel
    {
        private const float InputDeadZone = 0.0001f;

        public static Vector3 ResolveCameraRelativeDirection(Vector2 input, float cameraYawDegrees)
        {
            Vector2 clamped = Vector2.ClampMagnitude(input, 1f);
            if (clamped.sqrMagnitude <= InputDeadZone) return Vector3.zero;

            // Shaiya's recovered client convention maps a +90 degree camera yaw
            // and forward input toward world -X. Keep the convention explicit so
            // Unity's native +Y yaw convention cannot silently change parity.
            Quaternion yaw = Quaternion.Euler(0f, -cameraYawDegrees, 0f);
            Vector3 local = new Vector3(clamped.x, 0f, clamped.y);
            return (yaw * local).normalized;
        }

        public static ShaiyaLocomotionState ResolveState(
            Vector2 input,
            bool runHeld,
            bool grounded,
            bool jumping,
            bool mounted,
            bool flying)
        {
            bool moving = input.sqrMagnitude > InputDeadZone;

            if (flying) return moving ? ShaiyaLocomotionState.Flight : ShaiyaLocomotionState.Hover;
            if (mounted)
            {
                if (!moving) return ShaiyaLocomotionState.MountedIdle;
                return runHeld ? ShaiyaLocomotionState.MountedRun : ShaiyaLocomotionState.MountedWalk;
            }

            if (jumping) return ShaiyaLocomotionState.Jump;
            if (!grounded) return ShaiyaLocomotionState.Airborne;
            if (!moving) return ShaiyaLocomotionState.Idle;
            return runHeld ? ShaiyaLocomotionState.Run : ShaiyaLocomotionState.Walk;
        }

        public static string SemanticAnimation(ShaiyaLocomotionState state)
        {
            switch (state)
            {
                case ShaiyaLocomotionState.Walk: return "PLAYER_WALK";
                case ShaiyaLocomotionState.Run: return "PLAYER_RUN";
                case ShaiyaLocomotionState.Jump: return "PLAYER_JUMP";
                case ShaiyaLocomotionState.Airborne: return "PLAYER_JUMP";
                case ShaiyaLocomotionState.MountedIdle: return "MOUNT_IDLE";
                case ShaiyaLocomotionState.MountedWalk: return "MOUNT_WALK";
                case ShaiyaLocomotionState.MountedRun: return "MOUNT_RUN";
                case ShaiyaLocomotionState.Hover: return "PLAYER_STOP_FLY";
                case ShaiyaLocomotionState.Flight: return "PLAYER_FLY";
                default: return "PLAYER_STOP";
            }
        }
    }
}
