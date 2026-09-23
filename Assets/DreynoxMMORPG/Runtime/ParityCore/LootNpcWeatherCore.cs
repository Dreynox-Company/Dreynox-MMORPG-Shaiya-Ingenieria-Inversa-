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
