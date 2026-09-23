using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum StatKind { Strength, Dexterity, Reaction, Intelligence, Wisdom, Luck, MaxHealth, MaxMana, MaxStamina }

    public sealed class StatsCore
    {
        private readonly Dictionary<StatKind, int> _base = new Dictionary<StatKind, int>();
        private readonly Dictionary<string, Dictionary<StatKind, int>> _modifiers = new Dictionary<string, Dictionary<StatKind, int>>(StringComparer.Ordinal);

        public void SetBase(StatKind stat, int value) => _base[stat] = value;

        public int Get(StatKind stat)
        {
            int value = _base.TryGetValue(stat, out int b) ? b : 0;
            foreach (Dictionary<StatKind, int> group in _modifiers.Values)
                if (group.TryGetValue(stat, out int m)) value += m;
            return value;
        }

        public void SetModifier(string sourceId, IReadOnlyDictionary<StatKind, int> values)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException(nameof(sourceId));
            var copy = new Dictionary<StatKind, int>();
            foreach (var pair in values) copy[pair.Key] = pair.Value;
            _modifiers[sourceId] = copy;
        }

        public bool RemoveModifier(string sourceId) => _modifiers.Remove(sourceId);
    }

    public sealed class BuffState
    {
        public int BuffId { get; }
        public int Stack { get; private set; }
        public double ExpiresAt { get; private set; }

        public BuffState(int buffId, int stack, double expiresAt)
        {
            BuffId = buffId;
            Stack = stack;
            ExpiresAt = expiresAt;
        }

        public void Refresh(int stack, double expiresAt)
        {
            Stack = stack;
            ExpiresAt = expiresAt;
        }
    }

    public sealed class BuffCore
    {
        private readonly Dictionary<int, BuffState> _active = new Dictionary<int, BuffState>();
        public IReadOnlyDictionary<int, BuffState> Active => _active;

        public void Apply(int buffId, int stack, double now, double durationSeconds)
        {
            if (buffId <= 0 || stack <= 0 || durationSeconds <= 0) throw new ArgumentOutOfRangeException();
            double expires = now + durationSeconds;
            if (_active.TryGetValue(buffId, out BuffState state)) state.Refresh(stack, expires);
            else _active[buffId] = new BuffState(buffId, stack, expires);
        }

        public void Tick(double now)
        {
            var expired = new List<int>();
            foreach (var pair in _active)
                if (pair.Value.ExpiresAt <= now) expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++) _active.Remove(expired[i]);
        }
    }

    public sealed class LifeCore
    {
        public int MaxHealth { get; private set; }
        public int Health { get; private set; }
        public bool Dead => Health <= 0;
        public int DeathCount { get; private set; }

        public LifeCore(int maxHealth)
        {
            SetMaxHealth(maxHealth, true);
        }

        public void SetMaxHealth(int maxHealth, bool refill = false)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth = maxHealth;
            Health = refill ? maxHealth : Math.Min(Health, maxHealth);
        }

        public int Damage(int amount)
        {
            if (amount <= 0 || Dead) return 0;
            int before = Health;
            Health = Math.Max(0, Health - amount);
            if (before > 0 && Health == 0) DeathCount++;
            return before - Health;
        }

        public int Heal(int amount)
        {
            if (amount <= 0 || Dead) return 0;
            int before = Health;
            Health = Math.Min(MaxHealth, Health + amount);
            return Health - before;
        }

        public void Rebirth(double healthFraction = 1.0)
        {
            if (healthFraction <= 0 || healthFraction > 1) throw new ArgumentOutOfRangeException(nameof(healthFraction));
            Health = Math.Max(1, (int)Math.Round(MaxHealth * healthFraction));
        }
    }
}
