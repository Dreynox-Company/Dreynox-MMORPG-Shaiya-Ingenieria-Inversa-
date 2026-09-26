using System;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    /// <summary>Visible FLD terrain must have matching, enabled physical support.</summary>
    public static class WorldTerrainCollision
    {
        public static TerrainCollider Require(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("Terrain or its authored heightfield is missing.");
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null)
                throw new InvalidOperationException("Terrain has no TerrainCollider: " + terrain.name +
                    ". Enable com.unity.modules.terrainphysics before generating the world.");
            if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                throw new InvalidOperationException("Terrain support is disabled, inactive or a trigger: " + terrain.name);
            if (collider.terrainData != terrain.terrainData)
                throw new InvalidOperationException("Rendered terrain and collision use different heightfields: " + terrain.name);
            Vector3 scale = terrain.transform.lossyScale;
            if ((scale - Vector3.one).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.001f)
                throw new InvalidOperationException("Terrain support requires an unscaled, unrotated heightfield.");
            return collider;
        }

        /// <summary>
        /// Query the actual collider, not SampleHeight alone. This reports terrain
        /// holes as unsupported and does not invent a floor or move an actor.
        /// </summary>
        public static bool TryProbe(Terrain terrain, Vector3 worldPosition, out RaycastHit hit)
        {
            hit = default;
            var collider = Require(terrain);
            if (!Finite(worldPosition.x) || !Finite(worldPosition.z)) return false;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float localX = worldPosition.x - origin.x, localZ = worldPosition.z - origin.z;
            if (localX < 0 || localZ < 0 || localX > size.x || localZ > size.z) return false;
            var ray = new Ray(new Vector3(worldPosition.x, origin.y + size.y + 1f, worldPosition.z), Vector3.down);
            return collider.Raycast(ray, out hit, size.y + 2f);
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
