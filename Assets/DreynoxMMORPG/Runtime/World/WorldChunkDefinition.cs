using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [CreateAssetMenu(menuName = "Dreynox MMORPG/World/Chunk Definition", fileName = "WorldChunk")]
    public sealed class WorldChunkDefinition : ScriptableObject
    {
        public string sceneName;
        public Vector3 center;
        [Min(1f)] public float loadRadius = 250f;
        [Min(1f)] public float unloadRadius = 310f;
    }
}
