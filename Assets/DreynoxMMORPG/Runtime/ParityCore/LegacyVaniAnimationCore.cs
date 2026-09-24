using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public static class LegacyVaniAnimationCore
    {
        public static double FrameDurationSeconds(
            int frameIntervalMilliseconds)
        {
            if (frameIntervalMilliseconds <= 0 ||
                frameIntervalMilliseconds > 10000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameIntervalMilliseconds));
            }

            return frameIntervalMilliseconds /
                   1000.0;
        }

        public static double CycleSeconds(
            int frameCount,
            int frameIntervalMilliseconds)
        {
            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameCount));
            }

            return checked(
                frameCount *
                FrameDurationSeconds(
                    frameIntervalMilliseconds));
        }

        public static int ResolveFrameIndex(
            double elapsedSeconds,
            int frameCount,
            int frameIntervalMilliseconds,
            double phaseOffsetSeconds = 0.0)
        {
            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameCount));
            }

            if (double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds));
            }

            if (double.IsNaN(phaseOffsetSeconds) ||
                double.IsInfinity(phaseOffsetSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(phaseOffsetSeconds));
            }

            double frameDuration =
                FrameDurationSeconds(
                    frameIntervalMilliseconds);

            double cycle =
                frameDuration *
                frameCount;

            double time =
                elapsedSeconds +
                phaseOffsetSeconds;

            time %= cycle;

            if (time < 0.0)
                time += cycle;

            int index =
                (int)Math.Floor(
                    time /
                    frameDuration);

            if (index >= frameCount)
                index = frameCount - 1;

            return index;
        }
    }
}
