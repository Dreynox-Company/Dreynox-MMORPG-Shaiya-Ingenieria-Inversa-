using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum AttackPhase
    {
        Idle,
        Windup,
        Recovery
    }

    public sealed class CombatTargetState
    {
        public int Id { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public bool Alive => Health > 0;

        public CombatTargetState(int id, int maxHealth)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            Id = id;
            MaxHealth = maxHealth;
            Health = maxHealth;
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
        private double _clock;
        private double _guardUntil;
        private double _phaseRemaining;
        private double _hitRemaining;
        private bool _hitApplied;
        private int _pendingDamage;
        private int? _selectedTargetId;
        private int? _lockedAttackTargetId;
        private AttackPhase _phase;

        public double Clock => _clock;
        public AttackPhase Phase => _phase;
        public int? SelectedTargetId => _selectedTargetId;
        public int? LockedAttackTargetId => _lockedAttackTargetId;
        public bool InCombatGuard => _clock < _guardUntil || _phase != AttackPhase.Idle;
        public double GuardRemaining => Math.Max(0, _guardUntil - _clock);
        public IReadOnlyDictionary<int, CombatTargetState> Targets => _targets;
        public int HitSerial { get; private set; }
        public int? LastHitTargetId { get; private set; }
        public int LastHitDamage { get; private set; }

        public void RegisterTarget(int id, int maxHealth)
        {
            _targets[id] = new CombatTargetState(id, maxHealth);
        }

        public bool SelectTarget(int id)
        {
            CombatTargetState target;
            if (!_targets.TryGetValue(id, out target) || !target.Alive) return false;
            _selectedTargetId = id;
            return true;
        }

        public bool RequestAttack(int damage, double windupSeconds = 0.12, double hitSeconds = 0.18, double recoverySeconds = 0.28)
        {
            if (!_selectedTargetId.HasValue) return false;
            return RequestAttackAt(
                _selectedTargetId.Value,
                damage,
                windupSeconds,
                hitSeconds,
                recoverySeconds);
        }

        public bool RequestAttackAt(
            int targetId,
            int damage,
            double windupSeconds = 0.12,
            double hitSeconds = 0.18,
            double recoverySeconds = 0.28)
        {
            if (damage <= 0) return false;
            if (_phase != AttackPhase.Idle) return false;

            CombatTargetState target;
            if (!_targets.TryGetValue(targetId, out target) || !target.Alive) return false;

            if (hitSeconds < windupSeconds) hitSeconds = windupSeconds;
            _lockedAttackTargetId = targetId;
            _pendingDamage = damage;
            _hitApplied = false;
            _phase = AttackPhase.Windup;
            _phaseRemaining = Math.Max(hitSeconds, windupSeconds);
            _hitRemaining = Math.Max(0.001, hitSeconds);
            _recoverySeconds = Math.Max(0.01, recoverySeconds);
            TouchGuard();
            return true;
        }

        private double _recoverySeconds;

        public void RegisterIncomingHit()
        {
            TouchGuard();
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || deltaSeconds > 10 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _clock += deltaSeconds;
            if (_phase == AttackPhase.Idle) return;

            if (!_hitApplied)
            {
                _hitRemaining -= deltaSeconds;
                if (_hitRemaining <= 0)
                {
                    ApplyLockedHit();
                    _hitApplied = true;
                }
            }

            _phaseRemaining -= deltaSeconds;
            if (_phaseRemaining > 0) return;

            if (_phase == AttackPhase.Windup)
            {
                if (!_hitApplied)
                {
                    ApplyLockedHit();
                    _hitApplied = true;
                }
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

        private void ApplyLockedHit()
        {
            if (!_lockedAttackTargetId.HasValue) return;
            CombatTargetState target;
            if (!_targets.TryGetValue(_lockedAttackTargetId.Value, out target) || !target.Alive) return;
            int applied = target.Damage(_pendingDamage);
            LastHitTargetId = target.Id;
            LastHitDamage = applied;
            HitSerial++;
            TouchGuard();
        }

        private void TouchGuard()
        {
            _guardUntil = Math.Max(_guardUntil, _clock + DefaultGuardSeconds);
        }
    }
}
