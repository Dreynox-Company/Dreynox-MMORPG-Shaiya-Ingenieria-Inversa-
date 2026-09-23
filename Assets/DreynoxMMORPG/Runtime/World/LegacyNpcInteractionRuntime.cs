using System;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public sealed class LegacyNpcInteractionRuntime : MonoBehaviour
    {
        private readonly NpcInteractionCore _interaction =
            new NpcInteractionCore();

        public LegacyNpcRuntimeDescriptor ActiveNpc { get; private set; }

        public event Action<LegacyNpcRuntimeDescriptor> Opened;
        public event Action<LegacyNpcRuntimeDescriptor> Closed;

        public bool Open(
            LegacyNpcRuntimeDescriptor npc)
        {
            if (npc == null)
                return false;

            int key =
                npc.ServiceKey;

            _interaction.Register(
                key,
                npc.Services);

            if (!_interaction.Open(key))
                return false;

            LegacyNpcRuntimeDescriptor previous =
                ActiveNpc;

            ActiveNpc = npc;

            if (previous != null &&
                previous != npc)
            {
                Closed?.Invoke(previous);
            }

            Opened?.Invoke(npc);
            return true;
        }

        public void Close()
        {
            LegacyNpcRuntimeDescriptor previous =
                ActiveNpc;

            ActiveNpc = null;
            _interaction.Close();

            if (previous != null)
                Closed?.Invoke(previous);
        }

        public bool Supports(
            NpcServiceKind service)
        {
            return ActiveNpc != null &&
                   _interaction.Supports(service);
        }

        public bool TryResolveGatekeeper(
            int destinationIndex,
            int playerLevel,
            long currentGold,
            out LegacyNpcGateTargetRuntime target,
            out long remainingGold)
        {
            target = default;
            remainingGold = currentGold;

            if (ActiveNpc == null ||
                !Supports(
                    NpcServiceKind.Gatekeeper) ||
                destinationIndex < 0 ||
                destinationIndex >=
                ActiveNpc.GateTargets.Count)
            {
                return false;
            }

            LegacyNpcGateTargetRuntime source =
                ActiveNpc.GateTargets[
                    destinationIndex];

            var gatekeeper =
                new GatekeeperCore();

            gatekeeper.Add(
                new GatekeeperDestination
                {
                    DestinationId =
                        destinationIndex,
                    MapId =
                        source.mapId,
                    X =
                        source.position.x,
                    Y =
                        source.position.y,
                    Z =
                        source.position.z,
                    MinimumLevel = 0,
                    Cost =
                        source.cost
                });

            long gold =
                currentGold;

            if (!gatekeeper.TryResolve(
                    destinationIndex,
                    playerLevel,
                    ref gold,
                    out GatekeeperDestination resolved))
            {
                return false;
            }

            target = source;
            remainingGold = gold;
            return true;
        }
    }
}
