using System;
using UnityEngine;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>
    /// Original 3DC/3DO UVs use D3D's top-left origin. Decoded DDS textures are
    /// already bottom-row-first for Unity; convert UV once at the mesh boundary.
    /// Readers keep source coordinates. Do not also flip image pixels/material ST.
    /// </summary>
    public static class LegacyTextureCoordinates
    {
        public static Vector2 ToUnity(Vector2 authored)
        {
            if (float.IsNaN(authored.x) || float.IsInfinity(authored.x) ||
                float.IsNaN(authored.y) || float.IsInfinity(authored.y))
                throw new ArgumentOutOfRangeException(nameof(authored));
            return new Vector2(authored.x, 1f - authored.y);
        }
    }
}
