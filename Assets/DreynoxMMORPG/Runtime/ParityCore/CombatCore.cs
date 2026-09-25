using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum AttackPhase { Idle, Windup, Recovery }

    public sealed class CombatTargetState
    {
        public int Id { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public bool Alive => Health > 0;
        public CombatTargetState(int id, int maxHealth)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            Id = id; MaxHealth = maxHealth; Health = maxHealth;
        }
        public int Damage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int before = Health;
            Health = Math.Max(0, Health - amount);
            return before - Health;
        }
    }

    public sealed class CombatCore
    {
        public const double DefaultGuardSeconds = 8.0;
        private readonly Dictionary<int, CombatTargetState> _targets = new Dictionary<int, CombatTargetState>();
        private double _clock, _guardUntil, _phaseRemaining, _recoverySeconds;
        private bool _hitApplied;
        private int _pendingDamage;
        private int? _selectedTargetId, _lockedAttackTargetId;
        private AttackPhase _phase;

        public double Clock => _clock;
        public AttackPhase Phase => _phase;
        public int? SelectedTargetId => _selectedTargetId;
        public int? LockedAttackTargetId => _lockedAttackTargetId;
        public bool InCombatGuard => _clock < _guardUntil || _phase != AttackPhase.Idle;
        public double GuardRemaining => Math.Max(0, _guardUntil - _clock);
        public IReadOnlyDictionary<int, CombatTargetState> Targets => _targets;
        public int HitSerial { get; private set; }
        public int AttackSerial { get; private set; }
        public int? LastHitTargetId { get; private set; }
        public int LastHitDamage { get; private set; }

        // The scene adapter validates range, visibility and pooled identity at impact.
        public Func<int, bool> ImpactValidator { get; set; }

        public void SynchronizeTarget(int id, int maxHealth, int currentHealth)
        {
            if (currentHealth < 0 || currentHealth > maxHealth) throw new ArgumentOutOfRangeException(nameof(currentHealth));
            var value = new CombatTargetState(id, maxHealth);
            value.Damage(maxHealth - currentHealth);
            _targets[id] = value;
        }
        public void UnregisterTarget(int id)
        {
            _targets.Remove(id);
            if (_selectedTargetId == id) _selectedTargetId = null;
            if (_lockedAttackTargetId == id)
            {
                _lockedAttackTargetId = null; _phase = AttackPhase.Idle;
                _phaseRemaining = 0; _pendingDamage = 0;
            }
        }
        public void RegisterTarget(int id, int maxHealth)
        {
            _targets[id] = new CombatTargetState(id, maxHealth);
        }
        public bool SelectTarget(int id)
        {
            if (!_targets.TryGetValue(id, out CombatTargetState target) || !target.Alive) return false;
            _selectedTargetId = id;
            return true;
        }
        public bool RequestAttack(int damage, double windupSeconds = 0.12, double hitSeconds = 0.18, double recoverySeconds = 0.28)
        {
            return _selectedTargetId.HasValue && RequestAttackAt(_selectedTargetId.Value, damage, windupSeconds, hitSeconds, recoverySeconds);
        }
        public bool RequestAttackAt(int targetId, int damage, double windupSeconds = 0.12, double hitSeconds = 0.18, double recoverySeconds = 0.28)
        {
            if (damage <= 0) return false;
            if (!FiniteNonNegative(windupSeconds) || !FiniteNonNegative(hitSeconds) || !FiniteNonNegative(recoverySeconds))
                throw new ArgumentOutOfRangeException(nameof(hitSeconds), "Attack timings must be finite and nonnegative.");
            if (_phase != AttackPhase.Idle) return false;
            if (!_targets.TryGetValue(targetId, out CombatTargetState target) || !target.Alive) return false;
            _lockedAttackTargetId = targetId;
            _pendingDamage = damage;
            _hitApplied = false;
            _phase = AttackPhase.Windup;
            _phaseRemaining = Math.Max(0.001, Math.Max(hitSeconds, windupSeconds));
            _recoverySeconds = Math.Max(0.01, recoverySeconds);
            AttackSerial++;
            TouchGuard();
            return true;
        }
        public void RegisterIncomingHit() { TouchGuard(); }

        public void Tick(double deltaSeconds)
        {
            if (!FiniteNonNegative(deltaSeconds) || deltaSeconds > 10)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            // Consume boundaries at their exact logical times instead of dropping
            // a frame's remaining time after impact. Do not simulate a new input.
            double remaining = deltaSeconds;
            while (remaining > 0 && _phase != AttackPhase.Idle)
            {
                double step = Math.Min(remaining, _phaseRemaining);
                _clock += step;
                remaining -= step;
                _phaseRemaining -= step;
                if (_phaseRemaining > 1e-10) break;
                if (_phase == AttackPhase.Windup)
                {
                    if (!_hitApplied) { ApplyLockedHit(); _hitApplied = true; }
                    _phase = AttackPhase.Recovery;
                    _phaseRemaining = _recoverySeconds;
                }
                else
                {
                    _phase = AttackPhase.Idle;
                    _phaseRemaining = 0;
                    _lockedAttackTargetId = null;
                    _pendingDamage = 0;
                }
            }
            _clock += remaining;
        }
        private static bool FiniteNonNegative(double value)
        {
            return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
        private void ApplyLockedHit()
        {
            if (!_lockedAttackTargetId.HasValue) return;
            if (ImpactValidator != null && !ImpactValidator(_lockedAttackTargetId.Value)) return;
            if (!_targets.TryGetValue(_lockedAttackTargetId.Value, out CombatTargetState target) || !target.Alive) return;
            LastHitDamage = target.Damage(_pendingDamage);
            LastHitTargetId = target.Id;
            HitSerial++;
            TouchGuard();
        }
        private void TouchGuard()
        {
            _guardUntil = Math.Max(_guardUntil, _clock + DefaultGuardSeconds);
        }
    }
}
