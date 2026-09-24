using System;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public sealed class LegacyManiRotationRuntime : MonoBehaviour
    {
        [SerializeField] private bool rotationEnabled;
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float radiansPerLegacyTick;
        [SerializeField, Min(1f)] private float legacyTicksPerSecond = 30f;
        [SerializeField] private bool timingCalibrated;

        public bool RotationEnabled => rotationEnabled;
        public Vector3 LocalAxis => localAxis;
        public float RadiansPerLegacyTick => radiansPerLegacyTick;
        public float LegacyTicksPerSecond => legacyTicksPerSecond;
        public bool TimingCalibrated => timingCalibrated;

        public float DegreesPerSecond =>
            ResolveDegreesPerSecond(
                radiansPerLegacyTick,
                legacyTicksPerSecond,
                rotationEnabled);

        public void Configure(
            bool enabled,
            Vector3 axis,
            float radiansPerTick,
            float ticksPerSecond,
            bool calibrated)
        {
            rotationEnabled =
                enabled;

            localAxis =
                axis.sqrMagnitude >
                0.000001f
                    ? axis.normalized
                    : Vector3.up;

            radiansPerLegacyTick =
                radiansPerTick;

            legacyTicksPerSecond =
                Mathf.Max(
                    1f,
                    ticksPerSecond);

            timingCalibrated =
                calibrated;
        }

        private void Update()
        {
            if (!rotationEnabled)
                return;

            float degrees =
                DegreesPerSecond *
                Time.deltaTime;

            transform.Rotate(
                localAxis,
                degrees,
                Space.Self);
        }

        public static float ResolveDegreesPerSecond(
            float radiansPerTick,
            float ticksPerSecond,
            bool enabled)
        {
            if (!enabled)
                return 0f;

            if (float.IsNaN(radiansPerTick) ||
                float.IsInfinity(radiansPerTick))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radiansPerTick));
            }

            if (float.IsNaN(ticksPerSecond) ||
                float.IsInfinity(ticksPerSecond) ||
                ticksPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ticksPerSecond));
            }

            return
                radiansPerTick *
                Mathf.Rad2Deg *
                ticksPerSecond;
        }
    }
}
