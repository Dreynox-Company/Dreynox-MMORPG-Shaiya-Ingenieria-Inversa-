using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public struct LegacyNpcGateTargetRuntime
    {
        public int mapId;
        public Vector3 position;
        public int cost;
        public string label;
    }

    [Serializable]
    public struct LegacyNpcSaleItemRuntime
    {
        public byte type;
        public byte typeId;
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
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, TextArea] private string welcomeMessage = string.Empty;
        [SerializeField] private NpcServiceKind services;
        [SerializeField] private LegacyNpcSaleItemRuntime[] saleItems =
            Array.Empty<LegacyNpcSaleItemRuntime>();
        [SerializeField] private int[] inQuestIds =
            Array.Empty<int>();
        [SerializeField] private int[] outQuestIds =
            Array.Empty<int>();
        [SerializeField] private LegacyNpcGateTargetRuntime[] gateTargets =
            Array.Empty<LegacyNpcGateTargetRuntime>();

        public int NpcType => npcType;
        public int TypeId => typeId;
        public int ModelIndex => modelIndex;
        public int Faction => faction;
        public int MoveDistance => moveDistance;
        public int MoveSpeed => moveSpeed;
        public int MerchantType => merchantType;
        public string DisplayName => displayName;
        public string WelcomeMessage => welcomeMessage;
        public NpcServiceKind Services => services;
        public IReadOnlyList<LegacyNpcSaleItemRuntime> SaleItems =>
            saleItems;
        public IReadOnlyList<int> InQuestIds =>
            inQuestIds;
        public IReadOnlyList<int> OutQuestIds =>
            outQuestIds;
        public IReadOnlyList<LegacyNpcGateTargetRuntime> GateTargets =>
            gateTargets;

        public int ServiceKey =>
            ComposeServiceKey(
                npcType,
                typeId);

        public void Configure(
            int type,
            int id,
            int model,
            int npcFaction,
            int movementDistance,
            int movementSpeed,
            int merchant,
            string name,
            string welcome,
            NpcServiceKind serviceKinds,
            LegacyNpcSaleItemRuntime[] items,
            int[] incomingQuestIds,
            int[] outgoingQuestIds,
            LegacyNpcGateTargetRuntime[] targets)
        {
            npcType = type;
            typeId = id;
            modelIndex = model;
            faction = npcFaction;
            moveDistance = movementDistance;
            moveSpeed = movementSpeed;
            merchantType = merchant;
            displayName = name ?? string.Empty;
            welcomeMessage = welcome ?? string.Empty;
            services = serviceKinds;
            saleItems =
                items ??
                Array.Empty<LegacyNpcSaleItemRuntime>();
            inQuestIds =
                incomingQuestIds ??
                Array.Empty<int>();
            outQuestIds =
                outgoingQuestIds ??
                Array.Empty<int>();
            gateTargets =
                targets ??
                Array.Empty<LegacyNpcGateTargetRuntime>();
        }

        public static int ComposeServiceKey(
            int type,
            int id)
        {
            if (type < 0 ||
                type > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(type));
            }

            if (id < short.MinValue ||
                id > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id));
            }

            return (type << 16) |
                   (ushort)(short)id;
        }
    }
}
