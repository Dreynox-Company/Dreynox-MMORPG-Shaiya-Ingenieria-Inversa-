using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public static class LegacyTerrainHeightCore
    {
        public const double UnitsPerRawStep = 1.0 / 50.0;
        public const double WorldYOffset = -200.0;
        public const double FullHeightRange = 65535.0 / 50.0;

        public static double Decode(ushort rawHeight)
        {
            return rawHeight * UnitsPerRawStep + WorldYOffset;
        }

        public static float Normalize(ushort rawHeight)
        {
            return rawHeight / 65535f;
        }

        public static int ResolutionForMapSize(int mapSize)
        {
            if (mapSize <= 0 || (mapSize & (mapSize - 1)) != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(mapSize),
                    "Shaiya FLD map size must be a positive power of two.");

            return checked(mapSize / 2 + 1);
        }

        public static int WorldToSampleIndex(
            double worldCoordinate,
            int mapSize)
        {
            int resolution = ResolutionForMapSize(mapSize);
            int index = (int)Math.Round(
                worldCoordinate * 0.5,
                MidpointRounding.AwayFromZero);

            if (index < 0) return 0;
            if (index >= resolution) return resolution - 1;
            return index;
        }
    }
}
