using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public sealed class LootDropCore
    {
        public long DropId { get; }
        public int ItemId { get; }
        public int Quantity { get; }
        public int? ReservedPlayerId { get; }
        public bool Collected { get; internal set; }
        public LootDropCore(long dropId, int itemId, int quantity, int? reservedPlayerId)
        {
            if (dropId <= 0 || itemId <= 0 || quantity <= 0) throw new ArgumentOutOfRangeException();
            DropId = dropId; ItemId = itemId; Quantity = quantity; ReservedPlayerId = reservedPlayerId;
        }
    }

    public sealed class LootCore
    {
        private readonly Dictionary<long, LootDropCore> _drops = new Dictionary<long, LootDropCore>();
        public IReadOnlyDictionary<long, LootDropCore> Drops => _drops;
        public bool Spawn(LootDropCore drop)
        {
            if (drop == null || _drops.ContainsKey(drop.DropId)) return false;
            _drops.Add(drop.DropId, drop); return true;
        }
        public bool TryCollect(long dropId, int playerId, InventoryCore inventory, int maxStack)
        {
            if (inventory == null || !_drops.TryGetValue(dropId, out LootDropCore drop) || drop.Collected) return false;
            if (drop.ReservedPlayerId.HasValue && drop.ReservedPlayerId.Value != playerId) return false;
            int remaining = inventory.Add(drop.ItemId, drop.Quantity, maxStack);
            if (remaining != 0) return false;
            drop.Collected = true;
            return true;
        }
        public bool Despawn(long dropId) => _drops.Remove(dropId);
    }

    [Flags]
    public enum NpcServiceKind
    {
        None = 0,
        Shop = 1,
        Warehouse = 2,
        Blacksmith = 4,
        Gatekeeper = 8,
        Quest = 16
    }

    public static class NpcServiceResolverCore
    {
        // NpcQuest.SData group ids observed in the canonical ps0032 client:
        // 1 Merchant, 2 GateKeeper, 3 Blacksmith, 4 PvPManager,
        // 5 GamblingHouse, 6 Warehouse, 7 Normal, 8 Guard, 9 Animal,
        // 10 Apprentice, 11 GuildMaster, 12 DeadNpc, 13 CombatCommander.
        public static NpcServiceKind Resolve(
            int npcType,
            bool hasQuestLinks)
        {
            NpcServiceKind result =
                NpcServiceKind.None;

            switch (npcType)
            {
                case 1:
                    result |=
                        NpcServiceKind.Shop;
                    break;

                case 2:
                    result |=
                        NpcServiceKind.Gatekeeper;
                    break;

                case 3:
                    result |=
                        NpcServiceKind.Blacksmith;
                    break;

                case 6:
                    result |=
                        NpcServiceKind.Warehouse;
                    break;
            }

            if (hasQuestLinks)
                result |=
                    NpcServiceKind.Quest;

            return result;
        }
    }

    public readonly struct PortalTravelCore
    {
        public readonly int SourceMapId;
        public readonly int PortalId;
        public readonly int MinimumLevel;
        public readonly int MaximumLevel;
        public readonly int TargetMapId;
        public readonly double TargetX;
        public readonly double TargetY;
        public readonly double TargetZ;

        public PortalTravelCore(
            int sourceMapId,
            int portalId,
            int minimumLevel,
            int maximumLevel,
            int targetMapId,
            double targetX,
            double targetY,
            double targetZ)
        {
            if (sourceMapId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceMapId));
            if (minimumLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumLevel));
            if (maximumLevel < minimumLevel)
                throw new ArgumentOutOfRangeException(nameof(maximumLevel));
            if (targetMapId < 0)
                throw new ArgumentOutOfRangeException(nameof(targetMapId));

            SourceMapId = sourceMapId;
            PortalId = portalId;
            MinimumLevel = minimumLevel;
            MaximumLevel = maximumLevel;
            TargetMapId = targetMapId;
            TargetX = targetX;
            TargetY = targetY;
            TargetZ = targetZ;
        }

        public bool CanEnter(int level)
        {
            return level >= MinimumLevel &&
                   level <= MaximumLevel;
        }
    }

    public sealed class NpcInteractionCore
    {
        private readonly Dictionary<int, NpcServiceKind> _services = new Dictionary<int, NpcServiceKind>();
        public int? ActiveNpcId { get; private set; }
        public void Register(int npcId, NpcServiceKind services)
        {
            if (npcId <= 0) throw new ArgumentOutOfRangeException(nameof(npcId));
            _services[npcId] = services;
        }
        public bool Open(int npcId)
        {
            if (!_services.ContainsKey(npcId)) return false;
            ActiveNpcId = npcId; return true;
        }
        public void Close() => ActiveNpcId = null;
        public bool Supports(NpcServiceKind service)
        {
            return ActiveNpcId.HasValue && (_services[ActiveNpcId.Value] & service) == service;
        }
    }

    public enum WeatherKindCore { Clear, Cloudy, Rain, Storm, Snow, Fog }

    public sealed class WeatherCore
    {
        public WeatherKindCore Current { get; private set; } = WeatherKindCore.Clear;
        public WeatherKindCore Target { get; private set; } = WeatherKindCore.Clear;
        public double Blend { get; private set; } = 1;
        private double _duration;
        private double _elapsed;

        public void TransitionTo(WeatherKindCore target, double durationSeconds)
        {
            if (durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            Target = target;
            _duration = durationSeconds;
            _elapsed = 0;
            Blend = durationSeconds <= 0 ? 1 : 0;
            if (durationSeconds <= 0) Current = target;
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (Current == Target) { Blend = 1; return; }
            _elapsed += deltaSeconds;
            Blend = _duration <= 0 ? 1 : Math.Min(1, _elapsed / _duration);
            if (Blend >= 1) Current = Target;
        }
    }
}
