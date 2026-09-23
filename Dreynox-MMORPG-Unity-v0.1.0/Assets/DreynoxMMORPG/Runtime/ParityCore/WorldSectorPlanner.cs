using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct SectorCoord : IEquatable<SectorCoord>
    {
        public readonly int X;
        public readonly int Z;

        public SectorCoord(int x, int z) { X = x; Z = z; }
        public bool Equals(SectorCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is SectorCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => X + "," + Z;
    }

    public sealed class SectorPlan
    {
        public readonly List<SectorCoord> Load = new List<SectorCoord>();
        public readonly List<SectorCoord> Unload = new List<SectorCoord>();
    }

    public sealed class WorldSectorPlanner
    {
        private readonly HashSet<SectorCoord> _loaded = new HashSet<SectorCoord>();
        public int Radius { get; }
        public double SectorSize { get; }
        public IReadOnlyCollection<SectorCoord> Loaded => _loaded;

        public WorldSectorPlanner(int radius, double sectorSize)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (sectorSize <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSize));
            Radius = radius;
            SectorSize = sectorSize;
        }

        public SectorPlan Update(double worldX, double worldZ)
        {
            int centerX = FloorToInt(worldX / SectorSize);
            int centerZ = FloorToInt(worldZ / SectorSize);
            HashSet<SectorCoord> desired = new HashSet<SectorCoord>();
            int rr = Radius * Radius;
            for (int dz = -Radius; dz <= Radius; dz++)
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                if (dx * dx + dz * dz > rr) continue;
                desired.Add(new SectorCoord(centerX + dx, centerZ + dz));
            }

            SectorPlan plan = new SectorPlan();
            foreach (SectorCoord coord in desired)
                if (!_loaded.Contains(coord)) plan.Load.Add(coord);
            foreach (SectorCoord coord in _loaded)
                if (!desired.Contains(coord)) plan.Unload.Add(coord);

            plan.Load.Sort(Compare);
            plan.Unload.Sort(Compare);
            _loaded.Clear();
            foreach (SectorCoord coord in desired) _loaded.Add(coord);
            return plan;
        }

        private static int Compare(SectorCoord a, SectorCoord b)
        {
            int z = a.Z.CompareTo(b.Z);
            return z != 0 ? z : a.X.CompareTo(b.X);
        }

        private static int FloorToInt(double value)
        {
            return (int)Math.Floor(value);
        }
    }
}
