using System;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>Derived normal compatibility, opt-in only. Never modifies authored positions/UV/weights.</summary>
    internal static class LegacyMeshNormalRepair
    {
        public static void Apply(Legacy3dcFile mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            int count = mesh.Vertices.Count;
            var invalid = new bool[count];
            int invalidCount = 0;
            for (int i = 0; i < count; i++)
                if (!Finite(mesh.Vertices[i].Normal)) { invalid[i] = true; invalidCount++; }
            if (invalidCount == 0) return;
            var sum = new Vector3[count];
            var strongest = new Vector3[count];
            foreach (var f in mesh.Faces)
            {
                if (!invalid[f.A] && !invalid[f.B] && !invalid[f.C]) continue;
                Vector3 n = Vector3.Cross(mesh.Vertices[f.B].Position - mesh.Vertices[f.A].Position,
                    mesh.Vertices[f.C].Position - mesh.Vertices[f.A].Position);
                if (!Finite(n) || !Finite(n.sqrMagnitude)) throw new InvalidDataException("3DC normal geometry overflow.");
                Add(f.A, n, invalid, sum, strongest);
                Add(f.B, n, invalid, sum, strongest);
                Add(f.C, n, invalid, sum, strongest);
            }
            for (int i = 0; i < count; i++)
            {
                if (!invalid[i]) continue;
                if (!Finite(sum[i]) || !Finite(sum[i].sqrMagnitude)) throw new InvalidDataException("3DC normal sum overflow.");
                Vector3 n = sum[i].sqrMagnitude > 1e-20f ? sum[i] : strongest[i];
                var vertex = mesh.Vertices[i];
                if (n.sqrMagnitude > 1e-20f)
                {
                    vertex.Normal = n / Mathf.Sqrt(n.sqrMagnitude);
                    mesh.ReconstructedNormals++;
                }
                else
                {
                    // A non-rendering vertex has no incident, nonzero-area normal to recover.
                    vertex.Normal = Vector3.up;
                    mesh.InactiveNormalDefaults++;
                }
                mesh.Vertices[i] = vertex;
            }
        }
        private static void Add(int i, Vector3 n, bool[] invalid, Vector3[] sum, Vector3[] largest)
        {
            if (!invalid[i]) return;
            sum[i] += n;
            if (n.sqrMagnitude > largest[i].sqrMagnitude) largest[i] = n;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
