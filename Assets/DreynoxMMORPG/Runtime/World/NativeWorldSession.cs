using UnityEngine;
using Dreynox.Mmorpg.Gameplay.Client;

namespace Dreynox.Mmorpg.World
{
    /// <summary>Scene identity is explicit. Never compare a Map0 frame to a native Map1 reference.</summary>
    public sealed class NativeWorldSession : MonoBehaviour
    {
        [SerializeField] private int mapId;
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private Vector3 authoredStart;
        public int MapId => mapId;
        public ShaiyaClientActor Actor => actor;
        public Vector3 AuthoredStart => authoredStart;
        public void Configure(int id, ShaiyaClientActor player, Vector3 start)
        { mapId = id; actor = player; authoredStart = start; }
    }
}
