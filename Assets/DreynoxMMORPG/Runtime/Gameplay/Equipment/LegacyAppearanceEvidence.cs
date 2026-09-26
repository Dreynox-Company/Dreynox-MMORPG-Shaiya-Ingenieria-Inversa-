using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    /// <summary>Source provenance in the preconverted Player; no original-file IO.</summary>
    public sealed class LegacyAppearanceEvidence : MonoBehaviour
    {
        [SerializeField] private string policy;
        [SerializeField] private string[] meshes = Array.Empty<string>();
        [SerializeField] private string[] textures = Array.Empty<string>();
        public string Policy => policy;
        public string[] Meshes => (string[])meshes.Clone();
        public string[] Textures => (string[])textures.Clone();
        public void Configure(string value, string[] meshSources, string[] textureSources)
        {
            if (meshSources == null || textureSources == null || meshSources.Length != 6 || textureSources.Length != 6)
                throw new ArgumentException("Six original body pieces are required.");
            policy = value; meshes = (string[])meshSources.Clone(); textures = (string[])textureSources.Clone();
        }
    }
}
