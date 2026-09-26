using Dreynox.Mmorpg.World;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.Build
{
    /// <summary>Reject packaged scenes whose visible terrain has no solid support.</summary>
    [BuildCallbackVersion(1)]
    public sealed class WorldTerrainBuildGuard : IProcessSceneWithReport
    {
        public int callbackOrder => -900;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                // Inactive additive sections still need their serialized collider/data;
                // active terrain additionally has to be enabled and collision-ready.
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    terrain.terrainData == null || collider.terrainData != terrain.terrainData)
                    throw new BuildFailedException("Unwalkable terrain in Player scene " + scene.path + ": " + terrain.name);
                if (terrain.gameObject.activeInHierarchy) WorldTerrainCollision.Require(terrain);
            }
        }
    }
}
