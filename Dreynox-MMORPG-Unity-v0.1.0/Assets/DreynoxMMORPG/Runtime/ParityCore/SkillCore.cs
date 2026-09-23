using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public sealed class SkillDefinitionCore
    {
        public int SkillId { get; }
        public int Rank { get; }
        public int ResourceCost { get; }
        public double WindupSeconds { get; }
        public double RecoverySeconds { get; }
        public double CooldownSeconds { get; }
        public bool RequiresTarget { get; }

        public SkillDefinitionCore(int skillId, int rank, int resourceCost,
            double windupSeconds, double recoverySeconds, double cooldownSeconds,
            bool requiresTarget)
        {
            if (skillId <= 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (rank <= 0) throw new ArgumentOutOfRangeException(nameof(rank));
            if (resourceCost < 0) throw new ArgumentOutOfRangeException(nameof(resourceCost));
            if (windupSeconds < 0 || recoverySeconds < 0 || cooldownSeconds < 0)
                throw new ArgumentOutOfRangeException("Skill timing cannot be negative.");
            SkillId = skillId;
            Rank = rank;
            ResourceCost = resourceCost;
            WindupSeconds = windupSeconds;
            RecoverySeconds = recoverySeconds;
            CooldownSeconds = cooldownSeconds;
            RequiresTarget = requiresTarget;
        }
    }

    public sealed class ResourcePoolCore
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }

        public ResourcePoolCore(int maximum)
        {
            SetMaximum(maximum, true);
        }

        public void SetMaximum(int maximum, bool refill = false)
        {
            if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Current = refill ? maximum : Math.Min(Current, maximum);
        }

        public bool Spend(int amount)
        {
            if (amount < 0 || amount > Current) return false;
            Current -= amount;
            return true;
        }

        public int Restore(int amount)
        {
            if (amount <= 0) return 0;
            int before = Current;
            Current = Math.Min(Maximum, Current + amount);
            return Current - before;
        }
    }

    public enum SkillCastPhase { Idle, Windup, Recovery }

    public sealed class SkillCastEvent
    {
        public int Serial { get; }
        public int SkillId { get; }
        public int? TargetId { get; }
        public int Rank { get; }

        public SkillCastEvent(int serial, int skillId, int? targetId, int rank)
        {
            Serial = serial;
            SkillId = skillId;
            TargetId = targetId;
            Rank = rank;
        }
    }

    public sealed class SkillCore
    {
        private readonly Dictionary<int, SkillDefinitionCore> _skills = new Dictionary<int, SkillDefinitionCore>();
        private readonly Dictionary<int, double> _cooldownUntil = new Dictionary<int, double>();
        private double _clock;
        private double _phaseRemaining;
        private SkillDefinitionCore _active;
        private int? _lockedTarget;
        private int _eventSerial;

        public SkillCastPhase Phase { get; private set; }
        public IReadOnlyDictionary<int, SkillDefinitionCore> Skills => _skills;
        public SkillCastEvent LastCast { get; private set; }
        public double Clock => _clock;

        public void Learn(SkillDefinitionCore definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_skills.TryGetValue(definition.SkillId, out SkillDefinitionCore existing) && existing.Rank > definition.Rank)
                return;
            _skills[definition.SkillId] = definition;
        }

        public double CooldownRemaining(int skillId)
        {
            return _cooldownUntil.TryGetValue(skillId, out double until) ? Math.Max(0, until - _clock) : 0;
        }

        public bool TryCast(int skillId, int? targetId, ResourcePoolCore resource)
        {
            if (resource == null || Phase != SkillCastPhase.Idle || !_skills.TryGetValue(skillId, out SkillDefinitionCore skill))
                return false;
            if (CooldownRemaining(skillId) > 0) return false;
            if (skill.RequiresTarget && !targetId.HasValue) return false;
            if (!resource.Spend(skill.ResourceCost)) return false;

            _active = skill;
            _lockedTarget = targetId;
            _cooldownUntil[skill.SkillId] = _clock + skill.CooldownSeconds;
            _phaseRemaining = skill.WindupSeconds;
            Phase = SkillCastPhase.Windup;
            if (_phaseRemaining <= 0) EmitCastAndBeginRecovery();
            return true;
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            double remaining = deltaSeconds;
            while (remaining > 0)
            {
                if (Phase == SkillCastPhase.Idle)
                {
                    _clock += remaining;
                    break;
                }
                double step = Math.Min(remaining, _phaseRemaining);
                _clock += step;
                _phaseRemaining -= step;
                remaining -= step;
                if (_phaseRemaining > 1e-9) continue;
                if (Phase == SkillCastPhase.Windup) EmitCastAndBeginRecovery();
                else if (Phase == SkillCastPhase.Recovery) ResetCast();
            }
        }

        public void Interrupt(bool refundResource = false, ResourcePoolCore resource = null)
        {
            if (Phase == SkillCastPhase.Idle) return;
            if (refundResource && resource != null && Phase == SkillCastPhase.Windup && _active != null)
                resource.Restore(_active.ResourceCost);
            ResetCast();
        }

        private void EmitCastAndBeginRecovery()
        {
            LastCast = new SkillCastEvent(++_eventSerial, _active.SkillId, _lockedTarget, _active.Rank);
            Phase = SkillCastPhase.Recovery;
            _phaseRemaining = _active.RecoverySeconds;
            if (_phaseRemaining <= 0) ResetCast();
        }

        private void ResetCast()
        {
            Phase = SkillCastPhase.Idle;
            _phaseRemaining = 0;
            _active = null;
            _lockedTarget = null;
        }
    }
}
