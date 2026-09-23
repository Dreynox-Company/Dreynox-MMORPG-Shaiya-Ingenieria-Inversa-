using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public static class ClientCoordinateCore
    {
        public static void ResolveCameraRelative(
            double inputX,
            double inputY,
            double cameraYawDegrees,
            out double worldX,
            out double worldZ)
        {
            double length = Math.Sqrt(inputX * inputX + inputY * inputY);
            if (length > 1.0)
            {
                inputX /= length;
                inputY /= length;
            }

            double radians = cameraYawDegrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            // Preserves the coordinate convention validated by the Flutter
            // parity tests: with camera yaw +90°, forward input resolves to -X.
            worldX = inputX * cos - inputY * sin;
            worldZ = inputX * sin + inputY * cos;

            double worldLength = Math.Sqrt(worldX * worldX + worldZ * worldZ);
            if (worldLength > 0.0000001)
            {
                worldX /= worldLength;
                worldZ /= worldLength;
            }
        }
    }
}
