using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public struct LegacyNpcGateTargetRuntime
    {
        public int mapId;
        public Vector3 position;
        public int cost;
    }

    public sealed class LegacyNpcRuntimeDescriptor : MonoBehaviour
    {
        [SerializeField] private int npcType;
        [SerializeField] private int typeId;
        [SerializeField] private int modelIndex;
        [SerializeField] private int faction;
        [SerializeField] private int moveDistance;
        [SerializeField] private int moveSpeed;
        [SerializeField] private int merchantType = -1;
        [SerializeField] private LegacyNpcGateTargetRuntime[] gateTargets =
            Array.Empty<LegacyNpcGateTargetRuntime>();

        public int NpcType => npcType;
        public int TypeId => typeId;
        public int ModelIndex => modelIndex;
        public int Faction => faction;
        public int MoveDistance => moveDistance;
        public int MoveSpeed => moveSpeed;
        public int MerchantType => merchantType;
        public IReadOnlyList<LegacyNpcGateTargetRuntime> GateTargets =>
            gateTargets;

        public void Configure(
            int type,
            int id,
            int model,
            int npcFaction,
            int movementDistance,
            int movementSpeed,
            int merchant,
            LegacyNpcGateTargetRuntime[] targets)
        {
            npcType = type;
            typeId = id;
            modelIndex = model;
            faction = npcFaction;
            moveDistance = movementDistance;
            moveSpeed = movementSpeed;
            merchantType = merchant;
            gateTargets =
                targets ?? Array.Empty<LegacyNpcGateTargetRuntime>();
        }
    }
}
